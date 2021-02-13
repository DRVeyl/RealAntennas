﻿using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace RealAntennas
{
    public enum AntennaShape
    {
        Auto, Omni, Dish
    }
    public class RealAntenna
    {
        public string Name { get; set; }
        public virtual double Gain { get; set; }         // Physical directionality, measured in dBi
        public double referenceGain = 0;
        public double referenceFrequency = 0;
        public double antennaDiameter = 0;
        public virtual double TxPower { get; set; }       // Transmit Power in dBm (milliwatts)
        public TechLevelInfo TechLevelInfo;
        public Antenna.BandInfo RFBand;
        public virtual double SymbolRate { get; set; }
        public virtual double MinSymbolRate => SymbolRate / 1000;
        public virtual double Frequency => RFBand.Frequency;
        public virtual double PowerEfficiency => TechLevelInfo.PowerEfficiency;
        public virtual double AntennaEfficiency => TechLevelInfo.ReflectorEfficiency;
        public virtual double DataRate { get; }
        public virtual double Bandwidth => DataRate;          // RF bandwidth required.
        public virtual double AMWTemp { get; set; }
        public virtual double Beamwidth => Physics.Beamwidth(Gain);

        internal double cachedRemoteBodyNoiseTemp;
        public Antenna.Encoder Encoder => Antenna.Encoder.GetFromTechLevel(TechLevelInfo.Level); 
        public virtual double RequiredCI => Encoder.RequiredEbN0;

        public ModuleRealAntenna Parent { get; internal set; }
        public ProtoPartModuleSnapshot ParentSnapshot { get; internal set; } = null;
        public CommNet.CommNode ParentNode { get; set; }
        public Vector3d Position => PrecisePosition;
        public Vector3d PrecisePosition => ParentNode.precisePosition;
        public Vector3d TransformPosition => ParentNode.position;
        public virtual AntennaShape Shape => Gain <= Physics.MaxOmniGain ? AntennaShape.Omni : AntennaShape.Dish;
        public virtual bool CanTarget => Shape != AntennaShape.Omni && (ParentNode == null || !ParentNode.isHome);
        public Vector3 ToTarget {
            get {
                if (!(CanTarget && Target != null)) return Vector3.zero;
                return (Target is Vessel v) ? v.transform.position - Position : (Vector3)(Target as CelestialBody).position - Position;
            }
        }

        public Vector3 ToTargetByTransform
        {
            get
            {
                if (!(CanTarget && Target != null)) return Vector3.zero;
                return (Target is Vessel v) ? v.transform.position - TransformPosition : (Vector3)(Target as CelestialBody).position - TransformPosition;
            }
        }

        public string TargetID { get; set; }
        private object _target = null;
        public object Target
        {
            get => _target;
            set
            {
                if (!CanTarget || value is null) SetTarget(null, DefaultTargetName, DefaultTargetName);
                else if (value is Vessel v) SetTarget(v, v.name, v.id.ToString());
                else if (value is CelestialBody body) SetTarget(body, body.name, body.name);
                else if (value is Network.RACommNetHome home) SetTarget(home, home.name, home.name);
                else Debug.LogWarningFormat($"{ModTag} Tried to set antenna target to {value} and failed");
            }
        }

        public string TargetString => (Target is Vessel v) ? v.vesselName :
                                      (Target is CelestialBody b) ? b.name :
                                      (Target is Network.RACommNetHome h) ? h.name :
                                      string.Empty;

        public double PowerDraw => RATools.LogScale(PowerDrawLinear);
//        public virtual double IdlePowerDraw => PowerDrawLinear * 1e-6 * ModuleRealAntenna.InactivePowerConsumptionMult;
        public virtual double IdlePowerDraw => TechLevelInfo.BasePower / 1000;    // Base power in W, 1ec/s = 1kW
        public virtual double PowerDrawLinear => RATools.LinearScale(TxPower) / PowerEfficiency;
        public virtual double MinimumDistance => (CanTarget && Beamwidth < 90 ? minimumSpotRadius / Math.Tan(Beamwidth) : 0);

        protected static readonly string ModTag = "[RealAntenna] ";
        public static readonly string DefaultTargetName = "None";
        private readonly double minimumSpotRadius = 1e3;

        public override string ToString() => $"[+RA] {Name} [{Gain:F1} dBi {RFBand.name} {TxPower} dBm [TL:{TechLevelInfo.Level:N0}]] {(CanTarget ? $" ->{Target}" : null)}";
        public virtual string ToStringShort() => $"{Name} [{RFBand.name} {TxPower} dBm] {(CanTarget ? $" ->{TargetString}" : null)}";

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

        public virtual double BestDataRateToPeer(RealAntenna rx)
        {
            RealAntenna tx = this;
            Vector3 toSource = rx.Position - tx.Position;
            double distance = toSource.magnitude;
            if (!Compatible(rx)) return 0;
            if ((tx.Parent is ModuleRealAntenna) && !tx.Parent.CanComm()) return 0;
            if ((rx.Parent is ModuleRealAntenna) && !rx.Parent.CanComm()) return 0;
            if ((distance < tx.MinimumDistance) || (distance < rx.MinimumDistance)) return 0;
            if (!(tx.DirectionCheck(rx) && rx.DirectionCheck(tx))) return 0;

            double RSSI = Physics.ReceivedPower(tx, rx, distance, tx.Frequency);
            double temp = Physics.NoiseTemperature(rx, tx.Position);
            double Noise = Physics.NoiseFloor(Bandwidth, temp);
            double CI = RSSI - Noise;
            double margin = CI - RequiredCI;

            return (CI > Encoder.RequiredEbN0) ? DataRate * Encoder.CodingRate : 0;
        }

        public virtual void LoadFromConfigNode(ConfigNode config)
        {
            int tl = (config.HasValue("TechLevel")) ? int.Parse(config.GetValue("TechLevel")) : 0;
            TechLevelInfo = TechLevelInfo.GetTechLevel(tl);
            string sRFBand = (config.HasValue("RFBand")) ? config.GetValue("RFBand") : Antenna.BandInfo.All.Keys.DefaultIfEmpty("S").First();
            RFBand = Antenna.BandInfo.Get(sRFBand);
            referenceGain = (config.HasValue("referenceGain")) ? double.Parse(config.GetValue("referenceGain")) : 0;
            referenceFrequency = (config.HasValue("referenceFrequency")) ? double.Parse(config.GetValue("referenceFrequency")) : 0;
            antennaDiameter = (config.HasValue("antennaDiameter")) ? double.Parse(config.GetValue("antennaDiameter")) : 0;
            Gain = (antennaDiameter > 0) ? Physics.GainFromDishDiamater(antennaDiameter, RFBand.Frequency, AntennaEfficiency) : Physics.GainFromReference(referenceGain, referenceFrequency * 1e6, RFBand.Frequency);
            TxPower = (config.HasValue("TxPower")) ? double.Parse(config.GetValue("TxPower")) : 30f;
            SymbolRate = RFBand.MaxSymbolRate(TechLevelInfo.Level);
            AMWTemp = (config.HasValue("AMWTemp")) ? double.Parse(config.GetValue("AMWTemp")) : 290f;
            if (config.HasValue("targetID"))
            {
                TargetID = config.GetValue("targetID");
                if (CanTarget && (_target == null))
                {
                    Target = FindTargetFromID(TargetID);
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
            double d=0;
            string s = string.Empty;
            if (config.TryGetValue("referenceGain", ref d)) referenceGain = d;
            if (config.TryGetValue("referenceFrequency", ref d)) referenceFrequency = d;
            if (config.TryGetValue("antennaDiameter", ref d)) antennaDiameter = d;
            Gain = (antennaDiameter > 0) ? Physics.GainFromDishDiamater(antennaDiameter, RFBand.Frequency, AntennaEfficiency) : Physics.GainFromReference(referenceGain, referenceFrequency * 1e6, RFBand.Frequency);
            //            if (config.TryGetValue("Gain", ref d)) Gain = d;
            if (config.TryGetValue("TxPower", ref d)) TxPower = d;
            if (config.TryGetValue("SymbolRate", ref d)) SymbolRate = d;
            if (config.TryGetValue("AMWTemp", ref d)) AMWTemp = d;
            if (config.TryGetValue("RFBand", ref s)) RFBand = Antenna.BandInfo.All[s];
        }

        private ITargetable FindTargetFromID(string id)
        {
            if (FlightGlobals.fetch && CanTarget)
            {
                if (string.IsNullOrEmpty(id)) return FlightGlobals.GetHomeBody();
                if (string.Equals(DefaultTargetName, id)) return FlightGlobals.GetHomeBody();
                if (FlightGlobals.GetBodyByName(id) is CelestialBody body) return body;
                try
                {
                    if (FlightGlobals.FindVessel(new Guid(id)) is Vessel v) return v;
                }
                catch (FormatException) { }
            }
            return null;
        }

        private void SetTarget(object tgt, string dispString, string tgtId)
        {
            _target = tgt; TargetID = tgtId;
            if (Parent is ModuleRealAntenna) { Parent.sAntennaTarget = dispString; Parent.targetID = tgtId; }
            ParentSnapshot?.moduleValues.SetValue("targetID", tgtId);
        }
    }

}