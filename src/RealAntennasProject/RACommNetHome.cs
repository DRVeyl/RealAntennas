using CommNet;
using KSP.Localization;
using UnityEngine;

namespace RealAntennas
{
    public class RACommNetHome : CommNetHome
    {
        protected static readonly string ModTag = "[RealAntennasCommNetHome] ";
        protected ConfigNode config = null;
        private CelestialBody tempBody = null;    // Carry backup because onStart() resets body.

        public void Configure(ConfigNode node, CelestialBody celestialBody)
        {
            nodeName = node.GetValue("name");
            name = node.GetValue("name");
            displaynodeName = Localizer.Format(node.GetValue("name"));
            //            displaynodeName = Localizer.Format(stockHome.displaynodeName);
            //            nodeTransform = celestialBody.transform;
            isKSC = true;
            isPermanent = true;
            body = celestialBody;
            tempBody = celestialBody;
            config = node;
            lat = double.Parse(node.GetValue("Latitude"));
            lon = double.Parse(node.GetValue("Longitude"));
            alt = double.Parse(node.GetValue("Height"));
        }

        protected override void CreateNode()
        {
            Vector3d vec = body.GetWorldSurfacePosition(lat, lon, alt);
            transform.SetPositionAndRotation(vec, Quaternion.identity);
            transform.SetParent(body.transform);
            base.CreateNode();
            comm = new RACommNode(comm);
            RACommNode t = comm as RACommNode;
            t.ParentBody = body;
            comm.OnNetworkPreUpdate += OnNetworkPreUpdate;
            Debug.LogFormat(ModTag + "CreateNode() {0} on {1} @ {2} resulted in {3}", this, body, transform.position, comm);
        }

        protected override void OnNetworkPreUpdate()
        {
            // Vessels, which change, will determine the appropriate antenna for their commnode in UpdateComm() which is part of PreUpdate()
            // So for commnet home nodes, which generally don't change (but no reason why we cannot make more complicated logic to give DSNs
            // MULTIPLE possible antenna configurations?), this seems the right place also to set the antenna.
            if (config != null && (comm is RACommNode vcn))
            {
                vcn.RAAntenna.LoadFromConfigNode(config);
            } else {
                Debug.LogWarning(ModTag + "No config node or improper CommNode for {0}!", this);
            }
            base.OnNetworkPreUpdate();
        }

        protected override void OnNetworkInitialized()
        {
            body = tempBody;
            base.OnNetworkInitialized();
        }

        protected override void Start()
        {
            Debug.LogFormat(ModTag + "OnStart() for {0}", this);
            Configure(config, tempBody);
            base.Start();
        }
    }
}