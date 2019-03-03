using System;
using UnityEngine;

namespace RealAntennas
{
    // Implements the old range model to fulfill general CommNetScenario requirement
    public class RealAntennasRangeModel : CommNet.IRangeModel, IRealAntennasRangeModel
    {
        protected static readonly string ModTag = "[RealAntennasRangeModel] ";
        protected double path_loss_constant;
        public RealAntennasRangeModel() => path_loss_constant = 20 * Math.Log10(4 * Math.PI / (2.998 * Math.Pow(10, 8)));
        public double GetMaximumRange(double aPower, double bPower) => 1e30;
        public double GetNormalizedRange(double aPower, double bPower, double distance) {
            Debug.LogWarningFormat(ModTag + "Old GetNormalizedRange() called");
            Debug.LogFormat(StackTraceUtility.ExtractStackTrace());
            return 1;
        }
        public bool InRange(double aPower, double bPower, double sqrDistance) => true;
        public double GetMaximumRange(RACommNode a, RACommNode b, double frequency = 1e9)
        {
            // Calc the range that yields this path loss.
            double pl1 = a.RAAntenna.TxPower - b.RAAntenna.Sensitivity + a.RAAntenna.CodingGain;
            double pl2 = b.RAAntenna.TxPower - a.RAAntenna.Sensitivity + b.RAAntenna.CodingGain;
            double targetPL = Math.Min(pl1, pl2) + a.RAAntenna.Gain + b.RAAntenna.Gain;
//            double log_10_dist = (targetPL - (20 * Math.Log10(frequency)) - (20 * Math.Log10(corr))) / 20;
            double log_10_dist = (targetPL - (20 * Math.Log10(frequency)) - path_loss_constant) / 20;
            double maxDistance = Math.Pow(10, log_10_dist);
//            Debug.LogFormat("Calculated max distance {0} (path loss {1}) for {2}/{3}", maxDistance, targetPL, a, b);
            return maxDistance;
        }
        public double GetNormalizedRange(RACommNode a, RACommNode b, double distance)
        {
            double RSSI = Math.Min(ComputeRSSI(a, b, distance), ComputeRSSI(b, a, distance));
            double CI = RSSI - b.RAAntenna.Sensitivity;
            return ConvertCIToScaleFactor(CI);
        }
        public bool InRange(RACommNode a, RACommNode b, double distance) => GetNormalizedRange(a, b, distance) > 0;
        public double PathLoss(double distance, double frequency = 1e9)   // Default 1GHz
        {
            //FSPL = 20 log D + 20 log freq + 20 log (4pi/c)
            double FSPL = (20 * Math.Log10(distance)) + (20 * Math.Log10(frequency)) + path_loss_constant;
            return FSPL;
        }
        public double ConvertCIToScaleFactor(double CI)
        {
            if (CI < 0) return 0;
            if (CI > 20) return 1;
            return (CI / 20);
        }

        public double ComputeRSSI(RACommNode tx, RACommNode rx, double distance, double frequency = 1e9)
        {
            // RSSI = Power_transmitter + Gain_transmitter - FSPL - Losses_other + Gain_receiver + Gain_coding
            double RSSI = tx.RAAntenna.TxPower + tx.RAAntenna.Gain - PathLoss(distance, frequency) - 0 +
                       rx.RAAntenna.Gain + tx.RAAntenna.CodingGain;
            return RSSI;
        }
    }
}
