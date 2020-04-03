using CommNet;
using RealAntennas.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Some Icons made by <a href="https://www.flaticon.com/<?=_('authors/')?>smalllikeart" title="smalllikeart"> smalllikeart</a>

namespace RealAntennas.MapUI
{
    public class RACommNetUI : CommNet.CommNetUI
    {
        private const string icon = "RealAntennas/radio-antenna";
        private const int numCirclePts = 180;
        internal int numCircles = 4;
        internal float ConeOpacity = 1f;

        public Color colorToTarget = XKCDColors.BananaYellow;
        public Color color3dB = XKCDColors.BarbiePink;
        public Color color10dB = XKCDColors.Lavender;
        public bool drawTarget = false;
        public bool drawCone3 = true;
        public bool drawCone10 = true;
        public float lineScaleWidth = 2.5f;

        private readonly List<Vector3> targetPoints = new List<Vector3>();
        private readonly List<Vector3> cone3Points = new List<Vector3>();
        private readonly List<Vector3> cone10Points = new List<Vector3>();
        private readonly List<CommLink> commLinkList = new List<CommLink>();
        private readonly Cone cone3 = new Cone();
        private readonly Cone cone10 = new Cone();
        private readonly Dictionary<CommLink, GameObject> linkRenderers = new Dictionary<CommLink, GameObject>();
        private readonly Dictionary<CommNode, Dictionary<RealAntenna, GameObject>> targetRenderers = new Dictionary<CommNode, Dictionary<RealAntenna, GameObject>>();
        private readonly Dictionary<CommNode, Dictionary<RealAntenna, GameObject>> cone3Renderers = new Dictionary<CommNode, Dictionary<RealAntenna, GameObject>>();
        private readonly Dictionary<CommNode, Dictionary<RealAntenna, GameObject>> cone10Renderers = new Dictionary<CommNode, Dictionary<RealAntenna, GameObject>>();

        public NetUIConfigurationWindow configWindow = null;

        internal enum DrawConesMode { None, Cone2D, Cone3D };
        internal DrawConesMode drawConesMode = DrawConesMode.Cone3D;
        internal enum RadioPerspective { Transmit, Receive };
        internal RadioPerspective linkEndPerspective = RadioPerspective.Transmit;

        protected override void Start()
        {
            base.Start();
            if (!(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedScene == GameScenes.TRACKSTATION))
                return;
            Debug.Log($"[RACN UI] Start() in {HighLogic.LoadedScene}");
            configWindow = gameObject.AddComponent<NetUIConfigurationWindow>();
            configWindow.parent = this;

            if (MapView.fetch is MapView)
            {
                Texture2D defaultTex = GameDatabase.Instance.GetTexture(icon, false);
                foreach (RACommNetHome home in GameObject.FindObjectsOfType<RACommNetHome>())
                {
                    MapUI.GroundStationSiteNode gs = new MapUI.GroundStationSiteNode(home.Comm as RACommNode);
                    SiteNode siteNode = SiteNode.Spawn(gs);
                    Texture2D stationTexture = (GameDatabase.Instance.GetTexture(home.icon, false) is Texture2D tex) ? tex : defaultTex;
                    siteNode.wayPoint.node.SetIcon(Sprite.Create(stationTexture, new Rect(0, 0, stationTexture.width, stationTexture.height), new Vector2(0.5f, 0.5f), 100f));
                    //                    MapView.fetch.siteNodes.Add(SiteNode.Spawn(gs));
                }

//                RAOrbitRenderer.ReplaceOrbitRenderers();
            }
            RATelemetryUpdate.Install();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (configWindow != null)
                Destroy(configWindow);
        }

        private void GatherLinkLines(List<CommLink> linkList)
        {
            foreach (RACommLink link in linkList)
            {
                GameObject go;
                if (!linkRenderers.ContainsKey(link))
                {
                    go = new GameObject("LinkLineRenderer");
                    LineRenderer rend = go.AddComponent<LineRenderer>();
                    InitializeRenderer(rend);
                    linkRenderers.Add(link, go);
                }
                go = linkRenderers[link];
                LineRenderer renderer = go.GetComponent<LineRenderer>();
                renderer.enabled = true;
                Vector3 scaledStart = ScaledSpace.LocalToScaledSpace(link.start.position);
                Vector3 scaledEnd = ScaledSpace.LocalToScaledSpace(link.end.position);
                Camera cam = PlanetariumCamera.Camera;
                Vector3 camPos = cam.transform.position;
                float dStart = Vector3.Distance(camPos, scaledStart);
                float dEnd = Vector3.Distance(camPos, scaledEnd);
                renderer.startWidth = dStart * lineScaleWidth / 1000;
                renderer.endWidth = dEnd * lineScaleWidth / 1000;
                Color startColor = (linkEndPerspective == RadioPerspective.Transmit) ? LinkColor(link.FwdMetric) : LinkColor(link.RevMetric);
                Color endColor = (linkEndPerspective == RadioPerspective.Transmit) ? LinkColor(link.RevMetric) : LinkColor(link.FwdMetric);
                SetColorGradient(renderer, startColor, endColor);
                renderer.positionCount = 2;
                renderer.SetPositions(new Vector3[] { scaledStart, scaledEnd });
            }
        }

        public void GatherAntennaCones(RACommNode node)
        {
            if (node == null || node.RAAntennaList.Count == 0) return;
            CheckRenderers(node);

            foreach (RealAntenna ra in node.RAAntennaList)
            {
                if (ra.CanTarget && ra.Target != null)
                {
                    CheckRenderer(targetRenderers[node], ra);
                    CheckRenderer(cone3Renderers[node], ra);
                    CheckRenderer(cone10Renderers[node], ra);
                    LineRenderer targetRenderer = targetRenderers[node][ra].GetComponent<LineRenderer>();
                    LineRenderer cone3Renderer = cone3Renderers[node][ra].GetComponent<LineRenderer>();
                    LineRenderer cone10Renderer = cone10Renderers[node][ra].GetComponent<LineRenderer>();

                    Vector3d axis = ra.ToTargetByTransform;
                    double len = Math.Min(1e8f, axis.magnitude);
                    if (ra.Target is CelestialBody body)
                        len = Math.Min(len, axis.magnitude - body.Radius);
                    axis.Normalize();
                    axis *= len;
                    cone10.Init(node.position, axis, Vector3.up, ra.Beamwidth);
                    cone3.Init(node.position, axis, Vector3.up, ra.Beamwidth / 2);

                    targetPoints.Clear();
                    cone3Points.Clear();
                    cone10Points.Clear();

                    Camera cam = PlanetariumCamera.Camera;
                    Vector3 camPos = cam.transform.position;

                    if (drawTarget)
                    {
                        targetPoints.Add(node.position);
                        targetPoints.Add(node.position + ra.ToTargetByTransform);
                        ScaledSpace.LocalToScaledSpace(targetPoints);
                        float dStart = Vector3.Distance(camPos, targetPoints[0]);
                        float dEnd = Vector3.Distance(camPos, targetPoints[1]);
                        targetRenderer.startWidth = dStart * lineScaleWidth / 1000;
                        targetRenderer.endWidth = dEnd * lineScaleWidth / 1000;
                        SetColorGradient(targetRenderer, colorToTarget, colorToTarget, ConeOpacity, ConeOpacity);
                    }

                    if (drawCone3 && drawConesMode != DrawConesMode.None)
                    {
                        cone3Points.Add(cone3.end1);
                        cone3Points.Add(cone3.vertex);
                        if (drawConesMode == DrawConesMode.Cone3D)
                            MakeCircles(cone3Points, cone3, numCircles);
                        else
                            cone3Points.Add(cone3.end2);

                        ScaledSpace.LocalToScaledSpace(cone3Points);
                        float dStart = Vector3.Distance(camPos, ScaledSpace.LocalToScaledSpace(cone3.vertex));
                        cone3Renderer.startWidth = dStart * lineScaleWidth / 1000;
                        cone3Renderer.endWidth = dStart * lineScaleWidth / 1000;
                        SetColorGradient(cone3Renderer, color3dB, color3dB, ConeOpacity, ConeOpacity);
                    }

                    if (drawCone10 && drawConesMode != DrawConesMode.None)
                    {
                        cone10Points.Add(cone10.end1);
                        cone10Points.Add(cone10.vertex);
                        if (drawConesMode == DrawConesMode.Cone3D)
                            MakeCircles(cone10Points, cone10, numCircles);
                        else
                            cone10Points.Add(cone10.end2);

                        ScaledSpace.LocalToScaledSpace(cone10Points);
                        float dStart = Vector3.Distance(camPos, ScaledSpace.LocalToScaledSpace(cone10.vertex));
                        cone10Renderer.startWidth = dStart * lineScaleWidth / 1000;
                        cone10Renderer.endWidth = dStart * lineScaleWidth / 1000;
                        SetColorGradient(cone10Renderer, color10dB, color10dB, ConeOpacity, ConeOpacity);
                    }

                    targetRenderer.positionCount = targetPoints.Count;
                    targetRenderer.SetPositions(targetPoints.ToArray());
                    targetRenderer.enabled = drawTarget;

                    cone3Renderer.positionCount = cone3Points.Count;
                    cone3Renderer.SetPositions(cone3Points.ToArray());
                    cone3Renderer.enabled = drawCone3 && drawConesMode != DrawConesMode.None;

                    cone10Renderer.positionCount = cone10Points.Count;
                    cone10Renderer.SetPositions(cone10Points.ToArray());
                    cone10Renderer.enabled = drawCone10 && drawConesMode != DrawConesMode.None;
                }
            }
        }

        private Color LinkColor(double metric)
        {
            if (metric <= 0) return Color.black;
            return Color.Lerp(Color.red, Color.green, Convert.ToSingle(metric));
        }

        private void MakeCircles(List<Vector3> points, Cone cone, int numCircles)
        {
            if (numCircles == 0)        // No circles, just complete the cone.
                points.Add(cone.end2);

            for (int circ = 1; circ <= numCircles; circ++)
            {
                float scale = 1f * circ / numCircles;
                // Traverse to next circle
                points.Add(Vector3.Lerp(cone.vertex, cone.end2, scale));
                DrawCircle(points,
                            Vector3.Lerp(cone.vertex, cone.Midpoint, scale),
                            cone.Midpoint - cone.vertex,
                            Vector3.Lerp(cone.vertex, cone.end2, scale),
                            360,
                            numCirclePts);
            }
        }

        // Given a circle center and its normal, draw an arc through <angle> degrees starting at startPoint on the circle using numPoints vertices
        // Direction is right-hand clockwise (normal cross startPoint)
        private void DrawCircle(List<Vector3> points, Vector3 center, Vector3 normal, Vector3 startPoint, float angle, int numPoints)
        {
            normal.Normalize();
            Vector3 start = startPoint - center;
            Vector3 startDir = start.normalized;
            Vector3 rotateDir = Vector3.Cross(normal, startDir);
            float radius = start.magnitude;

            float step = 4 * (angle / 360) / numPoints;
            for (int i = 0; i < numPoints; i++)
            {
                Vector3 cur = center + radius * Vector3.SlerpUnclamped(startDir, rotateDir, i * step);
                points.Add(cur);
            }
            points.Add(startPoint);
        }

        public class Cone
        {
            public Vector3d vertex, end1, end2;
            public Cone() : this(Vector3d.zero, Vector3d.up, Vector3d.right) { }
            public Cone(Vector3d vertex, Vector3d end1, Vector3d end2)
            {
                this.vertex = vertex;
                this.end1 = end1;
                this.end2 = end2;
            }
            public Cone(Vector3d vertex, Vector3d axis, Vector3d normal, double angle)
            {
                Init(vertex, axis, normal, angle);
            }
            public void Init(Vector3d vertex, Vector3d axis, Vector3d normal, double angle)
            {
                this.vertex = vertex;
                Vector3d perp = Vector3.Cross(axis, normal).normalized;
                float angleRadians = Convert.ToSingle(angle * Math.PI / 180);
                end1 = vertex + Vector3.RotateTowards(axis, perp, angleRadians, 0);
                end2 = vertex + Vector3.RotateTowards(axis, perp, -angleRadians, 0);
            }
            public Vector3d Midpoint => (end1 + end2) / 2;
        }

        protected override void UpdateDisplay()
        {
            //base.UpdateDisplay();
            if (CommNetNetwork.Instance == null) return;
            DisableAllLinkRenderers();
            DisableAllConeRenderers();
            if (CommNetUI.Mode == CommNetUI.DisplayMode.None) return;
            if (this.draw3dLines != MapView.Draw3DLines)
            {
                this.draw3dLines = MapView.Draw3DLines;
                ResetRendererLayer(MapView.Draw3DLines);
            }
            colorToTarget.a = ConeOpacity;
            color3dB.a = ConeOpacity;
            color10dB.a = ConeOpacity;

            if ((CommNetScenario.Instance as RACommNetScenario).Network.CommNet is RACommNetwork commNet)
            {
                if (CommNetUI.Mode == CommNetUI.DisplayMode.Network)
                {
                    foreach (RACommNode node in commNet.Nodes)
                    {
                        GatherLinkLines(node.Values.ToList());
                        GatherAntennaCones(node);
                    }
                }
                if (vessel is Vessel && vessel.Connection is CommNetVessel cnv && 
                    cnv.Comm is RACommNode commNode && cnv.ControlPath is CommPath commPath)
                {
                    switch (CommNetUI.Mode)
                    {
                        case DisplayMode.FirstHop:
                            CommLink cl = commPath.FirstOrDefault();
                            if (cl != null)
                            {
                                commLinkList.Clear();
                                commLinkList.Add(cl.start == commNode ? cl.start[cl.end] : cl.end[cl.start]);
                                GatherLinkLines(commLinkList);
                            }
                            GatherAntennaCones(commNode);
                            break;
                        case CommNetUI.DisplayMode.VesselLinks:
                            GatherLinkLines(commNode.Values.ToList());
                            GatherAntennaCones(commNode);
                            break;
                        case CommNetUI.DisplayMode.Path:
                            if (commPath.Count == 0)
                            {
                                GatherLinkLines(commNode.Values.ToList());
                                GatherAntennaCones(commNode);
                            }
                            foreach (CommLink link in commPath)
                            {
                                commLinkList.Clear();
                                commLinkList.Add(link.start[link.end]);
                                GatherLinkLines(commLinkList);
                                GatherAntennaCones(link.start as RACommNode);
                            }
                            break;
                    }
                }
            }
        }

        private void InitializeRenderer(LineRenderer rend)
        {
            //            rend.material = MapView.DottedLinesMaterial;
            rend.material = this.lineMaterial;
            rend.material.SetTexture("_MainTex", lineTexture);
            ResetRendererLayer(rend, MapView.Draw3DLines);
            rend.receiveShadows = false;
            rend.generateLightingData = false;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.useWorldSpace = true;
            rend.startWidth = 12;
            rend.endWidth = 12;
            rend.startColor = Color.green;
            rend.endColor = Color.red;
            rend.enabled = false;
        }

        public static void SetColorGradient(LineRenderer rend, Color startColor, Color endColor, float startAlpha=1, float endAlpha=1)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(startColor, 0.0f), new GradientColorKey(endColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(startAlpha, 0.0f), new GradientAlphaKey(endAlpha, 1.0f) }
            );
            rend.colorGradient = gradient;
        }

        public override void SwitchMode(int step)
        {
            base.SwitchMode(step);
        }

        public override void Hide()
        {
            DisableAllConeRenderers();
            DisableAllLinkRenderers();
            base.Hide();
        }

        #region Renderer List Maintenance Methods
        private void CheckRenderers(CommNode node)
        {
            if (!cone3Renderers.ContainsKey(node))
                cone3Renderers.Add(node, new Dictionary<RealAntenna, GameObject>());
            if (!cone10Renderers.ContainsKey(node))
                cone10Renderers.Add(node, new Dictionary<RealAntenna, GameObject>());
            if (!targetRenderers.ContainsKey(node))
                targetRenderers.Add(node, new Dictionary<RealAntenna, GameObject>());
        }

        private void CheckRenderer(Dictionary<RealAntenna, GameObject> dict, RealAntenna ra)
        {
            if (!dict.ContainsKey(ra))
            {
                GameObject go = new GameObject("ConeLineRenderer");
                LineRenderer rend = go.AddComponent<LineRenderer>();
                InitializeRenderer(rend);
                dict.Add(ra, go);
            }
        }

        private void DisableAllLinkRenderers()
        {
            foreach (GameObject go in linkRenderers.Values)
                DisableLineRenderer(go);
        }

        private void DisableAllConeRenderers()
        {
            foreach (Dictionary<RealAntenna, GameObject> dict in targetRenderers.Values)
                foreach (GameObject go in dict.Values)
                    DisableLineRenderer(go);

            foreach (Dictionary<RealAntenna, GameObject> dict in cone3Renderers.Values)
                foreach (GameObject go in dict.Values)
                    DisableLineRenderer(go);

            foreach (Dictionary<RealAntenna, GameObject> dict in cone10Renderers.Values)
                foreach (GameObject go in dict.Values)
                    DisableLineRenderer(go);
        }

        private void DisableLineRenderer(GameObject go)
        {
            if (go.GetComponent<LineRenderer>() is LineRenderer rend)
                rend.enabled = false;
            else
                Destroy(go);
        }

        public void GatherAllLinkRenderers(List<GameObject> res)
        {
            foreach (GameObject go in linkRenderers.Values)
            {
                if (go.GetComponent<LineRenderer>() is LineRenderer rend)
                    if (rend.enabled)
                        res.Add(go);
            }
        }

        private void ResetRendererLayer(bool mode3D)
        {
            foreach (GameObject go in linkRenderers.Values)
                if (go.GetComponent<LineRenderer>() is LineRenderer rend)
                    ResetRendererLayer(rend, mode3D);
            foreach (Dictionary<RealAntenna, GameObject> dict in targetRenderers.Values)
                foreach (GameObject go in dict.Values)
                    if (go.GetComponent<LineRenderer>() is LineRenderer rend)
                        ResetRendererLayer(rend, mode3D);
            foreach (Dictionary<RealAntenna, GameObject> dict in cone3Renderers.Values)
                foreach (GameObject go in dict.Values)
                    if (go.GetComponent<LineRenderer>() is LineRenderer rend)
                        ResetRendererLayer(rend, mode3D);
            foreach (Dictionary<RealAntenna, GameObject> dict in cone10Renderers.Values)
                foreach (GameObject go in dict.Values)
                    if (go.GetComponent<LineRenderer>() is LineRenderer rend)
                        ResetRendererLayer(rend, mode3D);
        }

        private void ResetRendererLayer(LineRenderer rend, bool mode3D)
        {
            //rend.material = mode3D ? MapView.DottedLinesMaterial : new Material(Shader.Find("Sprites/Default"));
            rend.gameObject.layer = mode3D ? 31 : 10;
        }

        #endregion
    }
}
