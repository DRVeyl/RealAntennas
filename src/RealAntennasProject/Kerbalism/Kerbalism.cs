using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Reflection;

namespace RealAntennas.Kerbalism
{
    public static class GenericDelegateFactory
    {
        public static object CreateDelegateByParameter(Type p1Type, Type p2Type, object target, MethodInfo method)
        {
            var createDelegate = typeof(GenericDelegateFactory).GetMethod("CreateDelegate").MakeGenericMethod(new Type[] { p1Type, p2Type });
            return createDelegate.Invoke(null, new object[] { target, method });
        }

        public static Action<T1,T2> CreateDelegate<T1,T2>(object target, MethodInfo method)
        {
            return (Action<T1,T2>)Delegate.CreateDelegate(typeof(Action<T1,T2>), target, method);
        }
    }
    public class Kerbalism
    {
        public static readonly string ModTag = "[RAKerbalismLink] ";
        public static Assembly KerbalismAssembly = null;
        public static void MyCommHandler<T>(T p1, Vessel v)
        {
            if (p1.GetType() != KerbalismAssembly.GetType("KERBALISM.AntennaInfo"))
            {
                Debug.LogFormat(ModTag + "Somehow we got called with the wrong type for {0} = {1}", p1, p1.GetType());
            }
            if (v.Connection is RACommNetVessel raCNV &&
                raCNV.Comm is RACommNode node &&
                node.AntennaTowardsHome() is RealAntenna ra &&
                KerbalismAssembly.GetType("KERBALISM.AntennaInfo") is Type KerbalismAntennaInfoType)
            {
                CommNet.CommPath path = new CommNet.CommPath();
                (node.Net as RACommNetwork).FindHome(node, path);
                double rate = (node.Net as RACommNetwork).MaxDataRateToHome(node) / 8e6;    // Convert rate from bps to MBps
                double packetInterval = 1.0F;
                double ec = ra.PowerDrawLinear * packetInterval * 1e-6;    // 1 EC/sec = 1KW.  Draw(mw) * interval(sec) * mW -> kW conversion
                double strength = 0;
                if (node[path.First.end] is RACommLink link)
                {
                    strength = link.start.Equals(node) ? link.FwdCI: link.RevCI;
                }

                p1.GetType().GetField("linked").SetValue(p1, raCNV.IsConnectedHome); // Link Status
                p1.GetType().GetField("ec").SetValue(p1, ec);       // EC/s
                p1.GetType().GetField("rate").SetValue(p1, rate);   // Rate in MB/s
                p1.GetType().GetField("status").SetValue(p1, !raCNV.IsConnectedHome ? 2 : path.Count == 1 ? 0 : 1);      // 0=direct, 1=indirect, 2=none
                p1.GetType().GetField("strength").SetValue(p1, strength);   // Signal quality indicator (float 0..1)
                p1.GetType().GetField("target_name").SetValue(p1, path.First.end.ToString());
                List<string[]> sList = new List<string[]>();
                foreach (CommNet.CommLink clink in path)
                {
                    sList.Add(new string[1] { clink.end.name });
                }
                p1.GetType().GetField("control_path").SetValue(p1, sList);

                Debug.LogFormat(ModTag + "Rate: {0:F1} EC: {1:F4}  Linked:{2}  Strength: {3:F2}  Target: {4}  CPath: {5}",
                    rate, ec, raCNV.IsConnectedHome, strength, path.First.end, sList);
            }

            /*
            antennaInfo.control_path = control_path; // List<string[title, value, tooltip]> for display in the UI (value+tooltip are optional)
            */
        }
        public static bool DetectKerbalismDLL()
        {
            foreach (var a in AssemblyLoader.loadedAssemblies)
            {
                if (a.name.StartsWith("Kerbalism") && 
                    !a.name.StartsWith("KerbalismBoot") &&
                    a.assembly.GetType("KERBALISM.API") is Type KerbalismAPIType &&
                    a.assembly.GetType("KERBALISM.AntennaInfo") is Type KerbalismAntennaInfoType &&
                    KerbalismAPIType.GetField("Comm", BindingFlags.Public | BindingFlags.Static) is var comm 
                    )
                {
                    KerbalismAssembly = a.assembly;
                    MethodInfo baseMethod = typeof(Kerbalism).GetMethod("MyCommHandler");
                    MethodInfo myGenericMethod = baseMethod.MakeGenericMethod(new Type[] { KerbalismAntennaInfoType });
                    var x = GenericDelegateFactory.CreateDelegateByParameter(KerbalismAntennaInfoType, typeof(Vessel), null, myGenericMethod);
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
