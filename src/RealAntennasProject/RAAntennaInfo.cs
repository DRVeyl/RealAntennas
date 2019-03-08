using UnityEngine;

namespace RealAntennas
{
    public class RAAntennaInfo: CommNet.CommNode.AntennaInfo
    {
        // Efficiency not yet used.  Dishes are typ 65% efficient.
        // Start implementing a calculation for sensitivity/noise floor based on thermal noise
        // Typ link budget is TxPower + AntennaGain + CodingGain - Path Loss - OtherLosses + Receiver Antenna Gain > Receiver Noise Floor

        public double Gain { get; set; }            // Physical directionality, measured in dBi
        public double CodingGain { get; set; }      // Coding/spreading gain, for transmitters only
        public double PowerDraw { get; set; }       // Measured in dBm (milliwatts)
        public double TxPower { get; set; }         // PowerConsumption * Efficiency convenience.  dBm.
        public double Efficiency { get; set; }      // 0.0-1.0, how efficient is the materiel, electronics, power amp, etc.
        public double Bandwidth { get; set; }       // BW in Hz of signal.
        public double Frequency { get; set; }       // Frequency in Hz of signal.
        public double NoiseFigure { get; set; }     // Noise figure of receiver electronics in dB
        protected static readonly string ModTag = "[RealAntennasAntennaInfo] ";

        //        public Part partReference;
        //        public ProtoPartSnapshot partSnapshotReference = null;


        public RAAntennaInfo() : this(0.0, 0.0, 0.0, 0.0, -1.0, 1e5, 1e9, 3)
        {
        }

        public RAAntennaInfo(ConfigNode node) : this(
                double.Parse(node.GetValue("gain")),
                double.Parse(node.GetValue("codingGain")),
                double.Parse(node.GetValue("powerDraw")),
                double.Parse(node.GetValue("txPower")),
                double.Parse(node.GetValue("efficiency")),
                double.Parse(node.GetValue("bandwidth")),
                double.Parse(node.GetValue("frequency")),
                double.Parse(node.GetValue("noiseFigure")))
        {
        }

        public RAAntennaInfo(double pow, DoubleCurve range, bool combine) : base(pow, range, combine)
        {
            Debug.LogWarning(ModTag + "Parent parameterized constructor called!");
        }

        public RAAntennaInfo(double gain, double codingGain, double powerDraw, double txPower, double efficiency, double bandwidth, double frequency, double noiseFigure)
        {
            Gain = gain;
            CodingGain = codingGain;
            PowerDraw = powerDraw;
            TxPower = txPower;
            Efficiency = efficiency;
            Bandwidth = bandwidth;
            Frequency = frequency;
            NoiseFigure = noiseFigure;
        }

        public void SetFromConfigNode(ConfigNode node) =>
            SetDoublesFromStrings(node.GetValue("gain"), node.GetValue("codingGain"), node.GetValue("powerDraw"),
                                  node.GetValue("txPower"), node.GetValue("efficiency"), node.GetValue("bandwidth"),
                                  node.GetValue("frequency"), node.GetValue("noiseFigure"));
        

        public void SetDoublesFromStrings(string gain, string codingGain, string powerDraw, string txPower, string efficiency, string bandwidth, string frequency, string noiseFigure)
        {
            if (gain != null) Gain = double.Parse(gain);
            if (codingGain != null) CodingGain = double.Parse(codingGain);
            if (powerDraw != null) PowerDraw = double.Parse(powerDraw);
            if (txPower != null) TxPower = double.Parse(txPower);
            if (efficiency != null) Efficiency = double.Parse(efficiency);
            if (bandwidth != null) Bandwidth = double.Parse(bandwidth);
            if (frequency != null) Frequency= double.Parse(frequency);
            if (noiseFigure != null) NoiseFigure = double.Parse(noiseFigure);
        }


        public override string ToString()
        {
            return string.Format("RealAntennas Gain:{0}dBi TxP:{1}dBm BW:{2}KHz Draw:{3}dBm Coding:{4}dB",
                                Gain, TxPower, Bandwidth/1000, PowerDraw, CodingGain);
        }
    }
}
