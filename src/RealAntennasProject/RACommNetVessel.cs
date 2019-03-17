using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RealAntennas
{
    public class RACommNetVessel : CommNet.CommNetVessel
    {
        protected static readonly string ModTag = "[RealAntennasCommNetVessel] ";
        public List<RealAntenna> antennaList = new List<RealAntenna>();

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
                Debug.LogFormat(ModTag + "Replacing original commNode");
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
            RealAntenna bestAntenna = null;
            double bestAntennaScore = -100;
            foreach (RealAntenna ant in antennaList)
            {
                // TODO: Deal with antenna A = better receiver, antenna B = better transmitter.
                // That can get rolled into the RealAntennasCommNode by discovering both options here and carrying them forward.
                // To get things off the ground, just pick the strongest transmitter.
                // RA's RangeModel checks the link in both directions.  We could optimize "best tx" and "best rx"
                // and will the RangeModel implicitly handle asymmetric connections?
                // (Do we want to get into asymmetric data rates?)
                //
                // Revision Idea: make the entire antenna list accessible in the RACommNode.
                // When evaluating InRange, test against the most sensitive antenna (lowest [sensitivity-gain])
                // When connecting two CommNodes, choose pairing that produces highest data rate?
                // Basically defer all these decisions to TryConnect().  Major efficiency hit, tho?
                // Implement reselection hysteresis?  (If a connection exists, stick with it, only consider updating at a max rate)
                if (ant.CanComm())
                {
                    double antennaScore = ant.Gain + ant.CodingGain + ant.TxPower;
//                    Debug.LogFormat(ModTag + " Antenna {0} scores {1} versus best {2}", ant, antennaScore, bestAntennaScore);
                    if (antennaScore > bestAntennaScore)
                    {
                        bestAntenna = ant;
                        bestAntennaScore = antennaScore;
                    }
                }
            }
            if ((Comm is RACommNode vcn) && bestAntenna != null)
            {
                Debug.LogFormat(ModTag + " {0} UpdateComm() chose {1}", name, bestAntenna);
                vcn.RAAntenna = bestAntenna;
            }
            base.UpdateComm();  // Can probably skip this...
        }

        protected List<RealAntenna> DiscoverAntennas()
        {
            if (Vessel == null) return new List<RealAntenna>();
            if (Vessel.loaded) return Vessel.FindPartModulesImplementing<RealAntenna>().ToList();
            // Grr, now we have to go scan ProtoParts...
            Debug.LogFormat(ModTag + "Discovering antennas for unloaded CommNet vessel {0}, protoVessel {1}", name, Vessel.protoVessel);
            List<RealAntenna> antList = new List<RealAntenna>();

            if (Vessel.protoVessel != null)
            {
                foreach (ProtoPartSnapshot part in Vessel.protoVessel.protoPartSnapshots)
                {
                    Debug.LogFormat("Testing protoPart {0}", part.partName);
                    if (part.FindModule(RealAntenna.ModuleName) != null)
                    {
                        Part tempPart = PartLoader.getPartInfoByName(part.partName).partPrefab;
                        RealAntenna raModule = tempPart.FindModuleImplementing<RealAntenna>();
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
        }
    }
}