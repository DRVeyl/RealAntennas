using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using KSP.Localization;


namespace RealAntennas.Network
{
    public class ConnectionDebugger : MonoBehaviour
    {
        string GUIName = Local.CDB_ConnectionDebugger;//"Connection Debugger";
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
            GUILayout.Label($"{Local.Gerneric_Vessel}: {parentVessel?.GetDisplayName() ?? Local.Gerneric_None}");  // Vessel  |  "None"
            GUILayout.Label($"{Local.Gerneric_Antenna}: {antenna.Name}");  // Antenna
            GUILayout.Label($"{Local.Gerneric_Band}: {antenna.RFBand.name}       {Local.Gerneric_Power}: {antenna.TxPower}dBm");  // Band  |  Power
            if (antenna.CanTarget)
                GUILayout.Label($"{Local.Gerneric_Target}: {antenna.Target}");  // Target
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
                        GUILayout.BeginVertical(Local.CDB_Transmitter, style, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));  // Transmitter
                        GUILayout.Label($"{Local.Gerneric_Antenna}: {data.tx.Name}");  // Antenna
                        GUILayout.Label($"{Local.Gerneric_Power}: {data.txPower}dBm"); // Power
                        GUILayout.Label($"{Local.Gerneric_Target}: {data.tx.Target}"); // Target
                        GUILayout.Label($"{Local.Gerneric_Position}: {data.txPos.x:F0}, {data.txPos.y:F0}, {data.txPos.z:F0}"); // Position
                        GUILayout.Label($"{Local.Gerneric_Beamwidth}: {data.txBeamwidth:F2}");  // Beamwidth (3dB full-width)
                        GUILayout.Label($"{Local.Gerneric_AntennaAoA}: {data.txToRxAngle:F1}");  // Antenna AoA
                        GUILayout.EndVertical();
                        // Display Rx box
                        GUILayout.BeginVertical(Local.CDB_Receiver, style, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));  // "Receiver"
                        GUILayout.Label($"{Local.Gerneric_Antenna}: {data.rx.Name}");  //Antenna
                        GUILayout.Label($"{Local.Gerneric_ReceivedPower}: {data.rxPower}dBm");  // Received Power
                        GUILayout.Label($"{Local.Gerneric_Target}: {data.rx.Target}");  // Target
                        GUILayout.Label($"{Local.Gerneric_Position}: {data.rxPos.x:F0}, {data.rxPos.y:F0}, {data.rxPos.z:F0}");  // Position
                        GUILayout.Label($"{Local.Gerneric_Beamwidth}: {data.rxBeamwidth:F2}");  // Beamwidth (3dB full-width)
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"{Local.Gerneric_AntennaAoA}: {data.rxToTxAngle:F1}");  // Antenna AoA
                        GUILayout.Space(20);
                        GUILayout.Label($"{Local.Gerneric_AntennaElevation}: {data.antennaElevation:F1}");  // Antenna Elevation
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(5);
                        // Display common stats
                        GUILayout.BeginHorizontal(HighLogic.Skin.box);

                        GUILayout.BeginVertical(Local.CDB_Noise, style, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));  // "Noise"
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"{Local.CDB_Atmosphere}: {data.atmosphereNoise:F0}K");  // Atmosphere
                        GUILayout.Space(20);
                        GUILayout.Label($"{Local.CDB_Body}: {data.bodyNoise:F0}K");  // Body
                        GUILayout.Space(20);
                        GUILayout.Label($"{Local.CDB_Receiver}: {data.noiseTemp:F0}K"); // Receiver
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"{Local.CDB_TotalNoise}: {data.noise:F2}K");  // Total Noise
                        GUILayout.Space(20);
                        GUILayout.Label($"N0: {data.N0:F2}dB/Hz");
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();

                        GUILayout.BeginVertical(Local.CDB_Losses, style, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));  // "Losses"
                        GUILayout.Label($"{Local.CDB_PathLoss}: {RATools.PrettyPrint(math.length(data.txPos - data.rxPos))}m ({data.pathLoss:F1}dB)");  // Path Loss
                        GUILayout.Label($"{Local.CDB_PointingLoss}: {data.pointingLoss:F1}dB  (Tx: {data.txPointLoss:F1}dB + Rx:  {data.rxPointLoss:F1}dB)"); // Pointing Loss
                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();
                        GUILayout.Space(5);

                        style.alignment = TextAnchor.UpperRight;
                        var encoder = data.tx.Encoder.BestMatching(data.rx.Encoder);
                        var channelNoise = RATools.LogScale(data.minSymbolRate) + data.N0;
                        GUILayout.BeginVertical(Local.CDB_LinkBudget, style);  // "Link Budget"
                        GUILayout.Label(Localizer.Format("#RA_CDB_RxPower", $"{data.tx.Gain:F1}", $"{data.txPower:F1}", $"{data.pathLoss + data.pointingLoss:F1}", $"{data.rx.Gain:F1}", $"{data.rxPower:F1}")); //$"RxPower = TxGain ({data.tx.Gain:F1} dBi) + TxPower ({data.txPower:F1} dBm) - Losses ({(data.pathLoss + data.pointingLoss):F1} dB) + RxGain ({data.rx.Gain:F1} dBi) = {data.rxPower:F1} dBm"
                        GUILayout.Label(Localizer.Format("#RA_CDB_MLCNP", $"{data.N0:F1}", RATools.PrettyPrint(data.minSymbolRate), $"{RATools.LogScale(data.minSymbolRate):F1}", $"{channelNoise:F1}"));  // $"Min Link Channel Noise Power = N0 ({data.N0:F1} dBm/Hz) * Bandwidth ({RATools.PrettyPrint(data.minSymbolRate)}Hz ({RATools.LogScale(data.minSymbolRate):F1} dB)) = {channelNoise:F1} dB"
                        GUILayout.Label($"{Local.CDB_Encoder}: {encoder}");  // Encoder
                        GUILayout.Label(Localizer.Format("#RA_CDB_MLeb", $"{data.rxPower:F1}", $"{channelNoise:F1}", $"{encoder.RequiredEbN0:F1}", $"{data.rxPower - channelNoise - encoder.RequiredEbN0:F1}"));  // $"Min Link Eb/N0 = RxPower ({data.rxPower:F1} dBm) - Channel Noise Power ({channelNoise:F1} dBm) - Margin ({encoder.RequiredEbN0:F1} dB) = {data.rxPower - channelNoise - encoder.RequiredEbN0:F1}"
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"{Local.CDB_AchievedRate}: {data.dataRate:F1}");  // Achieved Rate
                        GUILayout.Space(20);
                        GUILayout.Label($"{Local.CDB_ValidRates}: {data.minDataRate:F1} - {data.maxDataRate:F1}");  // Valid Rates
                        GUILayout.Space(20);
                        GUILayout.Label($"{Local.CDB_RateSteps}: {data.rateSteps}");  // Steps
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
            if (GUILayout.Button(Local.Gerneric_Close, GUILayout.ExpandWidth(true)))  // "Close"
            {
                Destroy(this);
                gameObject.DestroyGameObject();
            }
            GUI.DragWindow();
        }
    }
}
