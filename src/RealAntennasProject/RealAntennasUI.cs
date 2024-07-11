using ClickThroughFix;
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    internal class RealAntennasUI : MonoBehaviour
    {
        private const string modName = "RealAntennas";
        private const string icon = "RealAntennas/RealAntennas";

        private bool showUI = false;
        private ApplicationLauncherButton button;

        private Rect winPos = new Rect(450, 100, 400, 100);
        private GameObject antennaConsoleGO = null;
        private GameObject netUIConfigWindowGO = null;

        protected void Awake()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(OnGuiAppLauncherReady);
            GameEvents.OnMapExited.Add(OnMapExit);
        }

        public void OnDestroy()
        {
            GameEvents.OnMapExited.Remove(OnMapExit);
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
            if (button != null)
            {
                GameEvents.onGameSceneLoadRequested.Remove(OnSceneChange);
                ApplicationLauncher.Instance.RemoveModApplication(button);
            }
        }

        public void OnMapExit()
        {
            if (netUIConfigWindowGO is GameObject)
            {
                Destroy(netUIConfigWindowGO.GetComponent<MapUI.NetUIConfigurationWindow>());
                netUIConfigWindowGO.DestroyGameObject();
                netUIConfigWindowGO = null;
            }
        }

        public void Update()
        {
            if (GameSettings.MODIFIER_KEY.GetKey() && Input.GetKeyDown(KeyCode.I))
                showUI = !showUI;
        }

        public void OnGUI()
        {
            if (showUI)
            {
                winPos = ClickThruBlocker.GUILayoutWindow(GetHashCode(), winPos, WindowGUI, modName, GUILayout.MinWidth(200));
            }
        }

        private void WindowGUI(int ID)
        {
            GUILayout.BeginVertical();
            RACommNetScenario scen = RACommNetScenario.Instance as RACommNetScenario;
            VesselCounts(out int vessels, out int groundStations, out int antennas, out string net);
            GUILayout.Label($"{RACommNetScenario.assembly.GetName().Name} v{RACommNetScenario.info.FileVersion}");
            GUILayout.Label($"{net}");

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Vessels: {vessels}");
            GUILayout.Label($"GroundStations: {groundStations}");
            GUILayout.Label($"Antennas/vessel: {(float)antennas / vessels:F1}");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.ExpandWidth(true));
            GUILayout.Label("Iterations", GUILayout.ExpandWidth(true));
            GUILayout.Label("Avg Time (ms)", GUILayout.ExpandWidth(true));
            GUILayout.Label("Runs/sec", GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            foreach (KeyValuePair<string, MetricsElement> kvp in scen.metrics.data)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{kvp.Key}", GUILayout.ExpandWidth(true));
                GUILayout.Label($"{kvp.Value.iterations}");
                GUILayout.Label($"{kvp.Value.hysteresisTime:F4}");
                GUILayout.Label($"{kvp.Value.iterations / Time.timeSinceLevelLoad:F1}");
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            if (MapView.fetch is MapView && MapView.MapIsEnabled)
            {
                if (netUIConfigWindowGO is GameObject && GUILayout.Button("Hide Config Window"))
                {
                    Destroy(netUIConfigWindowGO.GetComponent<MapUI.NetUIConfigurationWindow>());
                    netUIConfigWindowGO.DestroyGameObject();
                    netUIConfigWindowGO = null;
                }
                else if (netUIConfigWindowGO == null && GUILayout.Button("Show Config Window"))
                {
                    netUIConfigWindowGO = new GameObject("RealAntennas.NetUIConfigWindow");
                    netUIConfigWindowGO.AddComponent<MapUI.NetUIConfigurationWindow>();
                }
            }

            if (antennaConsoleGO is null && GUILayout.Button("Launch Control Console"))
            {
                antennaConsoleGO = new GameObject();
                antennaConsoleGO.AddComponent<Targeting.RemoteAntennaControlUI>();
            } else if (antennaConsoleGO is GameObject && GUILayout.Button("Close Control Console")) {
                antennaConsoleGO.DestroyGameObject();
                antennaConsoleGO = null;
            }
            GUI.DragWindow();
        }

        private void ShowWindow() => showUI = true;
        private void HideWindow() => showUI = false;
        private void OnSceneChange(GameScenes s) => showUI = false;

        private void VesselCounts(out int vessels, out int groundStations, out int antennas, out string net)
        {
            vessels = groundStations = antennas = 0;
            if (RACommNetScenario.RACN is RACommNetwork racn)
            {
                net = $"{racn}";
                foreach (RACommNode node in racn.Nodes)
                {
                    if (node.isHome)
                    {
                        groundStations++;
                    }
                    else
                    {
                        vessels++;
                        antennas += node.RAAntennaList.Count;
                    }
                }
            }
            else
            {
                net = string.Empty;
            }
        }

        private void OnGuiAppLauncherReady()
        {
            if (HighLogic.CurrentGame.Parameters.CustomParams<RAParameters>().performanceUI)
            {
                try
                {
                    button = ApplicationLauncher.Instance.AddModApplication(
                        ShowWindow,
                        HideWindow,
                        null,
                        null,
                        null,
                        null,
                        ApplicationLauncher.AppScenes.ALWAYS & ~ApplicationLauncher.AppScenes.MAINMENU,
                        GameDatabase.Instance.GetTexture($"{icon}", false));
                    GameEvents.onGameSceneLoadRequested.Add(OnSceneChange);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{modName} failed to register button");
                    Debug.LogException(ex);
                }
            }
        }
    }
}