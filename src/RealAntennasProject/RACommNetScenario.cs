using CommNet;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RealAntennas
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames | ScenarioCreationOptions.AddToAllMissionGames, new GameScenes[] { GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER, GameScenes.EDITOR })]
    public class RACommNetScenario : CommNetScenario
    {
        private const string ModTag = "[RealAntennasCommNetScenario]";
        private const float DisabledNotifyInterval = 10;
        private static bool staticInit = false;
        public static bool debugWalkLogging = true;
        public static float debugWalkInterval = 60;
        public Metrics metrics = new Metrics();
        public static readonly Assembly assembly = Assembly.GetExecutingAssembly();
        public static readonly System.Diagnostics.FileVersionInfo info = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
        public static readonly Dictionary<string, Network.RACommNetHome> GroundStations = new Dictionary<string, Network.RACommNetHome>();
        public static int GroundStationTechLevel = 0;
        public static int MaxTL => Mathf.Min(TechLevelInfo.MaxTL, HighLogic.CurrentGame.Parameters.CustomParams<RAParameters>().MaxTechLevel);
        public static int minRelayTL = 0;

        public Network.RACommNetNetwork Network { get; private set; } = null;
        public MapUI.RACommNetUI UI { get; private set; } = null;
        public MapUI.Settings MapSettings { get; private set; } = null;
        public static RACommNetwork RACN => (Instance as RACommNetScenario)?.Network?.CommNet as RACommNetwork;
        public static MapUI.Settings MapUISettings => (Instance as RACommNetScenario)?.MapSettings;

        protected override void Start()
        {
            Debug.Log($"{ModTag} Start in {HighLogic.LoadedScene}, Enabled: {CommNetEnabled}");
            Initialize();
            Kerbalism.Kerbalism.DetectKerbalismDLL();
            if (CommNetEnabled)
            {
                Network = gameObject.AddComponent<Network.RACommNetNetwork>();
                UI = gameObject.AddComponent<MapUI.RACommNetUI>();
                RangeModel = new Network.RealAntennasRangeModel();

                ApplyGameSettings();
                GameEvents.OnGameSettingsApplied.Add(ApplyGameSettings);
            }
            else StartCoroutine(NotifyDisabled());
        }

        public override void OnAwake()
        {
            if (RealAntennas.Network.CommNetPatcher.GetCommNetScenarioModule() is ProtoScenarioModule psm &&
                !RealAntennas.Network.CommNetPatcher.CommNetPatched(psm))
            {
                RealAntennas.Network.CommNetPatcher.UnloadCommNet();
                DestroyNetwork();
                Debug.Log($"{ModTag} Ignore CommNetScenario ERR immediately after Homes rebuild logging.");
            }
            Targeting.TextureTools.Initialize();
            MapSettings = new MapUI.Settings();
            RebuildHomes();
            if (CommNetEnabled)     // Don't self-delete if we are not enabled.
                base.OnAwake();     // Will set CommNetScenario.Instance to this
        }

        public override void OnLoad(ConfigNode gameNode)
        {
            base.OnLoad(gameNode);
            if (gameNode.HasNode("MapUISettings"))
                ConfigNode.LoadObjectFromConfig(MapSettings, gameNode.GetNode("MapUISettings"));
            Targeting.TextureTools.Load(gameNode);
        }

        public override void OnSave(ConfigNode gameNode)
        {
            base.OnSave(gameNode);
            var node = ConfigNode.CreateConfigFromObject(MapSettings);
            node.name = "MapUISettings";
            gameNode.AddNode(node);
            Targeting.TextureTools.Save(gameNode);
        }

        private System.Collections.IEnumerator NotifyDisabled()
        {
            yield return new WaitForSeconds(2);
            while (!CommNetEnabled)
            {
                ScreenMessages.PostScreenMessage("RealAntennas: CommNet Disabled in Difficulty Settings", DisabledNotifyInterval / 2, ScreenMessageStyle.UPPER_CENTER, Color.yellow);
                yield return new WaitForSeconds(DisabledNotifyInterval);
            }
            ScreenMessages.PostScreenMessage("RealAntennas: CommNet enabled, requires scene change to take effect", 10, ScreenMessageStyle.UPPER_CENTER, Color.yellow);
        }


        public void OnDestroy()
        {
            if (Network) Destroy(Network);
            if (UI) Destroy(UI);
            GameEvents.OnGameSettingsApplied.Remove(ApplyGameSettings);
        }

        public void RebuildHomes()
        {
            Debug.LogFormat($"{ModTag} Rebuilding CommNetBody and CommNetHome list");
            UnloadHomes();
            BuildHomes();
        }

        private void DestroyNetwork()
        {
            if (FindObjectOfType<CommNetNetwork>() is CommNetNetwork cn) DestroyImmediate(cn);
        }

        private void ApplyGameSettings()
        {
            debugWalkLogging = HighLogic.CurrentGame.Parameters.CustomParams<RAParameters>().debugWalkLogging;
            debugWalkInterval = HighLogic.CurrentGame.Parameters.CustomParams<RAParameters>().debugWalkInterval;
        }

        private void Initialize()
        {
            if (!staticInit && GameDatabase.Instance.GetConfigNode("RealAntennas/RealAntennasCommNetParams/RealAntennasCommNetParams") is ConfigNode RAParamNode)
            {
                Antenna.BandInfo.Init(RAParamNode);
                Antenna.Encoder.Init(RAParamNode);
                TechLevelInfo.Init(RAParamNode);
                Targeting.TargetModeInfo.Init(RAParamNode);
                RAParamNode.TryGetValue("minRelayTL", ref minRelayTL);
                staticInit = true;
            }

            float fTSLvl = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation);
            GroundStationTechLevel = Mathf.RoundToInt(MaxTL * (HighLogic.CurrentGame.Mode == Game.Modes.CAREER ? fTSLvl : 1));
        }

        private void BuildHomes()
        {
            ConfigNode KopernicusNode = null;
            foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("Kopernicus"))
                KopernicusNode = n;

            if (KopernicusNode != null)
            {
                var sb = StringBuilderCache.Acquire();
                sb.Append($"{ModTag} Building homes: ");
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
                                CommNetHome h = BuildHome(cityNode, body);
                                sb.Append($"{h.nodeName}  ");
                            }
                        }
                    }
                }
                Debug.Log(sb.ToStringAndRelease());
            }
        }

        private void UnloadHomes()
        {
            var logger = StringBuilderCache.Acquire();
            logger.Append($"{ModTag} Unloaded the following homes: ");
            foreach (CommNetHome home in FindObjectsOfType<CommNetHome>())
            {
                logger.Append($"{home.nodeName}  ");
                DestroyImmediate(home);
            }
            GroundStations.Clear();
            Debug.Log(logger.ToStringAndRelease());
        }

        private CommNetHome BuildHome(ConfigNode node, CelestialBody body)
        {
            GameObject newHome = new GameObject(body.name);
            Network.RACommNetHome home = newHome.AddComponent<Network.RACommNetHome>();
            home.Configure(node, body);
            if (!GroundStations.ContainsKey(home.nodeName)) GroundStations.Add(home.nodeName, home);
            return home;
        }
    }
}