using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RealAntennas
{
    class RACommLink : CommNet.CommLink
    {
        private static readonly double CostScaler = 1e9;
        public double FwdDataRate { get; set; }
        public double RevDataRate { get; set; }
        public RealAntenna FwdAntennaTx { get; set; }
        public RealAntenna FwdAntennaRx { get; set; }
        public RealAntenna RevAntennaTx { get; set; }
        public RealAntenna RevAntennaRx { get; set; }
        public double FwdCost { get => CostFunc(FwdDataRate); }
        public double RevCost { get => CostFunc(RevDataRate); }
        public double FwdCI { get; set; }
        public double RevCI { get; set; }

        public override string ToString()
        {
            return $"{start.name} ({FwdCI:F1} dB) -to- {end.name} ({RevCI:F1} dB) : {cost:F3} ({signal})";
        }

        public static double CostFunc(double datarate) => CostScaler / Math.Pow(datarate, 2);
    }
}
