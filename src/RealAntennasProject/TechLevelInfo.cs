using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas
{
    public class TechLevelInfo
    {
        [Persistent] public string name;
        [Persistent] public int Level;
        [Persistent] public string Description;
        [Persistent] public float PowerEfficiency;
        [Persistent] public float ReflectorEfficiency;
        [Persistent] public float MinDataRate;
        [Persistent] public float MaxDataRate;
        [Persistent] public float MaxPower;
        [Persistent] public float MassPerWatt;
        [Persistent] public float BaseMass;
        [Persistent] public float BasePower;
        [Persistent] public float BaseCost;
        [Persistent] public float CostPerWatt;
        [Persistent] public float ReceiverNoiseTemperature;

        public static bool initialized = false;

        public static readonly Dictionary<int, TechLevelInfo> All = new Dictionary<int, TechLevelInfo>();
        public static int MaxTL { get; set; }  = -1;
        protected static readonly string ModTag = "[RealAntennas.TechLevelInfo] ";

        public TechLevelInfo() { }

        public static void Init(ConfigNode config)
        {
            string res = $"{ModTag} Init()";
            All.Clear();
            foreach (ConfigNode node in config.GetNodes("TechLevelInfo"))
            {
                TechLevelInfo obj = ConfigNode.CreateObjectFromConfig<TechLevelInfo>(node);
                res += $"\n{ModTag} Adding TL {obj}";
                All.Add(obj.Level, obj);
                MaxTL = Math.Max(MaxTL, obj.Level);
            }
            Debug.Log(res);
            initialized = true;
        }

        public override string ToString() => $"{name} L:{Level} MaxP:{MaxPower:N0}dBm MaxRate:{RATools.PrettyPrintDataRate(MaxDataRate)} Eff:{PowerEfficiency:F4}";

        public static TechLevelInfo GetTechLevel(int i)
        {
            if (!initialized) 
                Init(GameDatabase.Instance.GetConfigNode("RealAntennas/RealAntennasCommNetParams/RealAntennasCommNetParams"));
            i = Mathf.Clamp(i, 0, MaxTL);
            if (All.TryGetValue(i, out TechLevelInfo info))
            {
                return info;
            }
            return All[0];
        }
    }
}
