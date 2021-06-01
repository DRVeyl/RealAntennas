using System;
using UnityEngine;

namespace RealAntennas.Targeting
{
    // Represent the target that a directional antenna points at.
    // Track position and represent in transform
    public class AntennaTarget : MonoBehaviour, IConfigNode
    {
        public const string nodeName = "TARGET";
        public enum TargetMode { Vessel, BodyCenter, BodyLatLonAlt, AzEl, OrbitRelative };

        public static AntennaTarget LoadFromConfig(ConfigNode node, RealAntenna ra)
        {
            var go = AntennaTargetManager.AcquireTarget(ra);
            foreach (var del in go.GetComponents<AntennaTarget>())
                Destroy(del);
            AntennaTarget t = null;
            string n = string.Empty;
            if (node.TryGetValue("name", ref n))
            {
                if (n.Equals(TargetMode.BodyLatLonAlt.ToString()))
                    t = go.AddComponent<AntennaTargetLatLonAlt>();
                else if (n.Equals(TargetMode.Vessel.ToString()))
                    t = go.AddComponent<AntennaTargetVessel>();
                else if (n.Equals(TargetMode.AzEl.ToString()))
                    t = go.AddComponent<AntennaTargetAzEl>();
                else if (n.Equals(TargetMode.OrbitRelative.ToString()))
                    t = go.AddComponent<AntennaTargetOrbitRelative>();
                t?.Load(node);
            }
            return t;
        }
        public virtual void Awake()
        {
            Debug.Log("AntennaTarget.Awake()");
        }

        public virtual void FixedUpdate()
        {
        }

        public virtual void Load(ConfigNode node)
        {
        }

        public virtual void Save(ConfigNode node)
        {
        }

        public virtual bool Validate() => true;
    }

    public class AntennaTargetLatLonAlt : AntennaTarget
    {
        public CelestialBody body;
        [Persistent] public string bodyName = string.Empty;
        [Persistent] public Vector3 latLonAlt = Vector3.zero;
        public override string ToString() => $"{bodyName}:({latLonAlt.x:F2}:{latLonAlt.y:F2}:{latLonAlt.z:F0})";

        public override void Load(ConfigNode node)
        {
            base.Load(node);
            ConfigNode.LoadObjectFromConfig(this, node);
            body = FlightGlobals.GetBodyByName(bodyName);
            if (body is CelestialBody)
            {
                transform.SetParent(body.transform);
                transform.localPosition = body.GetRelSurfacePosition(latLonAlt.x, latLonAlt.y, latLonAlt.z);
                transform.localRotation = Quaternion.identity;
            }
        }

        public override void Save(ConfigNode node)
        {
            base.Save(node);
            var tgtNode = node.AddNode(nodeName);
            tgtNode.AddValue("name", $"{TargetMode.BodyLatLonAlt}");
            var res = ConfigNode.CreateConfigFromObject(this, tgtNode);
        }
    }

    public class AntennaTargetVessel : AntennaTarget
    {
        public Vessel vessel;
        [Persistent] public string vesselId = string.Empty;
        public override string ToString() => $"{vessel?.name}";
        public override void Load(ConfigNode node)
        {
            base.Load(node);
            ConfigNode.LoadObjectFromConfig(this, node);
            if (FlightGlobals.FindVessel(new Guid(vesselId)) is Vessel v)
            {
                vessel = v;
                transform.SetParent(vessel.transform);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
        }

        public override void Save(ConfigNode node)
        {
            base.Save(node);
            var tgtNode = node.AddNode(nodeName);
            tgtNode.AddValue("name", $"{TargetMode.Vessel}");
            var res = ConfigNode.CreateConfigFromObject(this, tgtNode);
        }

        public override bool Validate() => vessel is Vessel || FlightGlobals.FindVessel(new Guid(vesselId)) is Vessel;
    }

    public class AntennaTargetAzEl : AntennaTarget
    {
        public Vessel vessel;
        [Persistent] public string vesselId = string.Empty;
        [Persistent] public float azimuth = 0;
        [Persistent] public float elevation = 0;
        public override string ToString() => $"Az/El: {azimuth:F1}/{elevation:F1}";
        public override void Load(ConfigNode node)
        {
            base.Load(node);
            ConfigNode.LoadObjectFromConfig(this, node);
            if (FlightGlobals.FindVessel(new Guid(vesselId)) is Vessel v)
            {
                vessel = v;
                var dir = GetPosition();
                transform.position = vessel.GetWorldPos3D() + dir * 1e6f;
            }
        }

        public override void Save(ConfigNode node)
        {
            base.Save(node);
            var tgtNode = node.AddNode(nodeName);
            tgtNode.AddValue("name", $"{TargetMode.AzEl}");
            var res = ConfigNode.CreateConfigFromObject(this, tgtNode);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (vessel is Vessel)
            {
                var dir = GetPosition();
                transform.position = vessel.GetWorldPos3D() + dir * 1e6f;
            }
        }

        private Vector3d GetPosition()
        {
            var dir = Vector3.zero;
            if (vessel is Vessel)
            {
                var cb = vessel.mainBody;
                var north = cb.GetSurfaceNVector(90, 0);
                var outward = cb.GetSurfaceNVector(vessel.latitude, vessel.longitude);
                dir = Vector3.RotateTowards(outward, north, Mathf.PI / 2, 0);  // Point towards north, tangent to surface
                dir = Quaternion.AngleAxis(azimuth, outward) * dir;
                dir = Vector3.RotateTowards(dir, outward, elevation * Mathf.PI / 180, 0);
            }
            return dir;
        }

        public override bool Validate() => vessel is Vessel || FlightGlobals.FindVessel(new Guid(vesselId)) is Vessel;
    }


    public class AntennaTargetOrbitRelative : AntennaTarget
    {
        public Vessel vessel;
        [Persistent] public string vesselId = string.Empty;
        [Persistent] public float forward = 0;
        [Persistent] public float elevation = 0;
        public override string ToString() => $"OrbitRelative: {forward:F1}/{elevation:F1}";
        public override void Load(ConfigNode node)
        {
            base.Load(node);
            ConfigNode.LoadObjectFromConfig(this, node);
            if (FlightGlobals.FindVessel(new Guid(vesselId)) is Vessel v)
            {
                vessel = v;
                var dir = GetPosition();
                transform.position = vessel.GetWorldPos3D() + dir * 1e6f;
            }
        }

        public override void Save(ConfigNode node)
        {
            base.Save(node);
            var tgtNode = node.AddNode(nodeName);
            tgtNode.AddValue("name", $"{TargetMode.OrbitRelative}");
            var res = ConfigNode.CreateConfigFromObject(this, tgtNode);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (vessel is Vessel)
            {
                var dir = GetPosition();
                transform.position = vessel.GetWorldPos3D() + dir * 1e6f;
            }
        }

        private Vector3d GetPosition()
        {
            var dir = Vector3.zero;
            if (vessel is Vessel)
            {
                var cb = vessel.mainBody;
                var fwd = vessel.orbit?.GetRelativeVel().normalized ?? Vector3.zero;
                var outward = cb.GetSurfaceNVector(vessel.latitude, vessel.longitude);
                var up = Vector3.Cross(fwd, outward);
                dir = Quaternion.AngleAxis(forward, up) * fwd;
                dir = Vector3.RotateTowards(dir, up, elevation * Mathf.PI / 180, 0);
            }
            return dir;
        }

        public override bool Validate() => vessel is Vessel || FlightGlobals.FindVessel(new Guid(vesselId)) is Vessel;
    }
}
