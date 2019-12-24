using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace RealAntennas
{
    class Physics
    {
        public static readonly double boltzmann_dBW = 10 * Math.Log10(1.38064852e-23);      //-228.59917;
        public static readonly double boltzmann_dBm = boltzmann_dBW + 30;
        public static readonly double MaxPointingLoss = 200;
        private static readonly double path_loss_constant = 20 * Math.Log10(4 * Math.PI / (2.998 * Math.Pow(10, 8)));

        public static AnimationCurve AntennaGainCurve = new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(0.5f, -3, -10, -10), new Keyframe(1, -10, -20, -20))
        {
            postWrapMode = WrapMode.ClampForever,
            preWrapMode = WrapMode.ClampForever
        };
        public static double GainFromDishDiamater(double diameter, double freq, double efficiency=1)
        {
            double gain = 0;
            if (diameter > 0 && efficiency > 0)
            {
                double wavelength = Physics.c / freq;
                gain = RATools.LogScale(9.87 * efficiency * diameter * diameter / (wavelength * wavelength));
            }
            return gain;
        } 
        public static double GainFromReference(double refGain, double refFreq, double newFreq)
        {
            double gain = 0;
            if (refGain > 0)
            {
                gain = refGain;
                gain += (refGain <= RealAntenna.MaxOmniGain) ? 0 : RATools.LogScale(newFreq / refFreq);
            }
            return gain;
        }

        public static double Beamwidth(double gain) => Math.Sqrt(52525 / RATools.LinearScale(gain));

        public static double PathLoss(double distance, double frequency = 1e9)
            //FSPL = 20 log D + 20 log freq + 20 log (4pi/c)
            => (20 * Math.Log10(distance * frequency)) + path_loss_constant;

        public static double ReceivedPower(RealAntenna tx, RealAntenna rx, double distance, double frequency = 1e9)
            => tx.TxPower + tx.Gain - PathLoss(distance, frequency) - PointingLoss(tx, rx.Position) - PointingLoss(rx, tx.Position) + rx.Gain;

        public static double PointingLoss(double angle, double beamwidth) 
            => (angle > beamwidth) ? MaxPointingLoss : -1 * AntennaGainCurve.Evaluate(Convert.ToSingle(angle / beamwidth));
        public static double PointingLoss(RealAntenna ant, Vector3 origin)
            => (ant.CanTarget && ant.ToTarget != Vector3.zero) ? PointingLoss(Vector3.Angle(origin - ant.Position, ant.ToTarget), ant.Beamwidth) : 0;

        public static double c = 2.998e8;
        public static double SunTemp(double frequency) => 5672 * Math.Pow(frequency / c, .24517);   // QUIET Sun Temp, active can be 2-3x higher
        public static double AtmosphereMeanEffectiveTemp(double CD) => 255 + (25 * CD); // 0 <= CD <= 1
        public static double AtmosphereNoiseTemperature(double elevationAngle, double frequency=1e9)
        {
            float CD = 0.5f;
            double Atheta = AtmosphereAttenuation(CD, elevationAngle, frequency);
            double LossFactor = RATools.LinearScale(Atheta);  // typical values = 1.01 to 2.0 (A = 0.04 dB to 3 dB) 
            double meanTemp = AtmosphereMeanEffectiveTemp(CD);
            double result = meanTemp * (1 - (1 / LossFactor));
//            Debug.LogFormat("AtmosphereNoiseTemperature calc for elevation {0:F2} freq {1:F2}GHz yielded attenuation {2:F2}, LossFactor {3:F2} and mean temp {4:F2} for result {5:F2}", elevationAngle, frequency/1e9, Atheta, LossFactor, meanTemp, result);
            return result;
        }
        public static double AtmosphereAttenuation(float CD, double elevationAngle, double frequency=1e9)
        {
            double AirMasses = (1 / Math.Sin(RATools.DegToRad(Math.Abs(elevationAngle))));
            return AtmosphereZenithAttenuation(CD, frequency) * AirMasses;
        }
        public static double AtmosphereZenithAttenuation(float CD, double frequency = 1e9)
        {
            // This would be a gigantic table lookup per ground station.
            if (frequency < 3e9) return 0.035;          // S/C/L band, didn't really vary by CD
            else if (frequency < 10e9)                  // X-Band, varied 0.4 to 0.6
            {
                return Mathf.Lerp(0.4f, 0.6f, CD);
            }
            else if (frequency < 27e9)                  // Ka-Band, varied .116-.239, .124-.384, .121-.407
            {
                return Mathf.Lerp(0.121f, 0.384f, CD);
            }
            else                                      // K-Band, 0.084-.226, .086-.375, .084-.373
            {
                return Mathf.Lerp(0.084f, 0.373f, CD);
            }
        }
        public static double BodyNoiseTemp(RealAntenna rx, CelestialBody body, Vector3d rxPointing)
        {
            if (rx.Shape == AntennaShape.Omni) return 0;    // No purpose in per-body noise temp for an omni.
            Vector3 toBody = body.position - rx.Position;
            double angle = Vector3.Angle(rxPointing, toBody);
            //            if (rx.GainAtAngle(Convert.ToSingle(angle)) < 0)
            if (rx.Beamwidth < angle) return 0;  // Pointed too far away
            double bw = rx.Beamwidth;
            double t = body.GetTemperature(1);      // TODO: Get the BLACKBODY temperature!
            double d = body.Radius * 2;
            double Rsqr = (rx.Position - body.position).sqrMagnitude;
            double G = RATools.LinearScale(rx.Gain);
            double angleRatio = angle / bw;
            double result = (t * G * d * d / (16 * Rsqr)) * Math.Pow(Math.E, -2.77 * angleRatio * angleRatio);
            //            Debug.LogFormat("Planetary Body Noise Power Estimator: Body {0} base temp {1:F0} diameter {2:F0}km at {3:F2}Mm Gain {4:F1} vs HPBW {5:F1} incident angle {6:F1} yields {7:F1}K", body, t, d/1000, (rx.Position - body.position).magnitude/1e6, G, bw, angle, result);
            return result;
        }

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
            double amt = AntennaMicrowaveTemp(rx);
            double atmos = AtmosphericTemp(rx, origin);
            double cosmic = CosmicBackgroundTemp(rx, origin);
            //double allbody = (rx.ParentNode.isHome) ? AllBodyTemps(rx, origin - rx.Position) : AllBodyTemps(rx, rx.ToTarget);
            // Home Stations are directional, but treated as always pointing towards the peer.
            double allbody = (rx.ParentNode.isHome) ? AllBodyTemps(rx, origin - rx.Position) : rx.cachedRemoteBodyNoiseTemp;
            double total = amt + atmos + cosmic + allbody;
            //            Debug.LogFormat("NoiseTemp: Antenna {0:F2}  Atmos: {1:F2}  Cosmic: {2:F2}  Bodies: {3:F2}  Total: {4:F2}", amt, atmos, cosmic, allbody, total);
            return total;

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
        }
        private static double AntennaMicrowaveTemp(RealAntenna rx) =>
            ((rx.ParentNode as RACommNode)?.ParentBody is CelestialBody) ? rx.AMWTemp : rx.TechLevelInfo.ReceiverNoiseTemperature;

        private static double AtmosphericTemp(RealAntenna rx, Vector3d origin)
        {
            if (rx.ParentNode is RACommNode rxNode && rxNode.ParentBody != null)
            {
                Vector3d normal = rxNode.GetSurfaceNormalVector();
                Vector3d to_origin = origin - rx.Position;
                double angle = Vector3d.Angle(normal, to_origin);
                double elevation = Math.Max(0,90.0 - angle);
                return AtmosphereNoiseTemperature(elevation, rx.Frequency);
            }
            return 0;
        }

        private static double CosmicBackgroundTemp(RealAntenna rx, Vector3d origin)
        {
            double CMB = 2.725;
            double lossFactor = 1;
            if (rx.ParentNode is RACommNode rxNode && rxNode.ParentBody != null)
            {
                float CD = 0.5f;
                Vector3d normal = rxNode.GetSurfaceNormalVector();
                Vector3d to_origin = origin - rx.Position;
                double angle = Vector3d.Angle(normal, to_origin);
                double elevation = Math.Max(0, 90.0 - angle);
                lossFactor = RATools.LinearScale(AtmosphereAttenuation(CD, elevation, rx.Frequency));
            }
            return CMB / lossFactor;
        }

        public static double AllBodyTemps(RealAntenna rx, Vector3d rxPointing)
        {
            double temp = 0;
            if (rx.Shape != AntennaShape.Omni)
            {
                Profiler.BeginSample("RA Physics AllBodyTemps MainLoop");
                RACommNode node = rx.ParentNode as RACommNode;
                // Note there are ~33 bodies in RSS.
                foreach (CelestialBody body in FlightGlobals.Bodies)
                {
                    if (!node.isHome || !node.ParentBody.Equals(body))
                    {
                        temp += BodyNoiseTemp(rx, body, rxPointing);
                    }
                }
                Profiler.EndSample();
            }
            return temp;
        }
    }
}
