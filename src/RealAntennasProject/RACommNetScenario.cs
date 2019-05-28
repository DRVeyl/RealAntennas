using CommNet;
using UnityEngine;

namespace RealAntennas
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames | ScenarioCreationOptions.AddToAllMissionGames, new GameScenes[] { GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER, GameScenes.EDITOR })]
    public class RACommNetScenario : CommNetScenario
    {
        protected static readonly string ModTag = "[RealAntennasCommNetScenario] ";
        public static new Network.RealAntennasRangeModel RangeModel = new Network.RealAntennasRangeModel();
        public static bool Enabled => true;

        private Network.RACommNetNetwork network = null;
        private CommNetUI ui;

        protected override void Start()
        {
            Debug.LogFormat(ModTag + "Start in {0}", HighLogic.LoadedScene);
            InitBandInfo();
            ui = gameObject.AddComponent<Network.RACommNetUI>();
            this.network = gameObject.AddComponent<Network.RACommNetNetwork>();
            CommNetScenario.RangeModel = RangeModel;

            RealAntennas.Kerbalism.Kerbalism.DetectKerbalismDLL();
        }

        public override void OnAwake()
        {
            if (RealAntennas.Network.CommNetPatcher.GetCommNetScenarioModule() is ProtoScenarioModule psm)
            {
                Debug.LogFormat(ModTag + "Scenario check: Found {0}", RATools.DisplayGamescenes(psm));
                if (! RealAntennas.Network.CommNetPatcher.CommNetPatched(psm))
                {
                    RealAntennas.Network.CommNetPatcher.UnloadCommNet();
                    DestroyNetwork();
                    Debug.LogFormat("Rebuilding CommNetBody and CommNetHome list");
                    UnloadHomes();
                    BuildHomes();
                    Debug.LogFormat("Ignore CommNetScenario ERR immediately following this.");
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

        private void InitBandInfo()
        {
            ConfigNode RAParamNode = null;
            foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("RealAntennasCommNetParams"))
                RAParamNode = n;

            if (RAParamNode != null) Antenna.BandInfo.Init(RAParamNode);
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
                Debug.LogFormat("Immediately destroying {0}", home);
                DestroyImmediate(home);
            }
        }
        private void BuildHome(ConfigNode node, CelestialBody body)
        {
            GameObject newHome = new GameObject(body.name);
            Network.RACommNetHome home = newHome.AddComponent<Network.RACommNetHome>();
            home.Configure(node, body);
            Debug.LogFormat(ModTag + "Built: {0}", home);
        }
        private void LoadTempCurves(ConfigNode bodyNode)
        {
            if (bodyNode?.GetNode("skyTemperature") is ConfigNode temperatureNode)
            {
                foreach (ConfigNode n in temperatureNode.GetNodes("temperatureCurve"))
                {
                    FloatCurve MyFloatCurve = new FloatCurve();
                    MyFloatCurve.Load(n);
                    MyFloatCurve.Curve.postWrapMode = WrapMode.ClampForever;
                    MyFloatCurve.Curve.preWrapMode = WrapMode.ClampForever;
                    Debug.LogFormat("Loaded temperature curve for declination {0} with {1} keys", n.GetValue("declination"), MyFloatCurve.Curve.length);
                }
            }
        }
    }
}