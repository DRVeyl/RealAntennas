using CommNet;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas
{
    public class RACommNetHome : CommNetHome
    {
        protected static readonly string ModTag = "[RealAntennasCommNetHome] ";
        protected ConfigNode config = null;
        protected string bodyName = string.Empty;

        public void SetTransformFromConfig(ConfigNode node, CelestialBody body)
        {
            double lat = double.Parse(node.GetValue("Latitude"));
            double lon = double.Parse(node.GetValue("Longitude"));
            double alt = double.Parse(node.GetValue("Height"));
            Vector3d vec = body.GetWorldSurfacePosition(lat, lon, alt);
            transform.SetPositionAndRotation(vec, Quaternion.identity);
            transform.SetParent(body.transform);
        }

        public void Configure(ConfigNode node, CelestialBody body)
        {
            nodeName = node.GetValue("name");
            name = node.GetValue("name");
            displaynodeName = name;
            isKSC = true;
            isPermanent = true;
            config = node;
            bodyName = body.name;
            SetTransformFromConfig(config, body);
        }
        protected override void CreateNode()
        {
            if (comm == null)
            {
                comm = new RACommNode(nodeTransform)
                {
                    OnNetworkPreUpdate = new Action(OnNetworkPreUpdate),
                    isHome = true,
                    isControlSource = true,
                    isControlSourceMultiHop = true
                };
            }
            comm.name = nodeName;
            comm.displayName = displaynodeName;
            comm.antennaRelay.Update(!isPermanent ? GameVariables.Instance.GetDSNRange(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation)) : antennaPower, GameVariables.Instance.GetDSNRangeCurve(), false);
            //          comm.antennaTransmit = null;
            comm.antennaTransmit.Update(comm.antennaRelay.power, comm.antennaRelay.rangeCurve, comm.antennaRelay.combined);
            Vector3d pos = (nodeTransform == null) ? transform.position : nodeTransform.position;
            body.GetLatLonAlt(pos, out lat, out lon, out alt);

            RACommNode t = comm as RACommNode;
            t.ParentBody = body;
            RealAntenna ant = new RealAntennaDigital(name);
            ant.LoadFromConfigNode(config);
            t.RAAntennaList = new List<RealAntenna> { ant };
            Debug.LogFormat(ModTag + "CreateNode() {0} on {1} @ {2} resulted in {3}", this, body, transform.position, comm);
        }

        protected override void Start()
        {
            Debug.LogFormat(ModTag + "OnStart() for {0}", this);
            //this.body = this.GetComponentInParent<CelestialBody>();
            body = FlightGlobals.GetBodyByName(bodyName);
            if (nodeTransform == null) nodeTransform = transform;
            Configure(config, body);
            if (CommNetNetwork.Initialized) OnNetworkInitialized();
            GameEvents.CommNet.OnNetworkInitialized.Add(new EventVoid.OnEvent(OnNetworkInitialized));
        }
    }
}