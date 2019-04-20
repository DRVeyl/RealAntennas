using System;
using UnityEngine;

namespace RealAntennas
{
    public class RealAntennaDigital : RealAntenna
    {
        public override double DataRate => modulator.DataRate * Encoder.CodingRate;
        public override double NoiseFigure => modulator.NoiseFigure;
        public override double SymbolRate => modulator.SymbolRate;
        public override double MinSymbolRate => modulator.MinSymbolRate;
        public RAModulator modulator;

        protected static new readonly string ModTag = "[RealAntennaDigital] ";

        public RealAntennaDigital() : this("New RealAntennaDigital") { }
        public RealAntennaDigital(string name)
        {
            Name = name;
            modulator = new RAModulator();
        }

        public override string ToString() => $"[+RA] {Name} [{Gain}dB {RFBand} {modulator}]{(CanTarget ? $" ->{Target}" : null)}";

        public override double BestDataRateToPeer(RealAntenna rx)
        {
            double dataRate = 0;
            if (BestPeerModulator(rx, out RAModulator mod, out Antenna.Encoder encoder))
            {
                dataRate = mod.DataRate * encoder.CodingRate;
                Debug.LogFormat(ModTag + "Proposed [{0}] w/Encoder {1} gives bitrate {2:F1}", mod, encoder, dataRate);
            }
            return dataRate;
        }

        private bool BestPeerModulator(RealAntenna rx, out RAModulator mod, out Antenna.Encoder encoder)
        {
            mod = null;
            RealAntennaDigital tx = this;
            encoder = Antenna.Encoder.BestMatching(tx.Encoder, rx.Encoder);
            if (!(rx is RealAntennaDigital)) return false;
            if (!Compatible(rx)) return false;
            if ((tx.Parent is ModuleRealAntenna) && !tx.Parent.CanComm()) return false;
            if ((rx.Parent is ModuleRealAntenna) && !rx.Parent.CanComm()) return false;

            Vector3 toSource = rx.Position - tx.Position;
            double distance = toSource.magnitude;
            RAModulator txMod = tx.modulator, rxMod = (rx as RealAntennaDigital).modulator;
            if ((distance < tx.MinimumDistance) || (distance < rx.MinimumDistance)) return false;
            if (!txMod.Compatible(rxMod)) return false;
            int maxBits = Math.Min(txMod.ModulationBits, rxMod.ModulationBits);
            int minBits = Math.Max(txMod.MinModulationBits, rxMod.MinModulationBits);
            double maxSymbolRate = Math.Min(txMod.SymbolRate, rxMod.SymbolRate);
            double minSymbolRate = Math.Max(txMod.MinSymbolRate, rxMod.MinSymbolRate);

            double RxPower = Physics.ReceivedPower(tx, rx, distance, tx.Frequency);
            double temp = Physics.NoiseTemperature(rx, tx.Position);
            double N0 = Physics.NoiseSpectralDensity(temp);     // In dBm
            double minEb = encoder.RequiredEbN0 + N0;           // in dBm
            double maxBitRateLog = RxPower - minEb;                // in dB*Hz
            double maxBitRate = RATools.LinearScale(maxBitRateLog);
            Debug.LogFormat(ModTag + "{0} to {1} RxP {2:F2} temp {6:F2} learned maxRate {3:F2} vs symbol rates {4:F4}-{5:F2}",
                tx, rx, RxPower, maxBitRate, minSymbolRate, maxSymbolRate, temp);
            // We cannot slow our modulation enough to achieve the required Eb/N0, so fail.
            if (maxBitRate < minSymbolRate) return false;
            double targetRate = 0;
            int negotiatedBits = 0;
            if (maxBitRate <= maxSymbolRate)
            {
                // The required Eb/N0 occurs at a lower symbol rate than we are capable of at 1 bit/sec/Hz.
                // Step down the symbol rate and modulate at 1 bit/sec/Hz (BPSK).
                // (What if the modulator only supports schemes with >1 bits/symbol?)
                // (Then our minimum EbN0 is an underestimate.)
                float ratio = Convert.ToSingle(maxBitRate / maxSymbolRate);
                double log2 = Math.Floor(Mathf.Log(ratio, 2));
                targetRate = maxSymbolRate * Math.Pow(2, log2);
                negotiatedBits = 1;
                Debug.LogFormat(ModTag + "Selected rate {0:F4} for max bitrate {1:F4} (MaxSymbolRate {2:F1} * log2 {3})",
                    targetRate, maxBitRate, maxSymbolRate, log2);
            } else
            {
                // We need to go to SNR here and rely a bit more on Shannon-Hartley
                double Noise = N0 + RATools.LogScale(maxSymbolRate);
                double CI = RxPower - Noise;
                double margin = CI - encoder.RequiredEbN0;
                targetRate = maxSymbolRate;
                negotiatedBits = Math.Min(maxBits, Convert.ToInt32(1 + Math.Floor(margin / 3)));
                Debug.LogFormat(ModTag + "Noise {0:F2} CI {1:F2} margin {2:F1}", Noise, CI, margin);
            }
            // Link can close.  Load & config modulator with agreed SymbolRate and ModulationBits range.
            mod = new RAModulator(txMod)
            {
                SymbolRate = targetRate,
                ModulationBits = negotiatedBits,
                MinModulationBits = negotiatedBits
            };
            return true;

            // Energy/bit (Eb) = Received Power / datarate
            // N0 = Noise Spectral Density = K*T
            // Noise = N0 * BW
            // SNR = RxPower / Noise = RxPower / (N0 * BW) = Eb*datarate / N0*BW  = (Eb/N0) * (datarate/BW)
            // I < B * log(1 + S/N)   where I = information rate, B=Bandwidth, S=Total Power, N=Total Noise Power = N0*B
            // 
            // Es/N0 = (Total Power / Symbol Rate) / N0
            // = Eb/N0 * log(modulation order)
        }

        public override void LoadFromConfigNode(ConfigNode config)
        {
            modulator.LoadFromConfigNode(config);
            base.LoadFromConfigNode(config);
        }
    }
}
