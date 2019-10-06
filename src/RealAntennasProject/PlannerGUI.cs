using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas
{
    public class PlannerGUI
    {
        public bool showGUI = false;
        Rect Window = new Rect(250, 100, 240, 50);
        Vector2 scrollVesselPos, scrollBodyPos;
        bool showProto = false, showConstruct = false, showVessels = false, showBodies = false;
        public Planner parent;

        public void Start() { showProto = showConstruct = showVessels = showBodies = false; }

        public void OnGUI()
        {
            if (showGUI)
            {
                Window = GUILayout.Window(93938174, Window, GUIDisplay, "Antenna Planning Target", GUILayout.Width(200), GUILayout.Height(200));
            }
        }

        void GUIDisplay(int windowID)
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                if (GUILayout.Button("Show ProtoVessel Antennas")) showProto = !showProto;
                if (showProto) GUI_HandleProtoVessels();
                if (GUILayout.Button("Show This Vessel's Antennas")) showConstruct = !showConstruct;
                if (showConstruct) GUI_HandleShipConstruct();
            }
            else
            {
                if (GUILayout.Button("Show Vessel Antennas")) showVessels = !showVessels;
                if (showVessels) GUI_HandleVessels();
            }

            if (GUILayout.Button("Show Bodies")) showBodies = !showBodies;
            if (showBodies)
            {
                scrollBodyPos = GUILayout.BeginScrollView(scrollBodyPos, GUILayout.Width(200), GUILayout.Height(200));
                List<CelestialBody> selectedList = new List<CelestialBody> { Planetarium.fetch.Sun };
                selectedList.AddRange(Planetarium.fetch.Sun.orbitingBodies);
                foreach (CelestialBody body in selectedList)
                {
                    if (GUILayout.Button(body.name)) ConfigTarget(body.name, body);
                }
                GUILayout.EndScrollView();
            }
            if (GUILayout.Button("Close")) showGUI = false;
            GUI.DragWindow();
        }

        private void GUI_HandleProtoVessels()
        {
            foreach (ProtoVessel pv in HighLogic.CurrentGame.flightState.protoVessels)
            {
                foreach (ProtoPartSnapshot part in pv.protoPartSnapshots)
                {
                    if (part.FindModule(ModuleRealAntenna.ModuleName) is ProtoPartModuleSnapshot snap)
                    {
                        ModuleRealAntenna mra = part.partInfo.partPrefab.FindModuleImplementing<ModuleRealAntenna>();
                        string sTarget = $"{pv.vesselName}_{mra.RAAntenna}";
                        if (GUILayout.Button(sTarget))
                        {
                            RealAntenna ra = new RealAntennaDigital(mra.name) { ParentNode = null };
                            ra.LoadFromConfigNode(snap.moduleValues);
                            ConfigTarget(sTarget, ra);
                        }
                    }
                }
            }
        }

        private void GUI_HandleShipConstruct()
        {
            ShipConstruct sc = parent.parent.part.ship;
            foreach (Part p in sc.Parts)
            {
                foreach (ModuleRealAntenna mra in p.FindModulesImplementing<ModuleRealAntenna>())
                {
                    string sTarget = $"{sc.shipName}_{mra.RAAntenna}";
                    if (GUILayout.Button(sTarget)) ConfigTarget(sTarget, mra.RAAntenna);
                }
            }
        }

        private void GUI_HandleVessels()
        {
            scrollVesselPos = GUILayout.BeginScrollView(scrollVesselPos, GUILayout.Width(400), GUILayout.Height(200));
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (v.Connection is RACommNetVessel racnv && racnv.Comm is RACommNode racn)
                {
                    foreach (RealAntenna ra in racn.RAAntennaList)
                    {
                        string sTarget = $"{v.name}_{ra}";
                        if (GUILayout.Button(sTarget)) ConfigTarget(sTarget, ra);
                    }
                }
            }
            GUILayout.EndScrollView();
        }

        private void ConfigTarget(string sTarget, object Target) => parent.ConfigTarget(sTarget, Target);

    }
}
