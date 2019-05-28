using System;

namespace RealAntennas.Network
{
    // Implements the old range model to fulfill general CommNetScenario requirement
    public class RealAntennasRangeModel : CommNet.IRangeModel
    {
        public double GetMaximumRange(double aPower, double bPower) => 1e30;
        public double GetNormalizedRange(double aPower, double bPower, double distance) => 1;
        public bool InRange(double aPower, double bPower, double sqrDistance) => true;
    }
}
