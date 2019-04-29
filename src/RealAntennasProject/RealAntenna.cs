using System;
using UnityEngine;

namespace RealAntennas
{
    public enum AntennaShape
    {
        Auto, Omni, Dish
    }
    public class RealAntenna : IComparable
    {
        public string Name { get; set; }
        public virtual double Gain { get; set; }         // Physical directionality, measured in dBi
        public virtual double TxPower { get; set; }       // Transmit Power in dBm (milliwatts)
        public virtual int TechLevel { get; set; }
        public Antenna.BandInfo RFBand;
        public virtual double SymbolRate { get; set; }
        public virtual double MinSymbolRate => SymbolRate / 1000;
        public virtual double Frequency => RFBand.Frequency;
        public virtual double PowerEfficiency => Math.Min(1, 0.5 + (TechLevel * 0.05));
        public virtual double AntennaEfficiency => Math.Min(0.7, 0.5 + (TechLevel * 0.025));
        public virtual double DataRate { get; }
        public virtual double Bandwidth => DataRate;          // RF bandwidth required.
        public virtual double AMWTemp { get; set; }
        public virtual double NoiseFloor(Vector3 origin) => Physics.NoiseFloor(this, NoiseTemp(origin));
        public virtual double NoiseTemp(Vector3 origin) => Physics.NoiseTemperature(this, origin);
        public virtual double Beamwidth => Math.Sqrt(52525 * AntennaEfficiency / RATools.LinearScale(Gain));
        public virtual double GainAtAngle(double angle) => Gain - Physics.PointingLoss(angle, Beamwidth);
        // Beamwidth is the 3dB full beamwidth contour, ~= the offset angle to the 10dB contour.
        // 10dBi: Beamwidth = 72 = 4dB full beamwidth contour
        // 10dBi @ .6 efficiency: 57 = 3dB full beamwidth contour
        // 20dBi: Beamwidth = 23 = 4dB full beamwidth countour
        // 20dBi @ .6 efficiency: Beamwidth = 17.75 = 3dB full beamwidth contour
        public Antenna.Encoder Encoder => Antenna.Encoder.GetFromTechLevel(TechLevel); 
        public virtual double RequiredCI => Encoder.RequiredEbN0;

        public ModuleRealAntenna Parent { get; internal set; }
        public CommNet.CommNode ParentNode { get; set; }
        public Vector3 Position => ParentNode.position;

        public virtual AntennaShape Shape => Gain <= maxOmniGain ? AntennaShape.Omni : AntennaShape.Dish;
        public virtual bool CanTarget => Shape != AntennaShape.Omni && (ParentNode == null || !ParentNode.isHome);
        private readonly double minimumSpotRadius = 1e3;
        private readonly double maxOmniGain = 8;
        public Vector3 ToTarget {
            get {
                if (!(CanTarget && Target != null)) return Vector3.zero;
                return (Target is Vessel v) ? v.transform.position - Position : (Vector3)(Target as CelestialBody).position - Position;
            }
        }
        public string TargetID { get; set; }
        private ITargetable _target = null;
        public ITargetable Target
        {
            get => _target;
            set
            {
                if (!CanTarget) _internalSet(null, "None", "None");
                else if (value is Vessel v) _internalSet(v, v.name, v.id.ToString());
                else if (value is CelestialBody body) _internalSet(body, body.name, body.name);
                else Debug.LogWarningFormat("Tried to set antenna target to {0} and failed", value);
            }
        }

        public double PowerDraw => RATools.LogScale(PowerDrawLinear);
        public virtual double PowerDrawLinear => RATools.LinearScale(TxPower) / PowerEfficiency;
        public virtual double MinimumDistance => (CanTarget && Beamwidth < 90 ? minimumSpotRadius / Math.Tan(Beamwidth) : 0);

        protected static readonly string ModTag = "[RealAntenna] ";
        public override string ToString() => $"[+RA] {Name} [{Gain}dB] [{RFBand}] {(CanTarget ? $" ->{Target}" : null)}";

        public RealAntenna() : this("New RealAntennaDigital") { }
        public RealAntenna(string name, double dataRate = 1000)
        {
            Name = name;
            DataRate = dataRate;
        }

        public virtual bool Compatible(RealAntenna other) => RFBand == other.RFBand;

        public int CompareTo(object obj)
        {
            if (obj is RealAntenna ra) return DataRate.CompareTo(ra.DataRate);
            else throw new System.ArgumentException();
        }

        public virtual double BestDataRateToPeer(RealAntenna rx)
        {
            RealAntenna tx = this;
            Vector3 toSource = rx.Position - tx.Position;
            double distance = toSource.magnitude;
            if (!Compatible(rx)) return 0;
            if ((tx.Parent is ModuleRealAntenna) && !tx.Parent.CanComm()) return 0;
            if ((rx.Parent is ModuleRealAntenna) && !rx.Parent.CanComm()) return 0;
            if ((distance < tx.MinimumDistance) || (distance < rx.MinimumDistance)) return 0;

            double RSSI = Physics.ReceivedPower(tx, rx, distance, tx.Frequency);
            double temp = Physics.NoiseTemperature(rx, toSource);
            double NoiseSpectralDensity = Physics.NoiseSpectralDensity(temp);

            double Noise = NoiseFloor(toSource);
            double CI = RSSI - Noise;
            double margin = CI - RequiredCI;

            return (CI > Encoder.RequiredEbN0) ? DataRate * Encoder.CodingRate : 0;
        }

        public virtual void LoadFromConfigNode(ConfigNode config)
        {
            Gain = double.Parse(config.GetValue("Gain"));
            TxPower = double.Parse(config.GetValue("TxPower"));
            TechLevel = int.Parse(config.GetValue("TechLevel"));
            SymbolRate = double.Parse(config.GetValue("SymbolRate"));
            RFBand = Antenna.BandInfo.All[config.GetValue("RFBand")];
            if (config.HasValue("targetID"))
            {
                TargetID = config.GetValue("targetID");
                if (CanTarget && (_target == null))
                {
                    Target = _findTargetFromID(TargetID);
                }
            }
        }

        public virtual void ProcessUpgrades(float tsLevel, ConfigNode node)
        {
            foreach (ConfigNode upgradeNode in node.GetNodes("UPGRADE"))
            {
                int upgradeLevel = Int32.Parse(upgradeNode.GetValue("TechLevel"));
                if (upgradeLevel <= tsLevel)
                {
                    UpgradeFromConfigNode(upgradeNode);
                }
            }
        }

        public virtual void UpgradeFromConfigNode(ConfigNode config)
        {
            Debug.LogFormat("Applying upgrade for {0}", config);
            double d=0;
            string s = string.Empty;
            if (config.TryGetValue("Gain", ref d)) Gain = d;
            if (config.TryGetValue("TxPower", ref d)) TxPower = d;
            if (config.TryGetValue("SymbolRate", ref d)) SymbolRate = d;
            if (config.TryGetValue("RFBand", ref s)) RFBand = Antenna.BandInfo.All[s];
        }

        private ITargetable _findTargetFromID(string id)
        {
            if (FlightGlobals.fetch && CanTarget)
            {
                if (string.IsNullOrEmpty(id)) return FlightGlobals.GetHomeBody();
                if (string.Equals("None", id)) return FlightGlobals.GetHomeBody();
                if (FlightGlobals.GetBodyByName(id) is CelestialBody body) return body;
                try
                {
                    if (FlightGlobals.FindVessel(new Guid(id)) is Vessel v) return v;
                }
                catch (FormatException) { }
            }
            return null;
        }

        private void _internalSet(ITargetable tgt, string dispString, string tgtId)
        {
            _target = tgt; TargetID = tgtId;
            if (Parent is ModuleRealAntenna) { Parent.AntennaTargetString = dispString; Parent.TargetID = tgtId; }
        }
    }

}