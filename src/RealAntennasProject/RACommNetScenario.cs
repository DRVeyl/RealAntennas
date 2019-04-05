using CommNet;
using UnityEngine;

namespace RealAntennas
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames | ScenarioCreationOptions.AddToAllMissionGames, new GameScenes[] { GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER, GameScenes.EDITOR })]
    public class RACommNetScenario : CommNetScenario
    {
        protected static readonly string ModTag = "[RealAntennasCommNetScenario] ";
        public static new RealAntennasRangeModel RangeModel = new RealAntennasRangeModel();
        public static bool Enabled => true;

        private RACommNetNetwork network = null;
        private CommNetUI ui;

        protected override void Start()
        {
            Debug.LogFormat(ModTag + "Start in {0}", HighLogic.LoadedScene);

            this.ui = gameObject.AddComponent<CommNetUI>();
            this.network = gameObject.AddComponent<RACommNetNetwork>();
            CommNetScenario.RangeModel = RangeModel;
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

        private void BuildHomes()
        {
            ConfigNode RACommNetParams = null;
            foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("RealAntennasCommNetParams"))
                RACommNetParams = n;

            foreach (CelestialBody body in FindObjectsOfType<CelestialBody>())
            {
                if ((RACommNetParams != null) && RACommNetParams.GetNode("CELESTIALBODY", "name", body.name) is ConfigNode bodyNode)
                {
                    BuildHome(bodyNode, body);
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
            ConfigNode gsTopNode = null;
            foreach (ConfigNode n in node.GetNodes("GroundStations"))
                gsTopNode = n;
            if (gsTopNode != null)
            {
                foreach (ConfigNode gsNode in gsTopNode.GetNodes("STATION"))
                {
                    GameObject newHome = new GameObject(body.name);
                    RACommNetHome home = newHome.AddComponent<RACommNetHome>();
                    home.Configure(gsNode, body);
                    Debug.LogFormat(ModTag + "Built: {0}", home);
                }
            }
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