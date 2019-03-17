using CommNet;
using UnityEngine;

namespace RealAntennas
{
    public class RACommNetwork : CommNetwork
    {
        protected static readonly string ModTag = "[RealAntennasCommNetwork] ";
        protected static readonly string ModTrace = ModTag + "[Trace] ";

        private readonly float updatePeriod = 1.0f;
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

            double distance = Vector3d.Distance(a.position, b.position);
            RACommNode tmp = a as RACommNode;
            double maxDistance = RACommNetScenario.RangeModel.GetMaximumRange(a as RACommNode, b as RACommNode, tmp.RAAntenna.Frequency);
            if (distance > maxDistance) {
//                Debug.LogFormat(ModTag + "SetNodeConnection() disconnecting: distance {0} > max {1}", distance, maxDistance);
                Disconnect(a, b);
                return false;
            }
            return base.SetNodeConnection(a, b);
        }

        protected override void PostUpdateNodes()
        {
            if (TimeToValidate()) ValidateNodes();
            base.PostUpdateNodes();
        }

        protected override bool TryConnect(CommNode a, CommNode b, double distance, bool aCanRelay, bool bCanRelay, bool bothRelay)
        {
            RACommNode tx = a as RACommNode, rx = b as RACommNode;
            if ((tx == null) || (rx == null))
            {
                Debug.LogErrorFormat(ModTag + "TryConnect() but a({0}) or b({1}) null or not VeylCommNodes!", a, b);
                return base.TryConnect(a, b, distance, aCanRelay, bCanRelay, bothRelay);
            }
            // This calc moved into RealAntennasRangeModel.
            //double FSPL = RACommNetScenario.RangeModel.PathLoss(distance);   // Default freq = 1GHz
            //double RSSI_Fwd = RACommNetScenario.RangeModel.ComputeRSSI(tx, rx, distance);
            //double RSSI_Rev = RACommNetScenario.RangeModel.ComputeRSSI(rx, tx, distance);
            //double CI_Fwd = RSSI_Fwd - RACommNetScenario.RangeModel.NoiseFloor(rx);
            //double CI_Rev = RSSI_Rev - RACommNetScenario.RangeModel.NoiseFloor(tx);
            //double CI = Math.Min(CI_Fwd, CI_Rev);   // Calc for bi-directional and take worst C/I.
            //double scaledCI = RACommNetScenario.RangeModel.ConvertCIToScaleFactor(CI);
            double scaledCI = RACommNetScenario.RangeModel.GetNormalizedRange(tx, rx, distance);
//            Debug.LogFormat(ModTag + "TryConnect: {0} / {1} distance {2}.  FSPL: {3}dB.  RSSI: {4}dBm.  C/I: {5}dB", tx, rx, distance, FSPL, RSSI, CI);
            if (scaledCI < 0)
            {
                Disconnect(a, b);
                return false;
            }
            CommLink link = Connect(tx, rx, distance);
            // TryConnect() is responsible for setting link parameters like below.
            //link.aCanRelay = a.isHome || a.isControlSourceMultiHop;     // This is wrong, it's the antennaType=RELAY or antennaType=DIRECT
            link.aCanRelay = true; 
            link.bCanRelay = true;      // All antennas can relay.  TDM/FDM.
            link.bothRelay = (link.aCanRelay && link.bCanRelay);
            link.strengthAR = (link.aCanRelay ? scaledCI : 0);
            link.strengthBR = (link.bCanRelay ? scaledCI : 0);
            link.strengthRR = (link.bothRelay ? scaledCI : 0);

            //            linkRes.SetSignalStrength(CI);
            // Consider calling base TryConnect(), it will query the RangeModel and also set these fields.
            // But will call RangeModel with wrong parameters/will call OLD RangeModel functions.
            //return base.TryConnect(tx, rx, distance*1e2, aCanRelay, bCanRelay, bothRelay);
            return true;
        }

        private bool TimeToValidate()
        {
            bool res = Time.timeSinceLevelLoad > lastRun + updatePeriod;
            if (res) lastRun = Time.timeSinceLevelLoad;
            return res;
        }

        // Instrumentation functions, no useful overrides below.  Delete when no longer instrumenting.

        protected override CommLink Connect(CommNode a, CommNode b, double distance)
        {
            return base.Connect(a, b, distance);
        }

        public override Occluder Add(Occluder conn)
        {
            Debug.LogFormat(ModTag + "Adding Occluder at {0} radius {1}", conn.position, conn.radius);
            return base.Add(conn);
        }
        public override bool FindClosestControlSource(CommNode from, CommPath path = null)
        {
            bool res = base.FindClosestControlSource(from, path);
            Debug.LogFormat(ModTrace + "FindClosestControlSource: from={0} result: {1}", from, res);
            return res;
        }
        public override bool FindHome(CommNode from, CommPath path = null)
        {
            bool res = base.FindHome(from, path);
            Debug.LogFormat(ModTrace + "FindHome result {2}: from={0} | path={1}", from, path, res);
            return res;
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

        protected override void CreateShortestPathTree(CommNode start, CommNode end)
        {
            Debug.LogFormat(ModTrace + "CreateShortestPathTree start={0}  end={1}", start, end);
            base.CreateShortestPathTree(start, end);
        }

        protected override void UpdateShortestPath(CommNode a, CommNode b, CommLink link, double bestCost, CommNode startNode, CommNode endNode)
        {
            Debug.LogFormat(ModTrace + "UpdateShortestPath a={0} b={1} link={2} bestCost={3} start={4} end={5}", a, b, link, bestCost, startNode, endNode);
            base.UpdateShortestPath(a, b, link, bestCost, startNode, endNode);
        }

        protected string CommNodeWalk()
        {
            string res = string.Format(ModTag + "CommNode walk\n");
            foreach (RACommNode item in nodes)
            {
                res += string.Format(ModTag + "{0} / {1}\n", item, item.name);
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
//                RACommNetVessel cnv = v.Connection as RACommNetVessel;
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