using UnityEngine;

namespace RealAntennas
{
    public class RealAntenna : ModuleDataTransmitter
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "dBi", guiFormat = "D1")]
        public double Gain;         // Physical directionality, measured in dBi

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "dBm", guiFormat = "D1")]
        public double TxPower;       // Transmit Power in dBm (milliwatts)

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "Hz", guiFormat = "D0")]
        public double Frequency;    // Frequency in Hz

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "Hz", guiFormat = "D0")]
        public double Bandwidth;    // Bandwidth in Hz

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiFormat = "D3")]
        public double PowerEfficiency;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiFormat = "D3")]
        public double SpectralEfficiency;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiFormat = "D3")]
        public double AntennaEfficiency;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "dB", guiFormat = "D1")]
        public double NoiseFigure;     // Noise figure of receiver electronics in dB

        public double PowerDraw { get => TxPower / PowerEfficiency; }

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "dB", guiFormat = "D1")]
        public double CodingGain;      // Coding/spreading gain, for transmitters only

        protected static readonly string ModTag = "[RealAntenna] ";
        public static readonly string ModuleName = "RealAntenna";

/*        public RealAntenna(double gain, double txPower, double frequency, double bandwidth,
                            double powerEff, double specEff, double antEff, double nf, double coding)
        {
            Debug.LogFormat(ModTag + "ctor()");
            Gain = gain;
            TxPower = txPower;
            Frequency = frequency;
            Bandwidth = bandwidth;
            PowerEfficiency = powerEff;
            SpectralEfficiency = specEff;
            AntennaEfficiency = antEff;
            NoiseFigure = nf;
            CodingGain = coding;
            Debug.LogFormat(ModTag + "ctor() exit");
        }
*/
        public override string GetInfo()
        {
            return string.Format(ModTag + "\n" +
                                "<b>Gain</b>: {0}\n" +
                                "<i>Transmit Power</i>: {1}\n" +
                                "<b>Data Rate</b>: {2}\n" +
                                "<b>Bandwidth</b>: {3}\n", Gain, TxPower, DataRate, Bandwidth);
        }

        public override string ToString()
        {
            return string.Format("[+RealAntennas] {0} [{1}dB]", name, Gain);
        }

        // Use this when you don't have an actual Part.
        public void LoadFromConfigNode(ConfigNode config)
        {
            Gain = double.Parse(config.GetValue("Gain"));
            TxPower = double.Parse(config.GetValue("TxPower"));
            Frequency = double.Parse(config.GetValue("Frequency"));
            Bandwidth = double.Parse(config.GetValue("Bandwidth"));
            PowerEfficiency = double.Parse(config.GetValue("PowerEfficiency"));
            SpectralEfficiency = double.Parse(config.GetValue("SpectralEfficiency"));
            AntennaEfficiency = double.Parse(config.GetValue("AntennaEfficiency"));
            NoiseFigure = double.Parse(config.GetValue("NoiseFigure"));
            CodingGain = double.Parse(config.GetValue("CodingGain"));
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
        }

        public override float GetVesselSignalStrength()
        {
            float x = base.GetVesselSignalStrength();
            Debug.LogFormat(ModTag + "Part {0} GetVesselSignalStrength was {1}",part,x);
            return x;
        }
    }
}