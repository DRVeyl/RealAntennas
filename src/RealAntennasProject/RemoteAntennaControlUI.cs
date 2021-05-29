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
        private Vector2 scrollSourcePos;
        private enum SortMode { Alphabetical, VesselType, ParentBody, RFBand };
        private SortMode sourceSortMode = SortMode.Alphabetical;
        private readonly List<Vessel> sourceVessels = new List<Vessel>();

        public void Start()
        {
            sourceVessels.Clear();
            sourceVessels.AddRange(FlightGlobals.Vessels.Where(v => v.Connection is RACommNetVessel && v.Connection.Comm is RACommNode));
        }

        public void OnGUI()
        {
            GUI.skin = HighLogic.Skin;
            Window = GUILayout.Window(GetHashCode(), Window, GUIDisplay, GUIName, HighLogic.Skin.window);
        }


        void GUIDisplay(int windowID)
        {
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
                {
                    Targeting.AntennaTargetManager.AcquireGUI(ra);
                }
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button("Close"))
            {
                Destroy(this);
                gameObject.DestroyGameObject();
            }
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
