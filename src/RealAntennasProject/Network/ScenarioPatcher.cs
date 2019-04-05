using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas.Network
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames | ScenarioCreationOptions.AddToAllMissionGames, new GameScenes[] { GameScenes.MAINMENU })]
    public class CommNetPatcher : ScenarioModule
    {
        protected static readonly string ModTag = "[CommNetPatcher] ";
        public override void OnAwake()
        {
            Debug.LogFormat(ModTag + "Started");
            if (GetCommNetScenarioModule() is ProtoScenarioModule psm)
            {
                if (!CommNetPatched(psm))
                {
                    Debug.LogFormat("{0} Patching out CommNetScenario", this.ClassName);
                    UnloadCommNet(psm);
                }
            }
        }
        internal static ProtoScenarioModule GetCommNetScenarioModule()
        {
            if (HighLogic.CurrentGame != null && HighLogic.CurrentGame?.scenarios is List<ProtoScenarioModule> l)
            {
                foreach (ProtoScenarioModule psm in l)
                {
                    if (psm.moduleName.Equals("CommNetScenario")) return psm;
                }
            }
            return null;
        }
        internal static bool CommNetPatched(ProtoScenarioModule psm) => psm != null ? psm.targetScenes.Contains(GameScenes.CREDITS) : false;
        internal static void UnloadCommNet(ProtoScenarioModule psm = null)
        {
            Debug.LogFormat(ModTag + "Patching CommNet's TargetScenes");
            if (psm == null) psm = GetCommNetScenarioModule();
            if (psm != null) psm.SetTargetScenes(new GameScenes[] { GameScenes.CREDITS });
        }
    }
}
