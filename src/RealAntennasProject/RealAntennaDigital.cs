namespace RealAntennas
{
    public class RealAntennaDigital : RealAntenna
    {
        public override double DataRate => modulator.DataRate * Encoder.CodingRate;
        public override double Bandwidth => SymbolRate * Encoder.CodingRate;
        public override double MinSymbolRate => modulator.MinSymbolRate;
        public RAModulator modulator;

        public RealAntennaDigital() : this("New RealAntennaDigital") { }
        public RealAntennaDigital(string name) : base(name) 
        {
            modulator = new RAModulator(this);
        }
        public RealAntennaDigital(RealAntenna orig) : base(orig)
        {
            if (orig is RealAntennaDigital o) modulator = new RAModulator(o.modulator);
        }
        public override string ToString() => $"{Name} [{RFBand.name} {Gain:F1} dBi {TxPower} dBm [TL:{TechLevelInfo.Level:N0}] {modulator}] {(CanTarget ? $" ->{Target}" : null)}";


        // Energy/bit (Eb) = Received Power / datarate
        // N0 = Noise Spectral Density = K*T
        // Noise = N0 * BW
        // SNR = RxPower / Noise = RxPower / (N0 * BW) = Eb*datarate / N0*BW  = (Eb/N0) * (datarate/BW)
        // I < B * log(1 + S/N)   where I = information rate, B=Bandwidth, S=Total Power, N=Total Noise Power = N0*B
        // 
        // Es/N0 = (Total Power / Symbol Rate) / N0
        // = Eb/N0 * log(modulation order)

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
