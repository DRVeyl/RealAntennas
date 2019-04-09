using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RealAntennas.Antenna
{
    public class Encoder : Enumeration
    {
        // Reed-Solomon RS 255,223 sends 255 data symbols then 32 parity symbols.
        // https://deepspace.jpl.nasa.gov/dsndocs/810-005/Binder/810-005_Binder_Change42.pdf
        // 810-005, Module 208, Rev B, Page 30, Figure 14.  For calculating BER ~= 10^-5.
        private static readonly double RSMult = 255 / (255 + 32);
        public readonly double CodingRate;
        public readonly double RequiredEbN0;
        public static Encoder None = new Encoder(1, "None", 1, 10);
        public static Encoder ReedSolomon = new Encoder(2, "Reed-Solomon", RSMult, 5);
        public static Encoder Convolutional = new Encoder(3, "Convolutional", 0.5, 4);
        public static Encoder Concatenated = new Encoder(4, "Concatenated Reed-Solomon,Convolutional", 0.5 * RSMult, 2.5);
        public static Encoder Turbo = new Encoder(5, "Turbo 1/2", 0.5, 1);

        public Encoder(int id, string name, double rate, double minEbN0)
            : base(id, name)
        {
            CodingRate = rate;
            RequiredEbN0 = minEbN0;
        }

        public Encoder BestMatching(Encoder a, Encoder b) => (a.Id > b.Id) ? b : a;
        public static Encoder GetFromTechLevel(int level)
        {
            if (level <= 1) return Encoder.None;
            switch(level)
            {
                case 2: return Encoder.ReedSolomon;
                case 3: return Encoder.Convolutional;
                case 4: return Encoder.Concatenated;
            }
            return Encoder.Turbo;
        }
    }
}
