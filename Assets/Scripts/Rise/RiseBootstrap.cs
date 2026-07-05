using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rise
{
    public sealed class RiseBootstrap : MonoBehaviour
    {
        private const string BootstrapName = "RiseBootstrap";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (ShouldSkipPrototypeBootstrap())
            {
                return;
            }

            if (Object.FindAnyObjectByType<RiseBootstrap>() != null)
            {
                return;
            }

            GameObject bootstrap = new GameObject(BootstrapName);
            bootstrap.AddComponent<RiseBootstrap>();
        }

        private void Start()
        {
            if (ShouldSkipPrototypeBootstrap())
            {
                Destroy(gameObject);
                return;
            }

            AudioService.EnsureExists();
            BuildPrototype();
        }

        private void BuildPrototype()
        {
            if (ShouldSkipPrototypeBootstrap())
            {
                return;
            }

            if (Object.FindAnyObjectByType<PlayerClimbController>() != null)
            {
                return;
            }

            Camera sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                sceneCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<AudioListener>();
            }

            sceneCamera.transform.position = new Vector3(-1.2f, 2.5f, -13f);
            sceneCamera.transform.rotation = Quaternion.identity;

            GameObject root = new GameObject("RisePrototypeWorld");
            GameObject levelRoot = new GameObject("Level");
            levelRoot.transform.SetParent(root.transform, false);

            BuildBackdrop(levelRoot.transform);
            BuildRoute(levelRoot.transform);

            PlayerClimbController player = BuildPlayer(root.transform, sceneCamera);
            BuildHud(root.transform, player);
            ConfigureCamera(sceneCamera, player.transform);
        }

        private static bool ShouldSkipPrototypeBootstrap()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && (activeScene.name == "Start" || activeScene.path == "Assets/Scenes/Start.unity"))
            {
                return true;
            }

            return Object.FindAnyObjectByType<StartMenuController>() != null;
        }

        private static void BuildBackdrop(Transform parent)
        {
            GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backdrop.name = "Backdrop";
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.position = new Vector3(0f, 8f, 4f);
            backdrop.transform.localScale = new Vector3(22f, 20f, 0.25f);
            Renderer renderer = backdrop.GetComponent<Renderer>();
            renderer.material.color = new Color(0.21f, 0.28f, 0.34f);
            Object.Destroy(backdrop.GetComponent<BoxCollider>());
        }

        private static void BuildRoute(Transform parent)
        {
            GameObject startPlatform = CreatePlatform(parent, "StartPlatform", new Vector3(-5f, -0.5f, 0.8f), new Vector3(5f, 1f, 2f), new Color(0.32f, 0.32f, 0.34f));
            GameObject shortRestLedge = CreatePlatform(parent, "ShortRestLedge", new Vector3(2.1f, 4.4f, 0.8f), new Vector3(2.2f, 0.8f, 2f), new Color(0.28f, 0.43f, 0.5f));
            GameObject longRestCamp = CreatePlatform(parent, "LongRestCamp", new Vector3(-1.4f, 9.3f, 0.8f), new Vector3(2.8f, 0.8f, 2f), new Color(0.45f, 0.31f, 0.18f));
            GameObject summit = CreatePlatform(parent, "Summit", new Vector3(2.3f, 15.6f, 0.8f), new Vector3(3f, 0.8f, 2f), new Color(0.62f, 0.58f, 0.44f));

            GameObject wallA = CreateWallColumn(parent, new Vector3(-0.6f, 2f, 1f), new Vector3(1.8f, 5.5f, 1.2f), new Color(0.28f, 0.29f, 0.31f));
            GameObject wallB = CreateWallColumn(parent, new Vector3(1.6f, 7f, 1f), new Vector3(1.8f, 4f, 1.2f), new Color(0.36f, 0.42f, 0.46f));
            GameObject wallC = CreateWallColumn(parent, new Vector3(0.3f, 12.2f, 1f), new Vector3(1.8f, 5f, 1.2f), new Color(0.4f, 0.44f, 0.5f));

            ConfigureClimbSurface(startPlatform, "Rough rock", 1.4f, 0.75f, 0f, 0f);
            ConfigureClimbSurface(shortRestLedge, "Short-rest shelf", 1.2f, 0.55f, 0f, 0f);
            ConfigureClimbSurface(longRestCamp, "Camp ledge", 1.1f, 0.45f, 0f, 0f);
            ConfigureClimbSurface(summit, "Summit ledge", 1.3f, 0.65f, 0.8f, 0.1f);
            ConfigureClimbSurface(wallA, "Rough wall", 1.8f, 0.95f, 0f, 0f);
            ConfigureClimbSurface(wallB, "Icy wall", 2.2f, 1.15f, 1.4f, 0.16f);
            ConfigureClimbSurface(wallC, "Wind-burnt wall", 2.5f, 1.3f, 1.1f, 0.22f);

            CreateRestPoint(parent, "ShortRestPoint", new Vector3(2.1f, 5.1f, 0f), new Vector3(2.4f, 1.6f, 1.2f), RestPointType.ShortRest, "Short rest: press F", "+35 stamina, light relief");
            CreateRestMarker(parent, new Vector3(2.1f, 5.15f, 0f), RestPointType.ShortRest);
            CreateRestPoint(parent, "LongRestPoint", new Vector3(-1.4f, 9.9f, 0f), new Vector3(3f, 1.8f, 1.2f), RestPointType.LongRest, "Long rest camp: press F", "Full recovery and checkpoint");
            CreateRestMarker(parent, new Vector3(-1.4f, 10.05f, 0f), RestPointType.LongRest);
            CreateGoalPoint(parent, new Vector3(2.3f, 16.3f, 0f), new Vector3(3f, 1.8f, 1.2f));
        }

        private static PlayerClimbController BuildPlayer(Transform parent, Camera sceneCamera)
        {
            GameObject playerObject = new GameObject("Player");
            playerObject.transform.SetParent(parent, false);

            CapsuleCollider collider = playerObject.AddComponent<CapsuleCollider>();
            collider.height = 1.9f;
            collider.radius = 0.35f;
            collider.center = new Vector3(0f, 0.95f, 0f);

            Rigidbody body = playerObject.AddComponent<Rigidbody>();
            body.mass = 1.4f;
            body.linearDamping = 0.4f;
            body.angularDamping = 0.2f;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            playerObject.AddComponent<PlayerVitals>();
            playerObject.AddComponent<PlayerInventory>();
            playerObject.AddComponent<ToolController>();
            PlayerClimbController controller = playerObject.AddComponent<PlayerClimbController>();
            playerObject.AddComponent<PlayerAudioBridge>();
            controller.Initialize(sceneCamera, new Vector3(-4.5f, 0.2f, 0f), parent);

            return controller;
        }

        private static void BuildHud(Transform parent, PlayerClimbController player)
        {
            GameObject hud = new GameObject("HUD");
            hud.transform.SetParent(parent, false);
            GameHUDPresenter presenter = hud.AddComponent<GameHUDPresenter>();
            presenter.Initialize(player);
        }

        private static void ConfigureCamera(Camera sceneCamera, Transform player)
        {
            CameraFollowSideView follow = sceneCamera.GetComponent<CameraFollowSideView>();
            if (follow == null)
            {
                follow = sceneCamera.gameObject.AddComponent<CameraFollowSideView>();
            }

            follow.Initialize(player);
        }

        private static GameObject CreatePlatform(Transform parent, string objectName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = objectName;
            platform.transform.SetParent(parent, false);
            platform.transform.position = position;
            platform.transform.localScale = scale;
            platform.GetComponent<Renderer>().material.color = color;
            return platform;
        }

        private static GameObject CreateWallColumn(Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "WallColumn";
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().material.color = color;
            return wall;
        }

        private static void CreateRestPoint(Transform parent, string objectName, Vector3 position, Vector3 scale, RestPointType restType, string prompt, string detail)
        {
            GameObject restObject = new GameObject(objectName);
            restObject.transform.SetParent(parent, false);
            restObject.transform.position = position;

            BoxCollider collider = restObject.AddComponent<BoxCollider>();
            collider.size = scale;
            collider.isTrigger = true;

            RestPoint restPoint = restObject.AddComponent<RestPoint>();
            restPoint.Configure(restType, prompt, detail);
        }

        private static void ConfigureClimbSurface(GameObject target, string label, float grabCost, float drainPerSecond, float slipInterval, float slipChance)
        {
            ClimbSurface climbSurface = target.GetComponent<ClimbSurface>();
            if (climbSurface == null)
            {
                climbSurface = target.AddComponent<ClimbSurface>();
            }
            climbSurface.Configure(label, grabCost, drainPerSecond, slipInterval, slipChance, true, true);
        }

        private static void CreateRestMarker(Transform parent, Vector3 position, RestPointType type)
        {
            if (type == RestPointType.ShortRest)
            {
                GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beacon.name = "ShortRestBeacon";
                beacon.transform.SetParent(parent, false);
                beacon.transform.position = position + new Vector3(0f, 0.55f, 0f);
                beacon.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);
                beacon.GetComponent<Renderer>().material.color = new Color(0.32f, 0.78f, 0.96f);

                GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crystal.name = "ShortRestCrystal";
                crystal.transform.SetParent(parent, false);
                crystal.transform.position = position + new Vector3(0f, 1.3f, 0f);
                crystal.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                crystal.GetComponent<Renderer>().material.color = new Color(0.66f, 0.9f, 1f);
            }
            else
            {
                GameObject fireBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fireBase.name = "LongRestFireBase";
                fireBase.transform.SetParent(parent, false);
                fireBase.transform.position = position + new Vector3(0f, 0.25f, 0f);
                fireBase.transform.localScale = new Vector3(0.55f, 0.12f, 0.55f);
                fireBase.GetComponent<Renderer>().material.color = new Color(0.34f, 0.2f, 0.14f);

                GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flame.name = "LongRestFlame";
                flame.transform.SetParent(parent, false);
                flame.transform.position = position + new Vector3(0f, 0.8f, 0f);
                flame.transform.localScale = new Vector3(0.38f, 0.55f, 0.38f);
                flame.GetComponent<Renderer>().material.color = new Color(1f, 0.58f, 0.18f);

                Light fireLight = flame.AddComponent<Light>();
                fireLight.type = LightType.Point;
                fireLight.range = 6f;
                fireLight.intensity = 2.4f;
                fireLight.color = new Color(1f, 0.68f, 0.32f);

                GameObject banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
                banner.name = "LongRestBanner";
                banner.transform.SetParent(parent, false);
                banner.transform.position = position + new Vector3(0.9f, 1.2f, 0f);
                banner.transform.localScale = new Vector3(0.12f, 1.6f, 0.12f);
                banner.GetComponent<Renderer>().material.color = new Color(0.52f, 0.34f, 0.14f);
            }
        }

        private static void CreateGoalPoint(Transform parent, Vector3 position, Vector3 scale)
        {
            GameObject goal = new GameObject("GoalPoint");
            goal.transform.SetParent(parent, false);
            goal.transform.position = position;

            BoxCollider collider = goal.AddComponent<BoxCollider>();
            collider.size = scale;
            collider.isTrigger = true;

            goal.AddComponent<GoalPoint>();
        }
    }
}
