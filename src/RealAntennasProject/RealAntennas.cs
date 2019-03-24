using System;

namespace RealAntennas
{
    public class RealAntenna : IComparable
    {
        public double Gain { get; set; }         // Physical directionality, measured in dBi
        public double TxPower { get; set; }       // Transmit Power in dBm (milliwatts)
        public double PowerEfficiency { get; set; }
        public RAModulator modulator;

        public double AntennaEfficiency { get; set; }
        public double PowerDraw { get => TxPower / PowerEfficiency; }
        public string Name { get; set; }

        protected static readonly string ModTag = "[RealAntenna] ";
        public static readonly string ModuleName = "RealAntenna";

        public RealAntenna() : this("New RealAntenna") { }
        public RealAntenna(string name)
        {
            Name = name;
            modulator = new RAModulator();
        }

        public override string ToString() => string.Format("[+RA] {0} [{1}dB {2}]", Name, Gain, modulator);

        public void LoadFromConfigNode(ConfigNode config)
        {
            Gain = double.Parse(config.GetValue("Gain"));
            TxPower = double.Parse(config.GetValue("TxPower"));
            PowerEfficiency = double.Parse(config.GetValue("PowerEfficiency"));
            AntennaEfficiency = double.Parse(config.GetValue("AntennaEfficiency"));
            modulator.LoadFromConfigNode(config);
        }

        // beamwidth = sqrt(52525*efficiency / g)   G = 10*log(g) => g = 10^(G/10)
        public double Beamwidth { get => Math.Sqrt(52525 * AntennaEfficiency / Math.Pow(10, Gain / 10)); }

        public int CompareTo(object obj) => (obj is RealAntenna ra) ? modulator.DataRate.CompareTo(ra.modulator.DataRate) : -1;
    }
}