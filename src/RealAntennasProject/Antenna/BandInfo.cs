using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas.Antenna
{
    public class BandInfo : Enumeration, IEquatable<BandInfo>
    {
        public readonly int minTechLevel;
        public readonly double Frequency;
        public readonly string DisplayName;
        public readonly float ChannelWidth;
        public static bool initialized = false;
        public static float[] MaxSymbolRateByTechLevel = { 32, 4e3f, 16e3f, 32e3f, 1e5f, 1e6f, 1e7f, 1e8f, 1e9f, 1e10f };
        public static Dictionary<string, BandInfo> All = new Dictionary<string, BandInfo>();
        public static BandInfo Get(string band)
        {
            if (!initialized)
            {
//                ConfigNode RAParamNode = GameDatabase.Instance.GetConfigNode("RealAntennas/RealAntennasCommNetParams/RealAntennasCommNetParams");
                ConfigNode RAParamNode = null;
                foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("RealAntennasCommNetParams"))
                    RAParamNode = n;

                if (RAParamNode != null) Init(RAParamNode);
            }
            return All[band];
        }
        public static void Init(ConfigNode config)
        {
            Debug.LogFormat("RealAntennas.BandInfo init() on node {0}", config);
            All.Clear();
            int i = 0;
            foreach (ConfigNode node in config.GetNodes("BandInfo"))
            {
                int tl = 0;
                double freq = 0f;
                float chan = 0f;
                string nm = node.GetValue("name");
                node.TryGetValue("TechLevel", ref tl);
                node.TryGetValue("Frequency", ref freq);
                node.TryGetValue("ChannelWidth", ref chan);
                Debug.LogFormat($"RealAntennas.BandInfo adding band {nm} TL: {tl} Freq: {RATools.PrettyPrint(freq)}Hz Width: {RATools.PrettyPrint(chan)}Hz");
                All.Add(nm, new BandInfo(i, nm, nm + "-Band", tl, freq, chan));
                i += 1;
            }
            i = 0;
            string[] sRates = config.GetValue("MaxSymbolRateByTechLevel").Split(new char[] { ',' });
            foreach (string sRate in sRates)
            {
                Debug.LogFormat("(Unimplemented) MaxSymbolRate learning max rate {0} at TL {1}", sRate, i);
                MaxSymbolRateByTechLevel[i] = Single.Parse(sRate);
                i += 1;
            }
            initialized = true;
        }

        public BandInfo(int id, string name, string dispName, int minLevel, double freq, float chanWidth)
            : base(id, name)
        {
            DisplayName = dispName;
            minTechLevel = minLevel;
            Frequency = freq;
            ChannelWidth = chanWidth;
        }

        public float MaxSymbolRate(int techLevel) => ChannelWidth * (1 + techLevel - minTechLevel);
//        public float MaxSymbolRate(int techLevel) => Math.Max(ChannelWidth * (1 + techLevel - minTechLevel), MaxSymbolRateByTechLevel[techLevel-1]);

        public override string ToString() => $"[{DisplayName} {Frequency/1e6} MHz]";

        public static List<BandInfo> GetFromTechLevel(int level)
        {
            List<BandInfo> l = new List<BandInfo>() { };
            foreach (BandInfo bi in All.Values)
            {
                if (level >= bi.minTechLevel) l.Add(bi);
            }
            return l;
        }

        public override bool Equals(object obj) => Equals(obj as BandInfo);

        public bool Equals(BandInfo other)
        {
            return other != null &&
                   base.Equals(other) &&
                   Frequency == other.Frequency &&
                   ChannelWidth == other.ChannelWidth;
        }

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
