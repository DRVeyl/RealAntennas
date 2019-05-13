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

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "RF Band"),
         UI_ChooseOption(scene = UI_Scene.Editor, options = new string[] { "S" }, display = new string[] { "VHF-Band", "UHF-Band", "S-Band", "X-Band", "K-Band", "Ka-Band" })]
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

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Extra Info")]
        public string guiExtraInfo = "";

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Transmitter Power")]
        public string sTransmitterPower = "";

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Power Consumption")]
        public string sPowerConsumed = "";

        [KSPField(guiActive = true, guiName = "Antenna Target")]
        private string _antennaTargetString = string.Empty;
        public string AntennaTargetString { get => _antennaTargetString; set => _antennaTargetString = value; }

        [KSPField(isPersistant = true)]
        private string targetID = "None";
        public string TargetID { get => targetID; set => targetID = value; }

        public ITargetable Target { get => RAAntenna.Target; set => RAAntenna.Target = value; }

        [KSPEvent(active = true, guiActive = true, guiActiveUnfocused = false, guiActiveEditor = false, externalToEVAOnly = false, guiName = "Antenna Targeting")]
        void AntennaTargetGUI() => GUI.showGUI = !GUI.showGUI;
        public void OnGUI() => GUI.OnGUI();

        private List<string> availableBands = new List<string>() { "S" };

        public override void OnAwake()
        {
            base.OnAwake();
            UI_FloatRange t = (UI_FloatRange)(Fields["TechLevel"].uiControlEditor);
            t.onFieldChanged = new Callback<BaseField, object>(OnTechLevelChange);

            UI_ChooseOption op = (UI_ChooseOption)(Fields["RFBand"].uiControlEditor);
            op.onFieldChanged = new Callback<BaseField, object>(OnRFBandChange);

            UI_FloatRange fr = (UI_FloatRange)Fields["TxPower"].uiControlEditor;
            fr.onFieldChanged = new Callback<BaseField, object>(OnTxPowerChange);
        }

        private void ConfigOptions()
        {
            availableBands.Clear();
            foreach (Antenna.BandInfo bi in Antenna.BandInfo.GetFromTechLevel(techLevel))
            {
                availableBands.Add(bi.Name);
            }

            UI_ChooseOption op = (UI_ChooseOption)Fields["RFBand"].uiControlEditor;
            op.options = availableBands.ToArray();
        }
        private void RecalculateFields()
        {
            RAAntenna.TechLevel = techLevel;
            SymbolRate = Antenna.BandInfo.All[RFBand].MaxSymbolRate(techLevel);
            RAAntenna.SymbolRate = SymbolRate;
            RAAntenna.RFBand = Antenna.BandInfo.All[RFBand];

            sTransmitterPower = $"{RATools.LinearScale(TxPower - 30):F2} Watts";
            sPowerConsumed = $"{PowerDrawLinear / 1000:F2} Watts";
            RAAntenna.TxPower = TxPower;
            ModulationBits = (RAAntenna as RealAntennaDigital).modulator.ModulationBitsFromTechLevel(TechLevel);
            (RAAntenna as RealAntennaDigital).modulator.ModulationBits = ModulationBits;
        }
        private void OnRFBandChange(BaseField f, object obj) => RecalculateFields();
        private void OnTxPowerChange(BaseField f, object obj) => RecalculateFields();
        private void OnTechLevelChange(BaseField f, object obj)     // obj is the OLD value
        {
            ConfigOptions();
            UI_ChooseOption op = (UI_ChooseOption)(Fields["RFBand"].uiControlEditor);
            if (op.options.IndexOf(RFBand) < 0)
            {
                RFBand = op.options[op.options.Length - 1];
                Debug.LogFormat("Forcing RFBand to {0}", RFBand);
            }
            RecalculateFields();
        }

        public override void OnFixedUpdate()
        {
            guiExtraInfo = RAAntenna.ToString();
            base.OnFixedUpdate();
            string err = string.Empty;
            double req = PowerDrawLinear * 1e-6 * 0.1;
            // Consume some standby power.  Default OnLoad() set a resource consumption rate=1.
            resHandler.UpdateModuleResourceInputs(ref err, req, 1, true, false);
            //Debug.LogFormat("FixedUpdate() for {0}: Consuming {1:F4} ec", this, req);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            { if (Events["TransmitIncompleteToggle"] is BaseEvent be) be.active = false; }
            { if (Events["StartTransmission"] is BaseEvent be) be.active = false; }
            { if (Events["StopTransmission"] is BaseEvent be) be.active = false; }
            if (Actions["StartTransmissionAction"] is BaseAction ba) ba.active = false;
            if (Fields["powerText"] is BaseField bf) bf.guiActive = bf.guiActiveEditor = false;      // "Antenna Rating"
            if (!RAAntenna.CanTarget)
            {
                Fields["_antennaTargetString"].guiActive = false;
                Events["AntennaTargetGUI"].active = false;
            }
            TargetID = RAAntenna.TargetID;
            GUI.ParentPart = part;
            GUI.ParentPartModule = this;
            GUI.Start();
            //            Debug.LogFormat(ModTag + "Forcing part {0} active.", part);
            //            part.force_activate();
            ConfigOptions();
            RecalculateFields();
        }

        public override void OnLoad(ConfigNode node)
        {
            Configure(node);
            base.OnLoad(node);
            Gain = (antennaDiameter > 0) ? Physics.GainFromDishDiamater(antennaDiameter, RFBandInfo.Frequency, RAAntenna.AntennaEfficiency) : Physics.GainFromReference(referenceGain, referenceFrequency, RFBandInfo.Frequency);
        }

        public void Configure(ConfigNode node)
        {
            RAAntenna.Name = name;
            RAAntenna.Parent = this;
            RAAntenna.LoadFromConfigNode(node);
        }

        public override string GetInfo()
        {
            return string.Format(ModTag + "\n" +
                                "<b>Gain</b>: {0}\n" +
                                "<b>Data Rate</b>: {1}\n", Gain, RATools.PrettyPrintDataRate(RAAntenna.DataRate));
        }

        public override string ToString() => RAAntenna.ToString();

        public override void StopTransmission()
        {
            Debug.LogFormat(ModTag + "StopTransmission() start");
            base.StopTransmission();
            Debug.LogFormat(ModTag + "StopTransmission() exit");
        }

        // StartTransmission -> CanTransmit()
        //                  -> OnStartTransmission() -> queueVesselData(), transmitQueuedData()
        // (Science) -> TransmitData() -> TransmitQueuedData()

        internal void SetTransmissionParams()
        {
            double data_rate = 0;
            if (this?.vessel?.Connection?.Comm is RACommNode node)
            {
                data_rate = (node.Net as RACommNetwork).MaxDataRateToHome(node);
                packetInterval = 0.1F;
                packetSize = Convert.ToSingle(data_rate * packetInterval / 10000);
                packetResourceCost = PowerDrawLinear * packetInterval * 1e-6; // 1 EC/sec = 1KW.  Draw(mw) * interval(sec) * mW->kW conversion
            }
            Debug.LogFormat(ModTag + "SetTransmissionParams() for {0}: data_rate={1}", this, data_rate);
        }

        public override bool CanTransmit()
        {
            SetTransmissionParams();
            return base.CanTransmit();
        }

        protected override List<ScienceData> queueVesselData(List<IScienceDataContainer> experiments)
        {
            Debug.LogFormat(ModTag + "queueVesselData({0}) start", experiments);
            return base.queueVesselData(experiments);
        }

        protected override IEnumerator transmitQueuedData(float transmitInterval, float dataPacketSize, Callback callback = null, bool sendData = true)
        {
            Debug.LogFormat(ModTag + "transmitQueuedData({0},{1},{2},{3}) start", transmitInterval, dataPacketSize, callback, sendData);
            return base.transmitQueuedData(transmitInterval, dataPacketSize, callback, sendData);
        }

        protected override void AbortTransmission(string message)
        {
            Debug.LogFormat(ModTag + "AbortTransmission({0}) start", message);
            base.AbortTransmission(message);
            Debug.LogFormat(ModTag + "AbortTransmission() stop");
        }

        public override void TransmitData(List<ScienceData> dataQueue)
        {
            Debug.LogFormat(ModTag + "TransmitData({0}) start", dataQueue);
            SetTransmissionParams();
            foreach (ScienceData sd in dataQueue)
            {
                Debug.LogFormat(ModTag + "Queue contents: {0} : {1}", sd.subjectID, sd.dataAmount);
            }
            base.TransmitData(dataQueue);
            Debug.LogFormat(ModTag + "TransmitData() stop");
        }

        public override void TransmitData(List<ScienceData> dataQueue, Callback callback)
        {
            SetTransmissionParams();
            base.TransmitData(dataQueue, callback);
        }
    }
}