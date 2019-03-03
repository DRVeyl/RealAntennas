using UnityEngine;
using CommNet;

namespace RealAntennas
{
    public class RACommNetBody : CommNetBody
    {
        protected static readonly string ModTag = "[RealAntennasCommNetBody] ";
        public static CelestialBody FindCelestialBody(string name)
        {
            foreach (CelestialBody x in GameObject.FindObjectsOfType<CelestialBody>())
            {
                if (x.GetName().Equals(name)) return x;
            }
            return null;
        }

        // KSP initializes body=null during Start(), so need to fix in OnNetworkInitialized()
        // Start() leads to here before returning, so this is place to fix before CreateOccluder() NRE.
        protected override void OnNetworkInitialized()
        {
            if (body == null)
            {
                body = FindCelestialBody(name);
                transform.position = body.GetTransform().position;
            }
            base.OnNetworkInitialized();
        }
    }
}