using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace RealAntennas.MapUI
{
    public class NetUIConfigurationWindow : MonoBehaviour
    {
        private Rect winPos = new Rect(Screen.width - 410, Screen.height - 250, 400, 100);
        private const int winID = 904011;
        public bool showUI = false;
        public const string ModTag = "[RealAntennas.NetUIConfigurationWindow]";

        internal RACommNetUI parent;
        private float coneOpacity = 1;
        private float fCircles = 4;
        private readonly List<GameObject> renderers = new List<GameObject>();

        private void Start()
        {
            GameEvents.OnMapExited.Add(HideWindow);
        }
        private void OnDestroy()
        {
            GameEvents.OnMapExited.Remove(HideWindow);
        }

        private void OnGUI()
        {
            if (showUI)
            {
                winPos = GUILayout.Window(winID, winPos, WindowGUI, $"{ModTag}", GUILayout.MinWidth(200));
            }
        }

        private void WindowGUI(int ID)
        {
            GUILayout.BeginVertical();
            GUILayout.Label($"{RACommNetScenario.assembly.GetName().Name} v{RACommNetScenario.info.FileVersion}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"ConeMode: {parent.drawConesMode}"))
            {
                parent.drawConesMode++;
                parent.drawConesMode = (RACommNetUI.DrawConesMode) ((int)parent.drawConesMode % Enum.GetValues(typeof(RACommNetUI.DrawConesMode)).Length);
            }
            if (GUILayout.Button($"Link End Mode: {parent.linkEndPerspective}"))
            {
                parent.linkEndPerspective++;
                parent.linkEndPerspective = (RACommNetUI.RadioPerspective)((int)parent.linkEndPerspective % Enum.GetValues(typeof(RACommNetUI.RadioPerspective)).Length);
            }
//            GUILayout.Label($"MapView.Draw3D: {MapView.Draw3DLines}", GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            /*
            if (GUILayout.Button($"3D distance mode: {RAOrbitRenderer.distanceType}"))
            {
                RAOrbitRenderer.distanceType = !RAOrbitRenderer.distanceType;
            }
            */

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"TargetLine: {parent.drawTarget}"))
                parent.drawTarget = !parent.drawTarget;
            if (GUILayout.Button($"3dB Cones: {parent.drawCone3}"))
                parent.drawCone3 = !parent.drawCone3;
            if (GUILayout.Button($"10dB Cones: {parent.drawCone10}"))
                parent.drawCone10 = !parent.drawCone10;
            GUILayout.EndHorizontal();


            GUILayout.BeginVertical();
            GUILayout.Label($"3D Drawing Distance {MapView.MapCamera.Distance:F0}, Max: {MapView.fetch.max3DlineDrawDist:F1}");
            MapView.fetch.max3DlineDrawDist = GUILayout.HorizontalSlider(MapView.fetch.max3DlineDrawDist, 100, 1e5f);
            GUILayout.EndVertical();

            /*
            List<Camera> orbitCamList = FindCamerasByLayer(31);
            List<Camera> mapUICamList = FindCamerasByLayer(5);
            Camera lastOrbitCam = null;
            Camera lastNodeCam = null;
            foreach (Camera camera in orbitCamList)
            {
                GUILayout.Label($"L31: {camera.name} @{camera.transform.position} Rot {camera.transform.rotation}");
                lastOrbitCam = camera;
            }
            foreach (Camera camera in mapUICamList)
            {
                GUILayout.Label($"L5: {camera.name} @{camera.transform.position} Rot {camera.transform.rotation}");
                lastNodeCam = camera;
            }
            if (lastOrbitCam is Camera && lastNodeCam is Camera)
            {
                Debug.Log($"Adjusting rotation of node camera!");
                lastNodeCam.transform.rotation = lastOrbitCam.transform.rotation;
            }

            List<Camera> activeCameras = GameObject.FindObjectsOfType<Camera>().Where(x => x.enabled).ToList();
            foreach (Camera camera in activeCameras)
            {
                string names = string.Empty;
                string s = string.Empty;
                for (int i=0;i<33;i++)
                {
                    if ((camera.cullingMask >> i) % 2 != 0)
                    {
                        names += $"{LayerMask.LayerToName(i)},";
                        s += $"{i},";
                        if (i == 31)
                            camera.transform.SetPositionAndRotation(Vector3.zero, camera.transform.rotation);
                    }
                }
                GUILayout.Label($"{camera.name} @{camera.transform.position} layers:{s} planes {camera.nearClipPlane:F0} / {camera.farClipPlane:F0} ortho:{camera.orthographic} mask:{camera.cullingMask:X} layers: {names}");
            }
            */
            /*
            if (PlanetariumCamera.Camera.enabled)
                GUILayout.Label($"Planetarium: {PlanetariumCamera.Camera.name }@{PlanetariumCamera.Camera.transform.position} Mask: {PlanetariumCamera.Camera.cullingMask:X}");
            if (MapView.fetch.VectorCamera.enabled)
                GUILayout.Label($"MapView {MapView.fetch.VectorCamera.name} @{MapView.fetch.VectorCamera.transform.position} Mask: {MapView.fetch.VectorCamera.cullingMask:X}");
            */
            /*
            ScaledSpace ss = ScaledSpace.Instance;
            GUILayout.Label($"{ss} @{ss.transform.position} target {ss.originTarget} {ss.originTarget.position}");
            

            Transform trans = PlanetariumCamera.fetch.GetCameraTransform();
            PlanetariumCamera planCam = PlanetariumCamera.fetch;
            GUILayout.Label($"Target: {planCam.target} @{planCam.target.transform.position}");
            float dist = (planCam.target.transform.position - trans.position).magnitude;
            GUILayout.Label($"Distance: {planCam.minDistance:F0} / {dist:F0} / {planCam.maxDistance:F0}");
            if (planCam.target.celestialBody is CelestialBody body)
            {
                RAOrbitRenderer rend = body.orbitDriver.Renderer as RAOrbitRenderer;
                List<Vector3> pts = rend.orbitPoints;
                GUILayout.Label($"Dist1: {(pts[0] - trans.position).magnitude}");
                GUILayout.Label($"Dist2: {(pts[pts.Count/8] - trans.position).magnitude}");
                GUILayout.Label($"Dist3: {(pts[pts.Count / 4] - trans.position).magnitude}");
                GUILayout.Label($"Dist4: {(pts[3*pts.Count / 8] - trans.position).magnitude}");
            }
            */


            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUILayout.Label($"Link Line Brightness: {parent.lineScaleWidth:F1}");
            parent.lineScaleWidth = GUILayout.HorizontalSlider(parent.lineScaleWidth, 1, 10);
            GUILayout.EndVertical();
            /*
            GUILayout.BeginVertical();
            GUILayout.Label($"Orbit Line Brightness: {RAOrbitRenderer.orbitLineScale:F1}");
            RAOrbitRenderer.orbitLineScale = GUILayout.HorizontalSlider(RAOrbitRenderer.orbitLineScale, 1, 10);
            GUILayout.EndVertical();
            */
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("Cone Circles");
            fCircles = GUILayout.HorizontalSlider(Convert.ToInt32(fCircles), 0, 8);
            parent.numCircles = Convert.ToInt32(fCircles);
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Label("Cone Opacity");
            coneOpacity = GUILayout.HorizontalSlider(coneOpacity, 0, 1);
            parent.ConeOpacity = Mathf.Clamp01(coneOpacity);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            /*
            renderers.Clear();
            parent.GatherAllLinkRenderers(renderers);
            GUILayout.Label($"Link Renderers {CommNet.CommNetUI.Mode}: {renderers.Count}");
            */
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        public void ShowWindow() => showUI = true;
        public void HideWindow() => showUI = false;
        private List<Camera> FindCamerasByLayer(int layer, bool active=true)
        {
            List<Camera> res = new List<Camera>();
            foreach (Camera cam in GameObject.FindObjectsOfType<Camera>())
            {
                if ((cam.cullingMask >> layer) % 2 != 0)
                {
                    if (!active || cam.enabled == active)
                        res.Add(cam);
                }
            }
            return res;
        }
    }
}
