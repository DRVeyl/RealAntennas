using CommNet;
using UnityEngine;

namespace RealAntennas
{
    /// <summary>
    /// This class is the key that allows to break into and customise KSP's CommNet.  Using TaxiService' CommNetConstellation model.
    /// </summary>
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.EDITOR, GameScenes.SPACECENTER })]
    public class RACommNetScenario : CommNetScenario
    {
        protected static readonly string ModTag = "[RealAntennasCommNetScenario] ";
        public static new RealAntennasRangeModel RangeModel = new RealAntennasRangeModel();

//        private void CustomCommNetUI = null;
        private RACommNetNetwork CustomCommNetNetwork = null;
//        private void CustomCommNetTelemetry = null;

        public static new RACommNetScenario Instance
        {
            get;
            protected set;
        }

        protected override void Start()
        {
            Debug.LogFormat(ModTag + "CommNetScenario Start() IN {0}/{1}", HighLogic.LoadedScene, HighLogic.LoadedScene.displayDescription());

            Instance = this;

            if (HighLogic.LoadedScene == GameScenes.SPACECENTER) { }
            Debug.LogFormat(ModTag + "Cleaning and rebuilding CommNetHome and CommNetBody");
            foreach (CommNetHome home in FindObjectsOfType<CommNetHome>())
            {
                //                Debug.LogFormat(ModTag + "Destroying {0}", home);
                Destroy(home);
            }
            foreach (CommNetBody body in FindObjectsOfType<CommNetBody>())
            {
                //                Debug.LogFormat(ModTag + "Destroying {0}", body);
                Destroy(body);
            }

            base.Start();

            CommNetScenario.RangeModel = RangeModel;

            //Replace the CommNet network
            CommNetNetwork net = FindObjectOfType<CommNetNetwork>();
            CustomCommNetNetwork = gameObject.AddComponent<RACommNetNetwork>();
            Destroy(net);

            ConfigNode RACommNetParams = null;
            foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("RealAntennasCommNetParams"))
                RACommNetParams = n;

//            Debug.LogFormat(ModTag + "Building fresh CommNetBodies and CommNetHomes");
            foreach (CelestialBody body in FindObjectsOfType<CelestialBody>())
            {
                GameObject newObject = new GameObject(body.name);
                RACommNetBody customBody = newObject.AddComponent<RACommNetBody>();
                customBody.name = body.name;
                Debug.LogFormat(ModTag + "Created {0}", customBody);
                if (RACommNetParams != null)
                {
                    if (RACommNetParams.GetNode("CELESTIALBODY", "name", body.name) is ConfigNode bodyNode)
                    {
                        foreach (ConfigNode gsNode in bodyNode.GetNodes("GROUNDSTATION"))
                        {
                            GameObject newHome = new GameObject(body.name);
                            RACommNetHome home = newHome.AddComponent<RACommNetHome>();
                            home.Configure(gsNode, body);
                            Debug.LogFormat(ModTag + "Built: {0}", home);
                        }
                    }
                }
            }

            Debug.Log(ModTag + "RealAntennas CommNet Scenario loading done!");
        }

        public override void OnAwake()
        {
            //override to turn off CommNetScenario's instance check

            GameEvents.onVesselCreate.Add(new EventData<Vessel>.OnEvent(OnVesselCountChanged));
            GameEvents.onVesselDestroy.Add(new EventData<Vessel>.OnEvent(OnVesselCountChanged));
        }

        private void OnDestroy()
        {
            if (CustomCommNetNetwork != null)
                Destroy(CustomCommNetNetwork);

            GameEvents.onVesselCreate.Remove(new EventData<Vessel>.OnEvent(OnVesselCountChanged));
            GameEvents.onVesselDestroy.Remove(new EventData<Vessel>.OnEvent(OnVesselCountChanged));
        }

        /// <summary>
        /// GameEvent call for newly-created vessels (launch, staging, new asteriod etc)
        /// NOTE: Vessel v is fresh bread straight from the oven before any curation is done on this (i.e. debris.Connection is valid)
        /// </summary>
        private void OnVesselCountChanged(Vessel v)
        {
            if (v.vesselType == VesselType.Base || v.vesselType == VesselType.Lander || v.vesselType == VesselType.Plane ||
               v.vesselType == VesselType.Probe || v.vesselType == VesselType.Relay || v.vesselType == VesselType.Rover ||
               v.vesselType == VesselType.Ship || v.vesselType == VesselType.Station)
            {
                Debug.Log(ModTag + "Change in the vessel list detected.  Do we need to rebuild?");
            }
        }
    }
}