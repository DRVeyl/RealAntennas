using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas.Antenna
{
    public class Encoder : Enumeration
    {
        // Reed-Solomon RS 255,223 sends 255 data symbols then 32 parity symbols.
        // https://deepspace.jpl.nasa.gov/dsndocs/810-005/Binder/810-005_Binder_Change42.pdf
        // 810-005, Module 208, Rev B, Page 30, Figure 14.  For calculating BER ~= 10^-5.
        public readonly int MinTechLevel;
        public readonly double CodingRate;
        public readonly double RequiredEbN0;
        public static bool initialized = false;

        public static Dictionary<string, Encoder> All = new Dictionary<string, Encoder>();
        protected static readonly string ModTag = "[RealAntennas.Encoder] ";

        public Encoder(int id, string name, int techLevel, double rate, double minEbN0)
            : base(id, name)
        {
            MinTechLevel = techLevel;
            CodingRate = rate;
            RequiredEbN0 = minEbN0;
        }

        public override string ToString() => $"[{Name} Rate {CodingRate:F2} Eb/N0 {RequiredEbN0}]";

        public static Encoder BestMatching(Encoder a, Encoder b) => (a.MinTechLevel > b.MinTechLevel) ? b : a;
        public static Encoder GetFromTechLevel(int level)
        {
            Encoder best = null;
            foreach (Encoder e in All.Values)
            {
                if (level >= e.MinTechLevel)
                {
                    if (best == null) best = e;
                    else if (e.MinTechLevel > best.MinTechLevel) best = e;
                }
            }
            return best;
        }

        public static void Init(ConfigNode config)
        {
            Debug.LogFormat($"{ModTag} Init()");
            All.Clear();
            int i = 0;
            foreach (ConfigNode node in config.GetNodes("EncoderInfo"))
            {
                double codingRate = 0f, requiredEbN0 = 0f;
                int tl = 0;
                string nm = node.GetValue("name");
                node.TryGetValue("TechLevel", ref tl);
                node.TryGetValue("CodingRate", ref codingRate);
                node.TryGetValue("RequiredEbN0", ref requiredEbN0);
                Debug.LogFormat($"{ModTag} Adding Encoder {nm} TL: {tl} Rate: {codingRate} RequiredEbN0: {requiredEbN0} dB");
                All.Add(nm, new Encoder(i, nm, tl, codingRate, requiredEbN0));
                i += 1;
            }
            initialized = true;
        }
    }
}
