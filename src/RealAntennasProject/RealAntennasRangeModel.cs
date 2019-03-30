using System;

namespace RealAntennas
{
    // Implements the old range model to fulfill general CommNetScenario requirement
    public class RealAntennasRangeModel : CommNet.IRangeModel
    {
        protected static readonly string ModTag = "[RealAntennasRangeModel] ";
        protected static readonly double path_loss_constant = 20 * Math.Log10(4 * Math.PI / (2.998 * Math.Pow(10, 8)));
        //double boltzman = 1.38064852 * Math.Pow(10,-23);
        public static readonly double boltzmann_dbW = 10 * Math.Log10(1.38064852e-23);      //-228.59917;
        public static readonly double boltzmann_dbm = boltzmann_dbW + 30;
        public double GetMaximumRange(double aPower, double bPower) => 1e30;
        public double GetNormalizedRange(double aPower, double bPower, double distance) => 1;
        public bool InRange(double aPower, double bPower, double sqrDistance) => true;

        public double RSSI(RealAntenna tx, RealAntenna rx, double distance, double frequency = 1e9)
            => tx.TxPower + tx.Gain - PathLoss(distance, frequency) - 0 + rx.Gain;

        public double PathLoss(double distance, double frequency = 1e9)
            //FSPL = 20 log D + 20 log freq + 20 log (4pi/c)
            => (20 * Math.Log10(distance * frequency)) + path_loss_constant;

        public double NoiseFloor (RealAntenna rx, double noiseTemp = 290)
        {
            double sensitivity_dbm = boltzmann_dbm + (10 * Math.Log10(noiseTemp * rx.Bandwidth));
            //          Debug.LogFormat("NoiseFloor() for {0}: {1}dBm.", rx, sensitivity_dbm);
            return sensitivity_dbm;
        }

        public double NoiseTemperature(RACommNode rx, Vector3d origin)
        {
            // Calculating sensitivity from [fake] effective temperature and bandwidth
            // How do we actually get temperature of an unloaded vessel?
            // (Requires knowing it's part's temperature, or the surface temperature if on a body, or distance to Sun?)
            //
            // https://www.itu.int/dms_pubrec/itu-r/rec/p/R-REC-P.372-7-200102-S!!PDF-E.pdf
            //
            // Sky Temperature:  curves that vary based on atmosphere composition of a body.
            //   Earth example: http://www.delmarnorth.com/microwave/requirements/satellite_noise.pdf
            //     <10 deg K @ <15GHz and >30deg elevation in clear weather
            //     Rain can be a very large contributor, tho.
            //     At 0 deg elevation, sky temperature is effectively Earth ambient temperature = 290K.
            //     An omni antenna near Earth has a temperature = Earth ambient temperature = 290K.
            //     A directional antenna on a satellite pointed at Earth s.t. the entire main lobe of the ant
            //        is the Earth also has a noise temperature ~= 290K.
            //     A directional antenna pointed at the sun s.t. its main lobe encompasses ONLY the sun
            //        (so very directional) has an antenna temperature ~= 6000K?
            //  Ref: Galactic noise is high below 1000 MHz. At around 150 MHz, it is approximately 1000 K. At 2500 MHz, it has leveled off to around 10 K.
            //  Atmospheric absorption:  Earth example:  <1dB @ <20GHz
            //  Rainfall has a specific attenuation from absorption, and 
            //    and a correlated contribution to sky temperature from re-emission.
            //  We should either track the effective temperature of the receiver electronics,
            //    or calculate it from its more conventional notation (to me) of noise figure.
            //  So we can get system temperature = antenna effective temperature + sky temperature + receiver effective temperature
            //  Sky we can "define" based on the near celestial body, or set as 3K for deep space.
            //  Receiver effective temperature we can derive from a noise figure.
            //  Antenna temperature is basically the notion of "sky" temperature + rain temperature, or 3-4K in deep space.
            //
            // P(dBW) = 10*log10(Kb*T*bandwidth) = -228.59917 + 10*log10(T*BW)
            if (rx.ParentBody != null)
            {
                Vector3d normal = rx.GetSurfaceNormalVector();
                Vector3d to_origin = origin - rx.position;
                double angle = Vector3d.Angle(normal, to_origin);   // Declination to incoming signal (0=vertical, 90=horizon)
//                Debug.LogFormat(ModTag + "AoA offset from vertical: {0}", angle);
                foreach (CelestialBody child in rx.ParentBody.orbitingBodies)
                {
                    double childAngle = Vector3d.Angle(to_origin, child.position - rx.position);
                    double childDistance = Vector3d.Distance(rx.position, child.position);
//                    Debug.LogFormat(ModTag + "Offset from Origin to sibling {0}: {1} deg and distance {2}", child, childAngle, childDistance);
                }
                CelestialBody refBody = rx.ParentBody;
                while (refBody != Planetarium.fetch.Sun)
                {
                    refBody = refBody.referenceBody;
                    double parentAngle = Vector3d.Angle(to_origin, refBody.position - rx.position);
                    double parentDistance = Vector3d.Distance(rx.position, refBody.position);
//                    Debug.LogFormat(ModTag + "Offset from Origin to {0}: {1} deg and distance {2}", refBody, parentAngle, parentDistance);
                }
            }
            return 290;
        }
        public double ConvertCIToScaleFactor(double CI)
        {
            if (CI < 0) return 0;
            if (CI > 20) return 1;
            return (CI / 20);
        }

    }
}
