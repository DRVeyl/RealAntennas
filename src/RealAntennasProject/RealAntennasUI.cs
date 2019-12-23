using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas
{
    [KSPAddon(KSPAddon.Startup.FlightEditorAndKSC, false)]
    internal class RealAntennasUI : MonoBehaviour
    {
        private const string modName = "RealAntennas";
        private const string icon = "RealAntennas/RealAntennas";

        private bool showUI = false;
        private ApplicationLauncherButton button;

        private Rect winPos = new Rect(450, 100, 400, 100);
        private const int winID = 731806;

        protected void Awake()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(OnGuiAppLauncherReady);
        }

        private void Update()
        {
            if (GameSettings.MODIFIER_KEY.GetKey() && Input.GetKeyDown(KeyCode.I))
                showUI = !showUI;
        }

        private void OnGUI()
        {
            if (showUI)
            {
                winPos = GUILayout.Window(winID, winPos, WindowGUI, $"{modName}", GUILayout.MinWidth(200));
            }
        }

        private void WindowGUI(int ID)
        {
            GUILayout.BeginVertical();

            RACommNetScenario scen = RACommNetScenario.Instance as RACommNetScenario;
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
            GUI.DragWindow();
        }

        private void ShowWindow() => showUI = true;
        private void HideWindow() => showUI = false;
        private void OnSceneChange(GameScenes s) => showUI = false;

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
                        ApplicationLauncher.AppScenes.ALWAYS ^ ApplicationLauncher.AppScenes.MAINMENU,
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

        public void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
            if (button != null)
                ApplicationLauncher.Instance.RemoveModApplication(button);
        }
    }
}