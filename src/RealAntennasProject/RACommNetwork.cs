using CommNet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// I see lots of comments that hate .Linq.  Is it really worse than doing a nested ForEach?

namespace RealAntennas
{
    public class RACommNetwork : CommNetwork
    {
        protected static readonly string ModTag = "[RealAntennasCommNetwork] ";
        protected static readonly string ModTrace = ModTag + "[Trace] ";

        private readonly float updatePeriod = 10.0f;
        private float lastRun = 0f;

        public override CommNode Add(CommNode conn)
        {
            if (!(conn is RACommNode))
            {
                Debug.LogWarningFormat(ModTag + "Wrong commnode type, so ignoring.");
                return conn;
            }
            Debug.LogFormat(ModTag + "Adding {0}", conn);
            return base.Add(conn);
        }
        protected override bool SetNodeConnection(CommNode a, CommNode b)
        {
            if (a.isHome && b.isHome) return base.SetNodeConnection(a, b);
            if (!(a is RACommNode)) return base.SetNodeConnection(a, b);
            if (!(b is RACommNode)) return base.SetNodeConnection(a, b);

            // Specific antenna selection within the set available to a CommNode is deferred until TryConnect()
            // Do not prematurely halt based on range here anymore, because we need to check (all?) pairings.
            // TryConnect() should call Disconnect() if no connection can be achieved.
            return base.SetNodeConnection(a, b);
        }

        protected override void PostUpdateNodes()
        {
            if (TimeToValidate()) ValidateNodes();
            base.PostUpdateNodes();
        }

        protected override bool TryConnect(CommNode a, CommNode b, double distance, bool aCanRelay, bool bCanRelay, bool bothRelay)
        {
            RACommNode rac_a = a as RACommNode, rac_b = b as RACommNode;
            if ((rac_a == null) || (rac_b == null))
            {
                Debug.LogErrorFormat(ModTag + "TryConnect() but a({0}) or b({1}) null or not RACommNode!", a, b);
                return base.TryConnect(a, b, distance, aCanRelay, bCanRelay, bothRelay);
            }
            // Antenna selection was deferred until here.  Each RACommNode has a List<RealAntenna>.
            var fwd_pairing =
                from first in rac_a.RAAntennaList
                from second in rac_b.RAAntennaList
                select new[] { first, second };
            var rev_pairing =
                from first in rac_a.RAAntennaList
                from second in rac_b.RAAntennaList
                select new[] { second, first };
            double[] noiseTemps = {
                RACommNetScenario.RangeModel.NoiseTemperature(rac_a, rac_b.position),
                RACommNetScenario.RangeModel.NoiseTemperature(rac_b, rac_a.position)
            };

            double FwdDataRate = BestDataRate(fwd_pairing, distance, noiseTemps[1], out RealAntenna[] bestFwdAntPair);
            double RevDataRate = BestDataRate(rev_pairing, distance, noiseTemps[0], out RealAntenna[] bestRevAntPair);

            if (FwdDataRate < double.Epsilon || RevDataRate < double.Epsilon)
            {
                Disconnect(a, b);
                return false;
            }

            RACommLink link = Connect(rac_a, rac_b, distance) as RACommLink;
            link.FwdAntennaTx = bestFwdAntPair[0];
            link.FwdAntennaRx = bestFwdAntPair[1];
            link.RevAntennaTx = bestRevAntPair[0];
            link.RevAntennaRx = bestRevAntPair[1];
            link.FwdDataRate = FwdDataRate;
            link.RevDataRate = RevDataRate;
            link.cost = link.CostFunc((FwdDataRate + RevDataRate) / 2);

            double FwdRSSI = RACommNetScenario.RangeModel.RSSI(link.FwdAntennaTx, link.FwdAntennaRx, distance, link.FwdAntennaTx.Frequency);
            link.FwdCI = FwdRSSI - RACommNetScenario.RangeModel.NoiseFloor(link.FwdAntennaRx, noiseTemps[1]);

            double RevRSSI = RACommNetScenario.RangeModel.RSSI(link.RevAntennaTx, link.RevAntennaRx, distance, link.RevAntennaTx.Frequency);
            link.RevCI = RevRSSI - RACommNetScenario.RangeModel.NoiseFloor(link.RevAntennaRx, noiseTemps[0]);

            // TryConnect() is responsible for setting link parameters like below.
            link.aCanRelay = true; 
            link.bCanRelay = true;      // All antennas can relay.
            link.bothRelay = link.aCanRelay && link.bCanRelay;
            // WIP: Set link strength to the achieved percentage of the maximum possible data rate for the fwd link.
            double scaledCI = FwdDataRate / bestFwdAntPair[0].DataRate;
            link.strengthAR = link.aCanRelay ? scaledCI : 0;
            link.strengthBR = link.bCanRelay ? scaledCI : 0;
            link.strengthRR = link.bothRelay ? scaledCI : 0;
            link.SetSignalStrength(scaledCI);
            link.signal = (SignalStrength) Convert.ToInt32(Math.Ceiling(4 * scaledCI));
            return true;
        }

        protected virtual double BestDataRate(IEnumerable<RealAntenna[]> pairList, double distance, double noiseTemp, out RealAntenna[] bestPair)
        {
            bestPair = new RealAntenna[2];
            double dataRate = 0;
            foreach (RealAntenna[] antPair in pairList)
            {
                double candidateRate = antPair[0].BestDataRateToPeer(antPair[1], distance, noiseTemp);
                if (dataRate < candidateRate)
                {
                    bestPair[0] = antPair[0];
                    bestPair[1] = antPair[1];
                    dataRate = candidateRate;
                }
            }
            return dataRate;
        }

        protected override CommLink Connect(CommNode a, CommNode b, double distance)
        {
            a.TryGetValue(b, out CommLink foundLink);
            if (foundLink != null)
            {
                foundLink.Update(distance);
            } else
            {
                foundLink = new RACommLink();
                foundLink.Set(a, b, distance);
                Links.Add(foundLink);
                a.Add(b, foundLink);
                b.Add(a, foundLink);
            }
            return foundLink;
        }

        private bool TimeToValidate()
        {
            bool res = Time.timeSinceLevelLoad > lastRun + updatePeriod;
            if (res) lastRun = Time.timeSinceLevelLoad;
            return res;
        }

        public double MaxDataRateToHome(RACommNode start)
        {
            CommPath path = new CommPath();
            if ((start == null) || !FindHome(start, path)) return 0;
            double data_rate = 1e10;
            foreach (CommLink l in path)
            {
                RACommLink link = l.start[l.end] as RACommLink;
                double linkRate = link.start.Equals(l.start) ? link.FwdDataRate : link.RevDataRate;
                data_rate = Math.Min(data_rate, linkRate);
            }
            return data_rate;
        }


        // Instrumentation functions, no useful overrides below.  Delete when no longer instrumenting.

        public override Occluder Add(Occluder conn)
        {
            Debug.LogFormat(ModTag + "Adding Occluder at {0} radius {1}", conn.position, conn.radius);
            return base.Add(conn);
        }

        public override void Rebuild()
        {
            //            Debug.Log(ModTrace + " Rebuild()");
            base.Rebuild();
            //            Debug.Log(ModTrace + " Rebuild() exit");
        }

        protected string CommNodeWalk()
        {
            string res = string.Format(ModTag + "CommNode walk\n");
            foreach (RACommNode item in nodes)
            {
                res += string.Format(ModTag + "{0}\n", item);
            }
            return res;
        }

        protected string CommLinkWalk()
        {
            string res = string.Format(ModTag + "CommLink walk\n");
            foreach (CommLink item in Links)
            {
                res += string.Format(ModTrace + "{0}\n", item);
                //                    res += string.Format(ModTag + "{0}\n", RealAntennasTools.DumpLink(item));
            }
            return res;
        }

        public void ValidateNodes()
        {
            Debug.Log(RATools.VesselWalk(this, ModTag));
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (v.Connection is RACommNetVessel cnv)
                {
                    if ((cnv.Comm is RACommNode vcn) && (!nodes.Contains(vcn)))
                    {
                        Debug.LogWarningFormat(ModTag + "Vessel {0} had commnode {1} not in the node list.", v, vcn);
                        Add(vcn);
                    }
                }
            }
            Debug.Log(CommNodeWalk());
            Debug.Log(CommLinkWalk());
        }
    }
}