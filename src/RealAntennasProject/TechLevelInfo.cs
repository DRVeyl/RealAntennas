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

        public static Dictionary<int, TechLevelInfo> All = new Dictionary<int, TechLevelInfo>();
        protected static readonly string ModTag = "[RealAntennas.TechLevelInfo] ";

        public TechLevelInfo() { }

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

        public override string ToString() => $"{name} L:{Level} MaxP:{MaxPower:N0}dBm MaxRate:{RATools.PrettyPrintDataRate(MaxDataRate)} Eff:{PowerEfficiency:F4}";
        
        public static TechLevelInfo GetTechLevel(int i) => All[(i < 0) ? 0 : i];

    }
}
