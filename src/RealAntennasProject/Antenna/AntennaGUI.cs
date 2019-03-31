using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RealAntennas.Antenna
{
    // HEAVILY borrowed from SeveredSolo's PAWS manipulator:
    // https://github.com/severedsolo/PAWS
    // Many thanks for the demonstration for manipulating BaseField/BaseEvent and basic GUI use.
    public class AntennaGUI
    {
        public Dictionary<BaseEvent, bool> enabledEvents = new Dictionary<BaseEvent, bool>();
        public Dictionary<BaseField, bool> enabledFields = new Dictionary<BaseField, bool>();
        public bool showGUI = false;
        Rect Window = new Rect(20, 100, 240, 50);
        Vector2 scrollPosition1, scrollPosition2, scrollVesselPos, scrollBodyPos;
        bool showFields = false, showEvents = false;
        bool showVessels = false, showBodies = false;
        List<BaseField> sortedFields;
        List<BaseEvent> sortedEvents;
        public Part ParentPart { get; set; }
        public ModuleRealAntenna ParentPartModule { get; set; }

        public void Start()
        {
            List<BaseField> myFieldList = new List<BaseField>();
            List<BaseEvent> myEventList = new List<BaseEvent>();
            foreach (BaseEvent e in ParentPart.Events)
            {
                if (e.guiActive) myEventList.Add(e);
            }
            foreach (BaseField bf in ParentPart.Fields)
            {
                if (bf.guiActive) myFieldList.Add(bf);
            }
            foreach (PartModule pm in ParentPart.Modules)
            {
                foreach (BaseField bf in pm.Fields)
                {
                    if (bf.guiActive) myFieldList.Add(bf);
                }
                foreach (BaseEvent e in pm.Events)
                {
                    int id = e.id;
                    if (e.guiActive) myEventList.Add(pm.Events[id]);
                }
            }
            sortedFields = myFieldList.OrderBy(baseField => baseField.guiName).ToList();
            sortedEvents = myEventList.OrderBy(baseEvent => baseEvent.guiName).ToList();
        }

        public void OnGUI()
        {
            if (showGUI)
            {
                Window = GUILayout.Window(93938174, Window, GUIDisplay, "Antenna Targeting", GUILayout.Width(200), GUILayout.Height(200));
            }
        }

        void GUIDisplay(int windowID)
        {
            string label;
            if (GUILayout.Button("Show Fields")) showFields = !showFields;
            if (showFields)
            {
                scrollPosition1 = GUILayout.BeginScrollView(scrollPosition1, GUILayout.Width(200), GUILayout.Height(200));
                foreach (BaseField bf in sortedFields) 
                {
                    if (bf.guiActive) label = "Toggle Off";
                    else label = "Toggle On";
                    GUILayout.Label(bf.name + "/" + bf.guiName);
                    if (GUILayout.Button(label))
                    {
                        bf.guiActive = !bf.guiActive;
                    }
                }
                GUILayout.EndScrollView();
            }
            if (GUILayout.Button("Show Events")) showEvents = !showEvents;
            if (showEvents)
            {
                scrollPosition2 = GUILayout.BeginScrollView(scrollPosition2, GUILayout.Width(200), GUILayout.Height(200));
                foreach (BaseEvent be in sortedEvents)
                {
                    if (be.guiActive) label = "Toggle Off";
                    else label = "Toggle On";
                    GUILayout.Label(be.guiName);
                    if (GUILayout.Button(label))
                    {
                        be.guiActive = !be.guiActive;
                    }
                }
                GUILayout.EndScrollView();
            }
            if (GUILayout.Button("Show Vessels")) showVessels = !showVessels;
            if (showVessels)
            {
                scrollVesselPos = GUILayout.BeginScrollView(scrollVesselPos, GUILayout.Width(200), GUILayout.Height(200));
                foreach (Vessel v in FlightGlobals.Vessels)
                {
                    //GUILayout.Label(v.name);
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
//                    GUILayout.Label(body.name);
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
