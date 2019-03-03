namespace RealAntennas
{
    public interface IRealAntennasRangeModel
    {
        double GetMaximumRange(RACommNode a, RACommNode b, double frequency = 1e9);
        double GetNormalizedRange(RACommNode a, RACommNode b, double distance);
        bool InRange(RACommNode a, RACommNode b, double distance);
    }
}
