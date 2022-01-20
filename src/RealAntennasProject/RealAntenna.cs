using System;
using System.Linq;
using UnityEngine;

namespace RealAntennas
{
    public enum AntennaShape
    {
        Auto, Omni, Dish
    }
    public class RealAntenna
    {
        public string Name { get; set; }
        public virtual float Gain { get; set; }         // Physical directionality, measured in dBi
        public float referenceGain = 0;
        public float referenceFrequency = 0;
        public float antennaDiameter = 0;
        public virtual float TxPower { get; set; }       // Transmit Power in dBm (milliwatts)
        public TechLevelInfo TechLevelInfo;
        public Antenna.BandInfo RFBand;
        public virtual double SymbolRate { get; set; }
        public virtual double MinSymbolRate => SymbolRate / 1000;
        public virtual float Frequency => RFBand.Frequency;
        public virtual float PowerEfficiency => TechLevelInfo.PowerEfficiency;
        public virtual float AntennaEfficiency => TechLevelInfo.ReflectorEfficiency;
        public virtual double DataRate { get; }
        public virtual double Bandwidth => DataRate;          // RF bandwidth required.
        public virtual float AMWTemp { get; set; }
        public virtual float Beamwidth => Physics.Beamwidth(Gain);

        public Antenna.Encoder Encoder => Antenna.Encoder.GetFromTechLevel(TechLevelInfo.Level); 
        public virtual float RequiredCI => Encoder.RequiredEbN0;

        public ModuleRealAntenna Parent { get; internal set; }
        public ProtoPartModuleSnapshot ParentSnapshot { get; internal set; } = null;
        public CommNet.CommNode ParentNode { get; set; }
        public Vector3d Position => PrecisePosition;
        public Vector3d PrecisePosition => ParentNode.precisePosition;
        public Vector3d TransformPosition => ParentNode.position;
        public virtual AntennaShape Shape => Gain <= Physics.MaxOmniGain ? AntennaShape.Omni : AntennaShape.Dish;
        public virtual bool CanTarget => Shape != AntennaShape.Omni && (ParentNode == null || !ParentNode.isHome);
        public Vector3 ToTarget => (CanTarget && Target != null) ? (Vector3) (Target.transform.position - Position) : Vector3.zero;

        private Targeting.AntennaTarget _target;
        public Targeting.AntennaTarget Target
        {
            get => _target;
            set
            {
                _target = value;
                if (Parent is ModuleRealAntenna) { Parent.sAntennaTarget = $"{_target}"; }
                if (ParentSnapshot is ProtoPartModuleSnapshot snap)
                {
                    snap.moduleValues.RemoveNode(Targeting.AntennaTarget.nodeName);
                    _target.Save(snap.moduleValues);
                }
            }
        }

        public float PowerDraw => RATools.LogScale(PowerDrawLinear);
        public virtual float IdlePowerDraw => TechLevelInfo.BasePower / 1000;    // Base power in W, 1ec/s = 1kW
        public virtual float PowerDrawLinear => RATools.LinearScale(TxPower) / PowerEfficiency;
        public virtual float MinimumDistance => (CanTarget && Beamwidth < 90 ? minimumSpotRadius / Mathf.Tan(Beamwidth) : 0);

        protected static readonly string ModTag = "[RealAntenna] ";
        public static readonly string DefaultTargetName = "None";
        private readonly float minimumSpotRadius = 1e3f;

        public override string ToString() => $"{Name} [{RFBand.name} {Gain:F1} dBi {TxPower} dBm [TL:{TechLevelInfo.Level:N0}]] {(CanTarget ? $" ->{Target}" : null)}";
        public virtual string ToStringShort() => $"{Name} [{RFBand.name} {TxPower} dBm] {(CanTarget ? $" ->{Target}" : null)}";

        public RealAntenna() : this("New RealAntennaDigital") { }
        public RealAntenna(string name, double dataRate = 1000)
        {
            Name = name;
            DataRate = dataRate;
            TechLevelInfo = TechLevelInfo.GetTechLevel(0);
            RFBand ??= Antenna.BandInfo.Get(Antenna.BandInfo.All.Keys.FirstOrDefault() ?? Antenna.BandInfo.DefaultBand);
        }
        public RealAntenna(RealAntenna orig)
        {
            Name = orig.Name;
            Gain = orig.Gain;
            referenceGain = orig.referenceGain;
            referenceFrequency = orig.referenceFrequency;
            antennaDiameter = orig.antennaDiameter;
            TxPower = orig.TxPower;
            TechLevelInfo = orig.TechLevelInfo;
            RFBand = orig.RFBand;
            SymbolRate = orig.SymbolRate;
            AMWTemp = orig.AMWTemp;
            Target = orig.Target;
            Parent = orig.Parent;
            ParentNode = orig.ParentNode;
            ParentSnapshot = orig.ParentSnapshot;
        }

        public virtual bool Compatible(RealAntenna other) => RFBand == other.RFBand;
        public virtual bool DirectionCheck(RealAntenna other) => DirectionCheck(other.Position);
        public virtual bool DirectionCheck(Vector3 pos) => Physics.PointingLoss(this, pos) < Physics.MaxPointingLoss;

        public virtual void LoadFromConfigNode(ConfigNode config)
        {
            int tl = (config.HasValue("TechLevel")) ? int.Parse(config.GetValue("TechLevel")) : 0;
            TechLevelInfo = TechLevelInfo.GetTechLevel(tl);
            string sRFBand = (config.HasValue("RFBand")) ? config.GetValue("RFBand") : Antenna.BandInfo.All.Keys.DefaultIfEmpty("S").First();
            RFBand = Antenna.BandInfo.Get(sRFBand);
            referenceGain = (config.HasValue("referenceGain")) ? float.Parse(config.GetValue("referenceGain")) : 0;
            referenceFrequency = (config.HasValue("referenceFrequency")) ? float.Parse(config.GetValue("referenceFrequency")) : 0;
            antennaDiameter = (config.HasValue("antennaDiameter")) ? float.Parse(config.GetValue("antennaDiameter")) : 0;
            Gain = (antennaDiameter > 0) ? Physics.GainFromDishDiamater(antennaDiameter, RFBand.Frequency, AntennaEfficiency) : Physics.GainFromReference(referenceGain, referenceFrequency * 1e6f, RFBand.Frequency);
            TxPower = (config.HasValue("TxPower")) ? float.Parse(config.GetValue("TxPower")) : 30f;
            SymbolRate = RFBand.MaxSymbolRate(TechLevelInfo.Level);
            AMWTemp = (config.HasValue("AMWTemp")) ? float.Parse(config.GetValue("AMWTemp")) : 290f;
            if (config.HasNode("TARGET"))
                Target = Targeting.AntennaTarget.LoadFromConfig(config.GetNode("TARGET"), this);
            if (CanTarget && !(Target?.Validate() == true) && HighLogic.LoadedSceneHasPlanetarium)
                Target = Targeting.AntennaTarget.LoadFromConfig(SetDefaultTarget(), this);
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
            double d=0;
            float f = 0;
            string s = string.Empty;
            if (config.TryGetValue("referenceGain", ref f)) referenceGain = f;
            if (config.TryGetValue("referenceFrequency", ref f)) referenceFrequency = f;
            if (config.TryGetValue("antennaDiameter", ref f)) antennaDiameter = f;
            Gain = (antennaDiameter > 0) ? Physics.GainFromDishDiamater(antennaDiameter, RFBand.Frequency, AntennaEfficiency) : Physics.GainFromReference(referenceGain, referenceFrequency * 1e6f, RFBand.Frequency);
            //            if (config.TryGetValue("Gain", ref d)) Gain = d;
            if (config.TryGetValue("TxPower", ref f)) TxPower = f;
            if (config.TryGetValue("SymbolRate", ref d)) SymbolRate = d;
            if (config.TryGetValue("AMWTemp", ref f)) AMWTemp = f;
            if (config.TryGetValue("RFBand", ref s)) RFBand = Antenna.BandInfo.All[s];
        }

        public virtual ConfigNode SetDefaultTarget()
        {
            var x = new ConfigNode(Targeting.AntennaTarget.nodeName);
            x.AddValue("name", $"{Targeting.AntennaTarget.TargetMode.BodyLatLonAlt}");
            x.AddValue("bodyName", Planetarium.fetch.Home.name);
            x.AddValue("latLonAlt", new Vector3(0, 0, (float)-Planetarium.fetch.Home.Radius));
            return x;
        }
    }
}