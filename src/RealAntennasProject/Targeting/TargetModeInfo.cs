using System;
using System.Collections.Generic;

namespace RealAntennas.Targeting
{
    public class TargetModeInfo
    {
        [Persistent] public string name;
        [Persistent] public string displayName;
        [Persistent] public int techLevel;
        [Persistent] public string hint;
        [Persistent] public string text;
        [Persistent] public string texture;
        public AntennaTarget.TargetMode mode;

        public static Dictionary<string, TargetModeInfo> All = new Dictionary<string, TargetModeInfo>();
        public static List<TargetModeInfo> ListAll = new List<TargetModeInfo>();

        public static void Init(ConfigNode config)
        {
            All.Clear();
            var msg = StringBuilderCache.Acquire();
            foreach (ConfigNode node in config.GetNodes("TargetingMode"))
            {
                var obj = ConfigNode.CreateObjectFromConfig<TargetModeInfo>(node);
                try
                {
                    obj.mode = (AntennaTarget.TargetMode)Enum.Parse(typeof(AntennaTarget.TargetMode), obj.name, true);
                }
                catch (ArgumentException) { obj.mode = AntennaTarget.TargetMode.Vessel; }
                if (string.IsNullOrEmpty(obj.displayName))
                    obj.displayName = obj.name;
                msg.AppendLine($"[RealAntennas] Adding TargetingMode {obj.name}");
                All.Add(obj.name, obj);
            }
            ListAll.Clear();
            ListAll.AddRange(All.Values);
            UnityEngine.Debug.Log(msg.ToStringAndRelease());
        }
    }
}
