using UnityEngine;

namespace RealAntennas
{
    public class RealAntenna
    {
        public double Gain { get; set; }         // Physical directionality, measured in dBi
        public double TxPower { get; set; }       // Transmit Power in dBm (milliwatts)
        public double Frequency { get; set; }    // Frequency in Hz
        public double Bandwidth { get; set; }    // Bandwidth in Hz
        public double PowerEfficiency { get; set; }
        public double SpectralEfficiency { get; set; }
        public double AntennaEfficiency { get; set; }
        public double NoiseFigure { get; set; }     // Noise figure of receiver electronics in dB
        public double PowerDraw { get => TxPower / PowerEfficiency; }
        public double CodingGain { get; set; }      // Coding/spreading gain, for transmitters only
        public string Name { get; set; }

        protected static readonly string ModTag = "[RealAntenna] ";
        public static readonly string ModuleName = "RealAntenna";

        public RealAntenna() : this("New RealAntenna") { }
        public RealAntenna(string name) => Name = name;

        public override string ToString()
        {
            return string.Format("[+RealAntennas] {0} [{1}dB]", Name, Gain);
        }

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
    }
}