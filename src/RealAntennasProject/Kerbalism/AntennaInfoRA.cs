using System.Collections.Generic;
/*
namespace KERBALISM
{
    public sealed class AntennaInfoRA
    {
        /// <summary> science data rate. note that internal transmitters can not transmit science data only telemetry data </summary>
        public double rate = 0.0;

        /// <summary> ec cost </summary>
        public double ec = 0.0;

        public void DisableStockButtons(Vessel v)
        {
            if (v.loaded)
            {
                foreach (ModuleDataTransmitter t in v.FindPartModulesImplementing<ModuleDataTransmitter>())
                {
                    // Disable all stock buttons
                    t.Events["TransmitIncompleteToggle"].active = false;
                    t.Events["StartTransmission"].active = false;
                    t.Events["StopTransmission"].active = false;
                    t.Actions["StartTransmissionAction"].active = false;
                }
            }
        }

        public AntennaInfoRA(Vessel v)
        {
            DisableStockButtons(v);
            if (v?.Connection?.Comm is RealAntennas.RACommNode node)
            {
                rate = (node.Net as RealAntennas.RACommNetwork).MaxDataRateToHome(node);
                double packetInterval = 1.0F;
                RealAntennas.RealAntenna ra = node.AntennaTowardsHome();
                ec = ra.PowerDrawLinear * packetInterval * 1e-6;    // 1 EC/sec = 1KW.  Draw(mw) * interval(sec) * mW -> kW conversion
            }
        }
    }


    public sealed class ConnectionInfo
    {
        /// <summary> true if there is a connection back to DSN </summary>
        public bool linked = false;

        /// <summary> status of the connection </summary>
        public LinkStatus status = LinkStatus.no_link;

        /// <summary> Controller Path </summary>
        public Guid[] controlPath;

        /// <summary> science data rate. note that internal transmitters can not transmit science data only telemetry data </summary>
        public double rate = 0.0;

        /// <summary> transmitter ec cost</summary>
        public double ec = 0.0;

        /// <summary> signal strength </summary>
        public double strength = 0.0;

        /// <summary> receiving node name </summary>
        public string target_name = "";

        // constructor
        /// <summary> Creates a <see cref="ConnectionInfo"/> object for the specified vessel from it's antenna modules</summary>
        public ConnectionInfo(Vessel v, bool powered, bool storm)
        {
            // Do the normal stuff...
            // If RealAntennas:
            {
                AntennaInfoRA antennaInfo = new AntennaInfoRA(v);
                if ((v.Connection != null) && v.Connection.IsConnectedHome)
                {
                    ec = antennaInfo.ec;
                    rate = antennaInfo.rate;
                    // Do not adjust rate for signal strength or hop limits: 
                    // RealAntennas core already does this, and it's represented in ConnectionInfoRA.
                    // Rest of this will get used in cache I guess?
                    linked = true;
                    status = v.connection.ControlPath.First.hopType == CommNet.HopType.Home ? LinkStatus.direct_link : LinkStatus.indirect_link;
                    strength = v.connection.SignalStrength;
                    target_name = Lib.Ellipsis(Localizer.Format(v.connection.ControlPath.First.end.displayName).Replace("Kerbin", "DSN"), 20);
                }
            }
        }
    }
}
*/