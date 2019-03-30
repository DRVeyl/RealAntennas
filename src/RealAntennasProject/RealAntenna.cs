using System;

namespace RealAntennas
{
    public class RealAntenna : IComparable
    {
        public virtual double Gain { get; set; }         // Physical directionality, measured in dBi
        public virtual double TxPower { get; set; }       // Transmit Power in dBm (milliwatts)
        public virtual int TechLevel { get; set; }
        public virtual double Frequency { get; set; }
        public virtual double PowerEfficiency => Math.Min(1, 0.5 + (TechLevel * 0.05));
        public virtual double AntennaEfficiency => Math.Min(0.7, 0.5 + (TechLevel * 0.025));
        public virtual double SpectralEfficiency => 1.01 - (1 / Math.Pow(2, TechLevel));
        public virtual double DataRate { get; }
        public virtual double NoiseFigure => 2 + ((10 - TechLevel) * 0.8);
        public virtual double Bandwidth => DataRate / SpectralEfficiency;          // RF bandwidth required.
        public virtual double RequiredCI() => 1;

        public double PowerDraw => RATools.LogScale(PowerDrawLinear);
        public virtual double PowerDrawLinear => RATools.LinearScale(TxPower) / PowerEfficiency;
        public double Beamwidth => Math.Sqrt(52525 * AntennaEfficiency / RATools.LinearScale(Gain));

        public string Name { get; set; }
        public ModuleRealAntenna Parent { get; internal set; }
        public override string ToString() => $"[+RA] {Name} [{Gain}dB]";

        public int CompareTo(object obj)
        {
            if (obj is RealAntenna ra) return DataRate.CompareTo(ra.DataRate);
            else throw new System.ArgumentException();
        }
        public virtual double BestDataRateToPeer(RealAntenna rx, double distance, double noiseTemp)
        {
            RealAntenna tx = this;
            if ((tx.Parent is ModuleRealAntenna) && !tx.Parent.CanComm()) return 0;
            if ((rx.Parent is ModuleRealAntenna) && !rx.Parent.CanComm()) return 0;

            double RSSI = RACommNetScenario.RangeModel.RSSI(tx, rx, distance, Frequency);
            double Noise = RACommNetScenario.RangeModel.NoiseFloor(rx, noiseTemp);
            double CI = RSSI - Noise;

            return (CI > RequiredCI()) ? DataRate : 0;
        }
        public RealAntenna() : this("New RealAntennaDigital") { }
        public RealAntenna(string name, double dataRate = 1000)
        {
            Name = name;
            DataRate = dataRate;
        }
        public virtual void LoadFromConfigNode(ConfigNode config)
        {
            Gain = double.Parse(config.GetValue("Gain"));
            TxPower = double.Parse(config.GetValue("TxPower"));
            TechLevel = int.Parse(config.GetValue("TechLevel"));
            Frequency = double.Parse(config.GetValue("Frequency"));
        }
    }

}