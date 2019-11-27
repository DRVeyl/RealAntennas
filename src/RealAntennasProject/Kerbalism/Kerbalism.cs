using System;
using System.Collections.Generic;
using System.Reflection;

namespace RealAntennas.Kerbalism
{
    public class Kerbalism
    {
        public static readonly string ModTag = "[RAKerbalismLink] ";
        public static Assembly KerbalismAssembly = null;
        public static void RAKerbalismLinkHandler(object p1, Vessel v)
        {
            if (v.Connection is RACommNetVessel raCNV && raCNV.Comm is RACommNode node)
            {
                double ec=0,rate = 0, strength = 0, packetInterval = 1.0f;
                double ecIdle = v.loaded ? 0 : raCNV.UnloadedPowerDraw();
                ec = ecIdle;
                int status = 2;
                bool powered = (bool) p1.GetType().GetField("powered").GetValue(p1);
                bool transmitting = (bool) p1.GetType().GetField("transmitting").GetValue(p1);
                string target_name = "NotConnected";
                List<string[]> sList = new List<string[]>();
                if (!v.loaded) raCNV.powered = powered;
                if (powered && node.AntennaTowardsHome() is RealAntenna ra)
                {
                    CommNet.CommPath path = new CommNet.CommPath();
                    (node.Net as RACommNetwork).FindHome(node, path);
                    status = !raCNV.IsConnectedHome ? 2 : path.Count == 1 ? 0 : 1;
                    rate = (node.Net as RACommNetwork).MaxDataRateToHome(node) / (8 * 1024 * 1024);    // Convert rate from bps to MBps;
                    if (transmitting) ec += ra.PowerDrawLinear * packetInterval * 1e-6;    // 1 EC/sec = 1KW.  Draw(mw) * interval(sec) * mW -> kW conversion
                    if (node[path.First.end] is RACommLink link)
                    {
                        strength = link.start.Equals(node) ? link.FwdMetric : link.RevMetric;
                    }
                    foreach (CommNet.CommLink clink in path)
                    {
                        sList.Add(new string[1] { clink.end.name });
                    }
                    //sList.Add(new string[1] { path.First.end.name });
                    target_name = path.First.end.name;
                }

                p1.GetType().GetField("linked").SetValue(p1, raCNV.IsConnectedHome); // Link Status
                p1.GetType().GetField("ec").SetValue(p1, ec);               // EC/s
                p1.GetType().GetField("rate").SetValue(p1, rate);           // Rate in MB/s
                p1.GetType().GetField("status").SetValue(p1, status);       // 0=direct, 1=indirect, 2=none
                p1.GetType().GetField("strength").SetValue(p1, strength);   // Signal quality indicator (float 0..1)
                p1.GetType().GetField("target_name").SetValue(p1, target_name);
                p1.GetType().GetField("control_path").SetValue(p1, sList);
                p1.GetType().GetField("ec_idle")?.SetValue(p1, ecIdle);

                //Debug.LogFormat($"{ModTag}Rate: {RATools.PrettyPrintDataRate(rate * 8 * 1024 * 1024)} EC: {ec:F4}  Linked:{raCNV.IsConnectedHome}  Strength: {strength:F2}  Target: {target_name}");
            }
        }
        public static bool DetectKerbalismDLL()
        {
            foreach (var a in AssemblyLoader.loadedAssemblies)
            {
                if (a.name.StartsWith("Kerbalism") && 
                    !a.name.StartsWith("KerbalismBoot") &&
                    a.assembly.GetType("KERBALISM.API") is Type KerbalismAPIType
                    )
                {
                    KerbalismAssembly = a.assembly;
                    MethodInfo baseMethod = typeof(Kerbalism).GetMethod(nameof(RAKerbalismLinkHandler));
                    var x = baseMethod;
                    var fInf = KerbalismAPIType.GetField("Comm", BindingFlags.Public | BindingFlags.Static);
                    var val = fInf.GetValue(null);
                    var mInf = val.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
                    mInf.Invoke(val, new object[1] { x });
                    return true;
                }
            }
            return false;
        }
    }
}
