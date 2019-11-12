﻿using System;
using System.Collections.Generic;
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
        protected static readonly string ModTag = "[RealAntennas.BandInfo] ";
        public static BandInfo Get(string band)
        {
            if (!initialized && GameDatabase.Instance.GetConfigNode("RealAntennas/RealAntennasCommNetParams/RealAntennasCommNetParams") is ConfigNode node)
                Init(node);
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

        public static List<BandInfo> GetFromTechLevel(int level)
        {
            List<BandInfo> l = new List<BandInfo>() { };
            foreach (BandInfo bi in All.Values)
            {
                if (level >= bi.TechLevel) l.Add(bi);
            }
            return l;
        }

        public override bool Equals(object obj) => Equals(obj as BandInfo);
        public bool Equals(BandInfo other)
        {
            if (other is null) return false;
            return (Frequency == other.Frequency) && (ChannelWidth == other.ChannelWidth);
        }

        public static bool operator ==(BandInfo lhs, BandInfo rhs)
        {
            if (lhs is null) return (rhs is null);
            return lhs.Equals(rhs);
        }

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
