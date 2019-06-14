using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas
{
    public class ModuleRealAntenna : ModuleDataTransmitter
    {
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Gain", guiUnits = " dBi", guiFormat = "F1")]
        public double Gain;          // Physical directionality, measured in dBi

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Transmit Power", guiUnits = " dBm", guiFormat = "F1"),
        UI_FloatRange(maxValue = 60f, minValue = 0f, scene =UI_Scene.Editor, stepIncrement = 1f, suppressEditorShipModified = true)]
        public float TxPower = 40f;       // Transmit Power in dBm (milliwatts)

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Tech Level", guiFormat = "N0"),
        UI_FloatRange(scene = UI_Scene.Editor, maxValue = 10f, minValue = 1f, stepIncrement = 1f, suppressEditorShipModified = true)]
        private float TechLevel = 1f;
        private int techLevel => Convert.ToInt32(TechLevel);

        [KSPField]
        private int maxTechLevel = 1;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "RF Band"),
         UI_ChooseOption(scene = UI_Scene.Editor, options = new string[] { "S" }, display = new string[] { "S-Band" })]
        public string RFBand = "S";

        public Antenna.BandInfo RFBandInfo => Antenna.BandInfo.All[RFBand];

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Symbol Rate", guiUnits = " S/s", guiFormat = "F0")]
        public double SymbolRate;    // Symbol Rate in Samples/second

        [KSPField(isPersistant = true)]
        public int ModulationBits;    // Constellation size (bits, 1=BPSK, 2=QPSK, 3=8-PSK, 4++ = 16-QAM)

        [KSPField(isPersistant = true)]
        public double AMWTemp;    // Antenna Microwave Temperature

        public double PowerEfficiency => RAAntenna.PowerEfficiency;
        public double PowerDraw => RATools.LogScale(PowerDrawLinear);
        public double PowerDrawLinear => RATools.LinearScale(TxPower) / PowerEfficiency;

        protected static readonly string ModTag = "[ModuleRealAntenna] ";
        public static readonly string ModuleName = "ModuleRealAntenna";
        public RealAntenna RAAntenna = new RealAntennaDigital();
        public Antenna.AntennaGUI GUI = new Antenna.AntennaGUI();

        [KSPField(isPersistant = true)]
        public double antennaDiameter = 0;

        [KSPField(isPersistant = true)]
        public double referenceGain = 0;

        [KSPField(isPersistant = true)]
        public double referenceFrequency = 0;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Transmitter Power")]
        public string sTransmitterPower = string.Empty;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Power Consumption")]
        public string sPowerConsumed = string.Empty;

        [KSPField(guiActive = true, guiName = "Antenna Target")]
        public string sAntennaTarget = string.Empty;

        [KSPField(isPersistant = true)]
        public string targetID = RealAntenna.DefaultTargetName;
        public ITargetable Target { get => RAAntenna.Target; set => RAAntenna.Target = value; }

        [KSPEvent(active = true, guiActive = true, guiActiveUnfocused = false, guiActiveEditor = false, externalToEVAOnly = false, guiName = "Antenna Targeting")]
        void AntennaTargetGUI() => GUI.showGUI = !GUI.showGUI;
        public void OnGUI() => GUI.OnGUI();

        private static double StockRateModifier = 0.00001;
        public static double InactivePowerConsumptionMult = 0.1;

        public override void OnAwake()
        {
            base.OnAwake();
            UI_FloatRange t = (UI_FloatRange)(Fields[nameof(TechLevel)].uiControlEditor);
            t.onFieldChanged = new Callback<BaseField, object>(OnTechLevelChange);

            UI_ChooseOption op = (UI_ChooseOption)(Fields[nameof(RFBand)].uiControlEditor);
            op.onFieldChanged = new Callback<BaseField, object>(OnRFBandChange);

            UI_FloatRange fr = (UI_FloatRange)Fields[nameof(TxPower)].uiControlEditor;
            fr.onFieldChanged = new Callback<BaseField, object>(OnTxPowerChange);
        }

        private void ConfigOptions()
        {
            List<string> availableBands = new List<string>();
            List<string> availableBandDisplayNames = new List<string>();
            foreach (Antenna.BandInfo bi in Antenna.BandInfo.GetFromTechLevel(techLevel))
            {
                availableBands.Add(bi.Name);
                availableBandDisplayNames.Add(bi.DisplayName);
            }

            UI_ChooseOption op = (UI_ChooseOption)Fields[nameof(RFBand)].uiControlEditor;
            op.options = availableBands.ToArray();
            op.display = availableBandDisplayNames.ToArray();
        }
        private void RecalculateFields()
        {
            RAAntenna.TechLevel = techLevel;
            RAAntenna.TxPower = TxPower;
            RAAntenna.RFBand = Antenna.BandInfo.All[RFBand];
            RAAntenna.SymbolRate = SymbolRate = RAAntenna.RFBand.MaxSymbolRate(techLevel);

            Gain = (antennaDiameter > 0) ? Physics.GainFromDishDiamater(antennaDiameter, RFBandInfo.Frequency, RAAntenna.AntennaEfficiency) : Physics.GainFromReference(referenceGain, referenceFrequency * 1e6, RFBandInfo.Frequency);
            sTransmitterPower = $"{RATools.LinearScale(TxPower - 30):F2} Watts";
            sPowerConsumed = $"{PowerDrawLinear / 1000:F2} Watts";
            ModulationBits = (RAAntenna as RealAntennaDigital).modulator.ModulationBitsFromTechLevel(TechLevel);
            (RAAntenna as RealAntennaDigital).modulator.ModulationBits = ModulationBits;
        }
        private void OnRFBandChange(BaseField f, object obj) => RecalculateFields();
        private void OnTxPowerChange(BaseField f, object obj) => RecalculateFields();
        private void OnTechLevelChange(BaseField f, object obj)     // obj is the OLD value
        {
            ConfigOptions();
            UI_ChooseOption op = (UI_ChooseOption)(Fields[nameof(RFBand)].uiControlEditor);
            if (op.options.IndexOf(RFBand) < 0)
            {
                RFBand = op.options[op.options.Length - 1];
                Debug.LogFormat("Forcing RFBand to {0}", RFBand);
            }
            RecalculateFields();
        }

        public void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                string err = string.Empty;
                double req = PowerDrawLinear * 1e-6 * InactivePowerConsumptionMult * Time.fixedDeltaTime;
                resHandler.UpdateModuleResourceInputs(ref err, req, 1, true, false);
                //Debug.LogFormat("FixedUpdate() for {0}: Consuming {1:F4} ec", this, req);
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            { if (Events[nameof(TransmitIncompleteToggle)] is BaseEvent be) be.active = false; }
            { if (Events[nameof(StartTransmission)] is BaseEvent be) be.active = false; }
            { if (Events[nameof(StopTransmission)] is BaseEvent be) be.active = false; }
            if (Actions[nameof(StartTransmissionAction)] is BaseAction ba) ba.active = false;
            if (Fields[nameof(powerText)] is BaseField bf) bf.guiActive = bf.guiActiveEditor = false;      // "Antenna Rating"

            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER) maxTechLevel = HighLogic.CurrentGame.Parameters.CustomParams<RAParameters>().MaxTechLevel;
            if (Fields[nameof(TechLevel)].uiControlEditor is UI_FloatRange fr) fr.maxValue = maxTechLevel;
            if (HighLogic.LoadedSceneIsEditor) TechLevel = maxTechLevel;

            if (!RAAntenna.CanTarget)
            {
                Fields[nameof(sAntennaTarget)].guiActive = false;
                Events[nameof(AntennaTargetGUI)].active = false;
            }
            GUI.ParentPart = part;
            GUI.ParentPartModule = this;
            GUI.Start();
            ConfigOptions();
            RecalculateFields();
        }

        public override void OnLoad(ConfigNode node)
        {
            Configure(node);
            base.OnLoad(node);
            Gain = (antennaDiameter > 0) ? Physics.GainFromDishDiamater(antennaDiameter, RFBandInfo.Frequency, RAAntenna.AntennaEfficiency) : Physics.GainFromReference(referenceGain, referenceFrequency*1e6, RFBandInfo.Frequency);
            Debug.LogFormat("OnLoad {0}, diameter {1}/Freq {2}/Efficiency {3} | refGain {4} / refFreq {5} / Freq {6} results Gain {7}",
                            this, antennaDiameter, RFBandInfo.Frequency, RAAntenna.AntennaEfficiency, referenceGain, referenceFrequency*1e6, RFBandInfo.Frequency, Gain);
        }

        public void Configure(ConfigNode node)
        {
            RAAntenna.Name = name;
            RAAntenna.Parent = this;
            RAAntenna.LoadFromConfigNode(node);
        }

        public override string GetModuleDisplayName() => "RealAntenna";
        public override string GetInfo()
        {
            return $"{ModTag}\n" + 
                   $"<b>Gain</b>: {Gain:F1} dBi\n" + 
                   $"<b>Reference Frequency</b>: {RATools.PrettyPrint(RAAntenna.Frequency)}Hz\n";
        }

        public override string ToString() => RAAntenna.ToString();

        // StartTransmission -> CanTransmit()
        //                  -> OnStartTransmission() -> queueVesselData(), transmitQueuedData()
        // (Science) -> TransmitData() -> TransmitQueuedData()

        internal void SetTransmissionParams()
        {
            if (this?.vessel?.Connection?.Comm is RACommNode node)
            {
                double data_rate = (node.Net as RACommNetwork).MaxDataRateToHome(node);
                packetInterval = 0.1F;
                packetSize = Convert.ToSingle(data_rate * packetInterval * StockRateModifier);
                packetResourceCost = PowerDrawLinear * packetInterval * 1e-6; // 1 EC/sec = 1KW.  Draw(mw) * interval(sec) * mW->kW conversion
            }
        }

        public override bool CanTransmit()
        {
            SetTransmissionParams();
            return base.CanTransmit();
        }

        public override void TransmitData(List<ScienceData> dataQueue)
        {
            SetTransmissionParams();
            base.TransmitData(dataQueue);
        }

        public override void TransmitData(List<ScienceData> dataQueue, Callback callback)
        {
            SetTransmissionParams();
            base.TransmitData(dataQueue, callback);
        }
    }
}