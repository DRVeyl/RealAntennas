using System;
using System.Collections.Generic;
using CommNet;
using System.Linq;
using UnityEngine;

namespace RealAntennas
{
    public class RATools : object
    {
        public static double LinearScale(double x) => Math.Pow(10, x / 10);
        public static float LinearScale(float x) => Mathf.Pow(10, x / 10);
        public static double LogScale(double x) => 10 * Math.Log10(x);
        public static double RadToDeg(double x) => x * 180 / Math.PI;
        public static double DegToRad(double x) => x * Math.PI / 180;

        public static string PrettyPrintDataRate(double rate) => $"{PrettyPrint(rate)}bps";

        public static string PrettyPrint(double d)
        {
            if (d > 1e9) return $"{d / 1e9:F2} G";
            else if (d > 1e6) return $"{d / 1e6:F2} M";
            else if (d > 1e3) return $"{d / 1e3:F1} K";
            else return $"{d:F0} ";
        }

        public static string PrettyPrint(List<RealAntenna> list)
        {
            string s = string.Empty;
            foreach (RealAntenna ra in list)
            {
                s += $"{ra.RFBand.name}-Band: {ra.Gain} dBi\n";
            }
            return s;
        }

        public static RealAntenna HighestGainCompatibleDSNAntenna(List<CommNode> nodes, RealAntenna peer)
        {
            RealAntenna result = null;
            double highestGain = 0;
            foreach (RACommNode node in nodes.Where(obj => obj.isHome))
            {
                foreach (RealAntenna ra in node.RAAntennaList)
                {
                    if (peer.Compatible(ra) && ra.Gain > highestGain)
                    {
                        highestGain = ra.Gain;
                        result = ra;
                    }
                }
            }
            return result;
        }

        public static string DisplayGamescenes(ProtoScenarioModule psm)
        {
            string s = string.Empty;
            foreach (GameScenes gs in psm.targetScenes) { s += $"{gs} "; }
            return $"{psm} {psm.moduleName} [{s}]";
        }

        public static string VesselWalk(RACommNetwork net, string ModTag="[RealAntennas] ")
        {
            string res = $"{ModTag} VesselWalk()\n";
            res += $"FlightData has {FlightGlobals.Vessels.Count} vessels.\n";
            foreach (Vessel v in FlightGlobals.Vessels.Where(x => x is Vessel && x.Connection == null))
            {
                res += $"Vessel {v.vesselName} has a null connection.\n";
            }
            foreach (Vessel v in FlightGlobals.Vessels.Where(x => x is Vessel && x.Connection is CommNetVessel))
            {
                res += $"Vessel {v.vesselName}: {v.Connection?.name} CommNode: {v.Connection?.Comm}\n";
                foreach (ModuleRealAntenna ra in v.FindPartModulesImplementing<ModuleRealAntenna>())
                {
                    res += $"... Contains RealAntenna part {ra.part} / {ra}.\n";
                }
            }
            return res;
        }

        public static string DumpLink(CommLink link)
        {
            return $"A/B/Both CanRelay: {link.aCanRelay}/{link.bCanRelay}/{link.bothRelay}\n" +
                $"StrengthAR/BR/RR: {link.strengthAR}/{link.strengthBR}/{link.strengthRR}\n" +
                $"Best signal: {link.GetBestSignal()}" +
                $"Cost: {link.cost}\n" +
                $"Start: {link.start}\n" +
                $"End: {link.end}\n" +
                $"GetSignalStrength(start) / (end) / (no relays) / (both relays): {link.GetSignalStrength(link.start)}/{link.GetSignalStrength(link.end)}/{link.GetSignalStrength(false, false)}/{link.GetSignalStrength(true, true)}\n" +
                $"signalStrength: {link.signalStrength}";

            /* Some sample results:
            [LOG 13:25:45.815] [RealAntennasCommNetwork] [Trace] Link: Kerbin: Mesa South -to- RA-1-CS16 : 150727254.72 (Green)
            [LOG 13:25:45.815] [RealAntennasCommNetwork] [Trace] A/B/Both CanRelay: True/False/False
            StrengthAR/BR/RR: 0.609718471365098/0/0
            Best signal: 0.609718471365098  Cost: 150727254.721891
            Start: Node: Kerbin: Mesa South Links=3 : Home  Control  MultiHop : RealAntennas Gain:40dBi TxP:60dBm BW:10000KHz Draw:60dBm Coding:12dB
            End: Node: RA-1-CS16 Links=6 :   : RealAntennas Gain:6dBi TxP:30dBm BW:10000KHz Draw:33dBm Coding:1dB
            GetSignalStrength(start): 0
            GetSignalStrength(end): 0.609718471365098
            GetSignalStrength(no relays): 0

            Note the different Strength fields based on A/B/Both Relay state.  So... there can be a notion of direction?
            */
        }
    }
}
