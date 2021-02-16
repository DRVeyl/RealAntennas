using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas.Antenna
{
    public class Encoder
    {
        // Reed-Solomon RS 255,223 sends 255 data symbols then 32 parity symbols.
        // https://deepspace.jpl.nasa.gov/dsndocs/810-005/Binder/810-005_Binder_Change42.pdf
        // 810-005, Module 208, Rev B, Page 30, Figure 14.  For calculating BER ~= 10^-5.
        [Persistent] public string name;
        [Persistent] public int TechLevel;
        [Persistent] public float CodingRate;
        [Persistent] public float RequiredEbN0;
        public static bool initialized = false;

        public static Dictionary<string, Encoder> All = new Dictionary<string, Encoder>();
        private const string ModTag = "[RealAntennas.Encoder]";

        public Encoder() { }
        public Encoder(string name, int techLevel, float rate, float minEbN0)
        {
            this.name = name;
            TechLevel = techLevel;
            CodingRate = rate;
            RequiredEbN0 = minEbN0;
        }

        public override string ToString() => $"[{name} Rate {CodingRate:F2} Eb/N0 {RequiredEbN0}]";

        public Encoder BestMatching(in Encoder other) => TechLevel > other.TechLevel ? other : this;
        public static Encoder BestMatching(in Encoder a, in Encoder b) => a.BestMatching(b);
        public static Encoder GetFromTechLevel(int level)
        {
            Encoder best = null;
            foreach (Encoder e in All.Values)
            {
                if (level >= e.TechLevel)
                {
                    if (best == null) best = e;
                    else if (e.TechLevel > best.TechLevel) best = e;
                }
            }
            return best;
        }

        public static void Init(ConfigNode config)
        {
            string res = $"{ModTag} Init()";
            All.Clear();
            foreach (ConfigNode node in config.GetNodes("EncoderInfo"))
            {
                Encoder obj = ConfigNode.CreateObjectFromConfig<Encoder>(node);
                res += $"\n{ModTag} Adding Encoder {obj}";
                All.Add(obj.name, obj);
            }
            Debug.Log(res);
            initialized = true;
        }
    }
}
