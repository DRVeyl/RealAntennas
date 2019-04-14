using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RealAntennas
{
    class Physics
    {
        public static readonly double boltzmann_dBW = 10 * Math.Log10(1.38064852e-23);      //-228.59917;
        public static readonly double boltzmann_dBm = boltzmann_dBW + 30;
        private static readonly double path_loss_constant = 20 * Math.Log10(4 * Math.PI / (2.998 * Math.Pow(10, 8)));

        public static double PathLoss(double distance, double frequency = 1e9)
            //FSPL = 20 log D + 20 log freq + 20 log (4pi/c)
            => (20 * Math.Log10(distance * frequency)) + path_loss_constant;

        public static double ReceivedPower(RealAntenna tx, RealAntenna rx, double distance, double frequency = 1e9)
            => tx.TxPower + tx.Gain - PathLoss(distance, frequency) - PointingLoss(tx, rx) + rx.Gain;

        public static double PointingLoss(RealAntenna tx, RealAntenna rx) => tx.PointingLoss(rx) + rx.PointingLoss(tx);
        public static double MinimumTheoreticalEbN0(double SpectralEfficiency)
        {
            // Given SpectralEfficiency in bits/sec/Hz (= Channel Capacity / Bandwidth)
            // Solve Shannon Hartley for Eb/N0 >= (2^(C/B) - 1) / (C/B)
            return RATools.LogScale(Math.Pow(2, SpectralEfficiency) - 1) / SpectralEfficiency;
            // 1=> 0dB, 2=> 1.7dB, 3=> 3.7dB, 4=> 5.7dB, 5=> 8dB, 6=> 10.2dB, 7=> 12.6dB, 8=> 15dB
            // 9=> 17.6dB, 10=> 20.1dB, 11=> 22.7dB, 20=> 47.2dB
            // 0.5 => -0.8dB.  Rate 1/2 BPSK Turbo code is EbN0 = +1dB, so about 1.8 above Shannon?
        }
        public static double NoiseFloor(RealAntenna rx, double noiseTemp) => NoiseSpectralDensity(noiseTemp) + (10 * Math.Log10(rx.Bandwidth));
        public static double NoiseSpectralDensity(double noiseTemp) => boltzmann_dBm + (10 * Math.Log10(noiseTemp));
        public static double NoiseTemperature(RealAntenna rx, Vector3d origin)
        {
            // Calculating antenna temperature
            return  AntennaMicrowaveTemp(rx, origin) +
                    AtmosphericTemp(rx, origin) +
                    SunTemp(rx, origin) +
                    CosmicBackgroundTemp(rx, origin) +
                    OtherBodyTemp(rx, origin);

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
            if (rx.ParentNode is RACommNode rxNode && rxNode.ParentBody != null)
            {
                Vector3d normal = rxNode.GetSurfaceNormalVector();
                Vector3d to_origin = origin - rx.Position;
                double angle = Vector3d.Angle(normal, to_origin);   // Declination to incoming signal (0=vertical, 90=horizon)
                                                                    //                Debug.LogFormat(ModTag + "AoA offset from vertical: {0}", angle);
                foreach (CelestialBody child in rxNode.ParentBody.orbitingBodies)
                {
                    double childAngle = Vector3d.Angle(to_origin, child.position - rx.Position);
                    double childDistance = Vector3d.Distance(rx.Position, child.position);
                    //                    Debug.LogFormat(ModTag + "Offset from Origin to sibling {0}: {1} deg and distance {2}", child, childAngle, childDistance);
                }
                CelestialBody refBody = rxNode.ParentBody;
                while (refBody != Planetarium.fetch.Sun)
                {
                    refBody = refBody.referenceBody;
                    double parentAngle = Vector3d.Angle(to_origin, refBody.position - rx.Position);
                    double parentDistance = Vector3d.Distance(rx.Position, refBody.position);
                    //                    Debug.LogFormat(ModTag + "Offset from Origin to {0}: {1} deg and distance {2}", refBody, parentAngle, parentDistance);
                }
            }
            return 290;
        }
        private static double AntennaMicrowaveTemp(RealAntenna rx, Vector3d origin) => 0;
        private static double AtmosphericTemp(RealAntenna rx, Vector3d origin) => 0;
        private static double SunTemp(RealAntenna rx, Vector3d origin) => 0;
        private static double CosmicBackgroundTemp(RealAntenna rx, Vector3d origin) => 3;
        private static double OtherBodyTemp(RealAntenna rx, Vector3d origin) => 287;
    }
}
