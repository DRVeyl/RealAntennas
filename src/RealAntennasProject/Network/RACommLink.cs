using System;

namespace RealAntennas
{
    public class RACommLink : CommNet.CommLink
    {
        private const double CostScalar = 1e5;
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
            return $"{start.name} -> {end.name} : {FwdMetric:F2}/{RevMetric:F2} : {RATools.PrettyPrintDataRate(FwdDataRate)}/{RATools.PrettyPrintDataRate(RevDataRate)} ({FwdCost:F3}/{RevCost:F3})";
        }

        public virtual double CostFunc(double datarate) => Math.Pow(CostScalar / datarate, 2);

        public override void Set(CommNet.CommNode a, CommNet.CommNode b, double datarate, double signalStrength)
        {
            this.a = a;
            this.b = b;
            cost = CostFunc(datarate);
            Update(signalStrength);
        }

        public void Copy(RACommLink source)
        {
            Set(source.a, source.b, source.FwdDataRate, source.signalStrength);
            FwdDataRate = source.FwdDataRate;
            RevDataRate = source.RevDataRate;
            FwdAntennaTx = source.FwdAntennaTx;
            FwdAntennaRx = source.FwdAntennaRx;
            RevAntennaTx = source.RevAntennaTx;
            RevAntennaRx = source.RevAntennaRx;
            FwdMetric = source.FwdMetric;
            RevMetric = source.RevMetric;
        }

        public void SwapEnds()
        {
            Set(b, a, RevDataRate, signalStrength);
            var x = FwdDataRate;
            FwdDataRate = RevDataRate;
            RevDataRate = x;
            var y = FwdAntennaTx;
            FwdAntennaTx = RevAntennaTx;
            RevAntennaTx = y;
            y = FwdAntennaRx;
            FwdAntennaRx = RevAntennaRx;
            RevAntennaRx = y;
            x = FwdMetric;
            FwdMetric = RevMetric;
            RevMetric = x;
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
