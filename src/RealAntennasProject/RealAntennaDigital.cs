using System;

namespace RealAntennas
{
    public class RealAntennaDigital : RealAntenna
    {
        public override double Frequency { get => modulator.Frequency; }
        public override double SpectralEfficiency { get => modulator.SpectralEfficiency; }
        public override double DataRate { get => modulator.DataRate; }
        public override double NoiseFigure { get => modulator.NoiseFigure; }
        public override double Bandwidth { get => modulator.Bandwidth; }         // RF bandwidth required.
        public override double RequiredCI() => modulator.RequiredCI();
        public RAModulator modulator;

        // beamwidth = sqrt(52525*efficiency / g)   G = 10*log(g) => g = 10^(G/10)

        protected static readonly string ModTag = "[RealAntenna] ";
        public static readonly string ModuleName = "RealAntenna";

        public RealAntennaDigital() : this("New RealAntennaDigital") { }
        public RealAntennaDigital(string name)
        {
            Name = name;
            modulator = new RAModulator();
        }

        public override string ToString() => string.Format("[+RA] {0} [{1}dB {2}]", Name, Gain, modulator);
        public override double BestDataRateToPeer(RealAntenna rx, double distance, double noiseTemp)
        {
            double dataRate = 0;
            if (BestPeerModulator(rx, distance, noiseTemp, out RAModulator mod))
            {
                dataRate = mod.DataRate;
            }
            return dataRate;
        }

        private bool BestPeerModulator(RealAntenna rx, double distance, double noiseTemp, out RAModulator mod)
        {
            mod = null;
            RealAntennaDigital tx = this;
            if (!(rx is RealAntennaDigital)) return false;

            RAModulator txMod = tx.modulator, rxMod = (rx as RealAntennaDigital).modulator;
            if ((tx.Parent is ModuleRealAntenna) && !tx.Parent.CanComm()) return false;
            if ((rx.Parent is ModuleRealAntenna) && !rx.Parent.CanComm()) return false;
            if (!txMod.Compatible(rxMod)) return false;
            int maxBits = Math.Min(txMod.ModulationBits, rxMod.ModulationBits);
            int minBits = Math.Max(txMod.MinModulationBits, rxMod.MinModulationBits);

            double RSSI = RACommNetScenario.RangeModel.RSSI(tx, rx, distance, txMod.Frequency);
            double Noise = RACommNetScenario.RangeModel.NoiseFloor(rx, noiseTemp);
            double CI = RSSI - Noise;

            if (CI < txMod.RequiredCI(minBits)) return false;   // Fast-Fail the easiest case.

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

        public override void LoadFromConfigNode(ConfigNode config)
        {
            Gain = double.Parse(config.GetValue("Gain"));
            TxPower = double.Parse(config.GetValue("TxPower"));
            TechLevel = int.Parse(config.GetValue("TechLevel"));
            modulator.LoadFromConfigNode(config);
        }
    }
}
