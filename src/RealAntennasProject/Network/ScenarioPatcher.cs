using System.Collections.Generic;
using UnityEngine;

namespace RealAntennas.Network
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames | ScenarioCreationOptions.AddToAllMissionGames, new GameScenes[] { GameScenes.MAINMENU })]
    public class CommNetPatcher : ScenarioModule
    {
        private const string ModTag = "[CommNetPatcher]";
        public override void OnAwake()
        {
            Debug.Log($"{ModTag} Started");
            VesselModuleManager.RemoveModuleOfType(typeof(CommNet.CommNetVessel));
            if (GetCommNetScenarioModule() is ProtoScenarioModule psm && !CommNetPatched(psm))
            {
                Debug.Log($"{ClassName} Patching out CommNetScenario");
                UnloadCommNet(psm);
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
            Debug.Log($"{ModTag} Patching CommNet's TargetScenes");
            psm ??= GetCommNetScenarioModule();
            if (psm != null) psm.SetTargetScenes(new GameScenes[] { GameScenes.CREDITS });
        }
    }
}
