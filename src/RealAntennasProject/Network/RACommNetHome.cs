﻿using CommNet;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas.Network
{
    public class RACommNetHome : CommNetHome
    {
        protected static readonly string ModTag = "[RealAntennasCommNetHome] ";
        protected ConfigNode config = null;
        private readonly double DriftTolerance = 10000.0;

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
            SetTransformFromLatLonAlt(lat, lon, alt, body);
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
//            Vector3d pos = (nodeTransform == null) ? transform.position : nodeTransform.position;
//            body.GetLatLonAlt(pos, out lat, out lon, out alt);

            RACommNode t = comm as RACommNode;
            t.ParentBody = body;
            int maxTL = HighLogic.CurrentGame.Parameters.CustomParams<RAParameters>().MaxTechLevel;
            float fTSLvl = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation);
            int tsLevel = (HighLogic.CurrentGame.Mode == Game.Modes.CAREER) ? Convert.ToInt32(1 + (fTSLvl * maxTL)) : maxTL;
            // Config node contains a list of antennas to build.
            //Debug.LogFormat("Building all antennas for tech level {0} from {1}", tsLevel, config);
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
                else
                {
                    //Debug.LogFormat("Skipped because current techLevel {0} is less than required {1}", tsLevel, targetLevel);
                }
            }
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