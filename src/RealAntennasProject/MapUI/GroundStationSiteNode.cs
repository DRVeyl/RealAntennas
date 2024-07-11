using KSP.UI.Screens.Mapview;
using UnityEngine;

namespace RealAntennas.MapUI
{
    public class GroundStationSiteNode : ISiteNode
    {
        public readonly RACommNode node;

        public GroundStationSiteNode(RACommNode node)
        {
            this.node = node;
        }

        public string GetName() => node.name;
        public Transform GetWorldPos() => node.transform;

        public void UpdateNodeCaption(MapNode mn, MapNode.CaptionData data)
        {
            data.captionLine1 = $"{node.name}";
            data.captionLine2 = RATools.PrettyPrint(node.RAAntennaList);
            //data.captionLine3 = "CapLine3";
        }
    }
}
