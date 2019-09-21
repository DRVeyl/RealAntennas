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
        List<RealAntenna> antennaList = new List<RealAntenna>();
        private EventData<Vessel>.OnEvent OnVesselModifiedEvent = null;
        public override IScienceDataTransmitter GetBestTransmitter() =>
            (IsConnected && Comm is RACommNode node && node.AntennaTowardsHome() is RealAntenna toHome) ? toHome.Parent : null;

        public double UnloadedPowerDraw()
        {
            double ec = 0;
            foreach (RealAntenna ra in antennaList)
            {
                ec += ra.PowerDrawLinear * 1e-6 * 0.1;  // mW->kW conversion 1e-6, Standby power SWAG 10%
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
        }

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
            if (vessel.loaded) _determineControlLoaded(); else _determineControlUnloaded();
        }

        private int _countControllingCrew()
        {
            int numControl = 0;
            foreach (ProtoCrewMember crewMember in vessel.GetVesselCrew())
            {
                if (crewMember.HasEffect<FullVesselControlSkill>() && !crewMember.inactive)
                    ++numControl;
            }
            return numControl;
        }

        private void _determineControlUnloaded()
        {
            int numControl = _countControllingCrew();
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
        private void _determineControlLoaded()
        {
            int numControl = _countControllingCrew();
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
            Debug.LogFormat($"OnVesselModified fired for {this} data {data}");
            if (this == null || data != this.vessel) return;
            if (Comm is RACommNode node)
            {
                DiscoverAntennas();
            }
        }

        protected List<ModuleRealAntenna> DiscoverModuleAntennas() => (Vessel != null && Vessel.loaded) ? Vessel.FindPartModulesImplementing<ModuleRealAntenna>().ToList() : null;

        protected List<RealAntenna> DiscoverAntennas()
        {
            antennaList.Clear();
            if (Vessel == null) return antennaList;
            if (Vessel.loaded)
            {
                foreach (ModuleRealAntenna ant in Vessel.FindPartModulesImplementing<ModuleRealAntenna>().ToList())
                {
                    ant.RAAntenna.ParentNode = Comm;
                    antennaList.Add(ant.RAAntenna);
                }
                return antennaList;
            }
            if (Vessel.protoVessel != null)
            {
                foreach (ProtoPartSnapshot part in Vessel.protoVessel.protoPartSnapshots)
                {
                    if (part.FindModule(ModuleRealAntenna.ModuleName) is ProtoPartModuleSnapshot snap)
                    {
                        Part prefab = part.partInfo.partPrefab;
                        ModuleRealAntenna raModule = prefab.FindModuleImplementing<ModuleRealAntenna>();
                        if (raModule.CanCommUnloaded(snap))
                        {
                            RealAntenna ra = new RealAntennaDigital(raModule.name) { ParentNode = Comm };
                            ra.LoadFromConfigNode(snap.moduleValues);
                            antennaList.Add(ra);
                        }
                    }
                }
            }
            return antennaList;
        }
    }
}