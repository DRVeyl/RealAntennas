using System;
using UnityEngine;

namespace RealAntennas
{
    public class RealAntennaDigital : RealAntenna
    {
        public override double Frequency => modulator.Frequency;
        public override double DataRate => modulator.DataRate * Encoder.CodingRate;
        public override double NoiseFigure => modulator.NoiseFigure;
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
            if (BestPeerModulator(rx, out RAModulator mod, out Antenna.Encoder encoder))
            {
                dataRate = mod.DataRate * encoder.CodingRate;
                Debug.LogFormat(ModTag + "BestPeerMod w/Encoder {2} returned {0}, calc'd rate {1:F1}", mod, dataRate, encoder);
            }
            return dataRate;
        }

        private bool BestPeerModulator(RealAntenna rx, out RAModulator mod, out Antenna.Encoder encoder)
        {
            mod = null;
            RealAntennaDigital tx = this;
            encoder = Antenna.Encoder.BestMatching(tx.Encoder, rx.Encoder);
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
            double maxSymbolRate = Math.Min(txMod.SymbolRate, rxMod.SymbolRate);
            double minSymbolRate = Math.Max(txMod.MinSymbolRate, rxMod.MinSymbolRate);

            double RxPower = Physics.ReceivedPower(tx, rx, distance, tx.Frequency);
            double temp = Physics.NoiseTemperature(rx, toSource);
            double N0 = Physics.NoiseSpectralDensity(temp);     // In dBm
            double minEb = encoder.RequiredEbN0 + N0;           // in dBm
            double maxBitRateLog = RxPower - minEb;                // in dB*Hz
            double maxBitRate = RATools.LinearScale(maxBitRateLog);
            Debug.LogFormat(ModTag + "{0} to {1} RxP {2:F2} learned maxRate {3:F2} vs symbol rates {4:F4}-{5:F2}",
                tx, rx, RxPower, maxBitRate, minSymbolRate, maxSymbolRate);
            // We cannot slow our modulation enough to achieve the required Eb/N0, so fail.
            if (maxBitRate < minSymbolRate) return false;
            double targetRate = 0;
            int negotiatedBits = 0;
            if (maxBitRate <= maxSymbolRate)
            {
                // The required Eb/N0 occurs at a lower symbol rate than we are capable of at 1 bit/sec/Hz.
                // Step down the symbol rate and modulate at 1 bit/sec/Hz (BPSK).
                // (What if the modulator only supports schemes with >1 bits/symbol?)
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
                negotiatedBits = Math.Min(maxBits, Convert.ToInt32(Math.Floor(margin / 3)));
                Debug.LogFormat(ModTag + "Noise {0:F2} CI {1:F2} margin {2:F1}", Noise, CI, margin);
            }
            // Link can close.  Load & config modulator with agreed SymbolRate and ModulationBits range.
            mod = new RAModulator(txMod)
            {
                SymbolRate = targetRate,
                ModulationBits = negotiatedBits,
                MinModulationBits = negotiatedBits
            };
            Debug.LogFormat(ModTag + "Created modulator {0} ({1} bits @ {2:F1} sym/sec)", mod, negotiatedBits, targetRate);
            return true;

            // Energy/bit (Eb) = Received Power / datarate
            // N0 = Noise Spectral Density = K*T
            // Noise = N0 * BW
            // SNR = RxPower / Noise = RxPower / (N0 * BW) = Eb*datarate / N0*BW  = (Eb/N0) * (datarate/BW)
            // I < B * log(1 + S/N)   where I = information rate, B=Bandwidth, S=Total Power, N=Total Noise Power = N0*B
            // 
            // Eb/No >= (S/N) / (C/B) = (2^(C/B) - 1) / (C/B) 
            // For a given C/B (Capacity/Bandwidth in bits/Hz), this defines the minimum Eb/N0 by theory.
            // If C/B = 1 (BPSK, 1 bit/Hz), Eb/N0 theory >= (2-1)/1 = 1 = 0dB.  (Shannon Limit)
            //   Then Turbo code is +1 dB above.
            // If C/B = 2 (QPSK, 2 bits/Hz), Eb/N0 theory >= (4-1)/2 = 1.5 = 1.7dB
            // If C/B = 3 (8PSK, 3 bits/Hz), Eb/N0 theory >= (8-1)/3 = 2.3 = 3.7dB
            // If C/B = 4 (16PSK, 4 bits/Hz),Eb/N0 theory >= (16-1)/4 = 3.75 = 5.7dB
            // If C/B = 5 (32QAM, 5 bits/Hz),Eb/N0 theory >= (32-1)/5 = 6.2 = 8dB
            // If C/B = 6 (64QAM, 6 bits/Hz),Eb/N0 theory >= (64-1)/6 = 10.5 = 10.2dB
            // If C/B = 7 (128QAM,7 bits/Hz),Eb/N0 theory >= (128-1)/7 = 18.1, 12.6dB
            // 8:  255/8 = 31.875 = 15dB
            // 9:  511/9 = 56.9 = 17.6dB
            // 10: 1023/10 = 102.3 = 20.1dB
            // 11: 2047/11 = 186.1 = 22.7dB
            // 20: 1M/20 = 52,250 = 47.2dB
            //   Then... Turbo code is +1 dB above those??
            // This appears a LOT more generous than my 3dB/rate doubling rule.

            //  RxPower = Energy/bit * bitrate  ==  Energy/symbol * symbolrate.
            // Similarly    Eb = RxPower / bitrate
            // 
            //
            // RxPower / N0 = Received Power / Noise Spectral Density
            // Carrier / Noise   =    Eb/N0 * bitrate / Bandwidth
            // (Carrier Energy = Energy/Bit * Bits/sec)
            //  Noise = Noise Density * Bandwidth
            //
            // Es/N0 = (Total Power / Symbol Rate) / N0
            // = Eb/N0 * log(modulation order)
            //
            // Eb/N0 = Es/N0 / log(modulation order) =   Es/N0 / (bits/symbol)

        }

        public override void LoadFromConfigNode(ConfigNode config)
        {
            modulator.LoadFromConfigNode(config);
            base.LoadFromConfigNode(config);
        }
    }
}
