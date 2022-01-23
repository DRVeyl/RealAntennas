using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;

namespace RealAntennas.Network
{
    public struct LinkDetails
    {
        public RACommNode txNode;
        public RACommNode rxNode;
        public RealAntenna tx;
        public RealAntenna rx;
        public float atmosphereNoise;
        public float antennaElevation;
        public float bodyNoise;
        public float noiseTemp;
        public float noise;
        public double3 txPos;
        public double3 rxPos;
        public double3 txToRx;
        public double3 rxToTx;
        public double txToRxAngle;
        public double rxToTxAngle;
        public float txBeamwidth;
        public float rxBeamwidth;
        public float txPointLoss;
        public float rxPointLoss;
        public float txPower;
        public float rxPower;
        public float minSymbolRate;
        public float N0;
        public float minEb;
        public double3 txDir;
        public double3 rxDir;
        public float pathLoss;
        public float pointingLoss;
        public float minDataRate;
        public float dataRate;
        public float maxDataRate;
        public int rateSteps;

        public override string ToString()
        {
            var s = StringBuilderCache.Acquire();
            s.AppendLine($"{txNode.name}:{tx} -> {rxNode.name}:{rx}");
            s.AppendLine($"  TxP: {txPower:F1}dBm");
            s.AppendLine($"  RxP: {rxPower:F1}dBm");
            s.AppendLine($"  Noise:{noise:F2}");
            s.AppendLine($"  N0:{N0:F2}dB/Hz");
            s.AppendLine($"  minEb:{minEb:F2}");
            s.AppendLine($"  txPos: {txPos}  rxPos: {rxPos}");
            s.AppendLine($"  txToRx: {txToRx}  rxToTx: {rxToTx}");
            s.AppendLine($"  txDir: {txDir}  rxDir: {rxDir}");
            s.AppendLine($"  TxBW: {txBeamwidth:F2}  RxBW: {rxBeamwidth:F2}");
            s.AppendLine($"  TxToRxAngle: {txToRxAngle:F2}  RxToTxAngle: {rxToTxAngle:F2}");
            s.AppendLine($"  TxPointLoss: {txPointLoss:F1}dB  RxPointLoss: {rxPointLoss:F1}dB");
            s.AppendLine($"  PointLoss:{pointingLoss:F1}dB");
            s.AppendLine($"  PathLoss:{pathLoss:F1}dB");
            s.AppendLine($"  Rates:{minDataRate:F1}/{dataRate:F1}/{maxDataRate:F1}  steps:{rateSteps}");
            return s.ToStringAndRelease();
        }
    }
}
