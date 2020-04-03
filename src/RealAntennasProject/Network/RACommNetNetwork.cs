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
            //CommNetNetwork.Reset();       // Don't call this way, it will invoke the parent class' ResetNetwork()
            ResetNetwork();
        }

        protected new void ResetNetwork()
        {
            Debug.Log($"{ModTag} ResetNetwork()");

            CommNet = new RACommNetwork();
            Debug.Log($"{ModTag} Firing onNetworkInitialized()");
            GameEvents.CommNet.OnNetworkInitialized.Fire();
            Debug.Log($"{ModTag} Completed onNetworkInitialized()");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            GameEvents.onPlanetariumTargetChanged.Remove(OnMapFocusChange);
            GameEvents.OnGameSettingsApplied.Remove(ResetNetwork);
        }
    }
}