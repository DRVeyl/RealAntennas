using UnityEngine;
using CommNet;

namespace RealAntennas
{
    /// <summary>
    /// Extend the functionality of the KSP's CommNetNetwork (co-primary model in the Model–view–controller sense; CommNet<> is the other co-primary one)
    /// </summary>
    public class RACommNetNetwork : CommNetNetwork
    {
        protected static readonly string ModTag = "[RACommNetNetwork] ";

        protected override void Awake()
        {
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (v.Connection != null && !(v.Connection is RACommNetVessel ra))
                {
                    Debug.LogFormat(ModTag + "Rebuilding CommVessel on {0}.  (Was {1} of type {2})", v, v.Connection, v.Connection.GetType());
                    CommNetVessel temp = v.Connection;
                    v.Connection.gameObject.AddComponent<RACommNetVessel>();
                    Destroy(temp);
                }
            }
            // Not sure why the base singleton Instance check is killing itself.  (Instance != this?)
            CommNetNetwork.Instance = this;
            if (HighLogic.LoadedScene == GameScenes.TRACKSTATION)
                GameEvents.onPlanetariumTargetChanged.Add(new EventData<MapObject>.OnEvent(this.OnMapFocusChange));
            GameEvents.OnGameSettingsApplied.Add(new EventVoid.OnEvent(this.ResetNetwork));
            //CommNetNetwork.Reset();       // Don't call this way, it will invoke the parent class' ResetNetwork()
            ResetNetwork();
            //            base.Awake();
        }

        protected new void ResetNetwork()
        {
            Debug.Log(ModTag + "CommNet Network resetNetwork() start");

            CommNet = new RACommNetwork();
            Debug.Log(ModTag + "CommNet Network Firing onNetworkInitialized()");
            GameEvents.CommNet.OnNetworkInitialized.Fire();
            Debug.Log(ModTag + "CommNet Network onNetworkInitialized() complete");
        }
    }
}