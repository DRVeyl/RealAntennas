using KSP.UI.Screens.Flight;
using UnityEngine;

namespace RealAntennas.MapUI
{
    public class RATelemetryUpdate : TelemetryUpdate
    {
        protected override void Awake()
        {
            // Do nothing, because we haven't actually copied any fields over...
            Debug.Log("RATelemetryUpdate Awake()");
            TelemetryUpdate.Instance = this;
//            base.Awake();
        }

        public void Copy(TelemetryUpdate t)
        {
            modeButton = t.modeButton;
            NOSIG = t.NOSIG;
            NOEP = t.NOEP;
            BLK = t.BLK;
            AUP = t.AUP;
            ADN = t.ADN;
            EP0 = t.EP0;
            EP1 = t.EP1;
            EP2 = t.EP2;
            CK1 = t.CK1;
            CK2 = t.CK2;
            CK3 = t.CK3;
            CP1 = t.CP1;
            CP2 = t.CP2;
            CP3 = t.CP3;
            SS0 = t.SS0;
            SS1 = t.SS1;
            SS2 = t.SS2;
            SS3 = t.SS3;
            SS4 = t.SS4;
            arrow_icon = t.arrow_icon;
            arrow_tooltip = t.arrow_tooltip;
            firstHop_icon = t.firstHop_icon;
            firstHop_tooltip = t.firstHop_tooltip;
            lastHop_icon = t.lastHop_icon;
            lastHop_tooltip = t.lastHop_tooltip;
            control_icon = t.control_icon;
            control_tooltip = t.control_tooltip;
            signal_icon = t.signal_icon;
            signal_tooltip = t.signal_tooltip;
        }

        protected override void Update()
        {
            base.Update();
            SetIcon(this.arrow_icon, BLK, true);
        }

        public static void Install()
        {
            if (TelemetryUpdate.Instance is TelemetryUpdate tu)
            {
                SignalToolTipController tc = tu.gameObject.AddComponent<SignalToolTipController>();
                tc.Copy(tu.signal_tooltip);
                DestroyImmediate(tu.signal_tooltip);
                tu.signal_tooltip = tc;

                RATelemetryUpdate telem = tu.transform.parent.gameObject.AddComponent<RATelemetryUpdate>();
                telem.Copy(tu);
                DestroyImmediate(tu);
            }
        }
    }
}
