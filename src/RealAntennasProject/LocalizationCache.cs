using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KSP.Localization;

namespace RealAntennas
{
    /// <summary>
    /// Localization cache, all the localized string of RealAntennas here
    /// </summary>
    public static class Local
    {
        /// <summary>
        /// "(No Connection)"
        /// </summary>
        public static string PlannerGUI_NoConnection = Localizer.Format("#RA_PlannerGUI_NoConnection");
        /// <summary>
        /// "Antenna Planning"
        /// </summary>
        public static string PlannerGUI_title = Localizer.Format("#RA_PlannerGUI_title");
        /// <summary>
        /// "Ground Station TechLevel: "
        /// </summary>
        public static string PlannerGUI_curGSTechLevel = Localizer.Format("#RA_PlannerGUI_curGSTechLevel");
        /// <summary>
        /// "Ground Station (Planning) TechLevel: "
        /// </summary>
        public static string PlannerGUI_iTechLevel = Localizer.Format("#RA_PlannerGUI_iTechLevel");
        /// <summary>
        /// "Antenna Selection"
        /// </summary>
        public static string PlannerGUI_AntennaSelection = Localizer.Format("#RA_PlannerGUI_AntennaSelection");
        /// <summary>
        /// "Vessel"
        /// </summary>
        public static string PlannerGUI_SelectionMode_Vessel = Localizer.Format("#RA_PlannerGUI_SelectionMode_Vessel"); 
        /// <summary>
        /// "GroundStation"
        /// </summary>
        public static string PlannerGUI_SelectionMode_GroundStation = Localizer.Format("#RA_PlannerGUI_SelectionMode_GroundStation");
        /// <summary>
        /// "Primary"
        /// </summary>
        public static string PlannerGUI_Primary = Localizer.Format("#RA_PlannerGUI_Primary");
        /// <summary>
        /// "Peer"
        /// </summary>
        public static string PlannerGUI_Peer = Localizer.Format("#RA_PlannerGUI_Peer");  
        /// <summary>
        /// "Remote Body Presets"
        /// </summary>
        public static string PlannerGUI_RemoteBodyPresets = Localizer.Format("#RA_PlannerGUI_RemoteBodyPresets");
        /// <summary>
        /// "Parameters"
        /// </summary>
        public static string PlannerGUI_Parameters = Localizer.Format("#RA_PlannerGUI_Parameters");
        /// <summary>
        /// "Primary Antenna:"
        /// </summary>
        public static string PlannerGUI_PrimaryAntenna = Localizer.Format("#RA_PlannerGUI_PrimaryAntenna");
        /// <summary>
        /// "Peer Antenna:"
        /// </summary>
        public static string PlannerGUI_PeerAntenna = Localizer.Format("#RA_PlannerGUI_PeerAntenna");
        /// <summary>
        /// "Distance Max:"
        /// </summary>
        public static string PlannerGUI_Distance_Max = Localizer.Format("#RA_PlannerGUI_Distance_Max");
        /// <summary>
        /// "Distance Min:"
        /// </summary>
        public static string PlannerGUI_Distance_Min = Localizer.Format("#RA_PlannerGUI_Distance_Min");
        /// <summary>
        /// "Tx/Rx rate at max distance:"
        /// </summary>
        public static string PlannerGUI_xrRate_Max = Localizer.Format("#RA_PlannerGUI_xrRate_Max");
        /// <summary>
        /// "Tx/Rx rate at min distance:"
        /// </summary>
        public static string PlannerGUI_xrRate_Min = Localizer.Format("#RA_PlannerGUI_xrRate_Min");
        /// <summary>
        /// "Plan!"
        /// </summary>
        public static string PlannerGUI_Planbutton = Localizer.Format("#RA_PlannerGUI_Planbutton");
        /// <summary>
        /// "Show Details"
        /// </summary>
        public static string PlannerGUI_ShowDetails = Localizer.Format("#RA_PlannerGUI_ShowDetails");
        /// <summary>
        /// [Best Station]
        /// </summary>
        public static string PlannerGUI_BestGS = Localizer.Format("#RA_PlannerGUI_BestGS");

        // Connection Debugger strings
        /// <summary>
        /// Planning Antenna Debugger
        /// </summary>
        public static string CDB_PlanningAntennaDebugger = Localizer.Format("#RA_CDB_PlanningAntennaDebugger");
        /// <summary>
        /// Connection Debugger
        /// </summary>
        public static string CDB_ConnectionDebugger= Localizer.Format("#RA_CDB_ConnectionDebugger");
        /// <summary>
        /// "Transmitter
        /// </summary>
        public static string CDB_Transmitter = Localizer.Format("#RA_CDB_Transmitter");
        /// <summary>
        /// "Receiver"
        /// </summary>
        public static string CDB_Receiver= Localizer.Format("#RA_CDB_Receiver");
        public static string CDB_Noise = Localizer.Format("#RA_CDB_Noise");
        /// <summary>
        /// "Atmosphere"
        /// </summary>
        public static string CDB_Atmosphere = Localizer.Format("#RA_CDB_Atmosphere");
        /// <summary>
        /// "Body"
        /// </summary>
        public static string CDB_Body = Localizer.Format("#RA_CDB_Body");
        /// <summary>
        /// "Total Noise"
        /// </summary>
        public static string CDB_TotalNoise = Localizer.Format("#RA_CDB_TotalNoise");
        /// <summary>
        /// "Losses"
        /// </summary>
        public static string CDB_Losses = Localizer.Format("#RA_CDB_Losses");
        /// <summary>
        /// Path Loss
        /// </summary>
        public static string CDB_PathLoss = Localizer.Format("#RA_CDB_PathLoss");
        /// <summary>
        /// Pointing Loss
        /// </summary>
        public static string CDB_PointingLoss = Localizer.Format("#RA_CDB_PointingLoss");
        /// <summary>
        /// Link Budget
        /// </summary>
        public static string CDB_LinkBudget = Localizer.Format("#RA_CDB_LinkBudget");
        /// <summary>
        /// "Encoder"
        /// </summary>
        public static string CDB_Encoder = Localizer.Format("#RA_CDB_Encoder");
        /// <summary>
        /// Achieved Rate
        /// </summary>
        public static string CDB_AchievedRate = Localizer.Format("#RA_CDB_AchievedRate");
        /// <summary>
        /// Valid Rates
        /// </summary>
        public static string CDB_ValidRates= Localizer.Format("#RA_CDB_ValidRates");
        /// <summary>
        /// Steps
        /// </summary>
        public static string CDB_RateSteps = Localizer.Format("#RA_CDB_RateSteps");

        // Notify Messages
        public static string NotifyMessages_disable = Localizer.Format("#RA_NotifyMessages_disable");
        public static string NotifyMessages_enable = Localizer.Format("#RA_NotifyMessages_enable");

        // Mod Settings
        public static string Setting_Title = Localizer.Format("#RA_Setting_Title");

        // NetUI strings
        public static string NetUI_ConeMode = Localizer.Format("#RA_NetUI_ConeMode");  // ConeMode
        public static string NetUI_LinkEndMode = Localizer.Format("#RA_NetUI_LinkEndMode"); // Link End Mode
        public static string NetUI_TargetLine = Localizer.Format("#RA_NetUI_TargetLine"); // TargetLine
        public static string NetUI_3dBCones = Localizer.Format("#RA_NetUI_3dBCones"); // 3dB Cones
        public static string NetUI_10dBCones = Localizer.Format("#RA_NetUI_10dBCones"); // 10dB Cones
        public static string NetUI_LinkLineBrightness  = Localizer.Format("#RA_NetUI_LinkLineBrightness"); // Link Line Brightness
        public static string NetUI_ConeCircles = Localizer.Format("#RA_NetUI_ConeCircles"); // Cone Circles
        public static string NetUI_ConeOpacity= Localizer.Format("#RA_NetUI_ConeOpacity"); // Cone Opacity
        public static string NetUI_SignalTooltips = Localizer.Format("#RA_NetUI_SignalTooltips");  // Signal (Tx/Rx)


        // Antenna Control Center strings
        public static string ControlCenterUI_title = Localizer.Format("#RA_ControlCenterUI_title");  // Antenna Control Center
        public static string ControlCenterUI_SortMode = Localizer.Format("#RA_ControlCenterUI_SortMode");  // Sort Mode

        // Antenna Targeting windows
        public static string AntennaTargeting_title = Localizer.Format("#RA_AntennaTargeting_title");  // "Antenna Targeting"
        public static string AntennaTargeting_Azimuth = Localizer.Format("#RA_AntennaTargeting_Azimuth");  // Azimuth
        public static string AntennaTargeting_Elevation = Localizer.Format("#RA_AntennaTargeting_Elevation");  // Elevation
        public static string AntennaTargeting_Deflection = Localizer.Format("#RA_AntennaTargeting_Deflection");  // Deflection


        // Gerneric strings
        public static string Gerneric_Apply = Localizer.Format("#RA_Gerneric_Apply");  // Apply
        public static string Gerneric_Close = Localizer.Format("#RA_Gerneric_Close");  // Close
        public static string Gerneric_Vessel = Localizer.Format("#RA_Gerneric_Vessel");  // Vessel
        public static string Gerneric_Antenna = Localizer.Format("#RA_Gerneric_Antenna");  // Antenna
        public static string Gerneric_Band = Localizer.Format("#RA_Gerneric_Band");  // Band
        public static string Gerneric_Power = Localizer.Format("#RA_Gerneric_Power");  // Power
        public static string Gerneric_Target = Localizer.Format("#RA_Gerneric_Target");  // Target
        public static string Gerneric_Position = Localizer.Format("#RA_Gerneric_Position");  // Position
        public static string Gerneric_None = Localizer.Format("#RA_Gerneric_None");  // None
        public static string Gerneric_Beamwidth= Localizer.Format("#RA_Gerneric_Beamwidth");  // Beamwidth (3dB full-width)
        public static string Gerneric_Beamwidth_moulde= Localizer.Format("#RA_Gerneric_Beamwidth_moulde");  // beamwidth
        public static string Gerneric_AntennaAoA = Localizer.Format("#RA_Gerneric_AntennaAoA");  // Antenna AoA
        public static string Gerneric_ReceivedPower= Localizer.Format("#RA_Gerneric_ReceivedPower");  // Received Power
        public static string Gerneric_AntennaElevation= Localizer.Format("#RA_Gerneric_AntennaElevation");  // Antenna Elevation
        public static string Gerneric_Omni_directional= Localizer.Format("#RA_Gerneric_Omni_directional");  // Omni-directional
        public static string Gerneric_comms = Localizer.Format("#RA_Gerneric_comms");  // comms
        public static string Gerneric_Vessels = Localizer.Format("#RA_Gerneric_Vessels");  // Vessels
        public static string Gerneric_GroundStations = Localizer.Format("#RA_Gerneric_GroundStations");  // GroundStations
        public static string Gerneric_Antennas_vessel = Localizer.Format("#RA_Gerneric_Antennas_vessel");  // Antennas/vessel
        public static string Gerneric_Name = Localizer.Format("#RA_Gerneric_Name");  // Name
        public static string Gerneric_Iterations = Localizer.Format("#RA_Gerneric_Iterations");  // Iterations
        public static string Gerneric_AvgTime_ms = Localizer.Format("#RA_Gerneric_AvgTime_ms");  // Avg Time (ms)
        public static string Gerneric_RunsPerSec= Localizer.Format("#RA_Gerneric_RunsPerSec");  // Runs/sec
        public static string Gerneric_HideConfigWindow = Localizer.Format("#RA_Gerneric_HideConfigWindow");  // Hide Config Window
        public static string Gerneric_ShowConfigWindow = Localizer.Format("#RA_Gerneric_ShowConfigWindow");  // Show Config Window
        public static string Gerneric_LaunchControlConsole = Localizer.Format("#RA_Gerneric_LaunchControlConsole");  // Launch Control Console
        public static string Gerneric_CloseControlConsole = Localizer.Format("#RA_Gerneric_CloseControlConsole");  // Close Control Console
        public static string Gerneric_TargetMode= Localizer.Format("#RA_Gerneric_TargetMode");  // Target Mode
        public static string Gerneric_Latitude = Localizer.Format("#RA_Gerneric_Latitude");  // Lat
        public static string Gerneric_Longitude = Localizer.Format("#RA_Gerneric_Longitude");  // Lon
        public static string Gerneric_Altitude = Localizer.Format("#RA_Gerneric_Altitude");  // Alt


    }
}
