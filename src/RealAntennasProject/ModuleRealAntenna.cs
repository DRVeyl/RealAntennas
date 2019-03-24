using System;
using UnityEngine;

namespace RealAntennas
{
    public class ModuleRealAntenna : ModuleDataTransmitter
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "dBi", guiFormat = "F1")]
        public double Gain;          // Physical directionality, measured in dBi

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "dBm", guiFormat = "F1")]
        public double TxPower;       // Transmit Power in dBm (milliwatts)

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiFormat = "P2")]
        public double PowerEfficiency;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "Hz", guiFormat = "N0")]
        public double Frequency;     // Frequency in Hz

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "S/s", guiFormat = "N0")]
        public double SymbolRate;    // Symbol Rate in Samples/second

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "bits", guiFormat = "N0")]
        public int ModulationBits;    // Constellation size (bits, 0=OOK, 1=BPSK, 2=QPSK, 3=8-PSK, 4++ = 16-QAM)

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "bits", guiFormat = "N0")]
        public int MinModulationBits;    // Minimum constellation size (bits)

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "dB", guiFormat = "F1")]
        public double NoiseFigure;     // Noise figure of receiver electronics in dB

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiFormat = "P2")]
        public double SpectralEfficiency;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiFormat = "P2")]
        public double AntennaEfficiency;

        public double PowerDraw { get => LogScale(LinearScale(TxPower) / PowerEfficiency); }

        protected static readonly string ModTag = "[ModuleRealAntenna] ";
        public static readonly string ModuleName = "ModuleRealAntenna";
        public static double LinearScale(double x) => Math.Pow(10, x / 10);
        public static double LogScale(double x) => 10 * Math.Log10(x);
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
                                "<b>Data Rate</b>: {2}\n", Gain, TxPower, DataRate);
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
                CommNet.CommPath path = new CommNet.CommPath();
                if  (node.Net.FindHome(node, path))
                {
                    Debug.LogFormat("Path {0} of len {1} with strength {2}/{3}", path, path.Count, path.signal, path.signalStrength);
                    foreach (CommNet.CommLink link in path)
                    {
                        Debug.LogFormat("Link {0} from {1} to {2} strength {3}/{4}", link, link.start, link.end, link.signalStrength, link.signal);
                    }
                }
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
            Debug.LogFormat(ModTag + "CanTransmit() for {0}", this);
            return base.CanTransmit();
        }

        public override bool IsBusy()
        {
            return base.IsBusy();
        }
    }
}
