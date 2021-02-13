using UnityEngine;
using CommNet;

namespace RealAntennas.Network
{
    /// <summary>
    /// Extend the functionality of the KSP's CommNetNetwork (co-primary model in the Model–view–controller sense; CommNet<> is the other co-primary one)
    /// </summary>
    public class RACommNetNetwork : CommNetNetwork
    {
        private const string ModTag = "[RACommNetNetwork]";
        private bool requestInit = false;

        protected override void Awake()
        {
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (v.Connection != null && !(v.Connection is RACommNetVessel ra))
                {
                    Debug.Log($"{ModTag} Rebuilding CommVessel on {v}.  (Was {v.Connection} of type {v.Connection.GetType()})");
                    CommNetVessel temp = v.Connection;
                    v.Connection = v.Connection.gameObject.AddComponent<RACommNetVessel>();
                    Destroy(temp);
                }
            }
            // Not sure why the base singleton Instance check is killing itself.  (Instance != this?)
            CommNetNetwork.Instance = this;
            if (HighLogic.LoadedScene == GameScenes.TRACKSTATION)
                GameEvents.onPlanetariumTargetChanged.Add(OnMapFocusChange);
            GameEvents.OnGameSettingsApplied.Add(ResetNetwork);
            GameEvents.onVesselCreate.Add(VesselCreateHandler);
            GameEvents.onVesselDestroy.Add(VesselDestroyHandler);
            //CommNetNetwork.Reset();       // Don't call this way, it will invoke the parent class' ResetNetwork()
        }

        private void VesselCreateHandler(Vessel v) => requestInit = true;
        private void VesselDestroyHandler(Vessel v)
        {
            requestInit = true;
            if (v?.Connection?.Comm is RACommNode node)
            {
                var l = new System.Collections.Generic.List<CommLink>();
                foreach (var link in node.Values)
                    l.Add(link);
                foreach (var link in l)
                    (CommNet as RACommNetwork).DoDisconnect(link.start, link.end);
            }
        }

        protected virtual void Start()
        {
            if (HighLogic.LoadedSceneHasPlanetarium)
            {
                TimingManager.UpdateAdd(TimingManager.TimingStage.ObscenelyEarly, UpdateEarly);
            }
            ResetNetwork();
        }

        protected new void ResetNetwork()
        {
            Debug.Log($"{ModTag} ResetNetwork()");

            CommNet = new RACommNetwork();
            Debug.Log($"{ModTag} Firing onNetworkInitialized()");
            GameEvents.CommNet.OnNetworkInitialized.Fire();
            Debug.Log($"{ModTag} Completed onNetworkInitialized()");
            (CommNet as RACommNetwork).precompute.Initialize();
        }

        protected override void Update()
        {
            (commNet as RACommNetwork).CompleteRebuild();
        }

        protected virtual void UpdateEarly()
        {
            if (requestInit)
            {
                (commNet as RACommNetwork).Validate();
                (commNet as RACommNetwork).precompute.Initialize();
                requestInit = false;
            }

            double interval = System.Math.Min(packedInterval, unpackedInterval);
            // double tm = Time.timeSinceLevelLoad;
            double tm = Planetarium.GetUniversalTime();
            graphDirty |= tm > prevUpdate + interval;
            if (graphDirty || queueRebuild || commNet.IsDirty)
            {
                //commNet.Rebuild();
                (commNet as RACommNetwork).StartRebuild();
                prevUpdate = tm;
                graphDirty = queueRebuild = false;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            GameEvents.onPlanetariumTargetChanged.Remove(OnMapFocusChange);
            GameEvents.OnGameSettingsApplied.Remove(ResetNetwork);
            GameEvents.onVesselCreate.Remove(VesselCreateHandler);
            GameEvents.onVesselDestroy.Remove(VesselDestroyHandler);
            TimingManager.UpdateRemove(TimingManager.TimingStage.ObscenelyEarly, UpdateEarly);
            (CommNet as RACommNetwork).precompute.Destroy();
        }
    }
}
