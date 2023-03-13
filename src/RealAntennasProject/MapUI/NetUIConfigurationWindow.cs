using System;
using UnityEngine;
using KSP.Localization;
namespace RealAntennas.MapUI
{
    public class NetUIConfigurationWindow : MonoBehaviour
    {
        private Rect winPos = new Rect(Screen.width - 410, Screen.height - 250, 400, 100);
        public const string ModTag = "[RealAntennas.NetUIConfigurationWindow]";

        public void OnGUI()
        {
            winPos = GUILayout.Window(GetHashCode(), winPos, WindowGUI, ModTag, GUILayout.MinWidth(200));
        }

        private void WindowGUI(int ID)
        {
            var settings = RACommNetScenario.MapUISettings;
            GUILayout.BeginVertical();
            GUILayout.Label($"{RACommNetScenario.assembly.GetName().Name} v{RACommNetScenario.info.FileVersion}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"{Local.NetUI_ConeMode}: {settings.drawConesMode}"))  // ConeMode
            {
                settings.drawConesMode++;
                settings.drawConesMode = (RACommNetUI.DrawConesMode) ((int)settings.drawConesMode % Enum.GetValues(typeof(RACommNetUI.DrawConesMode)).Length);
            }
            if (GUILayout.Button($"{Local.NetUI_LinkEndMode}: {settings.radioPerspective}"))  // Link End Mode
            {
                settings.radioPerspective++;
                settings.radioPerspective = (RACommNetUI.RadioPerspective)((int)settings.radioPerspective % Enum.GetValues(typeof(RACommNetUI.RadioPerspective)).Length);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"{Local.NetUI_TargetLine}: {settings.drawTarget}"))  // TargetLine
                settings.drawTarget = !settings.drawTarget;
            if (GUILayout.Button($"{Local.NetUI_3dBCones}: {settings.drawCone3}")) // 3dB Cones
                settings.drawCone3 = !settings.drawCone3;
            if (GUILayout.Button($"{Local.NetUI_10dBCones}: {settings.drawCone10}")) // 10dB Cones
                settings.drawCone10 = !settings.drawCone10;
            GUILayout.EndHorizontal();

            if (MapView.fetch is MapView && MapView.MapCamera is PlanetariumCamera)
            {
                GUILayout.BeginVertical();
                GUILayout.Label(Localizer.Format("#RA_NetUI_DrawingDistance", $"{MapView.MapCamera.Distance:F0}", $"{MapView.fetch.max3DlineDrawDist:F1}"));  // $"3D Drawing Distance {MapView.MapCamera.Distance:F0}, Max: {MapView.fetch.max3DlineDrawDist:F1}"
                MapView.fetch.max3DlineDrawDist = GUILayout.HorizontalSlider(MapView.fetch.max3DlineDrawDist, 100, 1e5f);
                GUILayout.EndVertical();
            }

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUILayout.Label($"{Local.NetUI_LinkLineBrightness}: {settings.lineScaleWidth:F1}");  // Link Line Brightness
            settings.lineScaleWidth = GUILayout.HorizontalSlider(settings.lineScaleWidth, 1, 10);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label(Local.NetUI_ConeCircles);  // "Cone Circles"
            settings.coneCircles = Convert.ToInt32(GUILayout.HorizontalSlider(settings.coneCircles, 0, 8));
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Label(Local.NetUI_ConeOpacity);  // "Cone Opacity"
            settings.coneOpacity = GUILayout.HorizontalSlider(settings.coneOpacity, 0, 1);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow();
        }
    }
}
