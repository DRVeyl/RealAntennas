using System;
using UnityEngine;

namespace RealAntennas
{
    public class RealAntennaDigital : RealAntenna
    {
        public override double Frequency => modulator.Frequency;
        public override double SpectralEfficiency => modulator.SpectralEfficiency;
        public override double DataRate => modulator.DataRate * Encoder.CodingRate;
        public override double NoiseFigure => modulator.NoiseFigure;
        public override double Bandwidth => modulator.Bandwidth;          // RF bandwidth required.
        public RAModulator modulator;

        protected static new readonly string ModTag = "[RealAntennaDigital] ";

        public RealAntennaDigital() : this("New RealAntennaDigital") { }
        public RealAntennaDigital(string name)
        {
            Name = name;
            modulator = new RAModulator();
        }

        public override string ToString() => $"[+RA] {Name} [{Gain}dB {modulator}]{(CanTarget ? $" ->{Target}" : null)}";

        public override double BestDataRateToPeer(RealAntenna rx)
        {
            double dataRate = 0;
            if (BestPeerModulator(rx, out RAModulator mod))
            {
                dataRate = mod.DataRate * Encoder.CodingRate;
            }
            return dataRate;
        }

        private bool BestPeerModulator(RealAntenna rx, out RAModulator mod)
        {
            mod = null;
            RealAntennaDigital tx = this;
            Vector3 toSource = rx.Position - tx.Position;
            double distance = toSource.magnitude;
            if (!(rx is RealAntennaDigital)) return false;

            RAModulator txMod = tx.modulator, rxMod = (rx as RealAntennaDigital).modulator;
            if ((tx.Parent is ModuleRealAntenna) && !tx.Parent.CanComm()) return false;
            if ((rx.Parent is ModuleRealAntenna) && !rx.Parent.CanComm()) return false;
            if ((distance < tx.MinimumDistance) || (distance < rx.MinimumDistance)) return false;
            if (!txMod.Compatible(rxMod)) return false;
            int maxBits = Math.Min(txMod.ModulationBits, rxMod.ModulationBits);
            int minBits = Math.Max(txMod.MinModulationBits, rxMod.MinModulationBits);

            double RSSI = RACommNetScenario.RangeModel.RSSI(tx, rx, distance, tx.Frequency);
            double Noise = NoiseFloor(tx.Position);
            double CI = RSSI - Noise;
            double margin = CI - RequiredCI;

            int negotiatedBits = Math.Min(maxBits, Convert.ToInt32(Math.Floor(margin / 3)));
            if (negotiatedBits < minBits) return false;

            // Link can close.  Load & config modulator with agreed SymbolRate and ModulationBits range.
            mod = new RAModulator(txMod)
            {
                SymbolRate = Math.Min(txMod.SymbolRate, rxMod.SymbolRate),
                ModulationBits = negotiatedBits
            };
            return true;
        }

        public override void LoadFromConfigNode(ConfigNode config)
        {
            modulator.LoadFromConfigNode(config);
            base.LoadFromConfigNode(config);
        }
    }
}
