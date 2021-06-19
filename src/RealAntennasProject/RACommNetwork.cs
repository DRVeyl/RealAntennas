using CommNet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using System.Text;

namespace RealAntennas
{
    public class RACommNetwork : CommNetwork
    {
        protected static readonly string ModTag = "[RealAntennasCommNetwork]";

        private float lastRun = 0f;
        private readonly System.Diagnostics.Stopwatch RebuildStopWatch = new System.Diagnostics.Stopwatch();
        private readonly System.Diagnostics.Stopwatch PathfindWatch = new System.Diagnostics.Stopwatch();
        private readonly System.Diagnostics.Stopwatch PrecomputeLateWatch = new System.Diagnostics.Stopwatch();
        private readonly System.Diagnostics.Stopwatch tempWatch = new System.Diagnostics.Stopwatch();
        internal readonly Precompute.Precompute precompute = new Precompute.Precompute();

        public List<CommNode> Nodes { get => nodes; }
        public RealAntenna DebugAntenna => connectionDebugger?.antenna;
        public Network.ConnectionDebugger connectionDebugger = null;

        public override CommNode Add(CommNode conn)
        {
            if (!(conn is RACommNode))
            {
                Debug.LogWarning($"{ModTag} Wrong commnode type, so ignoring.");
                return conn;
            }
            return base.Add(conn);
        }
        protected override bool SetNodeConnection(CommNode a, CommNode b)
        {
            Debug.LogError($"[RACommNetwork] SetNodeConnection called, but it should never be!");
            return false;
        }

        protected override void PostUpdateNodes()
        {
            if (TimeToValidate()) { Validate(); LogState(); }
            base.PostUpdateNodes();
        }

        protected override bool TryConnect(CommNode a, CommNode b, double distance, bool aCanRelay = true, bool bCanRelay = true, bool bothRelay = true)
        {
            Debug.LogError($"[RACommNetwork] TryConnect called, but it should never be!");
            return false;
        }

        internal void MakeLink(RealAntenna fwdTx,
                               RealAntenna fwdRx,
                               RealAntenna revTx,
                               RealAntenna revRx,
                               RACommNode a,
                               RACommNode b,
                               double distance,
                               double FwdDataRate,
                               double RevDataRate,
                               double FwdBestDataRate,
                               double FwdMetric,
                               double RevMetric
                               )
        {
            RACommLink link = Connect(a, b, distance) as RACommLink;
            link.aCanRelay = true;
            link.bCanRelay = true;      // All antennas can relay.
            link.bothRelay = link.aCanRelay && link.bCanRelay;

            link.FwdAntennaTx = fwdTx;
            link.FwdAntennaRx = fwdRx;
            link.RevAntennaTx = revTx;
            link.RevAntennaRx = revRx;
            link.FwdDataRate = FwdDataRate;
            link.RevDataRate = RevDataRate;
            link.cost = link.CostFunc((FwdDataRate + RevDataRate) / 2);
            link.FwdMetric = FwdMetric;
            link.RevMetric = RevMetric;
            if (FwdBestDataRate < FwdDataRate)
                Debug.LogWarning($"{ModTag} Detected actual rate {FwdDataRate} greater than expected max {FwdBestDataRate} for antennas {link.FwdAntennaTx} and {link.FwdAntennaRx}");

            link.Update(Math.Min(link.FwdMetric, link.RevMetric));
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
            bool res = RACommNetScenario.debugWalkLogging && (Time.timeSinceLevelLoad > lastRun + RACommNetScenario.debugWalkInterval);
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
        private bool calculating = false;

        private bool IsPaused => (KSCPauseMenu.Instance && KSCPauseMenu.Instance.enabled) || (PauseMenu.exists && PauseMenu.isOpen);
        public virtual void StartRebuild(bool compute)
        {
            isDirty = false;
            calculating = compute;
            if (OnNetworkPreUpdate is Action)
                OnNetworkPreUpdate();
            PreUpdateNodes();
            UpdateOccluders();
            if (compute)
            {
                Profiler.BeginSample("RealAntennas StartRebuild");
                tempWatch.Reset();
                tempWatch.Start();
                precompute.DoThings();
                tempWatch.Stop();
                Profiler.EndSample();
                (RACommNetScenario.Instance as RACommNetScenario).metrics.AddMeasurement("EarlyRebuild", tempWatch.Elapsed.TotalMilliseconds);
            }
        }
        public virtual void CompleteRebuild()
        {
            if (calculating)
            {
                Profiler.BeginSample("RealAntennas CompleteRebuild");
                tempWatch.Reset();
                tempWatch.Start();
                PrecomputeLateWatch.Reset();
                PrecomputeLateWatch.Start();
                calculating = false;
                Profiler.BeginSample("RealAntennas CompleteRebuild.UpdateNetwork");
                UpdateNetwork();
                Profiler.EndSample();
                PrecomputeLateWatch.Stop();
                PostUpdateNodes();
                if (OnNetworkPostUpdate is Action)
                    OnNetworkPostUpdate();
                tempWatch.Stop();
                Profiler.EndSample();
                (RACommNetScenario.Instance as RACommNetScenario).metrics.AddMeasurement("Precompute LateRebuild", PrecomputeLateWatch.Elapsed.TotalMilliseconds);
                (RACommNetScenario.Instance as RACommNetScenario).metrics.AddMeasurement("Full LateRebuild", tempWatch.Elapsed.TotalMilliseconds);
            }
        }

        // Call this to abort a pre-computation pass that has already started.
        // Main use case is the node list changed during processing, ie a vessel was created or destroyed.
        public virtual void Abort()
        {
            calculating = false;
            precompute.Abort();
        }

        protected override void UpdateNetwork()
        {
            //base.UpdateNetwork();
            precompute.Complete(this);
        }

        internal void DoDisconnect(CommNode a, CommNode b) => Disconnect(a, b, true);

        public override void Rebuild()
        {
            // Base behavior is:
            // set isDirty = false
            // this?.OnNetworkPreUpdate()
            // this.PreUpdateNodes();
            // this.UpdateOccluders();
            // -- This far is fine

            // -- This should be deferred.
            // this.UpdateNetwork();
            // this.PostUpdateNodes();
            // this?.OnNetworkPostUpdate();

            if (!IsPaused)
            {
                Profiler.BeginSample("RealAntennas CommNetwork Rebuild");
                RebuildStopWatch.Reset();
                RebuildStopWatch.Start();
                base.Rebuild();
                RebuildStopWatch.Stop();
                Profiler.EndSample();
                (RACommNetScenario.Instance as RACommNetScenario).metrics.AddMeasurement("Rebuild", RebuildStopWatch.Elapsed.TotalMilliseconds);
            }
        }

        protected string CommNodeWalk()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{ModTag} CommNode walk");
            foreach (RACommNode item in nodes)
            {
                sb.Append($"\n{item.DebugToString()}");
            }
            return sb.ToStringAndRelease();
        }

        protected string CommLinkWalk()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{ModTag} CommLink walk");
            foreach (CommLink item in Links)
            {
                sb.Append($"\n{item}");
            }
            return sb.ToStringAndRelease();
        }

        public void Validate()
        {
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (v.Connection?.Comm is RACommNode vcn && !nodes.Contains(vcn))
                {
                    Debug.LogWarning($"{ModTag} Vessel {v} had commnode {vcn} not in the node list.");
                    Add(vcn);
                }
            }
            CheckNodeConsistency();
        }

        public void CheckNodeConsistency()
        {
            foreach (var home in GameObject.FindObjectsOfType<Network.RACommNetHome>())
            {
                home.CheckNodeConsistency();
            }

            foreach (var link in from RACommNode node in Nodes
                                 from link in node.Values
                                 where !(Nodes.Contains(link.start) && Nodes.Contains(link.end))
                                 select link)
            {
                Debug.LogWarning($"{ModTag} Found defunct link {link}");
            }
        }

        public void LogState()
        {
            Debug.Log(CommNodeWalk());
            Debug.Log(CommLinkWalk());
        }

        #region Pathfinding
        /*
        public override bool FindPath(CommNode start, CommPath path, CommNode end)
        {
            return base.FindPath(start, path, end);
        }
        */

        private readonly HashSet<RACommNode> sptSet = new HashSet<RACommNode>();
        private readonly List<RACommNode> pathSortList = new List<RACommNode>();
        public override CommNode FindClosestWhere(CommNode cnStart, CommPath path, Func<CommNode, CommNode, bool> where)
        {
            if (!(cnStart is RACommNode start && where != null))
                return base.FindClosestWhere(cnStart, path, where);
            Profiler.BeginSample("RealAntennas.FindClosestWhere");
            PathfindWatch.Reset();
            PathfindWatch.Start();
            path?.Clear();
            sptSet.Clear();
            pathSortList.Clear();
            foreach (RACommNode racn in Nodes)
            {
                racn.bestCost = (racn == start) ? 0 : double.PositiveInfinity;
                racn.bestLink = null;
                racn.bestLinkNode = null;
            }
            pathSortList.Add(start);
            bool found = false;
            RACommNode candidate = null;
            while (!found && pathSortList.Count > 0)
            {
                pathSortList.Sort((x, y) => x.bestCost.CompareTo(y.bestCost));
                candidate = pathSortList.First();
                pathSortList.RemoveAt(0);
                sptSet.Add(candidate);
                if (!(found = where(start, candidate)))
                {
                    foreach (KeyValuePair<CommNode, CommLink> kvp in candidate)
                    {
                        if (kvp.Key is RACommNode node && kvp.Value is RACommLink link && !sptSet.Contains(node)
                            && (link.start == candidate ? link.FwdAntennaRx : link.RevAntennaRx) is RealAntenna rxAntenna
                            // Skip if the peer is unable to relay and is also not the destination
                            && (rxAntenna.TechLevelInfo.Level >= RACommNetScenario.minRelayTL || where(start, node)))
                        {
                            double cost = link.start == candidate ? link.FwdCost : link.RevCost;
                            if (node.bestCost > candidate.bestCost + cost)
                            {
                                node.bestCost = candidate.bestCost + cost;
                                node.bestLink = link;
                                node.bestLinkNode = candidate;
                            }
                            pathSortList.AddUnique(node);
                        }
                    }
                }
            }
            if (found)
            {
                CommNode n = candidate;
                while (n is RACommNode && n != start)
                {
                    var link = new RACommLink();
                    link.Copy(n.bestLink as RACommLink);
                    if (link.a == n)
                        link.SwapEnds();
                    path?.Insert(0, link);
                    n = n.bestLinkNode as RACommNode;
                }
                path?.UpdateFromPath();
            }
            PathfindWatch.Stop();
            (RACommNetScenario.Instance as RACommNetScenario).metrics.AddMeasurement("Pathfinding", PathfindWatch.Elapsed.TotalMilliseconds);
            Profiler.EndSample();
            return found ? candidate : null;
        }
        #endregion
    }
}
