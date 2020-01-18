using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace RealAntennas
{
    public class RealAntennaDigital : RealAntenna
    {
        public override double DataRate => modulator.DataRate * Encoder.CodingRate;
        public override double Bandwidth => SymbolRate * Encoder.CodingRate;
        public override double MinSymbolRate => modulator.MinSymbolRate;
        public RAModulator modulator;

        protected static new readonly string ModTag = "[RealAntennaDigital] ";

        public RealAntennaDigital() : this("New RealAntennaDigital") { }
        public RealAntennaDigital(string name) : base(name) 
        {
            modulator = new RAModulator(this);
        }
        public RealAntennaDigital(RealAntenna orig) : base(orig)
        {
            if (orig is RealAntennaDigital o) modulator = new RAModulator(o.modulator);
        }
        public override string ToString() => $"[+RA] {Name} [{Gain:F1} dBi {RFBand.name} {TxPower} dBm [TL:{TechLevelInfo.Level:N0}] {modulator}] {(CanTarget ? $" ->{Target}" : null)}";

        public override double BestDataRateToPeer(RealAntenna rx)
        {
            double dataRate = (BestPeerModulator(rx, out double modRate, out double codeRate)) ? modRate * codeRate : 0;
            return dataRate;
        }

        private bool BestPeerModulator(RealAntenna rx, out double modRate, out double codeRate)
        {
            RealAntennaDigital tx = this;
            modRate = 0;
            codeRate = 0;
            if (!(rx is RealAntennaDigital)) return false;
            if (!Compatible(rx)) return false;
            if ((tx.Parent is ModuleRealAntenna) && !tx.Parent.CanComm()) return false;
            if ((rx.Parent is ModuleRealAntenna) && !rx.Parent.CanComm()) return false;
            if (!(tx.DirectionCheck(rx) && rx.DirectionCheck(tx))) return false;

            Antenna.Encoder encoder = Antenna.Encoder.BestMatching(tx.Encoder, rx.Encoder);
            codeRate = encoder.CodingRate;
            Vector3d toSource = rx.Position - tx.Position;
            double distance = toSource.magnitude;
            RAModulator txMod = tx.modulator, rxMod = (rx as RealAntennaDigital).modulator;
            if ((distance < tx.MinimumDistance) || (distance < rx.MinimumDistance)) return false;
            if (distance < 0.1f)
            {
                Debug.LogWarning($"{ModTag} Aborting calculation for {tx} and {rx}: Distance < 0.1");
                return false;
            }
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
            /*           
            Vessel tv = (tx.ParentNode as RACommNode).ParentVessel;
            Vessel rv = (rx.ParentNode as RACommNode).ParentVessel;
            if (tv != null && rv != null)
            {
                string debugStr = $"{ModTag} {tx} to {rx} RxP {RxPower:F2} vs temp {temp:F2}. NSD {N0:F1}, ReqEb/N0 {encoder.RequiredEbN0:F1} -> minEb {minEb:F1} gives maxRate {RATools.PrettyPrint(maxBitRate)}bps vs symbol rates {RATools.PrettyPrint(minSymbolRate)}Sps-{RATools.PrettyPrint(maxSymbolRate)}Sps";
                Debug.Log(debugStr);
            }
            */
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
                //debugStr += $" Selected rate {RATools.PrettyPrint(targetRate)}bps (MaxSymbolRate * log2 {log2})";
            }
            else
            {
                // We need to go to SNR here and rely a bit more on Shannon-Hartley
                double Noise = N0 + RATools.LogScale(maxSymbolRate);
                double CI = RxPower - Noise;
                double margin = CI - encoder.RequiredEbN0;
                targetRate = maxSymbolRate;
                // Someone got this Convert.ToInt32 to overflow?
                double d = 1 + Math.Floor(margin / 3);
                if (d < Int32.MinValue || d > Int32.MaxValue)
                {
                    Debug.LogError($"{ModTag} Max bits {d} OUT OF RANGE of Int32 for Tx: {tx} Rx: {rx} N0: {N0} MaxSymbolRate: {maxSymbolRate} Noise: {Noise} RxP: {RxPower} CI: {CI} Encoder: {encoder} margin: {margin} distance: {distance} freq: {tx.Frequency} txNode: {tx.ParentNode} rxNode: {rx.ParentNode}");
                    negotiatedBits = 1;
                } else
                {
                    negotiatedBits = Math.Min(maxBits, Convert.ToInt32(1 + Math.Floor(margin / 3)));
                }
                //debugStr += $" Noise {Noise:F2} CI {CI:F2} margin {margin:F1}";
            }
            modRate = targetRate * negotiatedBits;
            //Debug.LogFormat(debugStr);
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
            modulator.LoadFromConfigNode(config);
        }
        public override void UpgradeFromConfigNode(ConfigNode config)
        {
            base.UpgradeFromConfigNode(config);
            modulator.UpgradeFromConfigNode(config);
        }
    }
}
