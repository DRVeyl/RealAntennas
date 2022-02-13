using Expansions.Serenity.DeployedScience.Runtime;
using Experience.Effects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RealAntennas
{
    public class RACommNetVessel : CommNet.CommNetVessel
    {
        protected const string ModTag = "[RealAntennasCommNetVessel]";
        readonly List<RealAntenna> antennaList = new List<RealAntenna>();
        readonly List<RealAntenna> inactiveAntennas = new List<RealAntenna>();
        private PartResourceDefinition electricChargeDef;
        public override IScienceDataTransmitter GetBestTransmitter() =>
            (IsConnected && Comm is RACommNode node && node.AntennaTowardsHome() is RealAntenna toHome) ? toHome.Parent : null;

        [KSPField(isPersistant = true)] public bool powered = true;

        public override void OnNetworkPreUpdate()
        {
            base.OnNetworkPreUpdate();
            var cluster = GetDeployedScienceCluster(Vessel);
            if (cluster != null)
                powered = cluster.IsPowered;
            else if (Vessel.loaded && electricChargeDef != null)
            {
                Vessel.GetConnectedResourceTotals(electricChargeDef.id, out double amt, out double _);
                powered = amt > 0;
            }
        }

        public double IdlePowerDraw()
        {
            double ec = 0;
            if (!IsDeployedScienceCluster(Vessel))
            {
                foreach (RealAntenna ra in antennaList)
                {
                    ec += ra.IdlePowerDraw;
                }
                foreach (RealAntenna ra in inactiveAntennas)
                {
                    ec += ra.IdlePowerDraw;
                }
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
                GameEvents.CommNet.OnNetworkInitialized.Add(OnNetworkInitialized);
                if (HighLogic.LoadedScene == GameScenes.TRACKSTATION)
                    GameEvents.onPlanetariumTargetChanged.Add(OnMapFocusChange);
                GameEvents.onVesselWasModified.Add(OnVesselModified);
                foreach (ModuleDeployablePart mdp in Vessel.FindPartModulesImplementing<ModuleDeployablePart>())
                {
                    mdp.OnMoving.Add(OnMoving);
                    mdp.OnStop.Add(OnStop);
                }
                overridePostUpdate = true;
                electricChargeDef = PartResourceLibrary.Instance.GetDefinition("ElectricCharge");
            }
        }

        private void OnMoving(float f1, float f2) => DiscoverAntennas();
        private void OnStop(float f1) => DiscoverAntennas();

        protected override void OnDestroy()
        {
            GameEvents.onVesselWasModified.Remove(OnVesselModified);
            GameEvents.CommNet.OnNetworkInitialized.Remove(OnNetworkInitialized);
            GameEvents.onPlanetariumTargetChanged.Remove(OnMapFocusChange);
            base.OnDestroy();
            comm?.Net.Remove(comm);
            comm = null;
            if (vessel) vessel.connection = null;
        }

        protected override void UpdateComm()
        {
            if (comm is RACommNode)
            {
                comm.name = gameObject.name;
                comm.displayName = vessel.GetDisplayName();
                comm.isControlSource = false;
                comm.isControlSourceMultiHop = false;
                comm.antennaRelay.power = comm.antennaTransmit.power = 0.0;
                hasScienceAntenna = (comm as RACommNode).RAAntennaList.Count > 0;
                if (vessel.loaded) DetermineControlLoaded(); else DetermineControlUnloaded();
            }
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
            (RACommNetScenario.Instance as RACommNetScenario)?.Network?.InvalidateCache();
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
                        ValidateAntennaTarget(ant.RAAntenna);
                    }
                }
                return antennaList;
            }
            if (Vessel.protoVessel != null)
            {
                foreach (ProtoPartSnapshot part in Vessel.protoVessel.protoPartSnapshots)
                {
                    foreach (ProtoPartModuleSnapshot snap in part.modules.Where(x => x.moduleName == ModuleRealAntenna.ModuleName))
                    {
                        bool _enabled = true;
                        snap.moduleValues.TryGetValue(nameof(ModuleRealAntenna._enabled), ref _enabled);
                        // Doesn't get the correct PartModule if multiple, but the only impact is the name, which defaults to the part anyway.
                        if (_enabled && part.partInfo.partPrefab.FindModuleImplementing<ModuleRealAntenna>() is ModuleRealAntenna mra && mra.CanCommUnloaded(snap))
                        {
                            RealAntenna ra = new RealAntennaDigital(part.partPrefab.partInfo.title) { ParentNode = Comm, ParentSnapshot = snap };
                            ra.LoadFromConfigNode(snap.moduleValues);
                            if (DeployedUnloaded(part)) antennaList.Add(ra);
                            else inactiveAntennas.Add(ra);
                            ValidateAntennaTarget(ra);
                        }
                    }
                }
            }
            return antennaList;
        }
        private void ValidateAntennaTarget(RealAntenna ra)
        {
            if (ra.CanTarget && !(ra.Target?.Validate() == true))
                ra.Target = Targeting.AntennaTarget.LoadFromConfig(ra.SetDefaultTarget(), ra);
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

        private bool IsDeployedScienceCluster(Vessel v) => GetDeployedScienceCluster(v) != null;
        private DeployedScienceCluster GetDeployedScienceCluster(Vessel vessel)
        {
            DeployedScienceCluster cluster = null;
            if (vessel.vesselType == VesselType.DeployedScienceController)
            {
                var id = vessel.loaded ? vessel.rootPart.persistentId : vessel.protoVessel.protoPartSnapshots[0].persistentId;
                DeployedScience.Instance?.DeployedScienceClusters?.TryGetValue(id, out cluster);
            }
            return cluster;
        }
    }
}
