using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RealAntennas.Antenna
{
    public class BandInfo : IEquatable<BandInfo>
    {
        [Persistent] public string name;
        [Persistent] public int TechLevel;
        [Persistent] public double Frequency;
        [Persistent] public float ChannelWidth;

        public static bool initialized = false;
        public static Dictionary<string, BandInfo> All = new Dictionary<string, BandInfo>();
        public static string DefaultBand = "L";
        private const string ModTag = "[RealAntennas.BandInfo]";
        public static BandInfo Get(string band)
        {
            if (!initialized && GameDatabase.Instance.GetConfigNode("RealAntennas/RealAntennasCommNetParams/RealAntennasCommNetParams") is ConfigNode node)
                Init(node);
            if (!All.ContainsKey(band))
            {
                Debug.LogError($"{ModTag} Band \"{band}\" not defined, defaulting to {All.Keys.FirstOrDefault()}");
                band = All.Keys.FirstOrDefault();
            }
            return All[band];
        }
        public static void Init(ConfigNode config)
        {
            Debug.Log($"{ModTag} Init()");
            All.Clear();
            foreach (ConfigNode node in config.GetNodes("BandInfo"))
            {
                BandInfo obj = ConfigNode.CreateObjectFromConfig<BandInfo>(node);
                Debug.Log($"{ModTag} Adding BandInfo {obj.ToDetailedString()}");
                All.Add(obj.name, obj);
            }
            initialized = true;
        }
      
        public BandInfo() { }

        public float MaxSymbolRate(int techLevel) => ChannelWidth;
        //        public float MaxSymbolRate(int techLevel) => Math.Max(ChannelWidth * (1 + techLevel - minTechLevel), MaxSymbolRateByTechLevel[techLevel-1]);

        public override string ToString() => $"[{name}-Band {RATools.PrettyPrint(Frequency)}Hz]";
        public virtual string ToDetailedString() => $"[{name}-Band TL:{TechLevel} {RATools.PrettyPrint(Frequency)}Hz]";

        public static List<BandInfo> GetFromTechLevel(int level) => All.Values.Where(x => level >= x.TechLevel).ToList();

        public override bool Equals(object obj) => Equals(obj as BandInfo);
        public bool Equals(BandInfo other) => other is null ? false : (Frequency == other.Frequency) && (ChannelWidth == other.ChannelWidth);
        public static bool operator ==(BandInfo lhs, BandInfo rhs) => lhs is null ? rhs is null : lhs.Equals(rhs);
        public static bool operator !=(BandInfo lhs, BandInfo rhs) => !(lhs == rhs);

        public override int GetHashCode()
        {
            var hashCode = -1244118993;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + Frequency.GetHashCode();
            hashCode = hashCode * -1521134295 + ChannelWidth.GetHashCode();
            return hashCode;
        }
    }
}
