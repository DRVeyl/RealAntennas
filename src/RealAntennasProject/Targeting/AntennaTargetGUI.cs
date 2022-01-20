using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static RealAntennas.Targeting.AntennaTarget;

namespace RealAntennas.Targeting
{
    public class AntennaTargetGUI : MonoBehaviour
    {
        const string GUIName = "Antenna Targeting";
        Rect Window = new Rect(20, 100, 280, 200);
        Vector2 scrollVesselPos, scrollBodyPos, iconSize = new Vector2(30, 30);
        enum SortMode { Alphabetical, Distance, VesselType, ParentBody, RFBand };
        SortMode sortMode = SortMode.Alphabetical;
        private TargetModeInfo targetMode = TargetModeInfo.All.Values.First();
        private string sLat = "0", sLon = "0", sAlt = "0", sAzimuth = "0", sElevation = "0", sForward = "0";
        float deflection = 0;
        private bool showTargetModeInfo = false;

        public RealAntenna antenna { get; set; }

        private readonly List<Vessel> vessels = new List<Vessel>();

        public void Start()
        {
            vessels.Clear();
            vessels.AddRange(FlightGlobals.Vessels);
        }

        public void OnGUI()
        {
            GUI.skin = HighLogic.Skin;
            Window = GUILayout.Window(GetHashCode(), Window, GUIDisplay, GUIName, HighLogic.Skin.window);
        }

        void GUIDisplay(int windowID)
        {
            Vessel parentVessel = (antenna?.ParentNode as RACommNode)?.ParentVessel;

            GUILayout.BeginVertical(HighLogic.Skin.box);
            GUILayout.Label($"Vessel: {parentVessel?.name ?? "None"}");
            GUILayout.Label($"Antenna: {antenna.Name}");
            GUILayout.Label($"Band: {antenna.RFBand.name}       Power: {antenna.TxPower}dBm");
            GUILayout.Label($"Target: {antenna.Target}");
            GUILayout.EndVertical();
            GUILayout.Space(7);

            GUILayout.BeginVertical(HighLogic.Skin.box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Target Mode: {targetMode.displayName}"))
                targetMode = GetNextTargetMode();
            showTargetModeInfo = GUILayout.Toggle(showTargetModeInfo, "ⓘ", HighLogic.Skin.button, GUILayout.ExpandWidth(false), GUILayout.Height(20));
            GUILayout.EndHorizontal();
            if (showTargetModeInfo)
            {
                GUILayout.Label(targetMode.hint, GUILayout.ExpandWidth(true));
                if (GameDatabase.Instance.GetTexture(targetMode.texture, false) is Texture2D tex)
                    GUILayout.Box(tex, GUILayout.Height(200), GUILayout.Width(200));
            }
            GUILayout.EndVertical();
            GUILayout.Space(7);

            GUILayout.BeginVertical(HighLogic.Skin.box);
            var sortIcon = GetSortIcon(sortMode);

            if (targetMode.mode == TargetMode.Vessel)
            {
                GUILayout.BeginHorizontal();
                foreach (var vType in TextureTools.vesselTypes)
                {
                    if (TextureTools.filterTextures.TryGetValue(vType, out Texture2D tex))
                        TextureTools.filterStates[vType] = GUILayout.Toggle(TextureTools.filterStates[vType], tex, HighLogic.Skin.button, GUILayout.Height(iconSize.y), GUILayout.Width(iconSize.x));
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label(targetMode.text, GUILayout.ExpandWidth(true));
                HandleSortMode(sortIcon, parentVessel);
                GUILayout.EndHorizontal();

                scrollVesselPos = GUILayout.BeginScrollView(scrollVesselPos, GUILayout.Height(200), GUILayout.ExpandWidth(true));
                foreach (Vessel v in vessels)
                {
                    if (TextureTools.filterStates.TryGetValue(v.vesselType, out bool show)
                        && show && GUILayout.Button(v.name))
                    {
                        var x = new ConfigNode(AntennaTarget.nodeName);
                        x.AddValue("name", $"{TargetMode.Vessel}");
                        x.AddValue("vesselId", v.id);
                        antenna.Target = AntennaTarget.LoadFromConfig(x, antenna);
                    }
                }
                GUILayout.EndScrollView();
            }
            if (targetMode.mode == TargetMode.BodyCenter)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(targetMode.text, GUILayout.ExpandWidth(true));
                HandleSortMode(sortIcon);
                GUILayout.EndHorizontal();
                scrollBodyPos = GUILayout.BeginScrollView(scrollBodyPos, GUILayout.Height(200), GUILayout.ExpandWidth(true));
                foreach (CelestialBody body in FlightGlobals.Bodies)
                {
                    if (GUILayout.Button(body.name))
                    {
                        var x = new ConfigNode(AntennaTarget.nodeName);
                        x.AddValue("name", $"{AntennaTarget.TargetMode.BodyLatLonAlt}");
                        x.AddValue("bodyName", body.name);
                        x.AddValue("latLonAlt", new Vector3(0, 0, (float)-body.Radius));
                        antenna.Target = AntennaTarget.LoadFromConfig(x, antenna);
                    }
                }
                GUILayout.EndScrollView();
            }
            if (targetMode.mode == TargetMode.BodyLatLonAlt)
            {
                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Lat");
                sLat = GUILayout.TextField(sLat, 4);
                GUILayout.Label("Lon");
                sLon = GUILayout.TextField(sLon, 4);
                GUILayout.Label("Alt");
                sAlt = GUILayout.TextField(sAlt, 15);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label(targetMode.text, GUILayout.ExpandWidth(true));
                HandleSortMode(sortIcon);
                GUILayout.EndHorizontal();
                scrollBodyPos = GUILayout.BeginScrollView(scrollBodyPos, GUILayout.Height(200), GUILayout.ExpandWidth(true));
                foreach (CelestialBody body in FlightGlobals.Bodies)
                {
                    if (GUILayout.Button(body.name))
                    {
                        var x = new ConfigNode(AntennaTarget.nodeName);
                        if (float.TryParse(sLat, out float flat) &&
                            float.TryParse(sLon, out float flon) &&
                            float.TryParse(sAlt, out float falt))
                        {
                            x.AddValue("name", $"{AntennaTarget.TargetMode.BodyLatLonAlt}");
                            x.AddValue("bodyName", body.name);
                            x.AddValue("latLonAlt", new Vector3(flat, flon, falt));
                            antenna.Target = AntennaTarget.LoadFromConfig(x, antenna);
                        }
                    }
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
            if (targetMode.mode == TargetMode.AzEl)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Azimuth");
                sAzimuth = GUILayout.TextField(sAzimuth, 4);
                GUILayout.Label("Elevation");
                sElevation = GUILayout.TextField(sElevation, 4);
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Apply"))
                {
                    var x = new ConfigNode(AntennaTarget.nodeName);
                    if (float.TryParse(sAzimuth, out float azimuth) &&
                        float.TryParse(sElevation, out float elevation))
                    {
                        azimuth = Mathf.Clamp(azimuth, 0, 360);
                        elevation = Mathf.Clamp(elevation, -90, 90);
                        x.AddValue("name", $"{AntennaTarget.TargetMode.AzEl}");
                        x.AddValue("vesselId", parentVessel?.id);
                        x.AddValue("azimuth", azimuth);
                        x.AddValue("elevation", elevation);
                        antenna.Target = AntennaTarget.LoadFromConfig(x, antenna);
                    }
                }

            }
            if (targetMode.mode == TargetMode.OrbitRelative)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Deflection");
                sForward = GUILayout.TextField($"{deflection}", 5);
                GUILayout.Label("Elevation");
                sElevation = GUILayout.TextField(sElevation, 4);
                GUILayout.EndHorizontal();
                float.TryParse(sForward, out deflection);
                deflection = GUILayout.HorizontalSlider(deflection, -180, 180);
                if (GUILayout.Button("Apply"))
                {
                    var x = new ConfigNode(AntennaTarget.nodeName);
                    if (float.TryParse(sElevation, out float elevation))
                    {
                        deflection = Mathf.Clamp(deflection, -360, 360);
                        elevation = Mathf.Clamp(elevation, -90, 90);
                        x.AddValue("name", $"{AntennaTarget.TargetMode.OrbitRelative}");
                        x.AddValue("vesselId", parentVessel?.id);
                        x.AddValue("forward", deflection);
                        x.AddValue("elevation", elevation);
                        antenna.Target = AntennaTarget.LoadFromConfig(x, antenna);
                    }
                }

            }
            GUILayout.EndVertical();
            GUILayout.Space(15);
            if (GUILayout.Button("Close")) Destroy(this);
            GUI.DragWindow();
        }

        public void OnDestroy()
        {
            AntennaTargetManager.Release(antenna, this);
        }

        private Texture2D GetSortIcon(SortMode mode)
        {
            return mode switch
            {
                SortMode.Alphabetical => null,
                SortMode.Distance => GameDatabase.Instance.GetTexture("RealAntennas/Textures/Ruler", false),
                SortMode.ParentBody => null,
                SortMode.VesselType => GameDatabase.Instance.GetTexture("RealAntennas/Textures/Ship", false),
                SortMode.RFBand => GameDatabase.Instance.GetTexture("RealAntennas/Textures/Band", false),
                _ => null
            };
        }

        private void HandleSortMode(Texture2D sortIcon, Vessel parentVessel = null)
        {
            if ((sortIcon is Texture2D && GUILayout.Button(sortIcon, GUILayout.Width(25), GUILayout.Height(25))) ||
                (sortIcon is null && GUILayout.Button($"{sortMode}")))
            {
                sortMode = (SortMode)(((int)(sortMode + 1)) % System.Enum.GetNames(typeof(SortMode)).Length);
                switch (sortMode)
                {
                    case SortMode.Alphabetical: vessels.Sort((x, y) => x.name.CompareTo(y.name)); break;
                    case SortMode.VesselType: vessels.Sort((x, y) => x.vesselType.CompareTo(y.vesselType)); break;
                    case SortMode.ParentBody: vessels.Sort((x, y) => x.mainBody.bodyName.CompareTo(y.mainBody.bodyName)); break;
                    case SortMode.RFBand: vessels.Sort(new RFBandComparer()); break;
                    case SortMode.Distance: vessels.Sort(new DistanceComparer(parentVessel)); break;
                }
            }
        }

        public TargetModeInfo GetNextTargetMode()
        {
            int start = TargetModeInfo.ListAll.IndexOf(targetMode);
            int i = 0;
            int maxIter = TargetModeInfo.ListAll.Count;
            do
            {
                i++;
                int ind = (start + i) % maxIter;
                targetMode = TargetModeInfo.ListAll[ind];
            } while (antenna.TechLevelInfo.Level < targetMode.techLevel && i <= maxIter);
            return targetMode;
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

        private class DistanceComparer : IComparer<Vessel>
        {
            private Vector3d origin;
            public DistanceComparer(Vessel v) => origin = v?.GetWorldPos3D() ?? Vector3d.zero;

            public int Compare(Vessel x, Vessel y)
            {
                return (x.GetWorldPos3D() - origin).sqrMagnitude.CompareTo((y.GetWorldPos3D() - origin).sqrMagnitude);
            }
        }
    }
}
