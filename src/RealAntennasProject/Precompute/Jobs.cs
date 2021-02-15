using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System;

namespace RealAntennas.Precompute
{
    // Derive the noise temp of an antenna.  Skip homes, since they do not have valid pointing yet.
    [BurstCompile]
    public struct PreCalcAntennaNoise : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AntennaData> antennas;
        [ReadOnly] public NativeArray<OccluderInfo> occluders;
        [WriteOnly] public NativeArray<float> noiseTemp;

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
        [ReadOnly] public NativeArray<float> noiseTempPrecalc;
        [WriteOnly] public NativeArray<float> atmosphereNoise;
        [WriteOnly] public NativeArray<float> bodyNoise;
        [WriteOnly] public NativeArray<float> noiseTemp;
        [WriteOnly] public NativeArray<float> N0;

        public void Execute(int index)
        {
            int x = pairs[index].x;
            int y = pairs[index].y;
            int w = pairs[index].w; 
            AntennaData ant = antennas[w];
            CNInfo txNode = nodes[x];
            CNInfo rxNode = nodes[y];
//            float vBodyNoise = ant.isHome ? Precompute.NoiseFromOccluders(ant, occluders) : noiseTempPrecalc[w];
            float vBodyNoise = ant.isHome ? Precompute.NoiseFromOccluders(ant.position, ant.gain, ant.dir, ant.freq, occluders) : noiseTempPrecalc[w];
            float vAtmoNoise = ant.isHome ? Convert.ToSingle(Physics.AtmosphericTemp(ant.position, rxNode.surfaceNormal, txNode.position, ant.freq)) : 0;
            float vNoiseTemp = vBodyNoise + ant.AMW + vAtmoNoise + Physics.CMB;
            atmosphereNoise[index] = vAtmoNoise;
            bodyNoise[index] = vBodyNoise;
            noiseTemp[index] = vNoiseTemp;
            N0[index] = Physics.NoiseSpectralDensity(vNoiseTemp);
        }
    }

    [BurstCompile]
    public struct PathLossJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<int4> pairs;
        [ReadOnly] public NativeArray<AntennaData> antennas;
        [WriteOnly] public NativeArray<float> pathloss;

        public void Execute(int index)
        {
            double3 pos1 = antennas[pairs[index].z].position;
            double3 pos2 = antennas[pairs[index].w].position;
            float freq = antennas[pairs[index].w].freq;
            float dist = Convert.ToSingle(math.distance(pos1, pos2));
            pathloss[index] = Physics.PathLoss(dist, freq);
        }
    }

    [BurstCompile]
    public struct PointingLossJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<int4> pairs;
        [ReadOnly] public NativeArray<AntennaData> antennas;
        [WriteOnly] public NativeArray<float> losses;

        public void Execute(int index)
        {
            AntennaData tx = antennas[pairs[index].z];
            AntennaData rx = antennas[pairs[index].w];
            double3 txToRx = rx.position - tx.position;
            double3 rxToTx = tx.position - rx.position;

            float txToRxAngle = Convert.ToSingle(MathUtils.Angle2(txToRx, tx.dir));
            float rxToTxAngle = Convert.ToSingle(MathUtils.Angle2(rxToTx, rx.dir));
            float txPointLoss = Physics.PointingLoss(txToRxAngle, Physics.Beamwidth(tx.gain));
            float rxPointLoss = Physics.PointingLoss(rxToTxAngle, Physics.Beamwidth(rx.gain));
            losses[index] = txPointLoss + rxPointLoss;
        }
    }

    [BurstCompile]
    public struct RxPowerJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<int4> pairs;
        [ReadOnly] public NativeArray<AntennaData> antennas;
        [ReadOnly] public NativeArray<float> pointLoss;
        [ReadOnly] public NativeArray<float> pathLoss;
        [WriteOnly] public NativeArray<float> rxPower;

        public void Execute(int index)
        {
            AntennaData tx = antennas[pairs[index].z];
            AntennaData rx = antennas[pairs[index].w];
            rxPower[index] = tx.gain + tx.txPower + rx.gain - pointLoss[index] - pathLoss[index];
        }
    }

    [BurstCompile]
    public struct MinEbJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<Encoder> encoder;
        [ReadOnly] public NativeArray<float> N0;
        [WriteOnly] public NativeArray<float> minEb;

        public void Execute(int index)
        {
            minEb[index] = encoder[index].RequiredEbN0 + N0[index];
        }
    }

    [BurstCompile]
    public struct MaxTheoreticalBitRateJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<float> rxPower;
        [ReadOnly] public NativeArray<float> minEb;
        [WriteOnly] public NativeArray<float> maxBitRate;

        public void Execute(int index)
        {
            float maxBitRateLog = rxPower[index] - minEb[index];       // in dB*Hz
            maxBitRate[index] = RATools.LinearScale(maxBitRateLog);
        }
    }

    [BurstCompile]
    public struct SelectBitRateJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<float> maxBitRate;
        [ReadOnly] public NativeArray<float> maxSymbolRate;
        [ReadOnly] public NativeArray<float> minSymbolRate;
        [ReadOnly] public NativeArray<int> maxModulationBits;
        [ReadOnly] public NativeArray<float> rxPower;
        [ReadOnly] public NativeArray<float> minEb;
        [ReadOnly] public NativeArray<Encoder> encoder;
        [WriteOnly] public NativeArray<float> chosenSymbolRate;
        [WriteOnly] public NativeArray<int> modulationBits;
        [WriteOnly] public NativeArray<float> chosenBitRate;

        public void Execute(int index)
        {
            float targetRate = 0;
            int negotiatedBits = 0;
            if (maxBitRate[index] < minSymbolRate[index]) { }
            else if (maxBitRate[index] <= maxSymbolRate[index])
            {
                // The required Eb/N0 occurs at a lower symbol rate than we are capable of at 1 bit/sec/Hz.
                // Step down the symbol rate and modulate at 1 bit/sec/Hz (BPSK).
                // (What if the modulator only supports schemes with >1 bits/symbol?)
                // (Then our minimum EbN0 is an underestimate.)
                float ratio = maxBitRate[index] / maxSymbolRate[index];
                float log2 = math.trunc(math.log2(ratio));
                targetRate = maxSymbolRate[index] * math.pow(2, log2);
                negotiatedBits = 1;
            }
            else
            {
                // margin = RxPower - (N0 + LogScale(MaxSymbolRate) - Encoder.RequriedEbN0)
                //        = RxPower - minEb - LogScale(MaxSymbolRate)

                float margin = rxPower[index] - minEb[index] - RATools.LogScale(maxSymbolRate[index]);
                margin = math.clamp(margin, 0, 100);
                negotiatedBits = math.min(maxModulationBits[index], 1 + System.Convert.ToInt32(math.floor(margin / 3)));
                targetRate = maxSymbolRate[index];
            }
            chosenSymbolRate[index] = targetRate;
            modulationBits[index] = negotiatedBits;
            chosenBitRate[index] = targetRate * encoder[index].CodingRate * math.pow(2, negotiatedBits - 1);
        }
    }


    [BurstCompile]
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

    [BurstCompile]
    public struct RateBoundariesJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<int4> pairs;
        [ReadOnly] public NativeArray<AntennaData> antennas;
        [ReadOnly] public NativeArray<Encoder> encoder;
        [WriteOnly] public NativeArray<float> maxSymbolRate;
        [WriteOnly] public NativeArray<float> minSymbolRate;
        [WriteOnly] public NativeArray<int> maxModulationBits;
        [WriteOnly] public NativeArray<float> maxDataRate;
        [WriteOnly] public NativeArray<float> minDataRate;
        [WriteOnly] public NativeArray<int> maxSteps;

        public void Execute(int index)
        {
            AntennaData tx = antennas[pairs[index].z];
            AntennaData rx = antennas[pairs[index].w];
            float max = math.min(tx.maxSymbolRate, rx.maxSymbolRate);
            float min = math.max(tx.minSymbolRate, rx.minSymbolRate);
            int bits = math.min(tx.modulationBits, rx.modulationBits);
            if (min > max)
            {
                min = 0;
                max = 0;
            }
            float maxData = max * encoder[index].CodingRate * math.pow(2, bits - 1);
            float minData = min * encoder[index].CodingRate;
            maxSymbolRate[index] = max;
            minSymbolRate[index] = min;
            maxModulationBits[index] = bits;
            maxDataRate[index] = maxData;
            minDataRate[index] = minData;
            maxSteps[index] = minData > 0 ? System.Convert.ToInt32(math.trunc(math.log2(maxData / minData))) : 0;
        }
    }

    [BurstCompile]
    public struct CountRateSteps : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<float> bestRate;
        [ReadOnly] public NativeArray<float> actualRate;
        [WriteOnly] public NativeArray<int> numSteps;

        public void Execute(int index)
        {
            float best = bestRate[index];
            float actual = actualRate[index];
            numSteps[index] = actual > 0 ? System.Convert.ToInt32(math.trunc(math.log2(best / actual))) : 0;
        }
    }

    [BurstCompile]
    public struct GetBestLinkJob : IJob
    {
        [ReadOnly] public NativeMultiHashMap<int2, int> connections;
        [ReadOnly] public NativeArray<float> dataRate;
        [WriteOnly] public NativeHashMap<int2, int> best;
        public void Execute()
        {
            var keys = connections.GetKeyArray(Allocator.Temp);
            for (int i=0; i<keys.Length; i++)
            {
                float bestRate = -1;
                int bestIndex = -1;
                int2 key = keys[i];
                var iter = connections.GetValuesForKey(key);
                while (iter.MoveNext())
                {
                    int index = iter.Current;
                    float rate = dataRate[index];
                    if (rate > bestRate)
                    {
                        bestRate = rate;
                        bestIndex = index;
                    }
                }
                best.TryAdd(key, bestIndex);
            }
            keys.Dispose();
        }
    }
}
