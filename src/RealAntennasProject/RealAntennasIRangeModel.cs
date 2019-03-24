namespace RealAntennas
{
    public interface IRealAntennasRangeModel
    {
        double GetMaximumRange(RACommNode tx, RACommNode rx, RealAntenna txAnt, RealAntenna rxAnt, double frequency = 1e9);
        double GetNormalizedRange(RACommNode tx, RACommNode rx, RealAntenna txAnt, RealAntenna rxAnt, double distance);
        bool InRange(RACommNode tx, RACommNode rx, RealAntenna txAnt, RealAntenna rxAnt, double distance);
    }
}
