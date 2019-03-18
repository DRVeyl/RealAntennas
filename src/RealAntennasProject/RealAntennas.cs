using System;

namespace RealAntennas
{
    public class RealAntenna
    {
        public double Gain { get; set; }         // Physical directionality, measured in dBi
        public double TxPower { get; set; }       // Transmit Power in dBm (milliwatts)
        public double DataRate { get; set; }        // Data Rate in bits/sec.
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
            DataRate = double.Parse(config.GetValue("DataRate"));
            Frequency = double.Parse(config.GetValue("Frequency"));
            Bandwidth = double.Parse(config.GetValue("Bandwidth"));
            PowerEfficiency = double.Parse(config.GetValue("PowerEfficiency"));
            SpectralEfficiency = double.Parse(config.GetValue("SpectralEfficiency"));
            AntennaEfficiency = double.Parse(config.GetValue("AntennaEfficiency"));
            NoiseFigure = double.Parse(config.GetValue("NoiseFigure"));
            CodingGain = double.Parse(config.GetValue("CodingGain"));
        }
        // Given Bandwidth, DataRate and SpectralEfficiency, compute minimum C/I from Shannon-Hartley.
        // C = B log_2 (1 + SNR), where C=Channel Capacity.
        // We will substitute C = (DateRate / SpectralEfficiency) to account for non-ideal performance
        public double MinimumCI { get => Math.Pow(2, (DataRate / SpectralEfficiency) / Bandwidth) - 1; }

        // beamwidth = sqrt(52525*efficiency / g)   G = 10*log(g) => g = 10^(G/10)
        public double Beamwidth { get => Math.Sqrt(52525 * AntennaEfficiency / Math.Pow(10, Gain / 10)); }

        public string DebugMinCI()
        {
            return String.Format("GetNormalizedRange() MinimumCI for {0}, DataRate: {1}, BW: {2}, Eff: {3} is {4}",
                this, DataRate, Bandwidth, SpectralEfficiency, MinimumCI);
        }
    }
}