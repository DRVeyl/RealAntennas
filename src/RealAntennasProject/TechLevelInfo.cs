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
        [Persistent] public float Efficiency;
        [Persistent] public float MinDataRate;
        [Persistent] public float MaxDataRate;
        [Persistent] public float MaxPower;
        [Persistent] public float MassPerWatt;
        [Persistent] public float BaseMass;
        [Persistent] public float BasePower;
        [Persistent] public float BaseCost;
        [Persistent] public float CostPerWatt;

        public static bool initialized = false;

        public static Dictionary<int, TechLevelInfo> All = new Dictionary<int, TechLevelInfo>();
        protected static readonly string ModTag = "[RealAntennas.TechLevelInfo] ";

        public TechLevelInfo() { }
        public TechLevelInfo(float efficiency, float minDataRate, float maxDataRate, float maxPower, float massPerWatt, float baseMass, float basePower, float baseCost, float costPerWatt)
        {
            Efficiency = efficiency;
            MinDataRate = minDataRate;
            MaxDataRate = maxDataRate;
            MaxPower = maxPower;
            MassPerWatt = massPerWatt;
            BaseMass = baseMass;
            BasePower = basePower;
            BaseCost = baseCost;
            CostPerWatt = costPerWatt;
        }

        public static void Init(ConfigNode config)
        {
            Debug.LogFormat($"{ModTag} Init()");
            All.Clear();
            foreach (ConfigNode node in config.GetNodes("TechLevelInfo"))
            {
                TechLevelInfo obj = ConfigNode.CreateObjectFromConfig<TechLevelInfo>(node);
                Debug.LogFormat($"{ModTag} Adding TL {obj}");
                All.Add(obj.Level, obj);
            }
            initialized = true;
        }

        public override string ToString() => $"{name} L:{Level} MaxP:{MaxPower:N0}dBm MaxRate:{RATools.PrettyPrintDataRate(MaxDataRate)} Eff:{Efficiency:F4}";
        /*
        //public static TechLevelInfo GetTechLevel(int i) => _techLevels[(i < 0) ? 0 : i];

        private static IList<TechLevelInfo> _techLevels = new List<TechLevelInfo>()
            {
                new TechLevelInfo(1/18f, 4, 4, 0.1f, 1.6f, 1, 2f, 2, 5),
                new TechLevelInfo(1/13f, 4, 4, 1, 1.34f, 0.26f, 0.3f, 4, 4),
                new TechLevelInfo(1/10f, 1, 64, 5, 1.16f, 6.9f, 8f, 30, 3.5f),
                new TechLevelInfo(3/23f, 8, 64, 5, 1, 20.2f, 19.5f, 50, 3),
                new TechLevelInfo(1/6f, 8, 4096, 10, 0.86f, 17.2f, 25.7f, 80, 2.5f),
                new TechLevelInfo(1/4.5f, 16, 16384, 20, 0.75f, 21, 23, 120, 2),
                new TechLevelInfo(1/4f, 16, 131072, 20, 11.6f/18, 30.7f, 21.4f, 175, 1.7f),
                new TechLevelInfo(20/53.7f, 16, 262144, 40, 10.8f/20, 21.3f, 18.3f, 75, 0.5f),
                new TechLevelInfo(41.6f/94.6f, 16, 134217728, 100, 5.9f/41.6f, 7.5f, 11.7f, 50, 0.4f)
            };
            */
    }
}
