using System;

namespace RealAntennas
{
    public class RealAntenna : IComparable
    {
        public double Gain { get; set; }         // Physical directionality, measured in dBi
        public double TxPower { get; set; }       // Transmit Power in dBm (milliwatts)
        public int TechLevel { get; set; }
        public double PowerEfficiency { get => Math.Min(1, 0.5 + (TechLevel * 0.05)); }
        public double AntennaEfficiency { get => Math.Min(0.7, 0.5 + (TechLevel * 0.025)); }
        public double SpectralEfficiency { get => modulator.SpectralEfficiency; }
        public double DataRate { get => modulator.DataRate; }
        public RAModulator modulator;

        public double PowerDraw { get => LogScale(PowerDrawLinear); }
        public double PowerDrawLinear { get => LinearScale(TxPower) / PowerEfficiency; }
        public static double LinearScale(double x) => Math.Pow(10, x / 10);
        public static double LogScale(double x) => 10 * Math.Log10(x);
        // beamwidth = sqrt(52525*efficiency / g)   G = 10*log(g) => g = 10^(G/10)
        public double Beamwidth { get => Math.Sqrt(52525 * AntennaEfficiency / Math.Pow(10, Gain / 10)); }

        public string Name { get; set; }
        public ModuleRealAntenna Parent { get; internal set; }

        protected static readonly string ModTag = "[RealAntenna] ";
        public static readonly string ModuleName = "RealAntenna";

        public RealAntenna() : this("New RealAntenna") { }
        public RealAntenna(string name)
        {
            Name = name;
            modulator = new RAModulator();
        }

        public override string ToString() => string.Format("[+RA] {0} [{1}dB {2}]", Name, Gain, modulator);
        public virtual bool BestPeerModulator(RealAntenna rx, double distance, double noiseTemp, out RAModulator mod)
        {
            mod = null;
            RealAntenna tx = this;
            RAModulator txMod = tx.modulator, rxMod = rx.modulator;
            if ((tx.Parent is ModuleRealAntenna) && !tx.Parent.CanComm()) return false;
            if ((rx.Parent is ModuleRealAntenna) && !rx.Parent.CanComm()) return false;
            if (!txMod.Compatible(rxMod)) return false;
            int maxBits = Math.Min(txMod.ModulationBits, rxMod.ModulationBits);
            int minBits = Math.Max(txMod.MinModulationBits, rxMod.MinModulationBits);

            double RSSI = RACommNetScenario.RangeModel.RSSI(tx, rx, distance, txMod.Frequency);
            double Noise = RACommNetScenario.RangeModel.NoiseFloor(rx, noiseTemp);
            double CI = RSSI - Noise;

            if (CI < RAModulator.RequiredCI(minBits)) return false;   // Fast-Fail the easiest case.

            // Link can close.  Load & config modulator with agreed SymbolRate and ModulationBits range.
            mod = new RAModulator(txMod)
            {
                SymbolRate = Math.Min(txMod.SymbolRate, rxMod.SymbolRate),
                ModulationBits = maxBits
            };

            while (CI < mod.RequiredCI() && mod.ModulationBits > minBits)
            {
                mod.ModulationBits--;
            }
            return (CI >= mod.RequiredCI());
        }

        public void LoadFromConfigNode(ConfigNode config)
        {
            Gain = double.Parse(config.GetValue("Gain"));
            TxPower = double.Parse(config.GetValue("TxPower"));
            TechLevel = int.Parse(config.GetValue("TechLevel"));
            modulator.LoadFromConfigNode(config);
        }

        public int CompareTo(object obj) => (obj is RealAntenna ra) ? modulator.DataRate.CompareTo(ra.modulator.DataRate) : -1;
    }
}