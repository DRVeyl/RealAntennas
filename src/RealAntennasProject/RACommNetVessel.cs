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
        public List<ModuleRealAntenna> antennaList = new List<ModuleRealAntenna>();

        public override IScienceDataTransmitter GetBestTransmitter() =>
            (IsConnected && Comm is RACommNode node && node.AntennaTowardsHome() is RealAntenna toHome) ? toHome.Parent : null;

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
                antennaList = DiscoverAntennas();
                comm = new RACommNode(transform)
                {
                    OnNetworkPreUpdate = new Action(OnNetworkPreUpdate),
                    OnNetworkPostUpdate = new Action(OnNetworkPostUpdate),
                    OnLinkCreateSignalModifier = new Func<CommNet.CommNode, double>(GetSignalStrengthModifier),
                    ParentVessel = Vessel,
                    RAAntennaList = new List<RealAntenna>(GatherRealAntennas(antennaList))
                };
                vessel.connection = this;
                networkInitialised = false;
                if (CommNet.CommNetNetwork.Initialized)
                    OnNetworkInitialized();
                GameEvents.CommNet.OnNetworkInitialized.Add(new EventVoid.OnEvent(OnNetworkInitialized));
                if (HighLogic.LoadedScene == GameScenes.TRACKSTATION)
                    GameEvents.onPlanetariumTargetChanged.Add(new EventData<MapObject>.OnEvent(OnMapFocusChange));
            }
            Debug.LogFormat(ModTag + "OnStart() for {0}. ID:{1}.  Comm:{2}", name, gameObject.GetInstanceID(), Comm);
            GameEvents.onVesselWasModified.Add(new EventData<Vessel>.OnEvent(OnVesselModified));
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
            hasScienceAntenna = (antennaList.Count > 0);
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

        internal List<RealAntenna> GatherRealAntennas(List<ModuleRealAntenna> src)
        {
            List<RealAntenna> l = new List<RealAntenna>();
            foreach (ModuleRealAntenna ant in src)
            {
                l.Add(ant.RAAntenna);
            }
            return l;
        }

        protected List<ModuleRealAntenna> DiscoverAntennas()
        {
            if (Vessel == null) return new List<ModuleRealAntenna>();
            if (Vessel.loaded) return Vessel.FindPartModulesImplementing<ModuleRealAntenna>().ToList();
            // Grr, now we have to go scan ProtoParts...
            List<ModuleRealAntenna> antList = new List<ModuleRealAntenna>();

            if (Vessel.protoVessel != null)
            {
                foreach (ProtoPartSnapshot part in Vessel.protoVessel.protoPartSnapshots)
                {
                    if (part.FindModule(ModuleRealAntenna.ModuleName) is ProtoPartModuleSnapshot snap)
                    {
                        Part prefab = part.partInfo.partPrefab;
                        ModuleRealAntenna raModule = prefab.FindModuleImplementing<ModuleRealAntenna>();
                        raModule.Configure(snap.moduleValues);
                        antList.Add(raModule);
                    }
                }
            }
            return antList;
        }

        protected void OnVesselModified(Vessel data)
        {
            // Regenerate the antenna data.
            Debug.LogFormat("OnVesselModified() for {0}, vessel {1}.  Discovering antennas!", this, data);
            antennaList = DiscoverAntennas();
            if (Comm is RACommNode vcn)
            {
                vcn.RAAntennaList.Clear();
                vcn.RAAntennaList.AddRange(GatherRealAntennas(antennaList));
            }
        }
    }
}