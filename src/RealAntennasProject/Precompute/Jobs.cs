using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace RealAntennas.Precompute
{
    // Derive the noise temp of an antenna.  Skip homes, since they do not have valid pointing yet.
    [BurstCompile]
    public struct PreCalcAntennaNoise : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AntennaData> antennas;
        [ReadOnly] public NativeArray<OccluderInfo> occluders;
        [WriteOnly] public NativeArray<double> noiseTemp;

        public void Execute(int index)
        {
            AntennaData ant = antennas[index];
            noiseTemp[index] = ant.isHome ? 0 : Precompute.NoiseFromOccluders(ant, occluders);
        }
    }

    // Collect receiver noise.  Refer to the cache'd result, or derive new for isHome antennas.
    [BurstCompile]
    public struct LateCalcAntennaNoise : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<int4> pairs;
        [ReadOnly] public NativeArray<AntennaData> antennas;
        [ReadOnly] public NativeArray<CNInfo> nodes;
        [ReadOnly] public NativeArray<OccluderInfo> occluders;
        [ReadOnly] public NativeArray<double> noiseTempPrecalc;
        [WriteOnly] public NativeArray<double> atmosphereNoise;
        [WriteOnly] public NativeArray<double> bodyNoise;
        [WriteOnly] public NativeArray<double> noiseTemp;
        [WriteOnly] public NativeArray<double> N0;

        public void Execute(int index)
        {
            int x = pairs[index].x;
            int y = pairs[index].y;
            int w = pairs[index].w; 
            AntennaData ant = antennas[w];
            CNInfo txNode = nodes[x];
            CNInfo rxNode = nodes[y];
            double vBodyNoise = ant.isHome ? Precompute.NoiseFromOccluders(ant, occluders) : noiseTempPrecalc[w];
            double vAtmoNoise = ant.isHome ? Physics.AtmosphericTemp(ant.position, rxNode.surfaceNormal, txNode.position, ant.freq) : 0;
            double vNoiseTemp = vBodyNoise + ant.AMW + vAtmoNoise + Physics.CMB;
            atmosphereNoise[index] = vAtmoNoise;
            bodyNoise[index] = vBodyNoise;
            noiseTemp[index] = vNoiseTemp;
            N0[index] = Physics.NoiseSpectralDensity(vNoiseTemp);
        }
    }

    public struct PathLossJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<int4> pairs;
        [ReadOnly] public NativeArray<AntennaData> antennas;
        [WriteOnly] public NativeArray<double> pathloss;

        public void Execute(int index)
        {
            double3 pos1 = antennas[pairs[index].z].position;
            double3 pos2 = antennas[pairs[index].w].position;
            double freq = antennas[pairs[index].w].freq;
            double dist = math.distance(pos1, pos2);
            pathloss[index] = Physics.PathLoss(dist, freq);
        }
    }

    public struct PointingLossJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<int4> pairs;
        [ReadOnly] public NativeArray<AntennaData> antennas;
        [WriteOnly] public NativeArray<double> losses;

        public void Execute(int index)
        {
            AntennaData tx = antennas[pairs[index].z];
            AntennaData rx = antennas[pairs[index].w];
            double3 txToRx = rx.position - tx.position;
            double3 rxToTx = tx.position - rx.position;

            double txToRxAngle = MathUtils.Angle2(txToRx, tx.dir);
            double rxToTxAngle = MathUtils.Angle2(rxToTx, rx.dir);
            double txPointLoss = Physics.PointingLoss(txToRxAngle, Physics.Beamwidth(tx.gain));
            double rxPointLoss = Physics.PointingLoss(rxToTxAngle, Physics.Beamwidth(rx.gain));
            losses[index] = txPointLoss + rxPointLoss;
        }
    }

    public struct RxPowerJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<int4> pairs;
        [ReadOnly] public NativeArray<AntennaData> antennas;
        [ReadOnly] public NativeArray<double> pointLoss;
        [ReadOnly] public NativeArray<double> pathLoss;
        [WriteOnly] public NativeArray<double> rxPower;

        public void Execute(int index)
        {
            AntennaData tx = antennas[pairs[index].z];
            AntennaData rx = antennas[pairs[index].w];
            rxPower[index] = tx.gain + tx.txPower + rx.gain - pointLoss[index] - pathLoss[index];
        }
    }

    public struct MinEbJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<Encoder> encoder;
        [ReadOnly] public NativeArray<double> N0;
        [WriteOnly] public NativeArray<double> minEb;

        public void Execute(int index)
        {
            minEb[index] = encoder[index].RequiredEbN0 + N0[index];
        }
    }

    public struct MaxBitRateJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<double> rxPower;
        [ReadOnly] public NativeArray<double> minEb;
        [WriteOnly] public NativeArray<double> maxBitRate;

        public void Execute(int index)
        {
            double maxBitRateLog = rxPower[index] - minEb[index];       // in dB*Hz
            maxBitRate[index] = RATools.LinearScale(maxBitRateLog);
        }
    }

    public struct SelectBitRateJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<double> maxBitRate;
        [ReadOnly] public NativeArray<float> maxSymbolRate;
        [ReadOnly] public NativeArray<float> minSymbolRate;
        [ReadOnly] public NativeArray<int> maxModulationBits;
        [ReadOnly] public NativeArray<double> rxPower;
        [ReadOnly] public NativeArray<double> minEb;
        [WriteOnly] public NativeArray<double> chosenSymbolRate;
        [WriteOnly] public NativeArray<int> modulationBits;

        public void Execute(int index)
        {
            double targetRate = 0;
            int negotiatedBits = 0;
            if (maxBitRate[index] < minSymbolRate[index]) { }
            else if (maxBitRate[index] <= maxSymbolRate[index])
            {
                // The required Eb/N0 occurs at a lower symbol rate than we are capable of at 1 bit/sec/Hz.
                // Step down the symbol rate and modulate at 1 bit/sec/Hz (BPSK).
                // (What if the modulator only supports schemes with >1 bits/symbol?)
                // (Then our minimum EbN0 is an underestimate.)
                double ratio = maxBitRate[index] / maxSymbolRate[index];
                double log2 = math.trunc(math.log2(ratio));
                targetRate = maxSymbolRate[index] * math.pow(2, log2);
                negotiatedBits = 1;
            }
            else
            {
                // margin = RxPower - (N0 + LogScale(MaxSymbolRate) - Encoder.RequriedEbN0)
                //        = RxPower - minEb - LogScale(MaxSymbolRate)

                double margin = rxPower[index] - minEb[index] - RATools.LogScale(maxSymbolRate[index]);
                margin = math.clamp(margin, 0, 100);
                negotiatedBits = math.min(maxModulationBits[index], 1 + System.Convert.ToInt32(math.floor(margin / 3)));
                targetRate = maxSymbolRate[index];
            }
            chosenSymbolRate[index] = targetRate;
            modulationBits[index] = negotiatedBits;
        }
    }


    public struct MatchedEncoderJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<int4> pairs;
        [ReadOnly] public NativeArray<AntennaData> antennas;
        [WriteOnly] public NativeArray<Encoder> encoder;

        public void Execute(int index)
        {
            AntennaData tx = antennas[pairs[index].z];
            AntennaData rx = antennas[pairs[index].w];
            encoder[index] = tx.encoder.BestMatching(rx.encoder);
        }
    }

    public struct BoundSymbolRateJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<int4> pairs;
        [ReadOnly] public NativeArray<AntennaData> antennas;
        [WriteOnly] public NativeArray<float> maxSymbolRate;
        [WriteOnly] public NativeArray<float> minSymbolRate;
        [WriteOnly] public NativeArray<int> maxModulationBits;

        public void Execute(int index)
        {
            AntennaData tx = antennas[pairs[index].z];
            AntennaData rx = antennas[pairs[index].w];
            float max = math.min(tx.maxSymbolRate, rx.maxSymbolRate);
            float min = math.max(tx.minSymbolRate, rx.minSymbolRate);
            if (min > max)
            {
                min = 0;
                max = 0;
            }
            maxSymbolRate[index] = max;
            minSymbolRate[index] = min;
            maxModulationBits[index] = math.min(tx.modulationBits, rx.modulationBits);
        }
    }

    public struct SortCalculationsJob : IJob
    {
        [ReadOnly] public NativeArray<int4> pairs;
        [WriteOnly] public NativeMultiHashMap<int2, int> connections;

        public void Execute()
        {
            for (int i=0; i<pairs.Length; i++)
            {
                connections.Add(new int2(pairs[i].x, pairs[i].y), i);
            }
        }
    }

    public struct GetBestLinkJob : IJob
    {
        [ReadOnly] public NativeMultiHashMap<int2, int> connections;
        [ReadOnly] public NativeArray<double> symbolRate;
        [ReadOnly] public NativeArray<int> bits;
        [WriteOnly] public NativeHashMap<int2, int> best;
        public void Execute()
        {
            using var keys = connections.GetKeyArray(Allocator.Temp);
            for (int i=0; i<keys.Length; i++)
            {
                double bestRate = -1;
                int bestIndex = -1;
                int2 key = keys[i];
                foreach (var index in connections.GetValuesForKey(key))
                {
                    double rate = symbolRate[index] * bits[index];
                    if (rate > bestRate)
                    {
                        bestRate = rate;
                        bestIndex = index;
                    }
                }
                best.TryAdd(key, bestIndex);
            }
        }
    }

}

