using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas
{
    public class ModuleRealAntenna : ModuleDataTransmitter
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Gain", guiUnits = " dBi", guiFormat = "F1")]
        public double Gain;          // Physical directionality, measured in dBi

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Transmit Power", guiUnits = " dBm", guiFormat = "F1")]
        public double TxPower;       // Transmit Power in dBm (milliwatts)

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Tech Level", guiFormat = "N0")]
        public int TechLevel = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Frequency", guiUnits = " Hz", guiFormat = "N0")]
        public double Frequency;     // Frequency in Hz

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Symbol Rate", guiUnits = " S/s", guiFormat = "F0"),
        UI_FloatEdit(scene = UI_Scene.Editor, minValue = 1000f, incrementLarge = 10000f, incrementSmall = 1000f, incrementSlide = 10f, sigFigs = 6, unit = " S/s", suppressEditorShipModified = true)]
        public float SymbolRate;    // Symbol Rate in Samples/second

        [KSPField(isPersistant = true)]
        public int ModulationBits;    // Constellation size (bits, 0=OOK, 1=BPSK, 2=QPSK, 3=8-PSK, 4++ = 16-QAM)

        [KSPField(isPersistant = true)]
        public int MinModulationBits;    // Minimum constellation size (bits)

        public double PowerEfficiency { get => RAAntenna.PowerEfficiency; }
        public double PowerDraw { get => RATools.LogScale(PowerDrawLinear); }
        public double PowerDrawLinear { get => RATools.LinearScale(TxPower) / PowerEfficiency; }

        protected static readonly string ModTag = "[ModuleRealAntenna] ";
        public static readonly string ModuleName = "ModuleRealAntenna";
        public RealAntenna RAAntenna = new RealAntennaDigital();
        public Antenna.AntennaGUI GUI = new Antenna.AntennaGUI();

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Extra Info")]
        public string guiExtraInfo = "";

        [KSPField(guiActive = true, guiName = "Antenna Target")]
        private string _antennaTargetString = string.Empty;
        public string AntennaTargetString { get => _antennaTargetString; set => _antennaTargetString = value; }

        [KSPField(isPersistant = true)]
        private string targetID = string.Empty;
        public string TargetID { get => targetID; set => targetID = value; }

        public ITargetable Target { get => RAAntenna.Target; set => RAAntenna.Target = value; }

        [KSPEvent(active = true, guiActive = true, guiActiveUnfocused = false, guiActiveEditor = false, externalToEVAOnly = false, guiName = "Antenna Targeting")]
        void AntennaTargetGUI() => GUI.showGUI = !GUI.showGUI;
        public void OnGUI() => GUI.OnGUI();

        public override bool CanComm()
        {
            guiExtraInfo = RAAntenna.ToString();
            return base.CanComm();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            { if (Events["TransmitIncompleteToggle"] is BaseEvent be) be.active = false; }
            { if (Events["StartTransmission"] is BaseEvent be) be.active = false; }
            { if (Events["StopTransmission"] is BaseEvent be) be.active = false; }
            if (Actions["StartTransmissionAction"] is BaseAction ba) ba.active = false;
            if (Fields["powerText"] is BaseField bf) bf.guiActive = false;      // "Antenna Rating"
            if (!RAAntenna.CanTarget)
            {
                Fields["_antennaTargetString"].guiActive = false;
                Events["AntennaTargetGUI"].active = false;
            }
            GUI.ParentPart = part;
            GUI.ParentPartModule = this;
            GUI.Start();
        }

        public override void OnLoad(ConfigNode node)
        {
            Configure(node);
            base.OnLoad(node);
        }

        public void Configure(ConfigNode node)
        {
            RAAntenna.LoadFromConfigNode(node);
            RAAntenna.Name = name;
            RAAntenna.Parent = this;
        }

        public override string GetInfo()
        {
            return string.Format(ModTag + "\n" +
                                "<b>Gain</b>: {0}\n" +
                                "<b>Transmit Power</b>: {1}\n" +
                                "<b>Data Rate</b>: {2}\n", Gain, TxPower, DataRate);
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

        public override bool IsBusy()
        {
            Debug.LogFormat(ModTag + "IsBusy() for {0}", this);
            return base.IsBusy();
        }

        public override void OnUpdate()
        {
            //            Debug.LogFormat(ModTag + "OnUpdate() start");
            // Could update rates here, but way too heavy-weight.
            base.OnUpdate();
            //            Debug.LogFormat(ModTag + "OnUpdate() stop");
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
