using System;
using UnityEngine;

namespace RealAntennas
{
    public class Planner
    {
        private static readonly string ModTag = "[RealAntennas.Planner]";
        public PlannerGUI plannerGUI;
        public ModuleRealAntenna parent;

        private void SetPlannerTargetStr(string value) => parent.plannerTargetString = value;
        private void SetPlanningResult(string dl, string ul)
        {
            parent.sDownlinkPlanningResult = dl;
            parent.sUplinkPlanningResult = ul;
        }

        private object PlannerTarget;
        private float PlannerAltitude { get => parent.plannerAltitude; set => parent.plannerAltitude = value; }
        private RealAntenna RAAntenna => parent.RAAntenna;
        private bool PlanningEnabled => parent.planningEnabled;

        public Planner(ModuleRealAntenna p)
        {
            plannerGUI = new PlannerGUI() { parent = this };
            parent = p;
        }

        public void ConfigTarget(string sTarget, object target)
        {
            SetPlannerTargetStr(sTarget);
            PlannerTarget = target;
            RecalculatePlannerFields();
        }

            internal void SetPlanningFields()
        {
            { if (parent.Events[nameof(parent.AntennaPlanningGUI)] is BaseEvent be) be.active = PlanningEnabled; }
            { if (parent.Fields[nameof(parent.plannerTargetString)] is BaseField bf) bf.guiActive = bf.guiActiveEditor = PlanningEnabled; }
            { if (parent.Fields[nameof(parent.sDownlinkPlanningResult)] is BaseField bf) bf.guiActive = bf.guiActiveEditor = PlanningEnabled; }
            { if (parent.Fields[nameof(parent.sUplinkPlanningResult)] is BaseField bf) bf.guiActive = bf.guiActiveEditor = PlanningEnabled; }
            { if (parent.Fields[nameof(parent.plannerAltitude)] is BaseField bf) bf.guiActive = bf.guiActiveEditor = PlanningEnabled; }
        }

        internal void OnPlanningEnabledChange(BaseField f, object obj) 
        {  
            SetPlanningFields();
            CheckAntennaExtended();
            RecalculatePlannerFields();
        }
        internal void OnPlanningAltitudeChange(BaseField f, object obj)
        {
            if (PlannerAltitude < 1) PlannerAltitude = 1;
            RecalculatePlannerFields();
        }

        internal void RecalculatePlannerFields()
        {
            RACommNetwork net = (RACommNetScenario.Instance as RACommNetScenario)?.Network?.CommNet as RACommNetwork;
            if (!(net is RACommNetwork)) return;
            GameObject peerObj = new GameObject("localAntenna");
            RACommNode peerComm;
            RealAntenna peerAnt = null;

            GameObject selfObj = new GameObject("remoteAntenna");
            RACommNode selfComm = new RACommNode(selfObj.transform) { ParentVessel = parent.vessel };
            RealAntenna selfAnt = new RealAntennaDigital(RAAntenna) { ParentNode = selfComm };
            bool showAltitude = PlanningEnabled;
            Vector3d dir = Vector3d.up;
            double furthestDistance = PlannerAltitude * 1e6;
            double closestDistance = PlannerAltitude * 1e6;

            CelestialBody home = Planetarium.fetch.Home;
            if (PlannerTarget is CelestialBody b)
            {
                showAltitude = showAltitude && (b == home);
                if (RATools.HighestGainCompatibleDSNAntenna(net.Nodes, RAAntenna) is RealAntenna DSNAntenna)
                {
                    peerComm = new RACommNode(peerObj.transform) { ParentBody = home };
                    peerAnt = new RealAntennaDigital(DSNAntenna) { ParentNode = peerComm };
                    peerAnt.ParentNode.transform.SetPositionAndRotation(home.position + home.GetRelSurfacePosition(0, 0, 0), Quaternion.identity);
                    peerAnt.ParentNode.precisePosition = peerAnt.ParentNode.position;
                    dir = home.GetRelSurfaceNVector(0, 0).normalized;
                    if (b != home)
                    {
                        double maxAlt = (b == Planetarium.fetch.Sun) ? 0 : b.orbit.ApA;
                        double minAlt = (b == Planetarium.fetch.Sun) ? 0 : b.orbit.PeA;
                        double sunDistance = (Planetarium.fetch.Sun.position - home.position).magnitude;
                        furthestDistance = maxAlt + sunDistance;
                        closestDistance = (maxAlt < sunDistance) ? sunDistance - maxAlt : minAlt - sunDistance;
                    }
                } else
                {
                    SetPlanningResult("No compatible ground station", "Check TS upgrade level!");
                }
            } else if (PlannerTarget is RealAntenna ra)
            {
                peerComm = new RACommNode(peerObj.transform) { ParentVessel = ra?.Parent?.vessel };
                peerAnt = new RealAntennaDigital(ra) { ParentNode = peerComm };
                peerAnt.ParentNode.transform.SetPositionAndRotation(home.position + (closestDistance / 2 * Vector3d.up), Quaternion.identity);
                peerAnt.ParentNode.precisePosition = peerAnt.ParentNode.position;
                if (peerAnt.ToTarget != Vector3.zero) dir = peerAnt.ToTarget.normalized;
            }
            if (peerAnt != null)
            {
                if (parent.Fields[nameof(parent.plannerAltitude)] is BaseField bf) bf.guiActive = bf.guiActiveEditor = showAltitude;
                Vector3d adj = dir * closestDistance;
                selfAnt.ParentNode.transform.SetPositionAndRotation(peerAnt.Position + adj, Quaternion.identity);
                selfAnt.ParentNode.precisePosition = selfAnt.ParentNode.position;

                double rxp = parent.TxPower + parent.Gain - Physics.PathLoss(closestDistance, parent.RFBandInfo.Frequency) + peerAnt.Gain;
                double fwdDataRateHigh = selfAnt.BestDataRateToPeer(peerAnt);
                double revDataRateHigh = peerAnt.BestDataRateToPeer(selfAnt);
                string dl = RATools.PrettyPrintDataRate(fwdDataRateHigh);
                string ul = RATools.PrettyPrintDataRate(revDataRateHigh);

                if (furthestDistance != closestDistance)
                {
                    adj = dir * furthestDistance;
                    selfAnt.ParentNode.transform.SetPositionAndRotation(peerAnt.Position + adj, Quaternion.identity);
                    selfAnt.ParentNode.precisePosition = selfAnt.ParentNode.position;

                    rxp = parent.TxPower + parent.Gain - Physics.PathLoss(furthestDistance, parent.RFBandInfo.Frequency) + peerAnt.Gain;
                    double fwdDataRateLow = selfAnt.BestDataRateToPeer(peerAnt);
                    double revDataRateLow = peerAnt.BestDataRateToPeer(selfAnt);
                    dl += $" - {RATools.PrettyPrintDataRate(fwdDataRateLow)}";
                    ul += $" - {RATools.PrettyPrintDataRate(revDataRateLow)}";
                }

                SetPlanningResult(dl, ul);
            }
            peerObj.DestroyGameObject();
            selfObj.DestroyGameObject();
        }

        private void CheckAntennaExtended()
        {
            if (parent.Deployable && !parent.Deployed)
            {
                ScreenMessages.PostScreenMessage("You must deploy this antenna for the planner to work", 8f, ScreenMessageStyle.UPPER_CENTER, Color.red);
            }
        }
    }
}
