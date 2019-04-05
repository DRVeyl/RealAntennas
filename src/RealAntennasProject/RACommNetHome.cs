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
            Vector3d pos = (nodeTransform == null) ? transform.position : nodeTransform.position;
            body.GetLatLonAlt(pos, out lat, out lon, out alt);

            RACommNode t = comm as RACommNode;
            t.ParentBody = body;
            RealAntenna ant = new RealAntennaDigital(name);
            ant.LoadFromConfigNode(config);
            t.RAAntennaList = new List<RealAntenna> { ant };
        }
    }
}