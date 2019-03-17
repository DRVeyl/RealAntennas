using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace RealAntennas
{
    public class RACommNode : CommNet.CommNode
    {
        protected static readonly string ModTag = "[RealAntennasCommNode] ";
        protected static readonly string ModTrace = ModTag + "[Trace] ";

        public RealAntenna RAAntenna { get; set; }
        public CelestialBody ParentBody { get; set; }
        public Vessel ParentVessel { get; set; }

        public RACommNode() => Debug.Log("RACommNode constructor()");
        public RACommNode(CommNet.CommNode cn) : base()
        {
            antennaRelay = cn.antennaRelay;
            antennaTransmit = cn.antennaTransmit;
            ParentBody = null;
            ParentVessel = null;

            bestCost = cn.bestCost;
            bestLink = cn.bestLink;
            bestLinkNode = cn.bestLinkNode;
            distanceOffset = cn.distanceOffset;
            isControlSource = cn.isControlSource;
            isControlSourceMultiHop = cn.isControlSourceMultiHop;
            isHome = cn.isHome;
            isInCandidateList = cn.isInCandidateList;
            // Don't copy delegates.  Let the parent do so (because who knows where they point.)
            // OnLinkCreateSignalModifier = cn.OnLinkCreateSignalModifier;
            //OnNetworkPostUpdate = cn.OnNetworkPostUpdate;
            //OnNetworkPreUpdate = cn.OnNetworkPreUpdate;
            RAAntenna = new RealAntenna(cn.name);
            pathingID = cn.pathingID;
            scienceCurve = cn.scienceCurve;
            //            displayName = cn.displayName; // Certain I'm using displayName wrong.
            displayName = cn.name;
            name = cn.name;
            position = cn.position;
            transform = cn.transform;
            occluder = cn.occluder;
            precisePosition = cn.precisePosition;
        }

        public Vector3d GetSurfaceNormalVector()
        {
            ParentBody.GetLatLonAlt(position, out double lat, out double lon, out double alt);
            return ParentBody.GetSurfaceNVector(lat, lon);
        }

        public override string ToString()
        {
            return string.Format("{0}: {1}", base.ToString(), RAAntenna);
        }

        public override double GetSignalStrengthMultiplier(CommNet.CommNode b)
        {
            Debug.LogFormat(ModTrace + "GetSignalStrengthMultiplier({1}) for {0}",this,b);
            double res = base.GetSignalStrengthMultiplier(b);
            Debug.LogFormat(ModTrace + "GetSignalStrengthMultiplier() result {0}", res);
            return res;
        }
        public static string DebugDump(CommNet.CommNode obj)
        {
            string res = "CommNode is null";
            if (obj != null)
            {
                res = string.Format("Name:{0} DisplayName:{1} Cost:{2} Link:{3} LinkNode:{4} Position:{5} Transform:{6}",
                                            obj.name, obj.displayName, obj.bestCost, obj.bestLink, obj.bestLinkNode, obj.position, obj.transform);
            }
            return res;
        }

        public static string DebugDumpDelegates(CommNet.CommNode obj)
        {
            string res = "";
            if (obj.OnLinkCreateSignalModifier != null)
            {
                List<Delegate> delegates = obj.OnLinkCreateSignalModifier.GetInvocationList().ToList();
                foreach (Delegate dgel in delegates)
                {
                    res += string.Format("  CreateSignal Delegate: {0} / {1}\n",
                        obj.OnLinkCreateSignalModifier.Target,
                        obj.OnLinkCreateSignalModifier.Method);
                }
            }
            res += "OnNetworkPostUpdate Actions:\n" + RealAntennasTools.DumpAction(obj.OnNetworkPostUpdate);
            res += "OnNetworkPreUpdate Actions:\n" + RealAntennasTools.DumpAction(obj.OnNetworkPreUpdate);
            return res;
        }
    }
}