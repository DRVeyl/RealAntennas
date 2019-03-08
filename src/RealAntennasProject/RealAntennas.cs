using UnityEngine;

namespace RealAntennas
{
    public class RealAntenna : ModuleDataTransmitter
    {
        [KSPField] public string gain;
        [KSPField] public string codingGain;
        [KSPField] public string powerDraw;
        [KSPField] public string txPower;
        [KSPField] public string efficiency;
        [KSPField] public string bandwidth;
        [KSPField] public string frequency;
        [KSPField] public string noiseFigure;

        public RAAntennaInfo antennaInfo;
        protected static readonly string ModTag = "[RealAntenna] ";
        public static readonly string ModuleName = "RealAntenna";

        public RealAntenna() => antennaInfo = new RAAntennaInfo();

        public RealAntenna(double gain, double codingGain, double powerDraw, double txPower, double efficiency, double bandwidth, double frequency, double noiseFigure)
        {
            antennaInfo = new RAAntennaInfo(gain, codingGain, powerDraw, txPower, efficiency, bandwidth, frequency, noiseFigure);
        }

        public override string GetInfo()
        {
            return string.Format(ModTag + "Tx: {0}dBm +{1}dBi +{2}dB Coding",
                                antennaInfo.TxPower, antennaInfo.Gain, antennaInfo.CodingGain);
        }

        public override string ToString()
        {
            return string.Format("[+RealAntennas] {0} [{1}dB]", name, antennaInfo.Gain);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            antennaInfo.SetFromConfigNode(node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            antennaInfo.SetDoublesFromStrings(gain, codingGain, powerDraw, txPower, efficiency, bandwidth, frequency, noiseFigure);
        }

        public override float GetVesselSignalStrength()
        {
            float x = base.GetVesselSignalStrength();
            Debug.LogFormat(ModTag + "Part {0} GetVesselSignalStrength was {1}",part,x);
            return x;
        }
    }
}