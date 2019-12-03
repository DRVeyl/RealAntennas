using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RealAntennas
{
    public class ModuleRealAntenna : ModuleDataTransmitter
    {
        [KSPField(guiActiveEditor = true, guiName = "Antenna", isPersistant = true),
        UI_Toggle(disabledText = "<color=red><b>Disabled</b></color>", enabledText = "<color=green>Enabled</color>", scene =UI_Scene.Editor)]
        public bool _enabled = true;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Gain", guiUnits = " dBi", guiFormat = "F1")]
        public double Gain;          // Physical directionality, measured in dBi

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Transmit Power (dBm)", guiUnits = " dBm", guiFormat = "F1"),
        UI_FloatRange(maxValue = 60f, minValue = 0f, stepIncrement = 1f, scene = UI_Scene.Editor, suppressEditorShipModified = true)]
        public float TxPower = 30f;       // Transmit Power in dBm (milliwatts)

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Tech Level", guiFormat = "N0"),
        UI_FloatRange(minValue = 0f, stepIncrement = 1f, scene = UI_Scene.Editor, suppressEditorShipModified = true)]
        private float TechLevel = -1f;
        private int techLevel => Convert.ToInt32(TechLevel);

        [KSPField]
        private int maxTechLevel = 0;

        [KSPField(isPersistant = true)]
        public double AMWTemp;    // Antenna Microwave Temperature

        [KSPField(isPersistant = true)]
        public double antennaDiameter = 0;

        [KSPField(isPersistant = true)]
        public double referenceGain = 0;

        [KSPField(isPersistant = true)]
        public double referenceFrequency = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "RF Band"),
         UI_ChooseOption(scene = UI_Scene.Editor)]
        public string RFBand = "S";

        public Antenna.BandInfo RFBandInfo => Antenna.BandInfo.All[RFBand];

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Transmitter Power")]
        public string sTransmitterPower = string.Empty;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Power Consumption")]
        public string sPowerConsumed = string.Empty;

        [KSPField(guiActive = true, guiName = "Antenna Target")]
        public string sAntennaTarget = string.Empty;

        [KSPField(isPersistant = true)]
        public string targetID = RealAntenna.DefaultTargetName;
        public ITargetable Target { get => RAAntenna.Target; set => RAAntenna.Target = value; }

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Antenna Planning"),
         UI_Toggle(disabledText = "Disabled", enabledText = "Enabled", scene = UI_Scene.All)]
        public bool planningEnabled = false;

        [KSPField(guiActiveEditor = true, guiName = "Planning Peer")]
        public string plannerTargetString = string.Empty;

        [KSPField(guiActiveEditor = true, guiName = "Planning Altitude (Mm)", guiUnits = " Mm", guiFormat = "N0"),
         UI_FloatRange(maxValue = 1000, minValue = 1, stepIncrement = 10, scene = UI_Scene.All)]
        public float plannerAltitude = 1;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Transmit")]
        public string sDownlinkPlanningResult = string.Empty;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Receive")]
        public string sUplinkPlanningResult = string.Empty;

        [KSPEvent(active = true, guiActive = true, guiName = "Antenna Targeting")]
        void AntennaTargetGUI() => targetGUI.showGUI = !targetGUI.showGUI;

        [KSPEvent(active = true, guiActive = true, guiActiveEditor = true, guiName = "Antenna Planning GUI")]
        public void AntennaPlanningGUI() => planner.plannerGUI.showGUI = !planner.plannerGUI.showGUI;

        public void OnGUI() { targetGUI.OnGUI(); planner.plannerGUI.OnGUI(); }

        protected static readonly string ModTag = "[ModuleRealAntenna] ";
        public static readonly string ModuleName = "ModuleRealAntenna";
        public RealAntenna RAAntenna = new RealAntennaDigital();
        public Antenna.AntennaGUI targetGUI = new Antenna.AntennaGUI();
        public Planner planner;

        private ModuleDeployableAntenna deployableAntenna;
        public bool Deployable => deployableAntenna != null;
        public bool Deployed => deployableAntenna?.deployState == ModuleDeployablePart.DeployState.EXTENDED;

        private float StockRateModifier = 0.001f;
        public static double InactivePowerConsumptionMult = 0.1;
        private float defaultPacketInterval = 1.0f;

        public double PowerDraw => RATools.LogScale(PowerDrawLinear);
        public double PowerDrawLinear => RATools.LinearScale(TxPower) / RAAntenna.PowerEfficiency;
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            Configure(node);
            Debug.LogFormat($"{ModTag} OnLoad {this}");
        }

        public void Configure(ConfigNode node)
        {
            RAAntenna.Name = name;
            RAAntenna.Parent = this;
            RAAntenna.LoadFromConfigNode(node);
            Gain = RAAntenna.Gain;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            planner = new Planner(this);
            planner.ConfigTarget(Planetarium.fetch.Home.name, Planetarium.fetch.Home);
            SetupBaseFields();
            Fields[nameof(_enabled)].uiControlEditor.onFieldChanged = OnAntennaEnableChange;

            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER) maxTechLevel = HighLogic.CurrentGame.Parameters.CustomParams<RAParameters>().MaxTechLevel;
            if (Fields[nameof(TechLevel)].uiControlEditor is UI_FloatRange fr) fr.maxValue = maxTechLevel;
            if (HighLogic.LoadedSceneIsEditor && TechLevel < 0) TechLevel = maxTechLevel;
            defaultPacketInterval = HighLogic.CurrentGame.Parameters.CustomParams<RAParameters>().DefaultPacketInterval;
            StockRateModifier = HighLogic.CurrentGame.Parameters.CustomParams<RAParameters>().StockRateModifier;

            if (!RAAntenna.CanTarget)
            {
                Fields[nameof(sAntennaTarget)].guiActive = false;
                Events[nameof(AntennaTargetGUI)].active = false;
            }

            deployableAntenna = part.FindModuleImplementing<ModuleDeployableAntenna>();

            SetupGUIs();
            SetupUICallbacks();
            ConfigBandOptions();
            SetupIdlePower();
            RecalculateFields();
            SetFieldVisibility(_enabled);

            if (HighLogic.LoadedSceneIsFlight) isEnabled = _enabled;
        }

        private void SetupIdlePower()
        {
            if (HighLogic.LoadedSceneIsFlight && _enabled)
            {
                var electricCharge = resHandler.inputResources.First(x => x.id == PartResourceLibrary.ElectricityHashcode);
//                electricCharge.rate = RAAntenna.TechLevelInfo.BasePower / 1000; // Base Power in W, 1ec/s = 1kW
                electricCharge.rate = PowerDrawLinear * 1e-6 * InactivePowerConsumptionMult;
                string err = "";
                resHandler.UpdateModuleResourceInputs(ref err, 1, 1, false, false);
            }
        }

        public void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight && _enabled)
            {
                RAAntenna.AMWTemp = (AMWTemp > 0) ? AMWTemp : part.temperature;
                //part.AddThermalFlux(req / Time.fixedDeltaTime);
                //if (Kerbalism.Kerbalism.KerbalismAssembly is null)
                {
                    string err = "";
                    resHandler.UpdateModuleResourceInputs(ref err, 1, 1, true, false);
                }
            }
        }

        private void RecalculateFields()
        {
            RAAntenna.TechLevelInfo = TechLevelInfo.GetTechLevel(techLevel);
            RAAntenna.TxPower = TxPower;
            RAAntenna.RFBand = Antenna.BandInfo.All[RFBand];
            RAAntenna.SymbolRate = RAAntenna.RFBand.MaxSymbolRate(techLevel);
            RAAntenna.Gain = Gain = (antennaDiameter > 0) ? Physics.GainFromDishDiamater(antennaDiameter, RFBandInfo.Frequency, RAAntenna.AntennaEfficiency) : Physics.GainFromReference(referenceGain, referenceFrequency * 1e6, RFBandInfo.Frequency);

            sTransmitterPower = $"{RATools.LinearScale(TxPower - 30):F2} Watts";
            sPowerConsumed = $"{PowerDrawLinear / 1000:F2} Watts";
            int ModulationBits = (RAAntenna as RealAntennaDigital).modulator.ModulationBitsFromTechLevel(TechLevel);
            (RAAntenna as RealAntennaDigital).modulator.ModulationBits = ModulationBits;

            planner.RecalculatePlannerFields();
        }

        private void SetupBaseFields()
        {
            { if (Events[nameof(TransmitIncompleteToggle)] is BaseEvent be) be.active = false; }
            { if (Events[nameof(StartTransmission)] is BaseEvent be) be.active = false; }
            { if (Events[nameof(StopTransmission)] is BaseEvent be) be.active = false; }
            if (Actions[nameof(StartTransmissionAction)] is BaseAction ba) ba.active = false;
            if (Fields[nameof(powerText)] is BaseField bf) bf.guiActive = bf.guiActiveEditor = false;      // "Antenna Rating"
        }

        private void SetFieldVisibility(bool en)
        {
            Fields[nameof(Gain)].guiActiveEditor = Fields[nameof(Gain)].guiActive = en;
            Fields[nameof(TxPower)].guiActiveEditor = Fields[nameof(TxPower)].guiActive = en;
            Fields[nameof(TechLevel)].guiActiveEditor = Fields[nameof(TechLevel)].guiActive = en;
            Fields[nameof(RFBand)].guiActiveEditor = Fields[nameof(RFBand)].guiActive = en;
            Fields[nameof(sTransmitterPower)].guiActiveEditor = Fields[nameof(sTransmitterPower)].guiActive = en;
            Fields[nameof(sPowerConsumed)].guiActiveEditor = Fields[nameof(sPowerConsumed)].guiActive = en;
            Fields[nameof(sAntennaTarget)].guiActiveEditor = Fields[nameof(sAntennaTarget)].guiActive = en;
            Fields[nameof(planningEnabled)].guiActiveEditor = Fields[nameof(planningEnabled)].guiActive = en;
        }

        private void SetupGUIs()
        {
            targetGUI.ParentPart = part;
            targetGUI.ParentPartModule = this;
            targetGUI.Start();
            planner.SetPlanningFields();
            planner.plannerGUI.Start();
        }

        private void SetupUICallbacks()
        {
            UI_FloatRange t = Fields[nameof(TechLevel)].uiControlEditor as UI_FloatRange;
            t.onFieldChanged = new Callback<BaseField, object>(OnTechLevelChange);

            UI_ChooseOption op = Fields[nameof(RFBand)].uiControlEditor as UI_ChooseOption;
            op.onFieldChanged = new Callback<BaseField, object>(OnRFBandChange);

            UI_FloatRange fr = Fields[nameof(TxPower)].uiControlEditor as UI_FloatRange;
            fr.onFieldChanged = new Callback<BaseField, object>(OnTxPowerChange);

            UI_Toggle tE = Fields[nameof(planningEnabled)].uiControlEditor as UI_Toggle;
            UI_Toggle tF = Fields[nameof(planningEnabled)].uiControlFlight as UI_Toggle;
            tE.onFieldChanged = tF.onFieldChanged = new Callback<BaseField, object>(planner.OnPlanningEnabledChange);

            UI_FloatRange paE = Fields[nameof(plannerAltitude)].uiControlEditor as UI_FloatRange;
            UI_FloatRange paF = Fields[nameof(plannerAltitude)].uiControlFlight as UI_FloatRange;
            paE.onFieldChanged = paF.onFieldChanged = new Callback<BaseField, object>(planner.OnPlanningAltitudeChange);
        }

        private void OnAntennaEnableChange(BaseField field, object obj) => SetFieldVisibility(_enabled);
        private void OnRFBandChange(BaseField f, object obj) => RecalculateFields();
        private void OnTxPowerChange(BaseField f, object obj) => RecalculateFields();
        private void OnTechLevelChange(BaseField f, object obj)     // obj is the OLD value
        {
            ConfigBandOptions();
            RecalculateFields();
        }

        private void ConfigBandOptions()
        {
            List<string> availableBands = new List<string>();
            List<string> availableBandDisplayNames = new List<string>();
            foreach (Antenna.BandInfo bi in Antenna.BandInfo.GetFromTechLevel(techLevel))
            {
                availableBands.Add(bi.name);
                availableBandDisplayNames.Add($"{bi.name}-Band");
            }

            UI_ChooseOption op = (UI_ChooseOption)Fields[nameof(RFBand)].uiControlEditor;
            op.options = availableBands.ToArray();
            op.display = availableBandDisplayNames.ToArray();
            if (op.options.IndexOf(RFBand) < 0)
            {
                RFBand = op.options[op.options.Length - 1];
            }
        }

        public override string GetModuleDisplayName() => "RealAntenna";
        public override string GetInfo()
        {
            string res = string.Empty;
            if (RAAntenna.Shape != AntennaShape.Omni)
            {
                foreach (Antenna.BandInfo band in Antenna.BandInfo.All.Values)
                {
                    double tGain = (antennaDiameter > 0) ? Physics.GainFromDishDiamater(antennaDiameter, band.Frequency, RAAntenna.AntennaEfficiency) : Physics.GainFromReference(referenceGain, referenceFrequency * 1e6, band.Frequency);
                    res += $"<color=green><b>{band.name}</b></color>: {tGain:F1} dBi, {Physics.Beamwidth(tGain):F1} beamwidth\n";
                }
            } else
            {
                res = $"<color=green>Omni-directional</color>: {Gain:F1} dBi";
            }
            return res;
        }

        public override bool CanComm() => base.CanComm() && (!Deployable || Deployed);

        public override string ToString() => RAAntenna.ToString();

        // StartTransmission -> CanTransmit()
        //                  -> OnStartTransmission() -> queueVesselData(), transmitQueuedData()
        // (Science) -> TransmitData() -> TransmitQueuedData()

        internal void SetTransmissionParams()
        {
            if (this?.vessel?.Connection?.Comm is RACommNode node)
            {
                double data_rate = (node.Net as RACommNetwork).MaxDataRateToHome(node);
                packetInterval = defaultPacketInterval;
                packetSize = Convert.ToSingle(data_rate * packetInterval * StockRateModifier);
                packetResourceCost = PowerDrawLinear * packetInterval * 1e-6; // 1 EC/sec = 1KW.  Draw(mw) * interval(sec) * mW->kW conversion
                Debug.Log($"{ModTag} Setting transmission params: rate: {data_rate:F1}, interval: {packetInterval:N1}s, rescale: {StockRateModifier:N5}, size: {packetSize:N6}");
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