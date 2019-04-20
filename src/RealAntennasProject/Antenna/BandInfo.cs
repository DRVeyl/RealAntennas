using System.Collections.Generic;

namespace RealAntennas.Antenna
{
    public class BandInfo : Enumeration
    {
        public readonly double Frequency;
        public readonly string DisplayName;
        public readonly float ChannelWidth;
        public static BandInfo VHF = new BandInfo(1, "VHF", "VHF-Band", 150e6, 50e3f);
        public static BandInfo UHF = new BandInfo(2, "UHF", "UHF-Band", 430e6, 50e3f);
        public static BandInfo S = new BandInfo(3, "S", "S-Band", 2.25e9, 0.330e6f);
        public static BandInfo X = new BandInfo(4, "X", "X-Band", 8.45e9, 1.36e6f);
        public static BandInfo K = new BandInfo(5, "K", "K-Band", 26.250e9, 20e6f);
        public static BandInfo Ka = new BandInfo(6, "Ka", "Ka-Band", 32.0e9, 20e6f);
        public static Dictionary<string, BandInfo> All = new Dictionary<string, BandInfo>() {
            { VHF.Name, VHF },
            { UHF.Name, UHF },
            { S.Name, S },
            { X.Name, X },
            { K.Name, K },
            { Ka.Name, Ka } };

        public BandInfo(int id, string name, string dispName, double freq, float chanWidth)
            : base(id, name)
        {
            DisplayName = dispName;
            Frequency = freq;
            ChannelWidth = chanWidth;
        }

        public float MaxSymbolRate(int techLevel) => ChannelWidth * techLevel;

        public override string ToString() => $"[{DisplayName} {Frequency/1e6} MHz]";

        public static List<BandInfo> GetFromTechLevel(int level)
        {
            List<BandInfo> bi = new List<BandInfo>() { BandInfo.VHF, BandInfo.UHF, BandInfo.S };
            if (level > 3) bi.Add(BandInfo.X);
            if (level > 5) bi.Add(BandInfo.K);
            if (level > 7) bi.Add(BandInfo.Ka);
            return bi;
        }
        public static List<BandInfo> ListAll() => new List<BandInfo>() { UHF, VHF, S, X, K, Ka };
    }
}
