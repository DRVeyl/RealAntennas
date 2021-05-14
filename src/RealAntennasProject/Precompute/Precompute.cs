﻿using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Profiling;

namespace RealAntennas.Precompute
{
    public struct AntennaData
    {
        internal float txPower;
        internal float freq;
        internal float gain;
        internal float beamwidth;
        internal bool isHome;
        internal double3 position;
        internal float3 dir;
        internal float AMW;
        internal Encoder encoder;
        internal float maxSymbolRate;
        internal float minSymbolRate;
        internal int modulationBits;
    }

    public struct CNInfo
    {
        internal double3 position;
        internal double3 surfaceNormal;
        internal bool isHome;
        internal bool canComm;
    }

    public struct OccluderInfo
    {
        internal double3 position;
        internal float radius;
        internal float temp;
        internal bool isStar;
    }

    public struct Encoder
    {
        public int TechLevel;
        public float CodingRate;
        public float RequiredEbN0;

        public Encoder(in Antenna.Encoder e)
        {
            TechLevel = e.TechLevel;
            CodingRate = e.CodingRate;
            RequiredEbN0 = e.RequiredEbN0;
        }
        public Encoder BestMatching(in Encoder other) => TechLevel > other.TechLevel ? other : this;
    }

    internal class Precompute
    {
        public readonly Dictionary<RealAntenna, int> allAntennas = new Dictionary<RealAntenna, int>();
        public readonly Dictionary<int, RealAntenna> allAntennasReverse = new Dictionary<int, RealAntenna>();
        public NativeArray<AntennaData> antennaDataList;
        public NativeList<int4> allAntennaPairs = new NativeList<int4>(Allocator.Persistent);
        public NativeList<int2> allNodePairs = new NativeList<int2>(Allocator.Persistent);
        public NativeHashMap<int2, int> bestMap;
        public NativeHashMap<int2, bool> validMap;
        private JobHandle precomputeJobHandle;

        // Job data, allocated at start of job scatter, de-allocated during gather method.
        private NativeArray<float> antennaNoise;
        private NativeArray<bool> nodePairsValid;
        private NativeArray<bool> occlusionValid;
        private NativeList<int4> allValidAntennaPairs;

        private NativeArray<float> txPower;
        private NativeArray<float> txFreq;
        private NativeArray<float> txGain;
        private NativeArray<float> txBeamwidth;
        private NativeArray<bool> txHome;
        private NativeArray<double3> txPos;
        private NativeArray<float3> txDir;

        private NativeArray<float> rxFreq;
        private NativeArray<float> rxGain;
        private NativeArray<float> rxBeamwidth;
        private NativeArray<bool> rxHome;
        private NativeArray<double3> rxPos;
        private NativeArray<float3> rxDir;
        private NativeArray<float> rxAMW;
        private NativeArray<float> rxPrecalcNoise;
        private NativeArray<double3> rxSurfaceNormal;

        private NativeArray<Encoder> matchedEncoder;
        private NativeArray<int> maxModulationBits;
        private NativeArray<float> minSymbolRate;
        private NativeArray<float> maxSymbolRate;
        private NativeArray<float> pathLoss;
        private NativeArray<float> pointingLoss;
        private NativeArray<float> rxPower;
        private NativeArray<float> atmosphereNoise;
        private NativeArray<float> bodyNoise;
        private NativeArray<float> noiseTemp;
        private NativeArray<float> n0;
        private NativeArray<float> minEb;
        private NativeArray<float> maxTheoreticalBitRate;
        private NativeArray<float> symbolRate;
        private NativeArray<int> modulationBits;
        private NativeArray<float> dataRate;
        private NativeArray<float> maxDataRate;
        private NativeArray<float> minDataRate;
        private NativeArray<int> maxSteps;
        private NativeArray<int> rateSteps;
        private NativeMultiHashMap<int2, int> nodeRowMap;

        public NativeList<CNInfo> allNodes;
        public NativeArray<OccluderInfo> occluders;

        private bool tempAllocationsActive = false;

        public void Destroy()
        {
            if (antennaDataList.IsCreated) antennaDataList.Dispose();
            allAntennaPairs.Dispose();
            DisposeJobData();
        }

        public void Initialize()
        {
            if (antennaDataList.IsCreated) antennaDataList.Dispose();
            antennaDataList = GatherAllAntennas();
            PairAllAntennasAndNodes();
        }

        // Process:
        //  Gather all occluders
        //  Gather all antennas
        //  Construct list of all pairs of antennas (quads: node-pairs + antenna-pairs)
        //  Sequentially filter the list for impossible connections:
        //      Both isHome
        //      Either node is unpowered/cannot comm
        //      Occlusion

        // Each iteration, we have to update elements from KSP and prepare the jobs.
        // We replace so much of the CommNode and Occluder data structures that we might as well reallocate
        // Use the persistent antenna data structure, though.
        public void DoThings()
        {
            Profiler.BeginSample("RealAntennas PreCompute.Early");
            if (tempAllocationsActive)
            {
                UnityEngine.Debug.LogError($"[RealAntennas.Precompute] Jobs were already allocated!\n{new System.Diagnostics.StackTrace()}");
                DisposeJobData();
            }
            Profiler.BeginSample("RealAntennas PreCompute.Early.SetupCommNodes");
            SetupCommNodes(out allNodes);
            Profiler.EndSample();
            Profiler.BeginSample("RealAntennas PreCompute.Early.SetupOccluders");
            SetupOccluders(out occluders);
            Profiler.EndSample();
            Profiler.BeginSample("RealAntennas PreCompute.Early.UpdateAllAntennas");
            UpdateAllAntennas();
            Profiler.EndSample();
            Profiler.EndSample();

            Profiler.BeginSample("RealAntennas PreCompute.Early.JobCreate");
            nodePairsValid = new NativeArray<bool>(allNodePairs.Length, Allocator.TempJob);
            var filter1 = new FilterCommNodes
            {
                nodes = allNodes,
                pairs = allNodePairs,
                valid = nodePairsValid
            }.Schedule(allNodePairs.Length, 128);

            occlusionValid = new NativeArray<bool>(allNodePairs.Length, Allocator.TempJob);
            var filter3 = new FilterCommNodesByOcclusion
            {
                nodes = allNodes,
                pairs = allNodePairs,
                occluders = occluders,
                validIn = nodePairsValid,
                validOut = occlusionValid,
            }.Schedule(allNodePairs.Length, 16, filter1);

            validMap = new NativeHashMap<int2, bool>(allNodePairs.Length, Allocator.TempJob);
            var validPairMapJobHandle = new CreateValidPairMapJob
            {
                pairs = allNodePairs,
                valid = occlusionValid,
                output = validMap
            }.Schedule(filter3);

            allValidAntennaPairs = new NativeList<int4>(allAntennaPairs.Length, Allocator.TempJob);
            var allValidAntennaPairsHandle = new FilterAntennaPairsJob
            {
                allPairs = allAntennaPairs,
                valid = validMap,
                validPairs = allValidAntennaPairs
            }.Schedule(validPairMapJobHandle);

            nodeRowMap = new NativeMultiHashMap<int2, int>(allAntennaPairs.Length, Allocator.TempJob);
            var sortCalculations = new MapCommNodesToCalcRowsJob
            {
                pairs = allValidAntennaPairs.AsDeferredJobArray(),
                connections = nodeRowMap,
            }.Schedule(allValidAntennaPairsHandle);

            JobHandle.ScheduleBatchedJobs();

            antennaNoise = new NativeArray<float>(antennaDataList.Length, Allocator.TempJob);
            var noisePrecalcHandle = new PreCalcAntennaNoise
            {
                antennas = antennaDataList,
                occluders = occluders,
                noiseTemp = antennaNoise
            }.Schedule(antennaDataList.Length, 4);

            txPower = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            txFreq = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            txGain = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            txBeamwidth = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            txHome = new NativeArray<bool>(allAntennaPairs.Length, Allocator.TempJob);
            txPos = new NativeArray<double3>(allAntennaPairs.Length, Allocator.TempJob);
            txDir = new NativeArray<float3>(allAntennaPairs.Length, Allocator.TempJob);

            rxFreq = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            rxGain = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            rxBeamwidth = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            rxHome = new NativeArray<bool>(allAntennaPairs.Length, Allocator.TempJob);
            rxPos = new NativeArray<double3>(allAntennaPairs.Length, Allocator.TempJob);
            rxDir = new NativeArray<float3>(allAntennaPairs.Length, Allocator.TempJob);
            rxAMW = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            rxPrecalcNoise = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            rxSurfaceNormal = new NativeArray<double3>(allAntennaPairs.Length, Allocator.TempJob);

            var extractDataJob = new ExtractDataJob
            {
                pairs = allValidAntennaPairs.AsDeferredJobArray(),
                antennas = antennaDataList,
                nodes = allNodes,
                antennaNoise = antennaNoise,
                txPos = txPos,
                rxPos = rxPos,
                txDir = txDir,
                rxDir = rxDir,
                txGain = txGain,
                rxGain = rxGain,
                txBeamwidth = txBeamwidth,
                rxBeamwidth = rxBeamwidth,
                txHome = txHome,
                rxHome = rxHome,
                txFreq = txFreq,
                rxFreq = rxFreq,
                txPower = txPower,
                rxAMW = rxAMW,
                rxPrecalcNoise = rxPrecalcNoise,
                rxSurfaceNormal = rxSurfaceNormal
            }.Schedule(allValidAntennaPairs, 16, JobHandle.CombineDependencies(noisePrecalcHandle, allValidAntennaPairsHandle));

            matchedEncoder = new NativeArray<Encoder>(allAntennaPairs.Length, Allocator.TempJob);
            var matchedEncoderJob = new MatchedEncoderJob
            {
                pairs = allValidAntennaPairs.AsDeferredJobArray(),
                antennas = antennaDataList,
                encoder = matchedEncoder
            }.Schedule(allValidAntennaPairs, 16, allValidAntennaPairsHandle);

            maxModulationBits = new NativeArray<int>(allAntennaPairs.Length, Allocator.TempJob);
            minSymbolRate = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            maxSymbolRate = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            maxDataRate = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            minDataRate = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            maxSteps = new NativeArray<int>(allAntennaPairs.Length, Allocator.TempJob);
            var boundSymbolRateJob = new RateBoundariesJob
            {
                pairs = allValidAntennaPairs.AsDeferredJobArray(),
                antennas = antennaDataList,
                encoder = matchedEncoder,
                maxModulationBits = maxModulationBits,
                minSymbolRate = minSymbolRate,
                maxSymbolRate = maxSymbolRate,
                maxDataRate = maxDataRate,
                minDataRate = minDataRate,
                maxSteps = maxSteps
            }.Schedule(allValidAntennaPairs, 16, matchedEncoderJob);
            JobHandle.ScheduleBatchedJobs();

            pathLoss = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            var pathLossJob = new PathLossJob
            {
                txPos = txPos,
                rxPos = rxPos,
                freq = txFreq,
                pathloss = pathLoss
            }.Schedule(allValidAntennaPairs, 16, extractDataJob);

            pointingLoss = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            var pointingLossJob = new PointingLossJob
            {
                txPos = txPos,
                rxPos = rxPos,
                txDir = txDir,
                rxDir = rxDir,
                txBeamwidth = txBeamwidth,
                rxBeamwidth = rxBeamwidth,
                losses = pointingLoss
            }.Schedule(allValidAntennaPairs, 16, extractDataJob);

            rxPower = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            var rxPowerJob = new RxPowerJob
            {
                txGain = txGain,
                rxGain = rxGain,
                txPower = txPower,
                pathLoss = pathLoss,
                pointLoss = pointingLoss,
                rxPower = rxPower
            }.Schedule(allValidAntennaPairs, 128, JobHandle.CombineDependencies(pathLossJob, pointingLossJob));

            bodyNoise = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            var bodyNoiseJob = new CalcAntennaBodyNoise
            {
                occluders = occluders,
                rxPrecalcNoise = rxPrecalcNoise,
                rxHome = rxHome,
                rxPos = rxPos,
                rxGain = rxGain,
                rxBeamwidth = rxBeamwidth,
                rxDir = rxDir,
                rxFreq = rxFreq,
                bodyNoise = bodyNoise,
            }.Schedule(allValidAntennaPairs, 8, extractDataJob);

            atmosphereNoise = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            var atmoNoiseJob = new CalcAntennaAtmoNoise
            {
                txPos = txPos,
                rxPos = rxPos,
                rxFreq = rxFreq,
                rxHome = rxHome,
                rxSurfaceNormal = rxSurfaceNormal,
                atmoNoise = atmosphereNoise
            }.Schedule(allValidAntennaPairs, 8, extractDataJob);

            noiseTemp = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            n0 = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            var noiseCalcHandle = new LateCalcAntennaNoise
            {
                bodyNoise = bodyNoise,
                atmoNoise = atmosphereNoise,
                rxAMW = rxAMW,
                noiseTemp = noiseTemp,
                N0 = n0,
            }.Schedule(allValidAntennaPairs, 64, JobHandle.CombineDependencies(bodyNoiseJob, atmoNoiseJob));

            minEb = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            var minEbJob = new MinEbJob
            {
                encoder = matchedEncoder,
                N0 = n0,
                minEb = minEb,
            }.Schedule(allValidAntennaPairs, 128, JobHandle.CombineDependencies(noiseCalcHandle, matchedEncoderJob));

            maxTheoreticalBitRate = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            var maxTheoreticalBitRateJob = new MaxTheoreticalBitRateJob
            {
                rxPower = rxPower,
                minEb = minEb,
                maxBitRate = maxTheoreticalBitRate,
            }.Schedule(allValidAntennaPairs, 128, JobHandle.CombineDependencies(rxPowerJob, minEbJob));

            symbolRate = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            modulationBits = new NativeArray<int>(allAntennaPairs.Length, Allocator.TempJob);
            dataRate = new NativeArray<float>(allAntennaPairs.Length, Allocator.TempJob);
            var selectBitRateJob = new SelectBitRateJob
            {
                maxBitRate = maxTheoreticalBitRate,
                minSymbolRate = minSymbolRate,
                maxSymbolRate = maxSymbolRate,
                maxModulationBits = maxModulationBits,
                rxPower = rxPower,
                minEb = minEb,
                encoder = matchedEncoder,
                chosenSymbolRate = symbolRate,
                modulationBits = modulationBits,
                chosenBitRate = dataRate,
            }.Schedule(allValidAntennaPairs, 16, JobHandle.CombineDependencies(maxTheoreticalBitRateJob, boundSymbolRateJob));

            rateSteps = new NativeArray<int>(allAntennaPairs.Length, Allocator.TempJob);
            var rateStepsJob = new CountRateSteps
            {
                actualRate = dataRate,
                bestRate = maxDataRate,
                numSteps = rateSteps
            }.Schedule(allValidAntennaPairs, 16, selectBitRateJob);

            bestMap = new NativeHashMap<int2, int>(allNodePairs.Capacity, Allocator.TempJob);
            var bestLinkJob = new GetBestLinkJob
            {
                connections = nodeRowMap,
                dataRate = dataRate,
                best = bestMap,
            }.Schedule(JobHandle.CombineDependencies(selectBitRateJob, sortCalculations));

            precomputeJobHandle = JobHandle.CombineDependencies(bestLinkJob, rateStepsJob);

            /*
            if ((distance < tx.MinimumDistance) || (distance < rx.MinimumDistance)) return false;
            */
            tempAllocationsActive = true;
            JobHandle.ScheduleBatchedJobs();
            Profiler.EndSample();
        }

        public void Abort()
        {
            if (!tempAllocationsActive)
                return;
            precomputeJobHandle.Complete();
            DisposeJobData();
        }

        public void Complete(RACommNetwork RACN)
        {
            if (!tempAllocationsActive)
                return;
            Profiler.BeginSample("RealAntennas PreCompute.Complete.Gather");
            precomputeJobHandle.Complete();
            Profiler.EndSample();
            Profiler.BeginSample("RealAntennas PreCompute.Complete.Linkages");

            foreach (var pair in allNodePairs)
            {
                if (pair.x <= pair.y && pair.y < RACN.Nodes.Count)
                {
                    var p2 = new int2(pair.y, pair.x);
                    if (validMap.TryGetValue(pair, out bool valid) && valid &&
                        validMap.TryGetValue(p2, out bool valid2) && valid2 &&
                        bestMap.TryGetValue(pair, out int row) &&
                        bestMap.TryGetValue(p2, out int row2) &&
                        row >= 0 && row2 >= 0 &&
                        dataRate[row] > 0 && dataRate[row2] > 0)
                    {
                        // Connect Successful
                        RACommNode a = RACN.Nodes[pair.x] as RACommNode;
                        RACommNode b = RACN.Nodes[pair.y] as RACommNode;
                        int4 fwdQuad = allValidAntennaPairs[row];
                        int4 revQuad = allValidAntennaPairs[row2];
                        /*
                        RealAntenna fwdTx = allAntennasReverse[fwdQuad.z];
                        RealAntenna fwdRx = allAntennasReverse[fwdQuad.w];
                        RealAntenna revTx = allAntennasReverse[revQuad.z];
                        RealAntenna revRx = allAntennasReverse[revQuad.w];
                        double fwd = dataRate[row];
                        double rev = dataRate[row2];
                        double FwdBestDataRate = maxDataRate[row];
                        double RevBestDataRate = maxDataRate[row2];
                        */
                        double FwdMetric = 1.0 - ((float)rateSteps[row] / (maxSteps[row] + 1));
                        double RevMetric = 1.0 - ((float)rateSteps[row2] / (maxSteps[row2] + 1));
                        //RACN.MakeLink(fwdTx, fwdRx, revTx, revRx, a, b, (a.position - b.position).magnitude, fwd, rev, FwdBestDataRate, FwdMetric, RevMetric);
                        RACN.MakeLink(allAntennasReverse[fwdQuad.z],
                            allAntennasReverse[fwdQuad.w],
                            allAntennasReverse[revQuad.z],
                            allAntennasReverse[revQuad.w],
                            a,
                            b,
                            (a.position - b.position).magnitude,
                            dataRate[row],
                            dataRate[row2],
                            maxDataRate[row],
                            FwdMetric,
                            RevMetric);
                    } else
                    {
                        RACN.DoDisconnect(RACN.Nodes[pair.x], RACN.Nodes[pair.y]);
                    }
                }
            }

            if (RACN.DebugAntenna is RealAntenna)
            {
                var nodePairs = StringBuilderCache.Acquire();
                var antPairs = StringBuilderCache.Acquire();
                allAntennas.TryGetValue(RACN.DebugAntenna, out int targetIndex);
                foreach (var pair in allNodePairs)
                {
                    if (pair.x <= pair.y)
                    {
                        var p2 = new int2(pair.y, pair.x);
                        if (validMap.TryGetValue(pair, out bool valid) &&
                            validMap.TryGetValue(p2, out bool valid2))
                        {
                            RACommNode a = RACN.Nodes[pair.x] as RACommNode;
                            RACommNode b = RACN.Nodes[pair.y] as RACommNode;
                            if (a == RACN.DebugAntenna.ParentNode || b == RACN.DebugAntenna.ParentNode)
                            {
                                nodePairs.AppendLine($"Nodes: {a.name} -> {b.name} Valid: {valid} / {valid2}");
                            }
                        }
                    }
                }
                for (int i = 0; i < allValidAntennaPairs.Length; i++)
                {
                    var quad = allValidAntennaPairs[i];
                    if (targetIndex == quad.z || targetIndex == quad.w)
                    {
                        RACommNode a = RACN.Nodes[quad.x] as RACommNode;
                        RACommNode b = RACN.Nodes[quad.y] as RACommNode;
                        RealAntenna tx = allAntennasReverse[quad.z];
                        RealAntenna rx = allAntennasReverse[quad.w];
                        if (tx == RACN.DebugAntenna || rx == RACN.DebugAntenna)
                        {
                            float noise = atmosphereNoise[i] + bodyNoise[i] + noiseTemp[i];
                            double3 txToRx = rxPos[i] - txPos[i];
                            double3 rxToTx = txPos[i] - rxPos[i];
                            double txToRxAngle = MathUtils.Angle2(txToRx, txDir[i]);
                            double rxToTxAngle = MathUtils.Angle2(rxToTx, rxDir[i]);
                            float txPointLoss = Physics.PointingLoss(txToRxAngle, txBeamwidth[i]);
                            float rxPointLoss = Physics.PointingLoss(rxToTxAngle, rxBeamwidth[i]);

                            antPairs.AppendLine($"Testing {a.name}:{tx} --> {b.name}:{rx}");
                            antPairs.AppendLine($"  TxP: {txPower[i]:F1}dBm");
                            antPairs.AppendLine($"  RxP: {rxPower[i]:F1}dBm");
                            antPairs.AppendLine($"  Noise:{noise:F2}");
                            antPairs.AppendLine($"  N0:{n0[i]:F2}dB/Hz");
                            antPairs.AppendLine($"  minEb:{minEb[i]:F2}");
                            antPairs.AppendLine($"  txPos: {txPos[i]}  rxPos: {rxPos[i]}");
                            antPairs.AppendLine($"  txToRx: {txToRx}  rxToTx: {rxToTx}");
                            antPairs.AppendLine($"  txDir: {txDir[i]}  rxDir: {rxDir[i]}");
                            antPairs.AppendLine($"  TxBW: {txBeamwidth[i]:F2}  RxBW: {rxBeamwidth[i]:F2}");
                            antPairs.AppendLine($"  TxToRxAngle: {txToRxAngle:F2}  RxToTxAngle: {rxToTxAngle:F2}");
                            antPairs.AppendLine($"  TxPointLoss: {txPointLoss:F1}dB  RxPointLoss: {rxPointLoss:F1}dB");
                            antPairs.AppendLine($"  PointLoss:{pointingLoss[i]:F1}dB");
                            antPairs.AppendLine($"  PathLoss:{pathLoss[i]:F1}dB");
                            antPairs.AppendLine($"  Rates:{minDataRate[i]:F1}/{dataRate[i]:F1}/{maxDataRate[i]:F1}  steps:{rateSteps[i]}");
                        }
                        else
                        {
                            antPairs.AppendLine($"Thought indices {quad.z} or {quad.w} matched target {targetIndex} but discovered antennas {tx} | {rx} | looking for {RACN.DebugAntenna}");
                        }
                    }
                }
                UnityEngine.Debug.Log($"[RealAntennas.Jobs] {nodePairs.ToStringAndRelease()}");
                UnityEngine.Debug.Log($"[RealAntennas.Jobs] {antPairs.ToStringAndRelease()}");
                RACN.DebugAntenna = null;
            }
            DisposeJobData();
            Profiler.EndSample();
        }

        private void DisposeJobData()
        {
            if (!tempAllocationsActive)
                return;

            allNodes.Dispose();
            occluders.Dispose();
            validMap.Dispose();
            bestMap.Dispose();
            antennaNoise.Dispose();
            nodePairsValid.Dispose();
            occlusionValid.Dispose();
            allValidAntennaPairs.Dispose();

            txPower.Dispose();
            txFreq.Dispose();
            txGain.Dispose();
            txBeamwidth.Dispose();
            txHome.Dispose();
            txPos.Dispose();
            txDir.Dispose();

            rxFreq.Dispose();
            rxGain.Dispose();
            rxBeamwidth.Dispose();
            rxHome.Dispose();
            rxPos.Dispose();
            rxDir.Dispose();
            rxAMW.Dispose();
            rxPrecalcNoise.Dispose();
            rxSurfaceNormal.Dispose();

            matchedEncoder.Dispose();
            maxModulationBits.Dispose();
            minSymbolRate.Dispose();
            maxSymbolRate.Dispose();
            pathLoss.Dispose();
            pointingLoss.Dispose();
            rxPower.Dispose();
            atmosphereNoise.Dispose();
            bodyNoise.Dispose();
            noiseTemp.Dispose();
            n0.Dispose();
            minEb.Dispose();
            maxTheoreticalBitRate.Dispose();
            symbolRate.Dispose();
            modulationBits.Dispose();
            dataRate.Dispose();
            maxDataRate.Dispose();
            minDataRate.Dispose();
            maxSteps.Dispose();
            rateSteps.Dispose();
            nodeRowMap.Dispose();

            tempAllocationsActive = false;
        }

        internal static float NoiseFromOccluders(in AntennaData ant, in NativeArray<OccluderInfo> occluders) =>
            NoiseFromOccluders(ant.position, ant.gain, ant.dir, ant.freq, ant.beamwidth, occluders);
        internal static float NoiseFromOccluders(double3 position, float gain, float3 dir, float freq, float beamwidth, in NativeArray<OccluderInfo> occluders)
        {
            if (gain <= Physics.MaxOmniGain) return 0;
            float noise = 0;
            for (int i = 0; i < occluders.Length; i++)
            {
                OccluderInfo occluder = occluders[i];
                float t = occluder.isStar ? Physics.StarRadioTemp(occluder.temp, freq) : occluder.temp;
                noise += Physics.BodyNoiseTemp(position, gain, dir, occluder.position, occluder.radius, t, beamwidth);
            }
            return noise;
        }

        private void SetupCommNodes(out NativeList<CNInfo> infos)
        {
            if ((RACommNetScenario.Instance as RACommNetScenario)?.Network?.CommNet is RACommNetwork net)
            {
                infos = new NativeList<CNInfo>(net.Nodes.Count, Allocator.TempJob);
                foreach (RACommNode node in net.Nodes)
                {
                    Vector3d surfN = node.GetSurfaceNormalVector();
                    infos.Add(new CNInfo()
                    {
                        position = new double3(node.precisePosition.x, node.precisePosition.y, node.precisePosition.z),
                        isHome = node.isHome,
                        canComm = node.CanComm(),
                        surfaceNormal = new double3(surfN.x, surfN.y, surfN.z),
                        //name = $"{node}",
                    });
                }
            }
            else infos = new NativeList<CNInfo>(Allocator.TempJob);
        }

        // Gather all CelestialBodies and build Jobs structs
        private void SetupOccluders(out NativeArray<OccluderInfo> occluders)
        {
            int num = FlightGlobals.Bodies.Count;
            occluders = new NativeArray<OccluderInfo>(num, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            int index = 0;
            foreach (var body in FlightGlobals.Bodies)
            {
                occluders[index] = new OccluderInfo()
                {
                    position = new double3(body.position.x, body.position.y, body.position.z),
                    radius = Convert.ToSingle(body.Radius),
                    temp = Physics.BodyBaseTemperature(body),
                    isStar = body.isStar,
                    //name = $"{body}",
                };
                index++;
            }
        }


        // Iterate through all comm nodes, collect each RealAntenna, and extract a copy of data for precomputation
        private NativeArray<AntennaData> GatherAllAntennas()
        {
            allAntennas.Clear();
            allAntennasReverse.Clear();
            var antennaDatas = new NativeList<AntennaData>(Allocator.Persistent);
            int index = 0;
            if ((RACommNetScenario.Instance as RACommNetScenario)?.Network?.CommNet is RACommNetwork net)
                foreach (RACommNode node in net.Nodes)
                    foreach (RealAntenna ra in node.RAAntennaList)
                    {
                        allAntennas.Add(ra, index);
                        allAntennasReverse.Add(index, ra);
                        antennaDatas.Add(new AntennaData()
                        {
                            txPower = ra.TxPower,
                            freq = ra.Frequency,
                            gain = ra.Gain,
                            beamwidth = ra.Beamwidth,
                            isHome = node.isHome,
                            AMW = Physics.AntennaMicrowaveTemp(ra),
                            encoder = new Encoder(ra.Encoder),
                            maxSymbolRate = Convert.ToSingle(ra.SymbolRate),
                            minSymbolRate = Convert.ToSingle(ra.MinSymbolRate),
                            modulationBits = (ra as RealAntennaDigital).modulator.ModulationBits,
                        });
                        index++;
                    }
            return antennaDatas.AsArray();
        }

        internal void UpdateAllAntennas()
        {
            foreach (var x in allAntennas)
            {
                RealAntenna ra = x.Key;
                AntennaData data = antennaDataList[x.Value];
                if (ra.ParentNode is RACommNode node)
                {
                    data.position.x = node.precisePosition.x;
                    data.position.y = node.precisePosition.y;
                    data.position.z = node.precisePosition.z;
                    //data.position = new double3(node.precisePosition.x, node.precisePosition.y, node.precisePosition.z);
                    data.dir = ra.ToTarget;
                }
                antennaDataList[x.Value] = data;
            }
        }

        // Iterate through all CommNode pairs.
        // Produce a NativeList of all indices of antenna pairs to be computed. (int4: node1, node2, ant1, ant2)
        // Exclude combinations of antennas that are incompatible (eg out-of-band)
        public void PairAllAntennasAndNodes()
        {
            allAntennaPairs.Clear();
            allNodePairs.Clear();
            if ((RACommNetScenario.Instance as RACommNetScenario)?.Network?.CommNet is RACommNetwork net)
            {
                int index1 = 0;
                foreach (RACommNode node1 in net.Nodes)
                {
                    int index2 = 0;
                    foreach (RACommNode node2 in net.Nodes)
                    {
                        if (!ReferenceEquals(node1, node2))
                            foreach (RealAntenna a_ra in node1.RAAntennaList)
                                foreach (RealAntenna b_ra in node2.RAAntennaList)
                                    if (a_ra.Compatible(b_ra))
                                        allAntennaPairs.Add(new int4(index1, index2, allAntennas[a_ra], allAntennas[b_ra]));
                        allNodePairs.Add(new int2(index1, index2));
                        index2++;
                    }
                    index1++;
                }
            }
        }
    }
}
