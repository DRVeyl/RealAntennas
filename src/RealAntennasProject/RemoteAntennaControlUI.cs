using KSP.UI.Screens.Mapview.MapContextMenuOptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RealAntennas
{
    class RemoteAntennaControlUI : MonoBehaviour
    {
        const string GUIName = "Antenna Control Center";
        private Rect Window = new Rect(100, 100, 800, 500);
        private Vector2 scrollSourcePos, scrollTargetPos;
        private enum SortMode { Alphabetical, VesselType, ParentBody, RFBand };
        private SortMode sourceSortMode = SortMode.Alphabetical;
        private SortMode targetSortMode = SortMode.Alphabetical;

        private readonly List<Vessel> sourceVessels = new List<Vessel>();
        private readonly List<Vessel> targetVessels = new List<Vessel>();
        private RealAntenna sourceAntenna;

        GUIStyle styleOpts;

        public void Awake()
        {
            Debug.Log($"{this.GetType()} Awake()");
        }
        public void Start()
        {
            Debug.Log($"{this.GetType()} Start()");
            sourceVessels.Clear();
            targetVessels.Clear();
            sourceVessels.AddRange(FlightGlobals.Vessels.Where(v => v.Connection is RACommNetVessel && v.Connection.Comm is RACommNode));
            targetVessels.AddRange(FlightGlobals.Vessels);
            styleOpts = new GUIStyle(HighLogic.Skin.button) { fontSize = 10, };
        }

        public void OnGUI()
        {
            GUI.skin = HighLogic.Skin;
            Window = GUILayout.Window(GetHashCode(), Window, GUIDisplay, GUIName, styleOpts);
        }


        void GUIDisplay(int windowID)
        {
            string s = $"{(sourceAntenna?.ParentNode as RACommNode)?.ParentVessel?.vesselName} {sourceAntenna?.ToStringShort()}";
            GUILayout.Label($"Antenna: {(sourceAntenna is RealAntenna ? s : "Not Selected")}");
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            if (GUILayout.Button($"Source Sort Mode: {sourceSortMode}"))
            {
                //(i1, i2) => i1.ToString().CompareTo(i2.ToString())
                sourceSortMode = (SortMode)(((int)(sourceSortMode + 1)) % System.Enum.GetNames(typeof(SortMode)).Length);
                SortList(sourceVessels, sourceSortMode);
            }
            scrollSourcePos = GUILayout.BeginScrollView(scrollSourcePos, GUILayout.ExpandWidth(true));
            foreach (var ra in from Vessel v in sourceVessels
                               from RealAntenna ra in (v.Connection?.Comm as RACommNode)?.RAAntennaList?.Where(x => x.CanTarget)
                               select ra)
            {
                if (GUILayout.Button($"{(ra.ParentNode as RACommNode)?.ParentVessel?.vesselName} {ra.ToStringShort()}"))
                    sourceAntenna = ra;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if (GUILayout.Button($"Target Sort Mode: {targetSortMode}"))
            {
                targetSortMode = (SortMode)(((int)(targetSortMode + 1)) % System.Enum.GetNames(typeof(SortMode)).Length);
                SortList(targetVessels, targetSortMode);
            }
            scrollTargetPos = GUILayout.BeginScrollView(scrollTargetPos, GUILayout.ExpandWidth(true));
            foreach (var v in targetVessels)
            {
                if (sourceAntenna is RealAntenna && GUILayout.Button($"{v.vesselName}"))
                    sourceAntenna.Target = v;
            }
            foreach (var v in FlightGlobals.Bodies)
            {
                if (sourceAntenna is RealAntenna && GUILayout.Button($"{v.name}"))
                    sourceAntenna.Target = v;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        private class RFBandComparer : IComparer<Vessel>
        {
            public int Compare(Vessel x, Vessel y)
            {
                if ((x.connection?.Comm as RACommNode)?.RAAntennaList.FirstOrDefault()?.RFBand is Antenna.BandInfo rfband1 &&
                    (y.connection?.Comm as RACommNode)?.RAAntennaList.FirstOrDefault()?.RFBand is Antenna.BandInfo rfband2)
                    return rfband1.name.CompareTo(rfband2.name);
                else return x.name.CompareTo(y.name);
            }
        }

        private void SortList(List<Vessel> vessels, SortMode mode)
        {
            switch (mode)
            {
                case SortMode.Alphabetical: vessels.Sort((x, y) => x.name.CompareTo(y.name)); break;
                case SortMode.VesselType: vessels.Sort((x, y) => x.vesselType.CompareTo(y.vesselType)); break;
                case SortMode.ParentBody: vessels.Sort((x, y) => x.mainBody.bodyName.CompareTo(y.mainBody.bodyName)); break;
                case SortMode.RFBand: vessels.Sort(new RFBandComparer()); break;
            }
        }
    }
}
