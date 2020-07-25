using System.Collections.Generic;
using System.Linq;
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
        enum SortMode { Alphabetical, VesselType, ParentBody, RFBand };
        SortMode sortMode = SortMode.Alphabetical;
        public Part ParentPart { get; set; }
        public ModuleRealAntenna ParentPartModule { get; set; }

        private readonly List<Vessel> vessels = new List<Vessel>();

        public void Start()
        {
            vessels.Clear();
            vessels.AddRange(FlightGlobals.Vessels);
        }

        public void OnGUI()
        {
            if (showGUI)
            {
                Window = GUILayout.Window(GetHashCode(), Window, GUIDisplay, $"{ParentPart.partName} Antenna Targeting", GUILayout.Width(200), GUILayout.Height(200));
            }
        }

        void GUIDisplay(int windowID)
        {
            if (GUILayout.Button($"Sort Mode: {sortMode}"))
            {
                sortMode = (SortMode)(((int)(sortMode + 1)) % System.Enum.GetNames(typeof(SortMode)).Length);
                switch (sortMode)
                {
                    case SortMode.Alphabetical: vessels.Sort((x, y) => x.name.CompareTo(y.name)); break;
                    case SortMode.VesselType: vessels.Sort((x, y) => x.vesselType.CompareTo(y.vesselType)); break;
                    case SortMode.ParentBody: vessels.Sort((x, y) => x.mainBody.bodyName.CompareTo(y.mainBody.bodyName)); break;
                    case SortMode.RFBand: vessels.Sort(new RFBandComparer()); break;
                }
            }
            if (GUILayout.Button("Show Vessels")) showVessels = !showVessels;
            if (showVessels)
            {
                scrollVesselPos = GUILayout.BeginScrollView(scrollVesselPos, GUILayout.Width(200), GUILayout.Height(200));
                foreach (Vessel v in vessels)
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

        private class RFBandComparer : IComparer<Vessel>
        {
            public int Compare(Vessel x, Vessel y)
            {
                if ((x.connection?.Comm as RACommNode)?.RAAntennaList.FirstOrDefault()?.RFBand is BandInfo rfband1 &&
                    (y.connection?.Comm as RACommNode)?.RAAntennaList.FirstOrDefault()?.RFBand is BandInfo rfband2)
                    return rfband1.name.CompareTo(rfband2.name);
                else return x.name.CompareTo(y.name);
            }
        }
    }
}
