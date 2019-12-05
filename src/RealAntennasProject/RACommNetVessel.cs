using Experience.Effects;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RealAntennas
{
    public class RACommNetVessel : CommNet.CommNetVessel
    {
        protected static readonly string ModTag = "[RealAntennasCommNetVessel] ";
        readonly List<RealAntenna> antennaList = new List<RealAntenna>();
        readonly List<RealAntenna> inactiveAntennas = new List<RealAntenna>();
        private EventData<Vessel>.OnEvent OnVesselModifiedEvent = null;
        private PartResourceDefinition electricChargeDef;
        public override IScienceDataTransmitter GetBestTransmitter() =>
            (IsConnected && Comm is RACommNode node && node.AntennaTowardsHome() is RealAntenna toHome) ? toHome.Parent : null;

        [KSPField(isPersistant = true)]
        public bool powered = true;

        public override void OnNetworkPreUpdate()
        {
            base.OnNetworkPreUpdate();
            if (Vessel.loaded && electricChargeDef != null)
            {
                Vessel.GetConnectedResourceTotals(electricChargeDef.id, out double amt, out double _);
                powered = (amt > 0);
            }
        }

        public double IdlePowerDraw()
        {
            double ec = 0;
            foreach (RealAntenna ra in antennaList)
            {
                ec += ra.IdlePowerDraw;
            }
            foreach (RealAntenna ra in inactiveAntennas)
            {
                ec += ra.IdlePowerDraw;
            }
            return ec;
        }

        protected override void OnStart()
        {
            if (vessel.vesselType == VesselType.Flag || vessel.vesselType <= VesselType.Unknown)
            {
                vessel.vesselModules.Remove(this);
                vessel.connection = null;
                Destroy(this);
            }
            else
            {
                comm = new RACommNode(transform)
                {
                    OnNetworkPreUpdate = new Action(OnNetworkPreUpdate),
                    OnNetworkPostUpdate = new Action(OnNetworkPostUpdate),
                    OnLinkCreateSignalModifier = new Func<CommNet.CommNode, double>(GetSignalStrengthModifier),
                    ParentVessel = Vessel,
                };
                (comm as RACommNode).RAAntennaList = DiscoverAntennas();
                vessel.connection = this;
                networkInitialised = false;
                if (CommNet.CommNetNetwork.Initialized)
                    OnNetworkInitialized();
                GameEvents.CommNet.OnNetworkInitialized.Add(new EventVoid.OnEvent(OnNetworkInitialized));
                if (HighLogic.LoadedScene == GameScenes.TRACKSTATION)
                    GameEvents.onPlanetariumTargetChanged.Add(new EventData<MapObject>.OnEvent(OnMapFocusChange));
            }
            Debug.LogFormat(ModTag + "OnStart() for {0}. ID:{1}.  Comm:{2}", name, gameObject.GetInstanceID(), Comm);
            if (OnVesselModifiedEvent == null)
            {
                OnVesselModifiedEvent = new EventData<Vessel>.OnEvent(OnVesselModified);
                GameEvents.onVesselWasModified.Add(OnVesselModifiedEvent);
            }
            foreach (ModuleDeployablePart mdp in Vessel.FindPartModulesImplementing<ModuleDeployablePart>())
            {
                mdp.OnMoving.Add(new EventData<float, float>.OnEvent(OnMoving));
                mdp.OnStop.Add(new EventData<float>.OnEvent(OnStop));
            }
            this.overridePostUpdate = true;
            electricChargeDef = PartResourceLibrary.Instance.GetDefinition("ElectricCharge");
        }

        private void OnMoving(float f1, float f2) => DiscoverAntennas();
        private void OnStop(float f1) => DiscoverAntennas();

        protected override void OnDestroy()
        {
            if (OnVesselModifiedEvent != null) GameEvents.onVesselWasModified.Remove(OnVesselModifiedEvent);
            base.OnDestroy();
        }

        protected override void UpdateComm()
        {
            if (comm.name != gameObject.name)
                comm.name = gameObject.name;
            if (comm.displayName != vessel.GetDisplayName())
                comm.displayName = vessel.GetDisplayName();
            comm.isControlSource = false;
            comm.isControlSourceMultiHop = false;
            comm.antennaRelay.power = comm.antennaTransmit.power = 0.0;
            hasScienceAntenna = (comm as RACommNode).RAAntennaList.Count > 0;
            if (vessel.loaded) DetermineControlLoaded(); else DetermineControlUnloaded();
        }

        private int CountControllingCrew()
        {
            int numControl = 0;
            foreach (ProtoCrewMember crewMember in vessel.GetVesselCrew())
            {
                if (crewMember.HasEffect<FullVesselControlSkill>() && !crewMember.inactive)
                    ++numControl;
            }
            return numControl;
        }

        private void DetermineControlUnloaded()
        {
            int numControl = CountControllingCrew();
            int index = 0;
            foreach (ProtoPartSnapshot protoPartSnapshot in vessel.protoVessel.protoPartSnapshots)
            {
                index++;
                Part part = protoPartSnapshot.partInfo.partPrefab;
                foreach (PartModule module in part.Modules)
                {
                    if ((module is CommNet.ModuleProbeControlPoint probeControlPoint) && probeControlPoint.CanControlUnloaded(protoPartSnapshot.FindModule(module, index)))
                    {
                        if (numControl >= probeControlPoint.minimumCrew || probeControlPoint.minimumCrew <= 0)
                            comm.isControlSource = true;
                        if (probeControlPoint.multiHop)
                            comm.isControlSourceMultiHop = true;
                    }
                }
            }
        }
        private void DetermineControlLoaded()
        {
            int numControl = CountControllingCrew();
            foreach (Part part in vessel.Parts)
            {
                foreach (PartModule module in part.Modules)
                {
                    if ((module is CommNet.ModuleProbeControlPoint probeControlPoint) && probeControlPoint.CanControl())
                    {
                        if (numControl >= probeControlPoint.minimumCrew || probeControlPoint.minimumCrew <= 0)
                            comm.isControlSource = true;
                        if (probeControlPoint.multiHop)
                            comm.isControlSourceMultiHop = true;
                    }
                }
            }
        }

        protected void OnVesselModified(Vessel data)
        {
            if (this != null && data == Vessel && Comm is RACommNode) DiscoverAntennas();
        }

        protected List<RealAntenna> DiscoverAntennas()
        {
            antennaList.Clear();
            inactiveAntennas.Clear();
            if (Vessel == null) return antennaList;
            if (Vessel.loaded)
            {
                foreach (ModuleRealAntenna ant in Vessel.FindPartModulesImplementing<ModuleRealAntenna>().ToList())
                {
                    if (ant._enabled)
                    {
                        ant.RAAntenna.ParentNode = Comm;
                        if (DeployedLoaded(ant.part)) antennaList.Add(ant.RAAntenna);
                        else inactiveAntennas.Add(ant.RAAntenna);
                    }
                }
                return antennaList;
            }
            if (Vessel.protoVessel != null)
            {
                foreach (ProtoPartSnapshot part in Vessel.protoVessel.protoPartSnapshots)
                {
                    if (part.FindModule(ModuleRealAntenna.ModuleName) is ProtoPartModuleSnapshot snap)
                    {
                        bool _enabled = true;
                        snap.moduleValues.TryGetValue(nameof(ModuleRealAntenna._enabled), ref _enabled);
                        Part prefab = part.partInfo.partPrefab;
                        if (_enabled && prefab.FindModuleImplementing<ModuleRealAntenna>() is ModuleRealAntenna mra && mra.CanCommUnloaded(snap))
                        {
                            RealAntenna ra = new RealAntennaDigital(mra.name) { ParentNode = Comm };
                            ra.LoadFromConfigNode(snap.moduleValues);
                            if (DeployedUnloaded(part)) antennaList.Add(ra);
                            else inactiveAntennas.Add(ra);
                        }
                    }
                }
            }
            return antennaList;
        }
        public static bool DeployedUnloaded(ProtoPartSnapshot part)
        {
            if (part.FindModule("ModuleDeployableAntenna") is ProtoPartModuleSnapshot deploySnap)
            {
                string deployState = string.Empty;
                deploySnap.moduleValues.TryGetValue("deployState", ref deployState);
                return deployState.Equals("EXTENDED");
            }
            return true;
        }
        public static bool DeployedLoaded(Part part) =>
            (part.FindModuleImplementing<ModuleDeployableAntenna>() is ModuleDeployableAntenna mda) ?
            mda.deployState == ModuleDeployablePart.DeployState.EXTENDED : true;
    }
}