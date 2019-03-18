using UnityEngine;

namespace RealAntennas
{
    public class ModuleRealAntenna : ModuleDataTransmitter
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "dBi", guiFormat = "F1")]
        public double Gain;         // Physical directionality, measured in dBi

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "dBm", guiFormat = "F1")]
        public double TxPower;       // Transmit Power in dBm (milliwatts)

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "bps", guiFormat = "N0")]
        public new double DataRate;      // Maximum data rate in bits/sec

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "Hz", guiFormat = "N0")]
        public double Frequency;    // Frequency in Hz

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "Hz", guiFormat = "N0")]
        public double Bandwidth;    // Bandwidth in Hz

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiFormat = "P2")]
        public double PowerEfficiency;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiFormat = "P2")]
        public double SpectralEfficiency;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiFormat = "P2")]
        public double AntennaEfficiency;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "dB", guiFormat = "F1")]
        public double NoiseFigure;     // Noise figure of receiver electronics in dB

        public double PowerDraw { get => TxPower / PowerEfficiency; }

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "dB", guiFormat = "F1")]
        public double CodingGain;      // Coding/spreading gain, for transmitters only

        protected static readonly string ModTag = "[ModuleRealAntenna] ";
        public static readonly string ModuleName = "ModuleRealAntenna";
        public RealAntenna RAAntenna = new RealAntenna();

        public override void OnLoad(ConfigNode node)
        {
            RAAntenna.LoadFromConfigNode(node);
            RAAntenna.Name = name;
            base.OnLoad(node);
        }

        public override string GetInfo()
        {
            return string.Format(ModTag + "\n" +
                                "<b>Gain</b>: {0}\n" +
                                "<b>Transmit Power</b>: {1}\n" +
                                "<b>Data Rate</b>: {2}\n" +
                                "<b>Bandwidth</b>: {3}\n", Gain, TxPower, DataRate, Bandwidth);
        }

        public override string ToString()
        {
            return string.Format("[+RealAntennas] {0} [{1}dB]", name, Gain);
        }

        public override float GetVesselSignalStrength()
        {
            float x = base.GetVesselSignalStrength();
            Debug.LogFormat(ModTag + "Part {0} GetVesselSignalStrength was {1}", part, x);
            return x;
        }

        public override void StartTransmission()
        {
            Debug.LogFormat(ModTag + "StartTransmission() for {0}", this);
            if (this?.vessel?.Connection?.Comm is RACommNode node)
            {
                Debug.LogFormat("Found CommNode {0}, link {1} linkNode {2}", node, node.bestLink, node.bestLinkNode);
            }
            base.StartTransmission();
            Debug.LogFormat(ModTag + "StartTransmission() end");
        }

        public override void StopTransmission()
        {
            base.StopTransmission();
        }

        public override bool CanTransmit()
        {
            return base.CanTransmit();
        }

        public override bool IsBusy()
        {
            return base.IsBusy();
        }
    }
}
