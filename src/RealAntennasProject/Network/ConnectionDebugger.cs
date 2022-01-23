using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;


namespace RealAntennas.Network
{
    public class ConnectionDebugger : MonoBehaviour
    {
        const string GUIName = "Connection Debugger";
        public Dictionary<RealAntenna, List<LinkDetails>> items = new Dictionary<RealAntenna, List<LinkDetails>>();
        public Dictionary<RealAntenna, bool> visible = new Dictionary<RealAntenna, bool>();
        public RealAntenna antenna;
        public bool showUI = true;
        private Rect Window = new Rect(120, 120, 900, 900);
        private Vector2 scrollPos;

        public void Start()
        {
            if (!(antenna is RealAntenna))
            {
                Debug.LogError("ConnectionDebugger started, but nothing requested for debug!");
                Destroy(this);
                gameObject.DestroyGameObject();
            }
            else
            {
                ScreenMessages.PostScreenMessage($"Debugging {antenna}", 2, ScreenMessageStyle.UPPER_CENTER, Color.yellow);
                RACommNetScenario.RACN.connectionDebugger = this;
            }
        }
        public void OnGUI()
        {
            if (showUI)
            {
                GUI.skin = HighLogic.Skin;
                Window = GUILayout.Window(GetHashCode(), Window, GUIDisplay, GUIName, HighLogic.Skin.window);
            }
        }
        private void GUIDisplay(int windowID)
        {
            Vessel parentVessel = (antenna?.ParentNode as RACommNode)?.ParentVessel;
            var style = new GUIStyle(HighLogic.Skin.box);

            GUILayout.BeginVertical(HighLogic.Skin.box);
            GUILayout.Label($"Vessel: {parentVessel?.GetDisplayName() ?? "None"}");
            GUILayout.Label($"Antenna: {antenna.Name}");
            GUILayout.Label($"Band: {antenna.RFBand.name}       Power: {antenna.TxPower}dBm");
            if (antenna.CanTarget)
                GUILayout.Label($"Target: {antenna.Target}");
            GUILayout.EndVertical();
            GUILayout.Space(7);

            GUILayout.BeginVertical(HighLogic.Skin.box);
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            foreach (var item in items)
            {
                if (!visible.ContainsKey(item.Key))
                    visible[item.Key] = false;
                bool mode = visible[item.Key];
                mode = GUILayout.Toggle(mode, $"{item.Key.ParentNode?.displayName}:{item.Key}", HighLogic.Skin.button, GUILayout.ExpandWidth(true), GUILayout.Height(20));
                visible[item.Key] = mode;
                if (mode)
                {
                    GUILayout.BeginVertical(HighLogic.Skin.box);
                    foreach (var data in item.Value)
                    {
                        // Display Tx and Rx relevant boxes side-by-side.
                        GUILayout.BeginHorizontal(HighLogic.Skin.box);

                        // Display Tx box
                        style.alignment = TextAnchor.UpperRight;
                        GUILayout.BeginVertical("Transmitter", style, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                        GUILayout.Label($"Antenna: {data.tx.Name}");
                        GUILayout.Label($"Power: {data.txPower}dBm");
                        GUILayout.Label($"Target: {data.tx.Target}");
                        GUILayout.Label($"Position: {data.txPos.x:F0}, {data.txPos.y:F0}, {data.txPos.z:F0}");
                        GUILayout.Label($"Beamwidth (3dB full-width): {data.txBeamwidth:F2}");
                        GUILayout.Label($"Antenna AoA: {data.txToRxAngle:F1}");
                        GUILayout.EndVertical();
                        // Display Rx box
                        GUILayout.BeginVertical("Receiver", style, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                        GUILayout.Label($"Antenna: {data.rx.Name}");
                        GUILayout.Label($"Received Power: {data.rxPower}dBm");
                        GUILayout.Label($"Target: {data.rx.Target}");
                        GUILayout.Label($"Position: {data.rxPos.x:F0}, {data.rxPos.y:F0}, {data.rxPos.z:F0}");
                        GUILayout.Label($"Beamwidth (3dB full-width): {data.rxBeamwidth:F2}");
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"Antenna AoA: {data.rxToTxAngle:F1}");
                        GUILayout.Space(20);
                        GUILayout.Label($"Antenna Elevation: {data.antennaElevation:F1}");
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(5);
                        // Display common stats
                        GUILayout.BeginHorizontal(HighLogic.Skin.box);

                        GUILayout.BeginVertical("Noise", style, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"Atmosphere: {data.atmosphereNoise:F0}K");
                        GUILayout.Space(20);
                        GUILayout.Label($"Body: {data.bodyNoise:F0}K");
                        GUILayout.Space(20);
                        GUILayout.Label($"Receiver: {data.noiseTemp:F0}K");
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"Total Noise: {data.noise:F2}K");
                        GUILayout.Space(20);
                        GUILayout.Label($"N0: {data.N0:F2}dB/Hz");
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();

                        GUILayout.BeginVertical("Losses", style, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                        GUILayout.Label($"Path Loss: {RATools.PrettyPrint(math.length(data.txPos - data.rxPos))}m ({data.pathLoss:F1}dB)");
                        GUILayout.Label($"Pointing Loss: {data.pointingLoss:F1}dB  (Tx: {data.txPointLoss:F1}dB + Rx:  {data.rxPointLoss:F1}dB)");
                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();
                        GUILayout.Space(5);

                        style.alignment = TextAnchor.UpperRight;
                        var encoder = data.tx.Encoder.BestMatching(data.rx.Encoder);
                        var channelNoise = RATools.LogScale(data.minSymbolRate) + data.N0;
                        GUILayout.BeginVertical("Link Budget", style);
                        GUILayout.Label($"RxPower = TxGain ({data.tx.Gain:F1} dBi) + TxPower ({data.txPower:F1} dBm) - Losses ({(data.pathLoss + data.pointingLoss):F1} dB) + RxGain ({data.rx.Gain:F1} dBi) = {data.rxPower:F1} dBm");
                        GUILayout.Label($"Min Link Channel Noise Power = N0 ({data.N0:F1} dBm/Hz) * Bandwidth ({RATools.PrettyPrint(data.minSymbolRate)}Hz ({RATools.LogScale(data.minSymbolRate):F1} dB)) = {channelNoise:F1} dB");
                        GUILayout.Label($"Encoder: {encoder}");
                        GUILayout.Label($"Min Link Eb/N0 = RxPower ({data.rxPower:F1} dBm) - Channel Noise Power ({channelNoise:F1} dBm) - Margin ({encoder.RequiredEbN0:F1} dB) = {data.rxPower - channelNoise - encoder.RequiredEbN0:F1}");
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"Achieved Rate: {data.dataRate:F1}");
                        GUILayout.Space(20);
                        GUILayout.Label($"Valid Rates: {data.minDataRate:F1} - {data.maxDataRate:F1}");
                        GUILayout.Space(20);
                        GUILayout.Label($"Steps: {data.rateSteps}");
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                        GUILayout.Space(12);
                    }
                    GUILayout.EndVertical();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.Space(9);
            if (GUILayout.Button("Close", GUILayout.ExpandWidth(true)))
            {
                Destroy(this);
                gameObject.DestroyGameObject();
            }
            GUI.DragWindow();
        }
    }
}
