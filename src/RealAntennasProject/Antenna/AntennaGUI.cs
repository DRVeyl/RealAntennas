using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas.Antenna
{
    // HEAVILY borrowed from SeveredSolo's PAWS manipulator:
    // https://github.com/severedsolo/PAWS
    // Many thanks for the demonstration for manipulating BaseField/BaseEvent and basic GUI use.
    public class AntennaGUI
    {
        public bool showGUI = false;
        Rect Window = new Rect(20, 100, 240, 50);
        Vector2 scrollVesselPos, scrollBodyPos;
        bool showVessels = false, showBodies = false;
        public Part ParentPart { get; set; }
        public ModuleRealAntenna ParentPartModule { get; set; }

        public void Start() {}

        public void OnGUI()
        {
            if (showGUI)
            {
                Window = GUILayout.Window(GetHashCode(), Window, GUIDisplay, $"{ParentPart.partName} Antenna Targeting", GUILayout.Width(200), GUILayout.Height(200));
            }
        }

        void GUIDisplay(int windowID)
        {
            if (GUILayout.Button("Show Vessels")) showVessels = !showVessels;
            if (showVessels)
            {
                scrollVesselPos = GUILayout.BeginScrollView(scrollVesselPos, GUILayout.Width(200), GUILayout.Height(200));
                foreach (Vessel v in FlightGlobals.Vessels)
                {
                    if (GUILayout.Button(v.name))
                    {
                        ParentPartModule.Target = v;
                    }
                }
                GUILayout.EndScrollView();
            }
            if (GUILayout.Button("Show Bodies")) showBodies = !showBodies;
            if (showBodies)
            {
                scrollBodyPos = GUILayout.BeginScrollView(scrollBodyPos, GUILayout.Width(200), GUILayout.Height(200));
                foreach (CelestialBody body in FlightGlobals.Bodies)
                {
                    if (GUILayout.Button(body.name))
                    {
                        ParentPartModule.Target = body;
                    }
                }
                GUILayout.EndScrollView();
            }
            if (GUILayout.Button("Close")) showGUI = false;
            GUI.DragWindow();
        }
    }
}
