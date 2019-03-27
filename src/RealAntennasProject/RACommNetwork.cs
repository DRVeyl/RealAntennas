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
                Debug.LogWarning(string.Format(ModTag + "Wrong commnode type, so ignoring. Got: {0}", RACommNode.DebugDump(conn)));
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

            bool bestFwd = BestConnection(fwd_pairing, distance, noiseTemps[1], out RealAntenna[] bestFwdAntPair, out double FwdDataRate);
            bool bestRev = BestConnection(rev_pairing, distance, noiseTemps[0], out RealAntenna[] bestRevAntPair, out double RevDataRate);

            if (!(bestFwd && bestRev))
            {
                Disconnect(a, b);
                return false;
            }
            RealAntenna fwdAntTx = bestFwdAntPair[0];
            RealAntenna fwdAntRx = bestFwdAntPair[1];
            RealAntenna revAntTx = bestRevAntPair[0];
            RealAntenna revAntRx = bestRevAntPair[1];

            //Debug.LogFormat(ModTag + "TryConnect() {0}->{1} distance {2} chose {3} w/{4}", rac_a, rac_b, distance, bestFwdAnt, bestFwdMod);
            //Debug.LogFormat(ModTag + "TryConnect() {1}->{0} distance {2} chose {3} w/{4}", rac_b, rac_a, distance, bestRevAnt, bestRevMod);
            RACommLink link = Connect(rac_a, rac_b, distance) as RACommLink;
            link.FwdAntennaTx = fwdAntTx;
            link.FwdAntennaRx = fwdAntRx;
            link.RevAntennaTx = revAntTx;
            link.RevAntennaRx = revAntRx;
            link.FwdDataRate = FwdDataRate;
            link.RevDataRate = RevDataRate;
            link.cost = RACommLink.CostFunc((FwdDataRate + RevDataRate) / 2);

            double FwdRSSI = RACommNetScenario.RangeModel.RSSI(fwdAntTx, fwdAntRx, distance, fwdAntTx.Frequency);
            link.FwdCI = FwdRSSI - RACommNetScenario.RangeModel.NoiseFloor(fwdAntRx, noiseTemps[1]);

            double RevRSSI = RACommNetScenario.RangeModel.RSSI(revAntTx, revAntRx, distance, revAntTx.Frequency);
            link.RevCI = RevRSSI - RACommNetScenario.RangeModel.NoiseFloor(revAntRx, noiseTemps[0]);

            // TryConnect() is responsible for setting link parameters like below.
            //link.aCanRelay = a.isHome || a.isControlSourceMultiHop;     // This is wrong, it's the antennaType=RELAY or antennaType=DIRECT
            link.aCanRelay = true; 
            link.bCanRelay = true;      // All antennas can relay.
            link.bothRelay = (link.aCanRelay && link.bCanRelay);
            // Ok, so what is the link strength here?
            // Let's just make it, for now, the linear ratio between min and max data rate of the fwd link.
            // (Yeah, not very bidirectional yet.)
            double scaledCI = FwdDataRate / bestFwdAntPair[0].DataRate;
            link.strengthAR = (link.aCanRelay ? scaledCI : 0);
            link.strengthBR = (link.bCanRelay ? scaledCI : 0);
            link.strengthRR = (link.bothRelay ? scaledCI : 0);
            link.SetSignalStrength(scaledCI);
            link.signal = (SignalStrength) Convert.ToInt32(Math.Ceiling(4 * scaledCI));
            return true;
        }

        private static bool BestConnection(IEnumerable<RealAntenna[]> pairList, double distance, double noiseTemp, out RealAntenna[] bestPair, out double dataRate)
        {
            bestPair = new RealAntenna[2];
            dataRate = 0;
            foreach (RealAntenna[] antPair in pairList)
            {
                bool check = antPair[0].BestPeerModulator(antPair[1], distance, noiseTemp, out RAModulator candidateMod);
                if (check && (dataRate < candidateMod.DataRate))
                {
                    bestPair[0] = antPair[0];
                    bestPair[1] = antPair[1];
                    dataRate = candidateMod.DataRate;
                }
            }
            return (dataRate > 0);
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
        public override bool FindPath(CommNode start, CommPath path, CommNode end)
        {
            bool res = base.FindPath(start, path, end);
            Debug.LogFormat(ModTrace + "FindPath from {0} to {1} via {2} returned {3}", start, end, path, res);
            return res;
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
            Debug.Log(RealAntennasTools.VesselWalk(this, ModTag));
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