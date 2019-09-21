namespace RealAntennas.Antenna
{
    public class Encoder : Enumeration
    {
        // Reed-Solomon RS 255,223 sends 255 data symbols then 32 parity symbols.
        // https://deepspace.jpl.nasa.gov/dsndocs/810-005/Binder/810-005_Binder_Change42.pdf
        // 810-005, Module 208, Rev B, Page 30, Figure 14.  For calculating BER ~= 10^-5.
        private static readonly double RSMult = 255.0 / (255 + 32);
        public readonly double CodingRate;
        public readonly double RequiredEbN0;
        public static Encoder None = new Encoder(1, "None", 1, 10);
        public static Encoder ReedMuller = new Encoder(2, "Reed-Muller", 0.25, -1);
        public static Encoder ReedSolomon = new Encoder(3, "Reed-Solomon", RSMult, 5);
        public static Encoder Convolutional = new Encoder(4, "Convolutional", 0.5, 4);
        public static Encoder Concatenated = new Encoder(5, "Concatenated Reed-Solomon,Convolutional", 0.5 * RSMult, 2.5);
        public static Encoder Turbo = new Encoder(6, "Turbo 1/2", 0.5, 1);

        public Encoder(int id, string name, double rate, double minEbN0)
            : base(id, name)
        {
            CodingRate = rate;
            RequiredEbN0 = minEbN0;
        }

        public override string ToString() => $"[{Name} Rate {CodingRate:F2} Eb/N0 {RequiredEbN0}]";

        public static Encoder BestMatching(Encoder a, Encoder b) => (a.Id > b.Id) ? b : a;
        public static Encoder GetFromTechLevel(int level)
        {
            if (level <= 2) return Encoder.None;
            if (level <= 4) return Encoder.ReedMuller;
            switch(level)
            {
                case 5: return Encoder.ReedSolomon;
                case 6: return Encoder.Convolutional;
                case 7: return Encoder.Concatenated;
            }
            return Encoder.Turbo;
        }
    }
}
