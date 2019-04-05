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
            if (ParentBody == null) return Vector3d.zero;
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

        public virtual RealAntenna AntennaTowardsHome()
        {
            CommNet.CommPath path = new CommNet.CommPath();
            if (!(Net.FindHome(this, path))) return null;
            if (this[path.First.end] is RACommLink link)
            {
                return link.start.Equals(this) ? link.FwdAntennaTx: link.RevAntennaTx;
            }
            return null;
        }
    }
}