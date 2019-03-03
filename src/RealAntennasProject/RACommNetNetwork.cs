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
        public static new RACommNetNetwork Instance { get; protected set; }

        protected override void Awake()
        {
            Debug.Log(ModTag + "CommNet Network booting");
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if ((v.Connection != null) && (!(v.Connection is RACommNetVessel)))
                {
                    Debug.LogFormat(ModTag + "Rebuilding CommVessel on {0}.  (Was {1} of type {2})", v, v.Connection, v.Connection.GetType());
                    CommNetVessel temp = v.Connection;
                    v.Connection.gameObject.AddComponent<RACommNetVessel>();
                    Destroy(temp);
                }
            }

            CommNetNetwork.Instance = this;

            GameEvents.OnGameSettingsApplied.Add(new EventVoid.OnEvent(ResetNetwork));
            ResetNetwork();
        }

        protected new void ResetNetwork()
        {
            Debug.Log(ModTag + "CommNet Network resetNetwork() start");

            CommNet = new RACommNetwork();
            /* Maybe we skip firing CommNet.OnNetworkInitialized.  It's purpose seems to be:
             * Generate occluders for all celestial bodies.
             * Generate CommNet stations for the default CommNet set.
             * This seems without regard to the global list of CommNetBody (or extension) or CommNetHome.
             * That global list will LATER get OnStart() calls and separate OnNetworkInitialized() calls.
             */
            /*
            Debug.Log(ModTag + "CommNet Network Firing onNetworkInitialized()");
            GameEvents.CommNet.OnNetworkInitialized.Fire();
            Debug.Log(ModTag + "CommNet Network onNetworkInitialized() complete");
           */
            Debug.Log(ModTag + "Skipping GameEvents.CommNet.OnNetworkInitialized.Fire()");
        }
    }
}