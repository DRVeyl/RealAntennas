﻿using CommNet;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace RealAntennas
{
    public class RACommNetwork : CommNetwork
    {
        protected static readonly string ModTag = "[RealAntennasCommNetwork] ";
        protected static readonly string ModTrace = ModTag + "[Trace] ";

        private readonly float updatePeriod = 60.0f;
        private float lastRun = 0f;

        private RealAntenna[] bestFwdAntPair = new RealAntenna[2];
        private RealAntenna[] bestRevAntPair = new RealAntenna[2];
        public List<CommNode> Nodes { get => nodes; }

        public override CommNode Add(CommNode conn)
        {
            if (!(conn is RACommNode c))
            {
                Debug.LogWarningFormat(ModTag + "Wrong commnode type, so ignoring.");
                return conn;
            }
            Debug.LogFormat(ModTag + "Adding {0}", c.DebugToString());
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
            if ((!(a is RACommNode rac_a)) || (!(b is RACommNode rac_b)))
            {
                Debug.LogErrorFormat(ModTag + "TryConnect() but a({0}) or b({1}) null or not RACommNode!", a, b);
                return base.TryConnect(a, b, distance, aCanRelay, bCanRelay, bothRelay);
            }
            if (!rac_a.CanComm() || !rac_b.CanComm())
            {
                Disconnect(a, b);
                return false;
            }
            // Antenna selection was deferred until here.  Each RACommNode has a List<RealAntenna>.
            Profiler.BeginSample("RACommNetwork TryConnect");
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

            RACommLink link = Connect(rac_a, rac_b, distance) as RACommLink;
            link.aCanRelay = true;
            link.bCanRelay = true;      // All antennas can relay.
            link.bothRelay = link.aCanRelay && link.bCanRelay;

            link.FwdAntennaTx = bestFwdAntPair[0];
            link.FwdAntennaRx = bestFwdAntPair[1];
            link.RevAntennaTx = bestRevAntPair[0];
            link.RevAntennaRx = bestRevAntPair[1];
            link.FwdDataRate = FwdDataRate;
            link.RevDataRate = RevDataRate;
            link.cost = link.CostFunc((FwdDataRate + RevDataRate) / 2);

            Antenna.Encoder FwdEncoder = Antenna.Encoder.BestMatching(bestFwdAntPair[0].Encoder, bestFwdAntPair[1].Encoder);
            Antenna.Encoder RevEncoder = Antenna.Encoder.BestMatching(bestRevAntPair[0].Encoder, bestRevAntPair[1].Encoder);

            RAModulator FwdAntennaTxMod = (link.FwdAntennaTx as RealAntennaDigital).modulator;
            RAModulator FwdAntennaRxMod = (link.FwdAntennaRx as RealAntennaDigital).modulator;
            RAModulator RevAntennaTxMod = (link.RevAntennaTx as RealAntennaDigital).modulator;
            RAModulator RevAntennaRxMod = (link.RevAntennaRx as RealAntennaDigital).modulator;
            int FwdMaxModSteps = Math.Min(FwdAntennaTxMod.ModulationBits, FwdAntennaRxMod.ModulationBits) - 1;
            int RevMaxModSteps = Math.Min(RevAntennaTxMod.ModulationBits, RevAntennaRxMod.ModulationBits) - 1;
            double FwdBestSymRate = Math.Min(bestFwdAntPair[0].SymbolRate, bestFwdAntPair[1].SymbolRate);
            double RevBestSymRate = Math.Min(bestRevAntPair[0].SymbolRate, bestRevAntPair[1].SymbolRate);
            double FwdMinSymRate = Math.Max(bestFwdAntPair[0].MinSymbolRate, bestFwdAntPair[1].MinSymbolRate);
            double RevMinSymRate = Math.Max(bestRevAntPair[0].MinSymbolRate, bestRevAntPair[1].MinSymbolRate);
            double FwdBestDataRate = FwdBestSymRate * FwdEncoder.CodingRate * Math.Pow(2, FwdMaxModSteps);
            double RevBestDataRate = RevBestSymRate * RevEncoder.CodingRate * Math.Pow(2, RevMaxModSteps);
            double FwdMinDataRate = FwdMinSymRate * FwdEncoder.CodingRate;
            double RevMinDataRate = RevMinSymRate * RevEncoder.CodingRate;
            double FwdMaxSymSteps = Math.Floor(Mathf.Log(Convert.ToSingle(FwdBestSymRate / FwdMinSymRate), 2));
            double RevMaxSymSteps = Math.Floor(Mathf.Log(Convert.ToSingle(RevBestSymRate / RevMinSymRate), 2));
            double FwdRateSteps = Math.Floor(Mathf.Log(Convert.ToSingle(FwdBestDataRate / FwdDataRate), 2));
            double RevRateSteps = Math.Floor(Mathf.Log(Convert.ToSingle(RevBestDataRate / RevDataRate), 2));

            if (Convert.ToSingle(FwdBestDataRate / FwdDataRate) < 1.0)
            {
                Debug.LogWarningFormat($"{ModTag} Detected actual rate {FwdDataRate} greater than expected max {FwdBestDataRate} for antennas {link.FwdAntennaTx} and {link.FwdAntennaRx}");
            }

            link.FwdMetric = 1 - (FwdRateSteps / (FwdMaxSymSteps + FwdMaxModSteps + 1));
            link.RevMetric = 1 - (RevRateSteps / (RevMaxSymSteps + RevMaxModSteps + 1));
//            Debug.LogFormat("Think we have taken {0} of {1} steps on FWD", Fwdlog2, FwdSymSteps);
//            Debug.LogFormat("Think we have taken {0} of {1} steps on REV", Revlog2, RevSymSteps);

            link.Update(Math.Min(link.FwdMetric, link.RevMetric));
            Profiler.EndSample();
            return true;
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

        public override void Rebuild()
        {
            Profiler.BeginSample("RealAntennas CommNetwork Rebuild");
            base.Rebuild();
            Profiler.EndSample();
        }

        protected string CommNodeWalk()
        {
            string res = string.Format(ModTag + "CommNode walk\n");
            foreach (RACommNode item in nodes)
            {
                res += string.Format(ModTag + "{0}\n", item.DebugToString());
            }
            return res;
        }

        public void CheckNodeConsistency()
        {
            foreach (var home in GameObject.FindObjectsOfType<Network.RACommNetHome>())
            {
                home.CheckNodeConsistency();
            }
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
            CheckNodeConsistency();
            Debug.Log(CommNodeWalk());
            Debug.Log(CommLinkWalk());
        }
    }
}