using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RealAntennas
{
    public class PlannerGUI : MonoBehaviour
    {
        private const int GUIWidth = 800, GUIHeight = 400;
        private const int SPACING = 20;
        Rect Window = new Rect(250, 100, GUIWidth, GUIHeight);
        private GUIStyle windowStyle, boxStyle, leftButtonStyle;

        private readonly Dictionary<ProtoVessel, List<RealAntenna>> protoVesselAntennaCache = new Dictionary<ProtoVessel, List<RealAntenna>>();
        private readonly string[] distMults = new string[4] { "Km", "Mm", "Gm", "Tm" };
        private readonly List<string> rateStrings = new List<string>();
        private int iTechLevel = 0, currentGroundStationTechLevel;
        private double distanceMax = 1e6, distanceMin = 1e4;
        private string dMin, dMax;
        private int dMaxMultIndex, dMinMultIndex;
        private int bodyIndex = 0;
        private bool showDebugUI = false;
        public bool RequestUpdate { get; set; } = false;

        public RealAntenna primaryAntenna, fixedAntenna;
        public ModuleRealAntenna parentPartModule;
        private GameObject fixedGO, primaryNearGO, primaryFarGO;
        private RACommNode fixedNode, primaryNearNode, primaryFarNode;

        private Vector2 primaryScroller, fixedScroller;
        private Network.ConnectionDebugger connectionDebugger;

        enum SelectionMode { Vessel, GroundStation };
        private SelectionMode primarySelectionMode = SelectionMode.Vessel;
        private SelectionMode fixedSelectionMode = SelectionMode.GroundStation;
        private const string sNoConnection = "<color=orange><b>(No Connection)</b></color>";

        public void Start()
        {
            DiscoverProtoVesselAntennas(protoVesselAntennaCache);
            iTechLevel = RACommNetScenario.GroundStationTechLevel;
            float fTSLvl = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation);
            currentGroundStationTechLevel = Mathf.RoundToInt(RACommNetScenario.MaxTL * (HighLogic.CurrentGame.Mode == Game.Modes.CAREER ? fTSLvl : 1));
            GameEvents.onEditorPodDeleted.Add(OnEditorRestart);
            GameEvents.onEditorRestart.Add(OnEditorRestart);
            windowStyle = new GUIStyle(HighLogic.Skin.window) { alignment = TextAnchor.UpperLeft };
            boxStyle = new GUIStyle(HighLogic.Skin.box) { alignment = TextAnchor.UpperCenter };
            leftButtonStyle = new GUIStyle(HighLogic.Skin.button) { alignment = TextAnchor.MiddleLeft };
            // Terminology: peer antenna is on the left of the GUI, and will move around.
            // Fixed antenna is on the right of the GUI, and will be positioned at/near homeworld surface.

            fixedGO = new GameObject("Planning.Antenna.Fixed");
            primaryNearGO = new GameObject("Planning.Antenna.Near");
            primaryFarGO = new GameObject("Planning.Antenna.Far");
            fixedNode = new RACommNode(fixedGO.transform);
            primaryNearNode = new RACommNode(primaryNearGO.transform);
            primaryFarNode = new RACommNode(primaryFarGO.transform);

            ConvertDistance(distanceMax, out double tMax, out dMaxMultIndex);
            ConvertDistance(distanceMin, out double tMin, out dMinMultIndex);
            dMin = $"{tMin:F3}";
            dMax = $"{tMax:F3}";
            RequestUpdate = true;
        }

        public void OnDestroy()
        {
            fixedGO.DestroyGameObject();
            primaryNearGO.DestroyGameObject();
            primaryFarGO.DestroyGameObject();
            parentPartModule.plannerGUI = null;
            GameEvents.onEditorPodDeleted.Remove(OnEditorRestart);
            GameEvents.onEditorRestart.Remove(OnEditorRestart);
        }

        public void OnEditorRestart() => Destroy(this);

        public void OnGUI()
        {
            GUI.skin = HighLogic.Skin;
            Window = GUILayout.Window(GetHashCode(), Window, GUIDisplay, "Antenna Planning", windowStyle, GUILayout.Width(GUIWidth), GUILayout.Height(GUIHeight));
        }

        void GUIDisplay(int windowID)
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                GUILayout.Label($"Ground Station TechLevel: {currentGroundStationTechLevel:F0}");
                GUILayout.Label($"Ground Station (Planning) TechLevel: {iTechLevel}");
                GUILayout.EndVertical();
                iTechLevel = Mathf.RoundToInt(GUILayout.HorizontalSlider(iTechLevel, 0, RACommNetScenario.MaxTL, GUILayout.Width(150), GUILayout.ExpandWidth(false)));
                if (GUILayout.Button("Apply", GUILayout.ExpandWidth(false)))
                {
                    RACommNetScenario.GroundStationTechLevel = iTechLevel;
                    (RACommNetScenario.Instance as RACommNetScenario).RebuildHomes();
                    fixedAntenna = primaryAntenna;
                    ScreenMessages.PostScreenMessage($"Set Ground Station TL to {iTechLevel} and reset Home antenna to {primaryAntenna.Name}", 2, ScreenMessageStyle.UPPER_CENTER, Color.yellow);
                    RequestUpdate = true;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginVertical("Antenna Selection", boxStyle, GUILayout.ExpandHeight(true));
            GUILayout.Space(SPACING);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical("Primary", boxStyle, GUILayout.Height(200), GUILayout.ExpandHeight(true));
            GUILayout.Space(SPACING);
            if (GUILayout.Button($"{primarySelectionMode}"))
            {
                primarySelectionMode = primarySelectionMode == SelectionMode.Vessel ? SelectionMode.GroundStation : SelectionMode.Vessel;
            }
            if (RenderPanel(primarySelectionMode, ref primaryAntenna, ref primaryScroller, fixedAntenna, leftButtonStyle))
                RequestUpdate = true;

            GUILayout.EndVertical();

            GUILayout.BeginVertical("Peer", boxStyle, GUILayout.Height(200), GUILayout.ExpandHeight(true));
            GUILayout.Space(SPACING);
            if (GUILayout.Button($"{fixedSelectionMode}"))
            {
                fixedSelectionMode = fixedSelectionMode == SelectionMode.Vessel ? SelectionMode.GroundStation : SelectionMode.Vessel;
            }
            if (RenderPanel(fixedSelectionMode, ref fixedAntenna, ref fixedScroller, primaryAntenna, leftButtonStyle))
                RequestUpdate = true;

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            var bodyList = new List<CelestialBody> { Planetarium.fetch.Sun };
            bodyList.AddRange(Planetarium.fetch.Home.orbitingBodies);
            bodyList.AddRange(Planetarium.fetch.Sun.orbitingBodies);
            var bodyNames = (from CelestialBody body in bodyList
                             select body.name).ToArray();

            GUILayout.BeginVertical("Remote Body Presets", boxStyle);
            GUILayout.Space(SPACING);
            var prev = bodyIndex;
            bodyIndex = GUILayout.SelectionGrid(bodyIndex, bodyNames, 3);
            if (bodyIndex != prev)
            {
                var body = bodyIndex < bodyList.Count ? bodyList[bodyIndex] : bodyList.First();
                MinMaxDistance(body, out distanceMin, out distanceMax);
                ConvertDistance(distanceMax, out double tMax, out dMaxMultIndex);
                ConvertDistance(distanceMin, out double tMin, out dMinMultIndex);
                dMin = $"{tMin:F3}";
                dMax = $"{tMax:F3}";
                var homes = RACommNetScenario.GroundStations.Values.Where(x => x.Comm is RACommNode);
                if (GetBestMatchingGroundStation(primaryAntenna, homes) is RealAntenna bestDSNAntenna)
                    fixedAntenna = bestDSNAntenna;
                RequestUpdate = true;
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical("Parameters", boxStyle);
            GUILayout.Space(SPACING);
            GUILayout.Label($"Primary Antenna: {primaryAntenna.ParentNode?.displayName} {primaryAntenna}");
            GUILayout.Label($"Peer Antenna: {fixedAntenna.ParentNode?.displayName} {fixedAntenna}");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Distance Max:");
            dMax = GUILayout.TextArea(dMax, 10, GUILayout.Width(125));
            dMaxMultIndex = GUILayout.SelectionGrid(dMaxMultIndex, distMults, 4, GUILayout.ExpandWidth(false));
            double.TryParse(dMax, out distanceMax);
            distanceMax *= Math.Pow(1e3, dMaxMultIndex + 1);
            GUILayout.Label($"  ({distanceMax})");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Distance Min:");
            dMin = GUILayout.TextArea(dMin, 10, GUILayout.Width(125));
            dMinMultIndex = GUILayout.SelectionGrid(dMinMultIndex, distMults, 4, GUILayout.ExpandWidth(false));
            double.TryParse(dMin, out distanceMin);
            distanceMin *= Math.Pow(1e3, dMinMultIndex + 1);
            GUILayout.Label($"  ({distanceMin})");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            foreach (var s in rateStrings)
                GUILayout.Label(s);

            GUILayout.EndVertical();

            GUILayout.Space(SPACING);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Plan!", GUILayout.ExpandWidth(false)))
                RequestUpdate = true;

            GUILayout.Space(SPACING * 2);
            showDebugUI = GUILayout.Toggle(showDebugUI, "Show Details");
            if (connectionDebugger != null)
                connectionDebugger.showUI = showDebugUI;
            GUILayout.Space(SPACING * 2);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.ExpandWidth(false)))
            {
                Destroy(this);
                gameObject.DestroyGameObject();
            }
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        public void Update()
        {
            if (RequestUpdate)
                FireOnce();
            RequestUpdate = false;
        }

        private bool RenderPanel(SelectionMode mode, ref RealAntenna antenna, ref Vector2 scrollPos, in RealAntenna peer, GUIStyle buttonStyle)
        {
            bool res = false;
            scrollPos = GUILayout.BeginScrollView(scrollPos);
            if (mode == SelectionMode.Vessel)
            {
                if (HighLogic.LoadedSceneIsEditor)
                {
                    ShipConstruct sc = EditorLogic.RootPart.ship;
                    foreach (Part p in sc.Parts)
                        foreach (ModuleRealAntenna mra in p.FindModulesImplementing<ModuleRealAntenna>())
                            if (GUILayout.Button($"{sc.shipName} {mra.RAAntenna.ToStringShort()}", buttonStyle))
                            {
                                antenna = mra.RAAntenna;
                                res = true;
                            }

                    foreach (var x in protoVesselAntennaCache)
                        foreach (RealAntenna ra in x.Value)
                            if (GUILayout.Button($"{x.Key.vesselName} {ra.ToStringShort()}", buttonStyle))
                            {
                                antenna = ra;
                                res = true;
                            }
                }
                foreach (Vessel v in FlightGlobals.Vessels.Where(x => x.Connection?.Comm is RACommNode))
                    foreach (RealAntenna ra in (v.Connection.Comm as RACommNode).RAAntennaList)
                        if (GUILayout.Button($"{v.GetDisplayName()} {ra.ToStringShort()}", buttonStyle))
                        {
                            antenna = ra;
                            res = true;
                        }
            }
            else
            {
                var homes = RACommNetScenario.GroundStations.Values.Where(x => x.Comm is RACommNode);
                if (GetBestMatchingGroundStation(peer, homes) is RealAntenna bestDSNAntenna &&
                    GUILayout.Button($"<color=orange>[Best Station]</color>: {bestDSNAntenna.ToStringShort()}", buttonStyle))
                {
                    antenna = bestDSNAntenna;
                    res = true;
                }
                foreach (Network.RACommNetHome home in homes)
                    foreach (RealAntenna ra in home.Comm.RAAntennaList)
                        if (GUILayout.Button($"{home.nodeName} {ra.ToStringShort()}", buttonStyle))
                        {
                            antenna = ra;
                            res = true;
                        }
            }
            GUILayout.EndScrollView();
            return res;
        }

        public RealAntenna GetBestMatchingGroundStation(RealAntenna peer, IEnumerable<Network.RACommNetHome> stations)
        {
            RealAntenna best = null;
            foreach (var station in stations)
                foreach (RealAntenna ra in station.Comm.RAAntennaList.Where(x => x.Compatible(peer)))
                {
                    best ??= ra;
                    best = (best.Gain > ra.Gain) ? best : ra;
                }
            return best;
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
                        RealAntenna ra = new RealAntennaDigital(part.partInfo.title) { ParentNode = null };
                        ra.LoadFromConfigNode(snap.moduleValues);
                        antennas.Add(ra);
                    }
                }
                if (antennas.Count > 0)
                    dict.Add(pv, antennas);
            }
        }

        private void MinMaxDistance(CelestialBody b, out double min, out double max)
        {
            b ??= Planetarium.fetch.Sun;
            double maxAlt = (b == Planetarium.fetch.Sun) ? 0 : b.orbit.ApA;
            double minAlt = (b == Planetarium.fetch.Sun) ? 0 : b.orbit.PeA;
            double sunDistance = Planetarium.fetch.Home.orbitingBodies.Contains(b) ? 0 : (Planetarium.fetch.Sun.position - Planetarium.fetch.Home.position).magnitude;
            max = maxAlt + sunDistance;
            min = (maxAlt < sunDistance) ? sunDistance - maxAlt : minAlt - sunDistance;
        }

        private void ConvertDistance(in double distance, out double val, out int siSelector)
        {
            if (distance > 1e12) siSelector = 3;
            else if (distance > 1e9) siSelector = 2;
            else if (distance > 1e6) siSelector = 1;
            else siSelector = 0;
            val = distance / Math.Pow(1e3, siSelector + 1);
        }

        private void FireOnce()
        {
            if (!(fixedAntenna is RealAntenna && primaryAntenna is RealAntenna))
                return;
            var fixedAntennaCopy = new RealAntennaDigital(fixedAntenna);
            var peerAntennaCopy = new RealAntennaDigital(primaryAntenna);

            if (fixedAntennaCopy.CanTarget)
            {
                var x = new ConfigNode(Targeting.AntennaTarget.nodeName);
                x.AddValue("name", $"{Targeting.AntennaTarget.TargetMode.BodyLatLonAlt}");
                x.AddValue("bodyName", Planetarium.fetch.Home.name);
                x.AddValue("latLonAlt", new Vector3(0, 0, 1e15f));
                fixedAntennaCopy.Target = Targeting.AntennaTarget.LoadFromConfig(x, fixedAntennaCopy);
            }

            if (peerAntennaCopy.CanTarget)
            {
                var x = new ConfigNode(Targeting.AntennaTarget.nodeName);
                x.AddValue("name", $"{Targeting.AntennaTarget.TargetMode.BodyLatLonAlt}");
                x.AddValue("bodyName", Planetarium.fetch.Home.name);
                x.AddValue("latLonAlt", new Vector3(0, 0, 0));
                peerAntennaCopy.Target = Targeting.AntennaTarget.LoadFromConfig(x, peerAntennaCopy);
            }

            var precompute = new Precompute.Precompute();
            var home = Planetarium.fetch.Home;
            var defaultPos = home.GetWorldSurfacePosition(0, 0, 100);
            var defaultOffset = home.GetWorldSurfacePosition(0, 0, 1e6);
            var defaultDir = (defaultOffset - defaultPos).normalized;
            var offset = fixedNode.isHome ? 1e8 : 0;
            fixedNode.transform.SetPositionAndRotation(defaultPos + offset * defaultDir, Quaternion.identity);
            primaryNearNode.transform.SetPositionAndRotation(defaultPos + (offset + distanceMin) * defaultDir, Quaternion.identity);
            primaryFarNode.transform.SetPositionAndRotation(defaultPos + (offset + distanceMax) * defaultDir, Quaternion.identity);
            fixedNode.precisePosition = fixedNode.position;
            primaryNearNode.precisePosition = primaryNearNode.position;
            primaryFarNode.precisePosition = primaryFarNode.position;
            fixedNode.isHome = fixedAntenna.ParentNode?.isHome ?? false;
            primaryNearNode.isHome = primaryFarNode.isHome = primaryAntenna.ParentNode?.isHome ?? false;
            primaryNearNode.ParentBody = primaryNearNode.isHome ? home : null;
            primaryFarNode.ParentBody = primaryFarNode.isHome ? home : null;
            fixedNode.ParentBody = fixedNode.isHome ? home : null;

            var nodes = new List<CommNet.CommNode> { fixedNode, primaryNearNode };
            var bodies = new List<CelestialBody> { Planetarium.fetch.Home };
            var net = RACommNetScenario.RACN;
            var debugger = net.connectionDebugger;
            // Can't use ??= here, because Unity overrides the == operator for gameObjects and probably Components?
            if (connectionDebugger == null)
                connectionDebugger = new GameObject($"Planning Antenna Debugger: {peerAntennaCopy.Name}").AddComponent<Network.ConnectionDebugger>();
            connectionDebugger.showUI = showDebugUI;
            bool debuggerDisplayState = connectionDebugger.visible.FirstOrDefault().Value;
            connectionDebugger.visible.Clear();
            connectionDebugger.items.Clear();
            fixedNode.RAAntennaList.Clear();
            primaryNearNode.RAAntennaList.Clear();
            primaryFarNode.RAAntennaList.Clear();
            fixedNode.RAAntennaList.Add(fixedAntennaCopy);
            primaryNearNode.RAAntennaList.Add(peerAntennaCopy);
            primaryFarNode.RAAntennaList.Add(peerAntennaCopy);

            fixedAntennaCopy.ParentNode = fixedNode;
            peerAntennaCopy.ParentNode = primaryNearNode;
            connectionDebugger.antenna = peerAntennaCopy;
            net.connectionDebugger = connectionDebugger;

            precompute.Initialize(nodes);
            precompute.DoThings(bodies, nodes, true);
            precompute.SimulateComplete(ref net.connectionDebugger, nodes, log: false);

            nodes = new List<CommNet.CommNode> { fixedNode, primaryFarNode };
            peerAntennaCopy.ParentNode = primaryFarNode;
            net.connectionDebugger = connectionDebugger;

            precompute.Initialize(nodes);
            precompute.DoThings(bodies, nodes, true);
            precompute.SimulateComplete(ref net.connectionDebugger, nodes, log: false);

            net.connectionDebugger = debugger;
            rateStrings.Clear();
            connectionDebugger.visible[fixedAntennaCopy] = debuggerDisplayState;
            if (connectionDebugger.items.TryGetValue(fixedAntennaCopy, out var results))
            {
                int i = 0;
                float[] rates = { 0, 0, 0, 0 };
                foreach (var res in results)
                {
                    int txInd = res.tx == fixedAntennaCopy ? 1 : 0;
                    int index = 2 * (i / 2) + txInd;  // Integer math: 1 / 2 = 0
                    rates[index] = res.dataRate;    // 0 = Near Tx.  3 = Far Rx.
                    i++;
                }
                string sMax = $"Tx/Rx rate at max distance: {RATools.PrettyPrintDataRate(rates[2])}/{RATools.PrettyPrintDataRate(rates[3])}";
                string sMin = $"Tx/Rx rate at min distance: {RATools.PrettyPrintDataRate(rates[0])}/{RATools.PrettyPrintDataRate(rates[1])}";
                if (rates[2] == 0 || rates[3] == 0) sMax += $"  {sNoConnection}";
                if (rates[0] == 0 || rates[1] == 0) sMin += $"  {sNoConnection}";

                rateStrings.Add(sMax);
                rateStrings.Add(sMin);
            }
            else
                rateStrings.Add(sNoConnection);
            RequestUpdate = false;
        }
    }
}
