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

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Transmit Power (dBm)", guiUnits = " dBm", guiFormat = "F1"),
        UI_FloatRange(maxValue = 60f, minValue = 0f, stepIncrement = 1f, scene = UI_Scene.Editor, suppressEditorShipModified = true)]
        public float TxPower = 40f;       // Transmit Power in dBm (milliwatts)

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Tech Level", guiFormat = "N0"),
        UI_FloatRange(minValue = 1f, stepIncrement = 1f, scene = UI_Scene.Editor, suppressEditorShipModified = true)]
        private float TechLevel = 1f;
        private int techLevel => Convert.ToInt32(TechLevel);

        [KSPField]
        private int maxTechLevel = 1;

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
        public bool PlanningEnabled = false;

        [KSPField(guiActiveEditor = true, guiName = "Planning Peer")]
        public string sPlannerTarget = string.Empty;
        public ITargetable PlannerTarget;

        [KSPField(guiActiveEditor = true, guiName = "Planning Altitude (Mm)", guiUnits = " Mm", guiFormat = "N0"),
         UI_FloatRange(maxValue = 1000, minValue = 1, stepIncrement = 10, scene = UI_Scene.All)]
        public float PlannerAltitude = 1;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Planning Result")]
        public string sPlanningResult = string.Empty;

        [KSPEvent(active = true, guiActive = true, guiName = "Antenna Targeting")]
        void AntennaTargetGUI() => targetGUI.showGUI = !targetGUI.showGUI;

        [KSPEvent(active = true, guiActive = true, guiActiveEditor = true, guiName = "Antenna Planning GUI")]
        void AntennaPlanningGUI() => plannerGUI.showGUI = !plannerGUI.showGUI;

        public void OnGUI() { targetGUI.OnGUI(); plannerGUI.OnGUI(); }

        protected static readonly string ModTag = "[ModuleRealAntenna] ";
        public static readonly string ModuleName = "ModuleRealAntenna";
        public RealAntenna RAAntenna = new RealAntennaDigital();
        public Antenna.AntennaGUI targetGUI = new Antenna.AntennaGUI();
        public PlannerGUI plannerGUI = new PlannerGUI();

        private static double StockRateModifier = 0.00001;
        public static double InactivePowerConsumptionMult = 0.1;
        public float defaultPacketInterval = 1.0f;

        public double PowerDraw => RATools.LogScale(PowerDrawLinear);
        public double PowerDrawLinear => RATools.LinearScale(TxPower) / RAAntenna.PowerEfficiency;

        public override void OnAwake()
        {
            base.OnAwake();
            SetupUICallbacks();
        }

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
            SetupBaseFields();

            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER) maxTechLevel = HighLogic.CurrentGame.Parameters.CustomParams<RAParameters>().MaxTechLevel;
            if (Fields[nameof(TechLevel)].uiControlEditor is UI_FloatRange fr) fr.maxValue = maxTechLevel;
            if (HighLogic.LoadedSceneIsEditor) TechLevel = maxTechLevel;
            defaultPacketInterval = HighLogic.CurrentGame.Parameters.CustomParams<RAParameters>().DefaultPacketInterval;

            if (!RAAntenna.CanTarget)
            {
                Fields[nameof(sAntennaTarget)].guiActive = false;
                Events[nameof(AntennaTargetGUI)].active = false;
            }
            SetPlanningFields();
            SetupGUIs();
            ConfigBandOptions();
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

                RAAntenna.AMWTemp = (AMWTemp > 0) ? AMWTemp : part.temperature;
                //part.AddThermalFlux(req / Time.fixedDeltaTime);
            }
        }

        private void RecalculateFields()
        {
            RAAntenna.TechLevel = techLevel;
            RAAntenna.TxPower = TxPower;
            RAAntenna.RFBand = Antenna.BandInfo.All[RFBand];
            RAAntenna.SymbolRate = RAAntenna.RFBand.MaxSymbolRate(techLevel);
            RAAntenna.Gain = Gain = (antennaDiameter > 0) ? Physics.GainFromDishDiamater(antennaDiameter, RFBandInfo.Frequency, RAAntenna.AntennaEfficiency) : Physics.GainFromReference(referenceGain, referenceFrequency * 1e6, RFBandInfo.Frequency);

            sTransmitterPower = $"{RATools.LinearScale(TxPower - 30):F2} Watts";
            sPowerConsumed = $"{PowerDrawLinear / 1000:F2} Watts";
            int ModulationBits = (RAAntenna as RealAntennaDigital).modulator.ModulationBitsFromTechLevel(TechLevel);
            (RAAntenna as RealAntennaDigital).modulator.ModulationBits = ModulationBits;

            RecalculatePlannerFields();
        }
        internal void RecalculatePlannerFields()
        {
            if (PlannerTarget is CelestialBody b)
            {
                CelestialBody home = Planetarium.fetch.Home;
                RACommNetwork net = (RACommNetScenario.Instance as RACommNetScenario).Network.CommNet as RACommNetwork;
                if (Fields[nameof(PlannerAltitude)] is BaseField bf) bf.guiActive = bf.guiActiveEditor = PlanningEnabled && (b == home);
                if (RATools.HighestGainCompatibleDSNAntenna(net.Nodes, RAAntenna) is RealAntenna DSNAntenna)
                {
                    GameObject localObj = new GameObject("localAntenna");
                    GameObject remoteObj = new GameObject("remoteAntenna");
                    RACommNode localComm = new RACommNode(localObj.transform) { ParentBody = home };
                    RACommNode remoteComm = new RACommNode(remoteObj.transform) { ParentVessel = vessel };
                    RealAntenna localAnt = new RealAntennaDigital(DSNAntenna) { ParentNode = localComm };
                    RealAntenna remoteAnt = new RealAntennaDigital(RAAntenna) { ParentNode = remoteComm };
                    localAnt.ParentNode.transform.SetPositionAndRotation(home.position + home.GetRelSurfacePosition(0,0,0), Quaternion.identity);
                    Vector3 dir = home.GetRelSurfaceNVector(0, 0).normalized;
                    // Simplification: Use the current position of the homeworld, rather than choosing Ap/Pe/an average.
                    double maxAlt = (b == Planetarium.fetch.Sun) ? 0 : b.orbit.ApA;
                    double minAlt = (b == Planetarium.fetch.Sun) ? 0 : b.orbit.PeA;
                    double sunDistance = (Planetarium.fetch.Sun.position - home.position).magnitude;
                    double furthestDistance = PlannerAltitude * 1e6;
                    double closestDistance = PlannerAltitude * 1e6;
                    if (b != home)
                    {
                        furthestDistance = maxAlt + sunDistance;
                        closestDistance = (maxAlt < sunDistance) ? sunDistance - maxAlt : minAlt - sunDistance;
                    }

                    Vector3 adj = dir * Convert.ToSingle(furthestDistance);
                    remoteAnt.ParentNode.transform.SetPositionAndRotation(home.position + adj, Quaternion.identity);

                    double rxp = TxPower + Gain - Physics.PathLoss(furthestDistance, RFBandInfo.Frequency) + localAnt.Gain;
                    double dataRateLow = remoteAnt.BestDataRateToPeer(localAnt);

                    adj = dir * Convert.ToSingle(closestDistance);
                    remoteAnt.ParentNode.transform.SetPositionAndRotation(home.position + adj, Quaternion.identity);

                    rxp = TxPower + Gain - Physics.PathLoss(closestDistance, RFBandInfo.Frequency) + localAnt.Gain;
                    double dataRateHigh = remoteAnt.BestDataRateToPeer(localAnt);

                    sPlanningResult = $"Max {RATools.PrettyPrintDataRate(dataRateHigh)} Min {RATools.PrettyPrintDataRate(dataRateLow)}";

                    localObj.DestroyGameObject();
                    remoteObj.DestroyGameObject();
                }
            } else
            {
                if (Fields[nameof(PlannerAltitude)] is BaseField bf) bf.guiActive = bf.guiActiveEditor = false;
            }
        }

        private void SetupBaseFields()
        {
            { if (Events[nameof(TransmitIncompleteToggle)] is BaseEvent be) be.active = false; }
            { if (Events[nameof(StartTransmission)] is BaseEvent be) be.active = false; }
            { if (Events[nameof(StopTransmission)] is BaseEvent be) be.active = false; }
            if (Actions[nameof(StartTransmissionAction)] is BaseAction ba) ba.active = false;
            if (Fields[nameof(powerText)] is BaseField bf) bf.guiActive = bf.guiActiveEditor = false;      // "Antenna Rating"
        }

        private void SetPlanningFields()
        {
            { if (Events[nameof(AntennaPlanningGUI)] is BaseEvent be) be.active = PlanningEnabled; }
            { if (Fields[nameof(sPlannerTarget)] is BaseField bf) bf.guiActive = bf.guiActiveEditor = PlanningEnabled; }
            { if (Fields[nameof(sPlanningResult)] is BaseField bf) bf.guiActive = bf.guiActiveEditor = PlanningEnabled; }
            { if (Fields[nameof(PlannerAltitude)] is BaseField bf) bf.guiActive = bf.guiActiveEditor = PlanningEnabled; }
        }

        private void SetupGUIs()
        {
            targetGUI.ParentPart = part;
            targetGUI.ParentPartModule = this;
            targetGUI.Start();
            plannerGUI.ParentPart = part;
            plannerGUI.ParentPartModule = this;
            plannerGUI.Start();
        }

        private void SetupUICallbacks()
        {
            UI_FloatRange t = (UI_FloatRange)(Fields[nameof(TechLevel)].uiControlEditor);
            t.onFieldChanged = new Callback<BaseField, object>(OnTechLevelChange);

            UI_ChooseOption op = (UI_ChooseOption)(Fields[nameof(RFBand)].uiControlEditor);
            op.onFieldChanged = new Callback<BaseField, object>(OnRFBandChange);

            UI_FloatRange fr = (UI_FloatRange)Fields[nameof(TxPower)].uiControlEditor;
            fr.onFieldChanged = new Callback<BaseField, object>(OnTxPowerChange);

            UI_Toggle tE = (UI_Toggle)Fields[nameof(PlanningEnabled)].uiControlEditor;
            UI_Toggle tF = (UI_Toggle)Fields[nameof(PlanningEnabled)].uiControlFlight;
            tE.onFieldChanged = tF.onFieldChanged = new Callback<BaseField, object>(OnPlanningEnabledChange);

            UI_FloatRange paE = (UI_FloatRange)(Fields[nameof(PlannerAltitude)].uiControlEditor);
            UI_FloatRange paF = (UI_FloatRange)(Fields[nameof(PlannerAltitude)].uiControlFlight);
            paE.onFieldChanged = paF.onFieldChanged = new Callback<BaseField, object>(OnPlanningAltitudeChange);
        }

        private void OnPlanningEnabledChange(BaseField f, object obj) { SetPlanningFields(); RecalculatePlannerFields(); }
        private void OnPlanningAltitudeChange(BaseField f, object obj) => RecalculatePlannerFields();
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
                availableBands.Add(bi.Name);
                availableBandDisplayNames.Add(bi.DisplayName);
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
                packetInterval = defaultPacketInterval;
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