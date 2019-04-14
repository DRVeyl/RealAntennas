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
            if (a.isHome && b.isHome)
            {
                Disconnect(a, b);
                return false;
            }
            double distance = (b.precisePosition - a.precisePosition).magnitude;
            if (TestOcclusion(a.precisePosition, a.occluder, b.precisePosition, b.occluder, distance))
                return TryConnect(a, b, distance);

            Disconnect(a, b, true);
            return false;
            // Specific antenna selection within the set available to a CommNode is deferred until TryConnect()
            // Do not prematurely halt based on range here anymore, because we need to check (all?) pairings.
            // TryConnect() should call Disconnect() if no connection can be achieved.
        }

        protected override void PostUpdateNodes()
        {
            if (TimeToValidate()) ValidateNodes();
            base.PostUpdateNodes();
        }

        protected override bool TryConnect(CommNode a, CommNode b, double distance, bool aCanRelay = true, bool bCanRelay = true, bool bothRelay = true)
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

            double FwdDataRate = BestDataRate(fwd_pairing, out RealAntenna[] bestFwdAntPair);
            double RevDataRate = BestDataRate(rev_pairing, out RealAntenna[] bestRevAntPair);
            Debug.LogFormat(ModTag + "Queried {0}/{1} and got rates {2:F2}/{3:F2}", rac_a, rac_b, FwdDataRate, RevDataRate);
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

            double FwdRSSI = Physics.ReceivedPower(link.FwdAntennaTx, link.FwdAntennaRx, distance, link.FwdAntennaTx.Frequency);
            link.FwdCI = FwdRSSI - link.FwdAntennaRx.NoiseFloor(link.FwdAntennaTx.Position);

            double RevRSSI = Physics.ReceivedPower(link.RevAntennaTx, link.RevAntennaRx, distance, link.RevAntennaTx.Frequency);
            link.RevCI = RevRSSI - link.RevAntennaRx.NoiseFloor(link.RevAntennaTx.Position);

            // TryConnect() is responsible for setting link parameters like below.
            link.aCanRelay = true;
            link.bCanRelay = true;      // All antennas can relay.
            link.bothRelay = link.aCanRelay && link.bCanRelay;
            // WIP: Set link strength to the achieved percentage of the maximum possible data rate for some link.
            double scaledCI = Math.Max(FwdDataRate / bestFwdAntPair[0].DataRate, RevDataRate / bestRevAntPair[0].DataRate);
            link.Update(scaledCI);
            return true;
        }

        protected virtual double BestDataRate(IEnumerable<RealAntenna[]> pairList, out RealAntenna[] bestPair)
        {
            bestPair = new RealAntenna[2];
            double dataRate = 0;
            foreach (RealAntenna[] antPair in pairList)
            {
                double candidateRate = antPair[0].BestDataRateToPeer(antPair[1]);
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
            if (foundLink == null)
            {
                foundLink = new RACommLink();
                foundLink.Set(a, b, 0, 0);
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

        public int HopsToHome(RACommNode start)
        {
            CommPath path = new CommPath();
            if ((start == null) || !FindHome(start, path)) return -1;
            return path.Count;
        }

        public double MaxDataRateToHome(RACommNode start)
        {
            CommPath path = new CommPath();
            if ((start == null) || !FindHome(start, path)) return 0;
            double data_rate = double.MaxValue;
            foreach (CommLink l in path)
            {
                RACommLink link = l.start[l.end] as RACommLink;
                double linkRate = link.start.Equals(l.start) ? link.FwdDataRate : link.RevDataRate;
                data_rate = Math.Min(data_rate, linkRate);
            }
            return data_rate;
        }


        // Instrumentation functions, no useful overrides below.  Delete when no longer instrumenting.

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