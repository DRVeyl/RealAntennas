using CommNet;
using UnityEngine;

namespace RealAntennas
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames | ScenarioCreationOptions.AddToAllMissionGames, new GameScenes[] { GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER, GameScenes.EDITOR })]
    public class RACommNetScenario : CommNetScenario
    {
        protected static readonly string ModTag = "[RealAntennasCommNetScenario]";
        public static new Network.RealAntennasRangeModel RangeModel = new Network.RealAntennasRangeModel();
        public static bool Enabled => true;
        public static bool debugWalkLogging = true;
        public static float debugWalkInterval = 60;

        public Network.RACommNetNetwork Network { get => network; }
        private Network.RACommNetNetwork network = null;
        private CommNetUI ui;

        protected override void Start()
        {
            Debug.LogFormat($"{ModTag} Start in {HighLogic.LoadedScene}");
            Initialize();
            ui = gameObject.AddComponent<Network.RACommNetUI>();
            this.network = gameObject.AddComponent<Network.RACommNetNetwork>();
            CommNetScenario.RangeModel = RangeModel;

            RealAntennas.Kerbalism.Kerbalism.DetectKerbalismDLL();

            debugWalkLogging = HighLogic.CurrentGame.Parameters.CustomParams<RAParameters>().debugWalkLogging;
            debugWalkInterval = HighLogic.CurrentGame.Parameters.CustomParams<RAParameters>().debugWalkInterval;
        }

        public override void OnAwake()
        {
            if (RealAntennas.Network.CommNetPatcher.GetCommNetScenarioModule() is ProtoScenarioModule psm)
            {
                Debug.LogFormat($"{ModTag} Scenario check: Found {RATools.DisplayGamescenes(psm)}");
                if (! RealAntennas.Network.CommNetPatcher.CommNetPatched(psm))
                {
                    RealAntennas.Network.CommNetPatcher.UnloadCommNet();
                    DestroyNetwork();
                    Debug.LogFormat($"{ModTag} Rebuilding CommNetBody and CommNetHome list");
                    UnloadHomes();
                    BuildHomes();
                    Debug.LogFormat($"{ModTag} Ignore CommNetScenario ERR immediately following this.");
                }
            }
            base.OnAwake();     // Will set CommNetScenario.Instance to this
        }

        private void OnDestroy()
        {
            if (network != null) Destroy(network);
            if (ui != null) Destroy(ui);
        }

        private void DestroyNetwork()
        {
            if (FindObjectOfType<CommNetNetwork>() is CommNetNetwork cn) DestroyImmediate(cn);
        }

        private void Initialize()
        {
            if (GameDatabase.Instance.GetConfigNode("RealAntennas/RealAntennasCommNetParams/RealAntennasCommNetParams") is ConfigNode RAParamNode)
            {
                Antenna.BandInfo.Init(RAParamNode);
                Antenna.Encoder.Init(RAParamNode);
                TechLevelInfo.Init(RAParamNode);
            }
        }

        private void BuildHomes()
        {
            ConfigNode KopernicusNode = null;
            foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("Kopernicus"))
                KopernicusNode = n;

            if (KopernicusNode != null)
            {
                foreach (ConfigNode bodyNode in KopernicusNode.GetNodes("Body"))
                {
                    string t = bodyNode.GetValue("name");
                    string name = t.Equals("Kerbin") ? FlightGlobals.GetHomeBodyName() : t;

                    if (FlightGlobals.GetBodyByName(name) is CelestialBody body &&
                        bodyNode.GetNode("PQS") is ConfigNode pqsNode &&
                        pqsNode.GetNode("Mods") is ConfigNode pqsModNode)
                    {
                        foreach (ConfigNode cityNode in pqsModNode.GetNodes("City2"))
                        {
                            bool result = false;
                            if (cityNode.TryGetValue("RACommNetStation", ref result) && result)
                            {
                                BuildHome(cityNode, body);
                            }
                        }
                    }
                }
            }
        }

        private void UnloadHomes()
        {
            foreach (CommNetHome home in FindObjectsOfType<CommNetHome>())
            {
                Debug.LogFormat($"{ModTag} Immediately destroying {home}");
                DestroyImmediate(home);
            }
        }
        private void BuildHome(ConfigNode node, CelestialBody body)
        {
            GameObject newHome = new GameObject(body.name);
            Network.RACommNetHome home = newHome.AddComponent<Network.RACommNetHome>();
            home.Configure(node, body);
            Debug.LogFormat($"{ModTag} Built: {home.name} {home.nodeName}");
        }
    }
}