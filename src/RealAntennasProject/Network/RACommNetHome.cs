using CommNet;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas.Network
{
    public class RACommNetHome : CommNetHome
    {
        protected static readonly string ModTag = "[RealAntennasCommNetHome] ";
        protected ConfigNode config = null;
        protected bool isHome = true;
        protected bool isControlSource = true;
        protected bool isControlSourceMultiHop = true;
        private readonly double DriftTolerance = 10000.0;
        public string icon = "radio-antenna";
        public RACommNode Comm => comm as RACommNode;

        public void SetTransformFromLatLonAlt(double lat, double lon, double alt, CelestialBody body)
        {
            Vector3d vec = body.GetWorldSurfacePosition(lat, lon, alt);
            transform.SetPositionAndRotation(vec, Quaternion.identity);
            transform.SetParent(body.transform);
        }

        public void Configure(ConfigNode node, CelestialBody body)
        {
            name = node.GetValue("name");
            nodeName = node.GetValue("objectName");
            displaynodeName = nodeName;
            isKSC = true;
            isPermanent = true;
            config = node;
            lat = double.Parse(node.GetValue("lat"));
            lon = double.Parse(node.GetValue("lon"));
            alt = double.Parse(node.GetValue("alt"));
            string value = null;
            if (node.TryGetValue("isHome", ref value))
            {
                isHome = bool.Parse(value);
            }
            if (node.TryGetValue("isControlSource", ref value))
            {
                isControlSource = bool.Parse(value);
            }
            if (node.TryGetValue("isControlSourceMultiHop", ref value))
            {
                isControlSourceMultiHop = bool.Parse(value);
            }
            node.TryGetValue("icon", ref icon);
            SetTransformFromLatLonAlt(lat, lon, alt, body);
        }
        protected override void CreateNode()
        {
            if (comm == null)
            {
                comm = new RACommNode(nodeTransform)
                {
                    OnNetworkPreUpdate = new Action(OnNetworkPreUpdate),
                    isHome = isHome,
                    isControlSource = isControlSource,
                    isControlSourceMultiHop = isControlSourceMultiHop
                };
            }
            comm.name = nodeName;
            comm.displayName = displaynodeName;
            comm.antennaRelay.Update(!isPermanent ? GameVariables.Instance.GetDSNRange(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation)) : antennaPower, GameVariables.Instance.GetDSNRangeCurve(), false);
//            Vector3d pos = (nodeTransform == null) ? transform.position : nodeTransform.position;
//            body.GetLatLonAlt(pos, out lat, out lon, out alt);

            RACommNode t = comm as RACommNode;
            t.ParentBody = body;
            int tsLevel = RACommNetScenario.GroundStationTechLevel;
            // Config node contains a list of antennas to build.
            t.RAAntennaList = new List<RealAntenna> { };
            foreach (ConfigNode antNode in config.GetNodes("Antenna")) 
            {
                //Debug.LogFormat("Building an antenna for {0}", antNode);
                int targetLevel = Int32.Parse(antNode.GetValue("TechLevel"));
                if (tsLevel >= targetLevel)
                {
                    RealAntenna ant = new RealAntennaDigital(name) { ParentNode = comm };
                    ant.LoadFromConfigNode(antNode);
                    ant.ProcessUpgrades(tsLevel, antNode);
                    ant.TechLevelInfo = TechLevelInfo.GetTechLevel(tsLevel);
                    t.RAAntennaList.Add(ant);
                }
            }
        }

        public void OnUpdateVisible(KSP.UI.Screens.Mapview.MapNode mapNode, KSP.UI.Screens.Mapview.MapNode.IconData iconData)
        {
            Vector3d worldPos = ScaledSpace.LocalToScaledSpace(Comm.precisePosition);
            iconData.visible &= MapView.MapCamera.transform.InverseTransformPoint(worldPos).z >= 0 && !IsOccludedToCamera(Comm.precisePosition, body);
        }

        private bool IsOccludedToCamera(Vector3d position, CelestialBody body)
        {
            Vector3d camPos = ScaledSpace.ScaledToLocalSpace(PlanetariumCamera.Camera.transform.position);
            return Vector3d.Angle(camPos - position, body.position - position) <= 90;
        }

        public void CheckNodeConsistency()
        {
            Vector3d desiredPos = body.GetWorldSurfacePosition(lat, lon, alt);
            Vector3d pos = (nodeTransform == null) ? transform.position : nodeTransform.position;
            if (Vector3d.Distance(pos, desiredPos) > DriftTolerance)
            {
                body.GetLatLonAlt(pos, out double cLat, out double cLon, out double cAlt);
                Debug.LogFormat($"{ModTag} {name} {nodeName} correcting position from current {cLat:F2}/{cLon:F2}/{cAlt:F0} to desired {lat:F2}/{lon:F2}/{alt:F0}");
                transform.SetPositionAndRotation(desiredPos, Quaternion.identity);
            }
        }
    }
}