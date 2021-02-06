using System;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Mathematics;

namespace RealAntennas
{
    class Physics
    {
        //public static readonly double boltzmann_dBW = 10 * Math.Log10(1.38064852e-23);      //-228.59917;
        public const double boltzmann_dBW = -228.599168683097;
        public const double boltzmann_dBm = boltzmann_dBW + 30;
        public const double MaxPointingLoss = 200;
        public const float MaxOmniGain = 5;
        public const double c = 2.998e8;
        public const double CMB = 2.725;
        public static double SolarLuminosity => PhysicsGlobals.SolarLuminosity > double.Epsilon ? PhysicsGlobals.SolarLuminosity : Math.Pow(Planetarium.fetch.Home.orbit.semiMajorAxis, 2.0) * 4.0 * Math.PI * PhysicsGlobals.SolarLuminosityAtHome;

        //private static readonly double path_loss_constant = 20 * Math.Log10(4 * Math.PI / (2.998 * Math.Pow(10, 8)));
        private const double path_loss_constant = -147.552435289803;

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
                gain += (refGain <= MaxOmniGain) ? 0 : RATools.LogScale(newFreq / refFreq);
            }
            return gain;
        }

        public static double Beamwidth(double gain) => math.sqrt(52525 / RATools.LinearScale(gain));

        public static double PathLoss(double distance, double frequency = 1e9)
            //FSPL = 20 log D + 20 log freq + 20 log (4pi/c)
            => (20 * math.log10(distance * frequency)) + path_loss_constant;

        public static double ReceivedPower(RealAntenna tx, RealAntenna rx, double distance, double frequency = 1e9)
            => tx.TxPower + tx.Gain - PathLoss(distance, frequency) - PointingLoss(tx, rx.Position) - PointingLoss(rx, tx.Position) + rx.Gain;

        // Beamwidth is full side-to-side HPBW, == 0 to -10dB offset angle
        public static double PointingLoss(double angle, double beamwidth)
        //            => (angle > beamwidth) ? MaxPointingLoss : -1 * AntennaGainCurve.Evaluate(Convert.ToSingle(angle / beamwidth));
        {
            double norm = angle / beamwidth;
            if (norm > 1) return MaxPointingLoss;
            if (norm < 0.14) return math.lerp(0, 0.25, norm / 0.14);
            else if (norm < 0.2) return math.lerp(0.25, 0.5, (norm - 0.14) / 0.2);
            else if (norm < 0.29) return math.lerp(0.5, 1, (norm - 0.2) / 0.29);
            else if (norm < 0.41) return math.lerp(1, 2, (norm - 0.29) / 0.41);
            else if (norm < 0.5) return math.lerp(2, 3, (norm - 0.41) / 0.5);
            else if (norm < 0.57) return math.lerp(3, 4, (norm - 0.5) / 0.57);
            else if (norm < 0.61) return math.lerp(4, 4.5, (norm - 0.57) / 0.61);
            else if (norm < 0.64) return math.lerp(4.5, 5, (norm - 0.61) / 0.64);
            else if (norm < 0.7) return math.lerp(5, 6, (norm - 0.64) / 0.7);
            else if (norm < 0.76) return math.lerp(6, 7, (norm - 0.7) / 0.76);
            else if (norm < 0.81) return math.lerp(7, 8, (norm - 0.76) / 0.81);
            else if (norm < 0.86) return math.lerp(8, 9, (norm - 0.81) / 0.86);
            else return math.lerp(9, 10, (norm - 0.86) / 1);
        }
        public static double PointingLoss(RealAntenna ant, Vector3 origin)
            => (ant.CanTarget && ant.ToTarget != Vector3.zero) ? PointingLoss(Vector3.Angle(origin - ant.Position, ant.ToTarget), ant.Beamwidth) : 0;

        public static double GainAtAngle(double gain, double angle) => gain - PointingLoss(math.abs(angle), Beamwidth(gain));
        // Beamwidth is the 3dB full beamwidth contour, ~= the offset angle to the 10dB contour.
        // 10dBi: Beamwidth = 72 = 4dB full beamwidth contour
        // 10dBi @ .6 efficiency: 57 = 3dB full beamwidth contour
        // 20dBi: Beamwidth = 23 = 4dB full beamwidth countour
        // 20dBi @ .6 efficiency: Beamwidth = 17.75 = 3dB full beamwidth contour

        // Sun Temp vs Freq from https://deepspace.jpl.nasa.gov/dsndocs/810-005/Binder/810-005_Binder_Change51.pdf Manual 105 Eq 14: T = 5672 * lambda ^ 0.24517, lambda units is mm
        public static double StarRadioTemp(double surfaceTemp, double frequency) => surfaceTemp * math.pow(1000 * c / frequency, .24517);   // QUIET Sun Temp, active can be 2-3x higher
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
            double AirMasses = (1 / math.sin(math.radians(math.abs(elevationAngle))));
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
        //https://en.wikipedia.org/wiki/Planetary_equilibrium_temperature   - hopefully body.albedo is the bond albedo?
        public static double GetEquilibriumTemperature(CelestialBody body)
        {
            if (body == Planetarium.fetch.Sun) return 5e6;  // Failsafe, should not trigger.
            double sunDistSqr = (body.position - Planetarium.fetch.Sun.position).sqrMagnitude;
            double IncidentSolarRadiation = SolarLuminosity / (math.PI_DBL * 4 * sunDistSqr);
            double val = IncidentSolarRadiation * (1 - body.albedo) / (4 * PhysicsGlobals.StefanBoltzmanConstant);
            return math.pow(val, 0.25);
        }

        public static double BodyBaseTemperature(CelestialBody body) 
            => body.atmosphere ? body.GetTemperature(1) : GetEquilibriumTemperature(body) + body.coreTemperatureOffset;

        //double baseTemp = body.atmosphere ? body.GetTemperature(1) : GetEquilibriumTemperature(body) + body.coreTemperatureOffset;
        //return body.isStar ? StarRadioTemp(baseTemp, rx.Frequency) : baseTemp;      // TODO: Get the BLACKBODY temperature!


        public static double BodyNoiseTemp(double3 antPos,
                                            double gain,
                                            double3 dir,
                                            double3 bodyPos,
                                            double bodyRadius,
                                            double bodyTemp)
        {
            if (gain < MaxOmniGain) return 0;
            if (bodyTemp < double.Epsilon) return 0;
            double3 toBody = bodyPos - antPos;
            double angle = MathUtils.Angle2(toBody, dir);
            double distance = math.length(toBody);
            double beamwidth = Beamwidth(gain);
            double bodyRadiusAngularRad = (distance > 10 * bodyRadius)
                    ? math.atan2(bodyRadius, distance)
                    : math.radians(MathUtils.AngularRadius(bodyRadius, distance));
            double bodyRadiusAngularDeg = math.degrees(bodyRadiusAngularRad);
            if (beamwidth < angle - bodyRadiusAngularDeg) return 0;  // Pointed too far away

            double angleRad = math.radians(angle);
            double beamwidthRad = math.radians(beamwidth);
            double gainDelta; // Antenna Pointing adjustment
            double viewedAreaBase;

            // How much of the body is in view of the antenna?
            if (beamwidth < bodyRadiusAngularDeg - angle)    // Antenna viewable area completely enclosed by body
            {
                viewedAreaBase = math.PI_DBL * beamwidthRad * beamwidthRad;
                gainDelta = 0;
            }
            else if (beamwidth > bodyRadiusAngularDeg + angle)   // Antenna viewable area completely encloses body
            {
                viewedAreaBase = Mathf.PI * bodyRadiusAngularRad * bodyRadiusAngularRad;
                gainDelta = -PointingLoss(angle, beamwidth);
            }
            else
            {
                viewedAreaBase = MathUtils.CircleCircleIntersectionArea(beamwidthRad, bodyRadiusAngularRad, angleRad);
                double intersectionCenter = MathUtils.CircleCircleIntersectionOffset(beamwidthRad, bodyRadiusAngularRad, angleRad);
                gainDelta = -PointingLoss(math.degrees(intersectionCenter + beamwidthRad) / 2, beamwidth);
            }

            // How much of the antenna viewable area is occupied by the body
            double antennaViewableArea = math.PI_DBL * beamwidthRad * beamwidthRad;
            double viewableAreaRatio = viewedAreaBase / antennaViewableArea;

            /*
            double d = body.Radius * 2;
            double Rsqr = toBody.sqrMagnitude;
            double G = RATools.LinearScale(rx.Gain);
            double angleRatio = angle / rx.Beamwidth;

            // https://deepspace.jpl.nasa.gov/dsndocs/810-005/Binder/810-005_Binder_Change51.pdf Module 105: 2.4.3 Planetary Noise estimator
            // This estimator is correct for the DSN viewing planets, but wrong for the sun & moon.
            double result = (t * G * d * d / (16 * Rsqr)) * Math.Pow(Math.E, -2.77 * angleRatio * angleRatio);
            */

            double result = bodyTemp * viewableAreaRatio * RATools.LinearScale(gainDelta);
            //Debug.Log($"Planetary Body Noise Power Estimator: Body {body} Temp: {bodyTemp:F0} AngularDiameter: {bodyRadiusAngularDeg * 2:F1} @ {angle:F1} HPBW: {rx.Beamwidth:F1} ViewableAreaRatio: {viewableAreaRatio:F2} gainDelta: {gainDelta:F4} result: {result}");
            return result;
        }

        public static double BodyNoiseTemp(RealAntenna rx, CelestialBody body, Vector3d rxPointing) =>
            BodyNoiseTemp(new double3(rx.PrecisePosition.x, rx.PrecisePosition.y, rx.PrecisePosition.z),
                        rx.Gain,
                        new double3(rx.ToTarget.x, rx.ToTarget.y, rx.ToTarget.z),
                        new double3(body.position.x, body.position.y, body.position.z),
                        body.Radius,
                        body.isStar ? StarRadioTemp(BodyBaseTemperature(body), rx.Frequency) : BodyBaseTemperature(body));

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
        public static double AntennaMicrowaveTemp(RealAntenna rx) =>
            ((rx.ParentNode as RACommNode)?.ParentBody is CelestialBody) ? rx.AMWTemp : rx.TechLevelInfo.ReceiverNoiseTemperature;

        public static double AtmosphericTemp(RealAntenna rx, Vector3d origin)
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

        public static double CosmicBackgroundTemp(double3 surfaceNormal, double3 toOrigin, double freq, bool isHome)
        {
            double lossFactor = 1;
            if (isHome)
            {
                float CD = 0.5f;
                double angle = MathUtils.Angle2(surfaceNormal, toOrigin);
                double elevation = math.max(0, 90.0 - angle);
                lossFactor = RATools.LinearScale(AtmosphereAttenuation(CD, elevation, freq));
            }
            return CMB / lossFactor;
        }

        private static double CosmicBackgroundTemp(RealAntenna rx, Vector3d origin)
        {
            var rxNode = rx.ParentNode as RACommNode;
            Vector3d normal = (rxNode?.ParentBody is CelestialBody) ? rxNode.GetSurfaceNormalVector() : Vector3d.zero;
            bool isHome = (rx.ParentNode as RACommNode)?.ParentBody is CelestialBody;
            Vector3d to_origin = origin - rx.Position;
            return CosmicBackgroundTemp(new double3(normal.x, normal.y, normal.z),
                                        new double3(to_origin.x, to_origin.y, to_origin.z),
                                        rx.Frequency,
                                        isHome);
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
