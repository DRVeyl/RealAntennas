using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RealAntennas
{
    public class RACommNetVessel : CommNet.CommNetVessel
    {
        protected static readonly string ModTag = "[RealAntennasCommNetVessel] ";
        public List<ModuleRealAntenna> antennaList = new List<ModuleRealAntenna>();

        public override IScienceDataTransmitter GetBestTransmitter()
        {
            Debug.LogFormat(ModTag + " GetBestTransmitter() start for {0}", this.name);
            IScienceDataTransmitter e = base.GetBestTransmitter();
            Debug.LogFormat(ModTag + " GetBestTransmitter() stop, res: {0}", e);
            return e;
        }

        protected override bool CreateControlConnection()
        {
            bool e = base.CreateControlConnection();
            //            Debug.LogFormat(ModTag + " CreateControlConnection() for {0} using {1} was {2}", name, Comm, e);
            return e;       // Returns True if it changed the control connection state.
        }

        protected override void OnNetworkInitialized()
        {
            Debug.LogFormat(ModTag + "OnNetworkInitialized() for {0}.  Building antenna list.  ID:{1}.  Comm:{2}", name, gameObject.GetInstanceID(), Comm);
            antennaList = DiscoverAntennas();
            // It knows nothing about any ModuleDataTransmitters onboard.  It only seems to know its position and transform.
            // It registers OnLinkCreateSignalModifier to GetSignalStrengthModifier.
            // It registers OnNetworkPostUpdate and OnNetworkPreUpdate back to this CommNetVessel.
            if (!(Comm is RACommNode vcn))
            {
                vcn = new RACommNode(Comm);
                vcn.OnLinkCreateSignalModifier += GetSignalStrengthModifier;
                vcn.OnNetworkPreUpdate += OnNetworkPreUpdate;
                vcn.OnNetworkPostUpdate += OnNetworkPostUpdate;
                vcn.ParentVessel = Vessel;
                vcn.RAAntennaList = new List<RealAntenna>(GatherRealAntennas(antennaList));
                vcn.RAAntennaList.Sort();
                vcn.RAAntennaList.Reverse();
                Debug.LogFormat(ModTag + "Replacing original commNode, now have {0} with antList {1}", vcn, vcn.RAAntennaList);
                foreach (RealAntenna ra in vcn.RAAntennaList)
                {
                    Debug.LogFormat("RealAntenna {0}", ra);
                }
                Comm = vcn;
            }
            base.OnNetworkInitialized();
        }

        protected override void OnStart()
        {
            Debug.LogFormat(ModTag + "OnStart() for {0}. ID:{1}.  Comm:{2}", name, gameObject.GetInstanceID(), Comm);
            base.OnStart();
            GameEvents.onVesselWasModified.Add(new EventData<Vessel>.OnEvent(OnVesselModified));
        }

        protected override void OnDestroy()
        {
            Debug.LogFormat(ModTag + "OnDestroy() for {0}. ID:{1}.  Comm:{2}", name, gameObject.GetInstanceID(), Comm);
            base.OnDestroy();
        }

        protected override void UpdateComm()
        {
            base.UpdateComm();  // Need some features from base to set some features of the node I don't know about yet.
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
            Debug.LogFormat(ModTag + "Discovering antennas for unloaded CommNet vessel {0}, protoVessel {1}", name, Vessel.protoVessel);
            List<ModuleRealAntenna> antList = new List<ModuleRealAntenna>();

            if (Vessel.protoVessel != null)
            {
                foreach (ProtoPartSnapshot part in Vessel.protoVessel.protoPartSnapshots)
                {
                    Debug.LogFormat("Testing protoPart {0}", part.partName);
                    if (part.FindModule(ModuleRealAntenna.ModuleName) != null)
                    {
                        Part tempPart = PartLoader.getPartInfoByName(part.partName).partPrefab;
                        ModuleRealAntenna raModule = tempPart.FindModuleImplementing<ModuleRealAntenna>();
                        Debug.LogFormat("Gathered the RA module: {0}", raModule);
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
                vcn.RAAntennaList.Sort();
                vcn.RAAntennaList.Reverse();
            }
        }
    }
}