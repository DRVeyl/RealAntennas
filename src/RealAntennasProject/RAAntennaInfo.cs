using UnityEngine;

namespace RealAntennas
{
    public class RAAntennaInfo: CommNet.CommNode.AntennaInfo
    {
        // Efficiency not yet used.  Dishes are typ 65% efficient.
        // Sensitivity: thermal noise floor is -174dBm/Hz.  1MHz (10^6) BW is 60 dB => NF=-114dBm.
        // Early tech might not have been as sensitive, but since you can always slap an amplifier on the front [and you aren't worried
        // about being saturated from nearby noise] carrying around a sensitivity value might not matter.
        // Maybe better to just track bandwidth and calculate minimum received signal strength as above.
        // Typ link budget is TxPower + AntennaGain + CodingGain - Path Loss - OtherLosses + Receiver Antenna Gain > Receiver Sensitivity

        public double Gain { get; set; }             // Physical directionality, measured in dBi
        public double CodingGain { get; set; }       // Coding/spreading gain, for transmitters only
        public double PowerDraw { get; set; }        // Measured in dBm (milliwatts)
        public double TxPower { get; set; }          // PowerConsumption * Efficiency convenience.  dBm.
        public double Efficiency { get; set; }       // 0.0-1.0, how efficient is the materiel, electronics, power amp, etc.
        public double Sensitivity { get; set; }      // Min received signal level in dBm.
        public double Bandwidth { get; set; }        // BW of signal.
        protected static readonly string ModTag = "[RealAntennasAntennaInfo] ";

        //        public Part partReference;
        //        public ProtoPartSnapshot partSnapshotReference = null;


        public RAAntennaInfo() : this(0.0, 0.0, 0.0, 0.0, -1.0, -100.0, 1000.0)
        {
        }

        public RAAntennaInfo(ConfigNode node) : this(
                double.Parse(node.GetValue("gain")),
                double.Parse(node.GetValue("codingGain")),
                double.Parse(node.GetValue("powerDraw")),
                double.Parse(node.GetValue("txPower")),
                double.Parse(node.GetValue("efficiency")),
                double.Parse(node.GetValue("sensitivity")),
                double.Parse(node.GetValue("bandwidth")))
        {
        }

        public RAAntennaInfo(double pow, DoubleCurve range, bool combine) : base(pow, range, combine)
        {
            Debug.LogWarning(ModTag + "Parent parameterized constructor called!");
        }

        public RAAntennaInfo(double gain, double codingGain, double powerDraw, double txPower, double efficiency, double sensitivity, double bandwidth)
        {
            Gain = gain;
            CodingGain = codingGain;
            PowerDraw = powerDraw;
            TxPower = txPower;
            Efficiency = efficiency;
            Sensitivity = sensitivity;
            Bandwidth = bandwidth;
        }

        public void SetFromConfigNode(ConfigNode node) =>
            SetDoublesFromStrings(node.GetValue("gain"), node.GetValue("codingGain"), node.GetValue("powerDraw"),
                                  node.GetValue("txPower"), node.GetValue("efficiency"), node.GetValue("sensitivity"),
                                  node.GetValue("bandwidth"));

        public void SetDoublesFromStrings(string gain, string codingGain, string powerDraw, string txPower, string efficiency, string sensitivity, string bandwidth)
        {
            Gain = double.Parse(gain);
            CodingGain = double.Parse(codingGain);
            PowerDraw = double.Parse(powerDraw);
            TxPower = double.Parse(txPower);
            Efficiency = double.Parse(efficiency);
            Sensitivity = double.Parse(sensitivity);
            Bandwidth = double.Parse(bandwidth);
        }


        public override string ToString()
        {
            return string.Format("RealAntennas Gain:{0}dBi TxP:{1}dBm BW:{2}KHz Draw:{3}dBm Coding:{4}dB",
                                Gain, TxPower, Bandwidth/1000, PowerDraw, CodingGain);
        }
    }
}
