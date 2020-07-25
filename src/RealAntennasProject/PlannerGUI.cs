using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RealAntennas
{
    public class PlannerGUI
    {
        public bool showGUI = false;
        private const int GUIWidth = 650, GUIHeight = 400;
        private const float INDENT = 60;
        Rect Window = new Rect(250, 100, GUIWidth, GUIHeight);
        Vector2 scroller;
        bool showProto, showConstruct, showVessels, showBodies, showGroundStations;
        public Planner parent;
        public readonly Dictionary<ProtoVessel, List<RealAntenna>> protoVesselAntennaCache = new Dictionary<ProtoVessel, List<RealAntenna>>();
        public readonly Dictionary<ProtoVessel, bool> protoVesselToggles = new Dictionary<ProtoVessel, bool>();
        public readonly Dictionary<Vessel, bool> vesselToggles = new Dictionary<Vessel, bool>();
        public readonly Dictionary<Network.RACommNetHome, bool> groundStationToggles = new Dictionary<Network.RACommNetHome, bool>();
        private float fTechLevel = 0;

        public void Start()
        {
            showProto = showConstruct = showVessels = showBodies = showGroundStations = false;
            DiscoverProtoVesselAntennas(protoVesselAntennaCache);
            protoVesselToggles.Clear();
            vesselToggles.Clear();
            groundStationToggles.Clear();
            foreach (ProtoVessel pv in protoVesselAntennaCache.Keys)
                protoVesselToggles.Add(pv, false);
            foreach (Vessel v in FlightGlobals.Vessels)
                vesselToggles.Add(v, false);
            foreach (Network.RACommNetHome h in RACommNetScenario.GroundStations.Values)
                groundStationToggles.Add(h, false);
            fTechLevel = RACommNetScenario.GroundStationTechLevel;
        }

        public void OnGUI()
        {
            if (showGUI)
            {
                Window = GUILayout.Window(GetHashCode(), Window, GUIDisplay, "Antenna Planning Target", GUILayout.Width(GUIWidth), GUILayout.Height(GUIHeight));
            }
        }

        private HashSet<string> GetActiveCommNetHomeToggles()
        {
            HashSet<string> res = new HashSet<string>();
            foreach (Network.RACommNetHome home in groundStationToggles.Keys)
            {
                if (groundStationToggles[home] && !res.Contains(home.nodeName))
                    res.Add(home.nodeName);
            }
            return res;
        }

        void GUIDisplay(int windowID)
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Ground Station TechLevel: {fTechLevel}");
                fTechLevel = GUILayout.HorizontalSlider(Mathf.RoundToInt(fTechLevel), 0, RACommNetScenario.MaxTL);
                fTechLevel = Mathf.RoundToInt(fTechLevel);
                if (GUILayout.Button("Apply"))
                {
                    HashSet<string> names = GetActiveCommNetHomeToggles();
                    parent.ConfigTarget(Planetarium.fetch.Home.name, Planetarium.fetch.Home);
                    RACommNetScenario.GroundStationTechLevel = Mathf.RoundToInt(fTechLevel);
                    (RACommNetScenario.Instance as RACommNetScenario).RebuildHomes();
                    groundStationToggles.Clear();
                    foreach (Network.RACommNetHome h in RACommNetScenario.GroundStations.Values)
                        groundStationToggles.Add(h, names.Contains(h.nodeName));
                }
                GUILayout.EndHorizontal();
            }

            scroller = GUILayout.BeginScrollView(scroller);
            if (HighLogic.LoadedSceneIsEditor)
            {
                showProto = GUILayout.Toggle(showProto, "Vessel Antennas");
                if (showProto) GUI_HandleProtoVessels();

                GUI_HandleShipConstruct();
            }
            else
            {
                showVessels = GUILayout.Toggle(showVessels, "Vessel Antennas");
                if (showVessels) GUI_HandleVessels();
            }
            showBodies = GUILayout.Toggle(showBodies, "Celestial Bodies");
            if (showBodies) GUI_HandleBodies();
            showGroundStations = GUILayout.Toggle(showGroundStations, "Ground Stations");
            if (showGroundStations) GUI_HandleGroundStations();
            GUILayout.EndScrollView();

            if (GUILayout.Button("Close")) showGUI = false;
            GUI.DragWindow();
        }

        private void DiscoverProtoVesselAntennas(Dictionary<ProtoVessel, List<RealAntenna>> dict)
        {
            dict.Clear();
            foreach (ProtoVessel pv in HighLogic.CurrentGame.flightState.protoVessels)
            {
                List<RealAntenna> antennas = new List<RealAntenna>();
                foreach (ProtoPartSnapshot part in pv.protoPartSnapshots)
                {
                    if (part.FindModule(ModuleRealAntenna.ModuleName) is ProtoPartModuleSnapshot snap)
                    {
                        ModuleRealAntenna mra = part.partInfo.partPrefab.FindModuleImplementing<ModuleRealAntenna>();
                        RealAntenna ra = new RealAntennaDigital(mra.name) { ParentNode = null };
                        ra.LoadFromConfigNode(snap.moduleValues);
                        antennas.Add(ra);
                    }
                }
                if (antennas.Count > 0)
                    dict.Add(pv, antennas);
            }
        }

        private void GUI_HandleProtoVessels()
        {
            foreach (ProtoVessel pv in protoVesselAntennaCache.Keys)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(INDENT / 2);
                protoVesselToggles[pv] = GUILayout.Toggle(protoVesselToggles[pv], $"{pv.vesselName}");
                GUILayout.EndHorizontal();

                if (protoVesselToggles[pv])
                {
                    foreach (RealAntenna ra in protoVesselAntennaCache[pv])
                    {
                        AntennaButton($"{ra}", ra, INDENT);
                    }
                }
            }
        }

        private void GUI_HandleShipConstruct()
        {
            ShipConstruct sc = parent.parent.part.localRoot.ship;
            showConstruct = GUILayout.Toggle(showConstruct, $"{sc.shipName}");
            if (showConstruct)
            {
                foreach (Part p in sc.Parts)
                {
                    foreach (ModuleRealAntenna mra in p.FindModulesImplementing<ModuleRealAntenna>())
                    {
                        AntennaButton($"{mra.RAAntenna}", mra.RAAntenna, INDENT);
                    }
                }
            }
        }

        private void GUI_HandleVessels()
        {
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (v.Connection is RACommNetVessel racnv && racnv.Comm is RACommNode racn)
                {
                    if (vesselToggles.ContainsKey(v))
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(INDENT / 2);
                        vesselToggles[v] = GUILayout.Toggle(vesselToggles[v], $"{v.vesselName}");
                        GUILayout.EndHorizontal();
                    }

                    if (!vesselToggles.ContainsKey(v) || vesselToggles[v])
                    {
                        foreach (RealAntenna ra in racn.RAAntennaList)
                        {
                            AntennaButton($"{ra}", ra, INDENT);
                        }
                    }
                }
            }
        }

        private void GUI_HandleBodies()
        {
            List<CelestialBody> selectedList = new List<CelestialBody> { Planetarium.fetch.Sun };
            selectedList.AddRange(Planetarium.fetch.Sun.orbitingBodies);
            foreach (CelestialBody body in selectedList)
            {
                AntennaButton($"{body.name}", body, INDENT / 2);
            }
        }

        private void GUI_HandleGroundStations()
        {
            foreach (Network.RACommNetHome home in RACommNetScenario.GroundStations.Values)
            {
                if (home.Comm is RACommNode racn)
                {
                    if (groundStationToggles.ContainsKey(home))
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(INDENT / 2);
                        groundStationToggles[home] = GUILayout.Toggle(groundStationToggles[home], $"{home.nodeName}");
                        GUILayout.EndHorizontal();
                    }
                    if (!groundStationToggles.ContainsKey(home) || groundStationToggles[home])
                    {
                        foreach (RealAntenna ra in racn.RAAntennaList)
                        {
                            AntennaButton($"{ra}", ra, INDENT);
                        }
                    }
                }
            }
        }

        private void AntennaButton(string sTarget, object target, float gap)
        {
            GUILayout.BeginHorizontal();
            if (gap > 0) 
                GUILayout.Space(gap);
            if (GUILayout.Button(sTarget)) ConfigTarget(sTarget, target);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void ConfigTarget(string sTarget, object Target) => parent.ConfigTarget(sTarget, Target);

    }
}
