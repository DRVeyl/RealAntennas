using CommNet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace RealAntennas
{
    public class RACommNetwork : CommNetwork
    {
        protected static readonly string ModTag = "[RealAntennasCommNetwork]";

        private float lastRun = 0f;
        private readonly System.Diagnostics.Stopwatch RebuildStopWatch = new System.Diagnostics.Stopwatch();
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
            MakeLink(bestFwdAntPair[0], bestFwdAntPair[1], bestRevAntPair[0], bestRevAntPair[1], rac_a, rac_b, distance, FwdDataRate, RevDataRate);

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
                               double RevDataRate)
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

            Antenna.Encoder FwdEncoder = Antenna.Encoder.BestMatching(fwdTx.Encoder, fwdRx.Encoder);
            Antenna.Encoder RevEncoder = Antenna.Encoder.BestMatching(revTx.Encoder, revRx.Encoder);

            RAModulator FwdAntennaTxMod = (link.FwdAntennaTx as RealAntennaDigital).modulator;
            RAModulator FwdAntennaRxMod = (link.FwdAntennaRx as RealAntennaDigital).modulator;
            RAModulator RevAntennaTxMod = (link.RevAntennaTx as RealAntennaDigital).modulator;
            RAModulator RevAntennaRxMod = (link.RevAntennaRx as RealAntennaDigital).modulator;
            int FwdMaxModSteps = Math.Min(FwdAntennaTxMod.ModulationBits, FwdAntennaRxMod.ModulationBits) - 1;
            int RevMaxModSteps = Math.Min(RevAntennaTxMod.ModulationBits, RevAntennaRxMod.ModulationBits) - 1;
            double FwdBestSymRate = Math.Min(fwdTx.SymbolRate, fwdRx.SymbolRate);
            double RevBestSymRate = Math.Min(revTx.SymbolRate, revRx.SymbolRate);
            double FwdMinSymRate = Math.Max(fwdTx.MinSymbolRate, fwdRx.MinSymbolRate);
            double RevMinSymRate = Math.Max(revTx.MinSymbolRate, revRx.MinSymbolRate);
            double FwdBestDataRate = FwdBestSymRate * FwdEncoder.CodingRate * Math.Pow(2, FwdMaxModSteps);
            double RevBestDataRate = RevBestSymRate * RevEncoder.CodingRate * Math.Pow(2, RevMaxModSteps);
            double FwdMinDataRate = FwdMinSymRate * FwdEncoder.CodingRate;
            double RevMinDataRate = RevMinSymRate * RevEncoder.CodingRate;
            double FwdMaxSymSteps = Math.Floor(Mathf.Log(Convert.ToSingle(FwdBestSymRate / FwdMinSymRate), 2));
            double RevMaxSymSteps = Math.Floor(Mathf.Log(Convert.ToSingle(RevBestSymRate / RevMinSymRate), 2));
            double FwdRateSteps = Math.Floor(Mathf.Log(Convert.ToSingle(FwdBestDataRate / FwdDataRate), 2));
            double RevRateSteps = Math.Floor(Mathf.Log(Convert.ToSingle(RevBestDataRate / RevDataRate), 2));

            if (FwdBestDataRate < FwdDataRate)
                Debug.LogWarning($"{ModTag} Detected actual rate {FwdDataRate} greater than expected max {FwdBestDataRate} for antennas {link.FwdAntennaTx} and {link.FwdAntennaRx}");

            link.FwdMetric = 1 - (FwdRateSteps / (FwdMaxSymSteps + FwdMaxModSteps + 1));
            link.RevMetric = 1 - (RevRateSteps / (RevMaxSymSteps + RevMaxModSteps + 1));
            //            Debug.LogFormat("Think we have taken {0} of {1} steps on FWD", Fwdlog2, FwdSymSteps);
            //            Debug.LogFormat("Think we have taken {0} of {1} steps on REV", Revlog2, RevSymSteps);

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
            (RACommNetScenario.Instance as RACommNetScenario).metrics.AddMeasurement("EarlyRebuild", tempWatch.Elapsed.TotalMilliseconds);
        }
        public virtual void CompleteRebuild()
        {
            if (calculating)
            {
                tempWatch.Reset();
                tempWatch.Start();
                calculating = false;
                UpdateNetwork();
                PostUpdateNodes();
                if (OnNetworkPostUpdate is Action)
                    OnNetworkPostUpdate();
                tempWatch.Stop();
                (RACommNetScenario.Instance as RACommNetScenario).metrics.AddMeasurement("LateRebuild", tempWatch.Elapsed.TotalMilliseconds);
            }
        }

        protected override void UpdateNetwork()
        {
            //base.UpdateNetwork();
            precompute.complete(this);
        }

        internal void DoDisconnect(CommNode a, CommNode b) => Disconnect(a, b, true);
        internal void DoConnect(RACommNode a, RACommNode b, double distance, RealAntenna fwdTx, RealAntenna fwdRx, RealAntenna revTx, RealAntenna revRx, double fwdRate, double revRate) =>
            MakeLink(fwdTx, fwdRx, revTx, revRx, a, b, distance, fwdRate, revRate);

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
            tempWatch.Reset();
            tempWatch.Start();
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
            tempWatch.Stop();
            (RACommNetScenario.Instance as RACommNetScenario).metrics.AddMeasurement("Pathfinding", tempWatch.Elapsed.TotalMilliseconds);
            Profiler.EndSample();
            return found ? candidate : null;
        }
        #endregion
    }
}
