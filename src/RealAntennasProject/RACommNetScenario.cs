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
            Debug.LogFormat(ModTag + "CommNetScenario Start() IN {0}/{1}", HighLogic.LoadedScene, HighLogic.LoadedScene.displayDescription());

            Debug.LogFormat(ModTag + "Cleaning and rebuilding CommNetHome and CommNetBody");
            this.ui = gameObject.AddComponent<CommNetUI>();
            this.network = gameObject.AddComponent<RACommNetNetwork>();
            CommNetScenario.RangeModel = RangeModel;

            ConfigNode RACommNetParams = null;
            foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("RealAntennasCommNetParams"))
                RACommNetParams = n;

            foreach (CelestialBody body in FindObjectsOfType<CelestialBody>())
            {
                //                GameObject newObject = new GameObject(body.name);
                //RACommNetBody customBody = newObject.AddComponent<RACommNetBody>();
                CommNetBody customBody = body.GetComponent<CommNetBody>();
                Debug.LogFormat("{0} transform {1} @ {2}", customBody.gameObject, customBody.transform, customBody.transform.position);
                Debug.Log(RATools.TransformWalk(customBody.transform));
                if (RACommNetParams != null)
                {
                    if (RACommNetParams.GetNode("CELESTIALBODY", "name", body.name) is ConfigNode bodyNode)
                    {
                        BuildHome(bodyNode, body);
                    }
                }
            }
            Debug.Log(ModTag + "RealAntennas CommNet Scenario loading done!");
        }

        public override void OnAwake()
        {
            UnloadHomes();
            if (GetCommNetScenarioModule() is ProtoScenarioModule psm)
            {
                if (!_CommNetPatched(psm))
                {
                    Debug.LogFormat("Patching out CommNetScenario");
                    UnloadCommNet(psm);
                    DestroyNetwork();
                    /*
                    Debug.LogFormat("Rebuilding CommNetBody and CommNetHome list");
                    UnloadHomes();
                    BuildHomes();
                    */
                }
            }
            base.OnAwake();     // Will set CommNetScenario.Instance to this
        }

        private void OnDestroy()
        {
            if (network != null) Destroy(network);
            if (ui != null) Destroy(ui);
        }

        private ProtoScenarioModule GetCommNetScenarioModule()
        {
            foreach (ProtoScenarioModule psm in HighLogic.CurrentGame.scenarios)
            {
                if (psm.moduleName.Equals("CommNetScenario")) return psm;
            }
            return null;
        }
        private bool _CommNetPatched(ProtoScenarioModule psm) => psm != null ? psm.targetScenes.Contains(GameScenes.CREDITS) : false;
        private void UnloadCommNet(ProtoScenarioModule psm = null)
        {
            if (psm == null) psm = GetCommNetScenarioModule();
            if (psm != null) psm.SetTargetScenes(new GameScenes[] { GameScenes.CREDITS });
        }
        private void DestroyNetwork()
        {
            if (FindObjectOfType<CommNetNetwork>() is CommNetNetwork cn) DestroyImmediate(cn);
        }

        private void UnloadHomes()
        {
            foreach (CommNetHome home in FindObjectsOfType<CommNetHome>())
            {
                Debug.LogFormat("Going to destroy {0}", home);
                Debug.Log(RATools.TransformWalk(home.transform));
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
                    //RACommNetHome home = body.gameObject.AddComponent<RACommNetHome>();
                    //RACommNetHome home = body.GetComponent<CommNetBody>().gameObject.AddComponent<RACommNetHome>();
                    home.Configure(gsNode, body);
                    Debug.LogFormat(ModTag + "Built: {0}", home);
                    Debug.Log(RATools.TransformWalk(home.transform));
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