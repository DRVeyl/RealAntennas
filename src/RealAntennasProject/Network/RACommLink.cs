using System;

namespace RealAntennas
{
    class RACommLink : CommNet.CommLink
    {
        private readonly double CostScaler = 1e9;
        public double FwdDataRate { get; set; }
        public double RevDataRate { get; set; }
        public RealAntenna FwdAntennaTx { get; set; }
        public RealAntenna FwdAntennaRx { get; set; }
        public RealAntenna RevAntennaTx { get; set; }
        public RealAntenna RevAntennaRx { get; set; }
        public double FwdCost { get => CostFunc(FwdDataRate); }
        public double RevCost { get => CostFunc(RevDataRate); }
        public double FwdMetric { get; set; }
        public double RevMetric { get; set; }

        public override string ToString()
        {
            return $"{start.name} -to- {end.name} : {FwdMetric:F2}/{RevMetric:F2} : {RATools.PrettyPrintDataRate(FwdDataRate)}/{RATools.PrettyPrintDataRate(RevDataRate)} / {cost:F3} ({signal})";
        }

        public virtual double CostFunc(double datarate) => CostScaler / Math.Pow(datarate, 2);

        public override void Set(CommNet.CommNode a, CommNet.CommNode b, double datarate, double signalStrength)
        {
            this.a = a;
            this.b = b;
            cost = CostFunc(datarate);
            Update(signalStrength);
        }

        public override void Update(double signalStrength)
        {
            this.signalStrength = signalStrength;
            strengthAR = aCanRelay ? signalStrength : 0;
            strengthBR = bCanRelay ? signalStrength : 0;
            strengthRR = bothRelay ? signalStrength : 0;
            signal = CommNet.NodeUtilities.ConvertSignalStrength(Math.Ceiling(4 * this.signalStrength));
        }
    }
}
