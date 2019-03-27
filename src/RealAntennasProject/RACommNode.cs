using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas
{
    public class RACommNode : CommNet.CommNode
    {
        protected static readonly string ModTag = "[RealAntennasCommNode] ";
        protected static readonly string ModTrace = ModTag + "[Trace] ";

        public List<RealAntenna> RAAntennaList { get; set; }
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
            RAAntennaList = null;
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
            string s = string.Format("{0} : ", base.ToString());
            foreach (RealAntenna ra in RAAntennaList)
            {
                s += string.Format("{0}  ",ra);
            }
            return s;
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
    }
}