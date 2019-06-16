using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas
{
    public class PlannerGUI
    {
        public bool showGUI = false;
        Rect Window = new Rect(250, 100, 240, 50);
        Vector2 scrollVesselPos, scrollBodyPos;
        bool showVessels = false, showBodies = false;
        public Part ParentPart { get; set; }
        public ModuleRealAntenna ParentPartModule { get; set; }

        public void Start() {}

        public void OnGUI()
        {
            if (showGUI)
            {
                Window = GUILayout.Window(93938174, Window, GUIDisplay, "Antenna Planning Target", GUILayout.Width(200), GUILayout.Height(200));
            }
        }

        void GUIDisplay(int windowID)
        {
            /*
            if (GUILayout.Button("Show Vessels")) showVessels = !showVessels;
            if (showVessels)
            {
                scrollVesselPos = GUILayout.BeginScrollView(scrollVesselPos, GUILayout.Width(200), GUILayout.Height(200));
                foreach (Vessel v in FlightGlobals.Vessels)
                {
                    if (GUILayout.Button(v.name))
                    {
                        ParentPartModule.sPlannerTarget = v.name;
                        ParentPartModule.PlannerTarget = v;
                        ParentPartModule.RecalculatePlannerFields();
                    }
                }
                GUILayout.EndScrollView();
            }
            */
            if (GUILayout.Button("Show Bodies")) showBodies = !showBodies;
            if (showBodies)
            {
                scrollBodyPos = GUILayout.BeginScrollView(scrollBodyPos, GUILayout.Width(200), GUILayout.Height(200));
                CelestialBody sun = Planetarium.fetch.Sun;
                List<CelestialBody> selectedList = new List<CelestialBody> { Planetarium.fetch.Sun };
                selectedList.AddRange(Planetarium.fetch.Sun.orbitingBodies);
                foreach (CelestialBody body in selectedList)
                {
                    if (GUILayout.Button(body.name))
                    {
                        ParentPartModule.sPlannerTarget = body.name;
                        ParentPartModule.PlannerTarget = body;
                        ParentPartModule.RecalculatePlannerFields();
                    }
                }
                GUILayout.EndScrollView();
            }
            if (GUILayout.Button("Close")) showGUI = false;
            GUI.DragWindow();
        }
    }
}
