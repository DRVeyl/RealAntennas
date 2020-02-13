using CommNet;
using System.Collections.Generic;

namespace RealAntennas.MapUI
{
    class SignalToolTipController : TooltipController_SignalStrength
    {
        private List<KeyValuePair<CommLink, Tooltip_SignalStrengthItem>> items = new List<KeyValuePair<CommLink, Tooltip_SignalStrengthItem>>();

        public void Copy(TooltipController_SignalStrength tt)
        {
            prefab = tt.prefab;
            itemPrefab = tt.itemPrefab;
            TooltipPrefabInstance = tt.TooltipPrefabInstance;
            TooltipPrefabInstanceTransform = tt.TooltipPrefabInstanceTransform;
            TooltipPrefabType = tt.TooltipPrefabType;
            useGUILayout = tt.useGUILayout;
        }

        protected override void Awake()
        {
            base.Awake();
            name = "RealAntennasSignalIcon";
        }

        protected override void UpdateList()
        {
            if (FlightGlobals.ActiveVessel.Connection?.Comm is RACommNode comm &&
                FlightGlobals.ActiveVessel.Connection?.ControlPath is CommPath path)
            {
                if (items.Count != path.Count)
                {
                    ClearList();
                    foreach (CommLink link in path)
                    {
                        Tooltip_SignalStrengthItem x = Instantiate(itemPrefab);
                        x.transform.SetParent(tooltip.listParent, false);
                        items.Add(new KeyValuePair<CommLink, Tooltip_SignalStrengthItem>(link, x));
                    }
                }

                foreach (KeyValuePair<CommLink, Tooltip_SignalStrengthItem> item in items)
                {
                    RACommLink link = item.Key.start[item.Key.end] as RACommLink;
                    double linkRate = link.start.Equals(item.Key.start) ? link.FwdDataRate : link.RevDataRate;
                    RACommNode curNode = (link.start.Equals(item.Key.start) ? link.start : link.end) as RACommNode;
                    RACommNode oppNode = (link.start.Equals(item.Key.start) ? link.end : link.start) as RACommNode;
                    Tooltip_SignalStrengthItem x = item.Value;
                    x.Setup(item.Key.signal, $"{oppNode.displayName}: {RATools.PrettyPrintDataRate(linkRate)}");
                    x.transform.SetAsLastSibling();
                }
                if (path.Count > 0 && path.First is CommLink link1)
                {
                    if (link1.start[link1.end] is RACommLink raLink)
                    {
                        double rate = (comm.Net as RACommNetwork).MaxDataRateToHome(comm);
                        double txMetric = raLink.start.Equals(comm) ? raLink.FwdMetric : raLink.RevMetric;
                        double rxMetric = raLink.start.Equals(comm) ? raLink.RevMetric : raLink.FwdMetric;
                        tooltip.title.SetText($"Signal (Tx/Rx) : {txMetric * 100:F0}%/{rxMetric*100:F0}%  -  {RATools.PrettyPrintDataRate(rate)}");
                    }
                }
            }
        }

        protected override void ClearList()
        {
            foreach (KeyValuePair<CommLink, Tooltip_SignalStrengthItem> item in items)
            {
                if (item.Value != null)
                    Destroy(item.Value.gameObject);
            }
            items.Clear();
        }
    }
}
