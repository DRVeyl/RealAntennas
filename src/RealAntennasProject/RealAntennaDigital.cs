using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace RealAntennas
{
    public class RealAntennaDigital : RealAntenna
    {
        public override double DataRate => modulator.DataRate * Encoder.CodingRate;
        public override int TechLevel { get => modulator.TechLevel; set => modulator.TechLevel = value; }
        public override double Bandwidth => SymbolRate * Encoder.CodingRate;
        public override double SymbolRate { get => modulator.SymbolRate; set => modulator.SymbolRate = value; }
        public override double MinSymbolRate => modulator.MinSymbolRate;
        public RAModulator modulator = new RAModulator();

        protected static new readonly string ModTag = "[RealAntennaDigital] ";

        public RealAntennaDigital() : this("New RealAntennaDigital") { }
        public RealAntennaDigital(string name) : base(name) { }
        public RealAntennaDigital(RealAntenna orig) : base(orig)
        {
            if (orig is RealAntennaDigital o) modulator = new RAModulator(o.modulator);
        }
        public override string ToString() => $"[+RA] {Name} [{Gain:F1} dBi {RFBand} [TL:{TechLevel:N0}] {modulator}] {(CanTarget ? $" ->{Target}" : null)}";

        public override double BestDataRateToPeer(RealAntenna rx)
        {
            double dataRate = (BestPeerModulator(rx, out double modRate, out double codeRate)) ? modRate * codeRate : 0;
            return dataRate;
        }

        //        private bool BestPeerModulator(RealAntenna rx, RAModulator mod, out Antenna.Encoder encoder)
        private bool BestPeerModulator(RealAntenna rx, out double modRate, out double codeRate)
        {
            RealAntennaDigital tx = this;
            Antenna.Encoder encoder = Antenna.Encoder.BestMatching(tx.Encoder, rx.Encoder);
            codeRate = encoder.CodingRate;
            modRate = 0;
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
            double maxSymbolRate = Math.Min(txMod.SymbolRate, rxMod.SymbolRate);
            double minSymbolRate = Math.Max(txMod.MinSymbolRate, rxMod.MinSymbolRate);

            double RxPower = Physics.ReceivedPower(tx, rx, distance, tx.Frequency);
            double temp = Physics.NoiseTemperature(rx, tx.Position);
            double N0 = Physics.NoiseSpectralDensity(temp);     // In dBm
            double minEb = encoder.RequiredEbN0 + N0;           // in dBm
            double maxBitRateLog = RxPower - minEb;                // in dB*Hz
            double maxBitRate = RATools.LinearScale(maxBitRateLog);
            string debugStr = string.Empty;
#if DEBUG
            //            debugStr = string.Format(ModTag + $"{tx} to {rx} RxP {RxPower:F2} temp {temp:F2} learned maxRate {RATools.PrettyPrint(maxBitRate)}bps vs symbol rates {RATools.PrettyPrint(minSymbolRate)}Sps-{RATools.PrettyPrint(maxSymbolRate)}Sps");
#endif
            // We cannot slow our modulation enough to achieve the required Eb/N0, so fail.
            if (maxBitRate < minSymbolRate) return false;
            double targetRate;
            int negotiatedBits;
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
#if DEBUG
                //debugStr += $" Selected rate {RATools.PrettyPrint(targetRate)}bps (MaxSymbolRate * log2 {log2})";
#endif
            }
            else
            {
                // We need to go to SNR here and rely a bit more on Shannon-Hartley
                double Noise = N0 + RATools.LogScale(maxSymbolRate);
                double CI = RxPower - Noise;
                double margin = CI - encoder.RequiredEbN0;
                targetRate = maxSymbolRate;
                negotiatedBits = Math.Min(maxBits, Convert.ToInt32(1 + Math.Floor(margin / 3)));
#if DEBUG
                //debugStr += $" Noise {Noise:F2} CI {CI:F2} margin {margin:F1}";
#endif
            }
            // Link can close.  Load & config modulator with agreed SymbolRate and ModulationBits range.
            //mod.Copy(txMod);
            //mod.SymbolRate = targetRate;
            //mod.ModulationBits = negotiatedBits;
            modRate = targetRate * negotiatedBits;
            //Debug.LogFormat(debugStr);
            //Debug.LogFormat(ModTag + "Proposed [{0}] w/Encoder {1} gives bitrate {2:F1}bps", mod, encoder, RATools.PrettyPrint(mod.DataRate * encoder.CodingRate));
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
            base.LoadFromConfigNode(config);
            modulator.TechLevel = TechLevel;
            modulator.LoadFromConfigNode(config);
        }
        public override void UpgradeFromConfigNode(ConfigNode config)
        {
            base.UpgradeFromConfigNode(config);
            modulator.UpgradeFromConfigNode(config);
        }
    }
}
