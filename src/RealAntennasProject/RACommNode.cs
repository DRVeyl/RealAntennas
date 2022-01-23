using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas
{
    public class RACommNode : CommNet.CommNode
    {
        protected const string ModTag = "[RealAntennasCommNode]";

        public List<RealAntenna> RAAntennaList { get; set; }
        public CelestialBody ParentBody { get; set; }
        public Vessel ParentVessel { get; set; }

        public RACommNode() : base() { }
        public RACommNode(Transform t) : base(t)
        {
            ParentBody = null;
            ParentVessel = null;
            RAAntennaList = new List<RealAntenna>();
        }
        public RACommNode(CommNet.CommNode cn) : this(cn.transform) { }


        public Vector3d GetSurfaceNormalVector()
        {
            CelestialBody body = ParentBody ? ParentBody : ParentVessel?.mainBody;
            if (body == null) return Vector3d.zero;
            body.GetLatLonAlt(position, out double lat, out double lon, out double _);
            return body.GetSurfaceNVector(lat, lon);
        }

        public bool CanComm()
        {
            if (ParentBody != null) return true;
            return (ParentVessel?.Connection is RACommNetVessel raCNV) && raCNV.powered && raCNV.CanComm;
        }

        public virtual string DebugToString()
        {
            string s = base.ToString();
            foreach (RealAntenna ra in RAAntennaList)
            {
                s += $"{ra} ";
            }
            return s;
        }

        public virtual RealAntenna AntennaTowardsHome()
        {
            if (Net is CommNet.CommNetwork && new CommNet.CommPath() is CommNet.CommPath path 
                && Net.FindHome(this, path) && this[path.First.end] is RACommLink link)
            {
                return link.start.Equals(this) ? link.FwdAntennaTx : link.RevAntennaTx;
            }
            return null;
        }
    }
}