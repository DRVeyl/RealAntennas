using System;

namespace RealAntennas
{
    public class RealAntenna : IComparable
    {
        public virtual double Gain { get; set; }         // Physical directionality, measured in dBi
        public virtual double TxPower { get; set; }       // Transmit Power in dBm (milliwatts)
        public virtual int TechLevel { get; set; }
        public virtual double Frequency { get; set; }
        public virtual double PowerEfficiency { get => Math.Min(1, 0.5 + (TechLevel * 0.05)); }
        public virtual double AntennaEfficiency { get => Math.Min(0.7, 0.5 + (TechLevel * 0.025)); }
        public virtual double SpectralEfficiency { get; }
        public virtual double DataRate { get; }
        public virtual double NoiseFigure { get; }
        public virtual double Bandwidth { get => DataRate / SpectralEfficiency; }         // RF bandwidth required.
        public virtual double RequiredCI() => 1;

        public double PowerDraw { get => RATools.LogScale(PowerDrawLinear); }
        public double PowerDrawLinear { get => RATools.LinearScale(TxPower) / PowerEfficiency; }
        public double Beamwidth { get => Math.Sqrt(52525 * AntennaEfficiency / RATools.LinearScale(Gain)); }

        public string Name { get; set; }
        public ModuleRealAntenna Parent { get; internal set; }
        public override string ToString() => string.Format("[+RA] {0} [{1}dB]", Name, Gain);

        public int CompareTo(object obj)
        {
            if (obj is RealAntenna ra) return DataRate.CompareTo(ra.DataRate);
            else throw new System.ArgumentException();
        }
        public virtual double BestDataRateToPeer(RealAntenna rx, double distance, double noiseTemp) => DataRate;
        public RealAntenna() : this("New RealAntennaDigital") { }
        public RealAntenna(string name)
        {
            Name = name;
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