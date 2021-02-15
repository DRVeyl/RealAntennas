using CommNet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Mathematics;

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

        private readonly RealAntenna[] bestFwdAntPair = new RealAntenna[2];
        private readonly RealAntenna[] bestRevAntPair = new RealAntenna[2];
        public List<CommNode> Nodes { get => nodes; }

        public override CommNode Add(CommNode conn)
        {
            if (!(conn is RACommNode c))
            {
                Debug.LogWarning($"{ModTag} Wrong commnode type, so ignoring.");
                return conn;
            }
            Debug.Log($"{ModTag} Adding {c.DebugToString()}");
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
            if (TimeToValidate()) { Validate(); LogState(); }
            base.PostUpdateNodes();
        }

        protected override bool TryConnect(CommNode a, CommNode b, double distance, bool aCanRelay = true, bool bCanRelay = true, bool bothRelay = true)
        {
            if ((!(a is RACommNode rac_a)) || (!(b is RACommNode rac_b)))
            {
                Debug.LogError($"{ModTag} TryConnect() but a({a}) or b({b}) null or not RACommNode!");
                return base.TryConnect(a, b, distance, aCanRelay, bCanRelay, bothRelay);
            }
            if (!rac_a.CanComm() || !rac_b.CanComm())
            {
                Disconnect(a, b);
                return false;
            }
            // Antenna selection was deferred until here.  Each RACommNode has a List<RealAntenna>.
            Profiler.BeginSample("RealAntennas CommNetwork TryConnect");
            bestFwdAntPair[0] = null;
            bestFwdAntPair[1] = null;
            bestRevAntPair[0] = null;
            bestRevAntPair[1] = null;

            //if (FwdDataRate < double.Epsilon || RevDataRate < double.Epsilon)
            if (!SelectBestAntennaPairs(rac_a.RAAntennaList, rac_b.RAAntennaList, bestFwdAntPair, bestRevAntPair, out double FwdDataRate, out double RevDataRate))
            {
                Disconnect(a, b);
                Profiler.EndSample();
                return false;
            }
            //MakeLink(bestFwdAntPair[0], bestFwdAntPair[1], bestRevAntPair[0], bestRevAntPair[1], rac_a, rac_b, distance, FwdDataRate, RevDataRate);

            Profiler.EndSample();
            return true;
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

        protected virtual bool SelectBestAntennaPairs(List<RealAntenna> fwdList, List<RealAntenna> revList, RealAntenna[] bestFwdAntPair, RealAntenna[] bestRevAntPair, out double FwdDataRate, out double RevDataRate)
        {
            FwdDataRate = RevDataRate = 0;
            foreach (RealAntenna first in fwdList)
            {
                foreach (RealAntenna second in revList)
                {
                    double candidateFwdRate = first.BestDataRateToPeer(second);
                    double candidateRevRate = second.BestDataRateToPeer(first);
                    if (FwdDataRate < candidateFwdRate)
                    {
                        bestFwdAntPair[0] = first;
                        bestFwdAntPair[1] = second;
                        FwdDataRate = candidateFwdRate;
                    }
                    if (RevDataRate < candidateRevRate)
                    {
                        bestRevAntPair[0] = second;
                        bestRevAntPair[1] = first;
                        RevDataRate = candidateRevRate;
                    }
                }
            }
            //Debug.LogFormat(ModTag + "Queried {0}/{1} and got rates {2}/{3}", rac_a, rac_b, RATools.PrettyPrint(FwdDataRate)+"bps", RATools.PrettyPrint(RevDataRate)+"bps");
            return (FwdDataRate >= double.Epsilon && RevDataRate >= double.Epsilon);
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
        public virtual void StartRebuild()
        {
            Profiler.BeginSample("RealAntennas StartRebuild");
            tempWatch.Reset();
            tempWatch.Start();
            isDirty = false;
            calculating = true;
            if (OnNetworkPreUpdate is Action)
                OnNetworkPreUpdate();
            PreUpdateNodes();
            UpdateOccluders();
            precompute.DoThings();
            tempWatch.Stop();
            Profiler.EndSample();
            (RACommNetScenario.Instance as RACommNetScenario).metrics.AddMeasurement("EarlyRebuild", tempWatch.Elapsed.TotalMilliseconds);
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

        protected override void UpdateNetwork()
        {
            //base.UpdateNetwork();
            precompute.complete(this);
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
            string res = $"{ModTag} CommNode walk\n";
            foreach (RACommNode item in nodes)
            {
                res += $"{item.DebugToString()}\n";
            }
            return res;
        }

        protected string CommLinkWalk()
        {
            string res = $"{ModTag} CommLink walk\n";
            foreach (CommLink item in Links)
            {
                res += $"{item}\n";
            }
            return res;
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

        private HashSet<RACommNode> sptSet = new HashSet<RACommNode>();
        private List<RACommNode> pathSortList = new List<RACommNode>();
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
                        if (kvp.Key is RACommNode node && kvp.Value is RACommLink link && !sptSet.Contains(node))
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
