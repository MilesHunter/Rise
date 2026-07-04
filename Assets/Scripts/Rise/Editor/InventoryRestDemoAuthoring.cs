using System;
using System.IO;
using Rise;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Rise.Editor
{
    public static class InventoryRestDemoAuthoring
    {
        public const string ScenePath = "Assets/Scenes/InventoryRestDemo.unity";
        private const string RootName = "InventoryRestDemoWorld";
        private static readonly string AutorunFlagPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "author-inventory-rest-demo.flag"));
        private static bool autorunQueued;

        [InitializeOnLoadMethod]
        private static void MaybeAutorun()
        {
            if (!File.Exists(AutorunFlagPath) || autorunQueued)
            {
                return;
            }

            autorunQueued = true;
            EditorApplication.update += ProcessAutorun;
        }

        private static void ProcessAutorun()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            EditorApplication.update -= ProcessAutorun;
            try
            {
                File.Delete(AutorunFlagPath);
                CreateScene();
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
            finally
            {
                autorunQueued = false;
            }
        }

        [MenuItem("Rise/Author Inventory Rest Demo Scene")]
        public static void CreateScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "InventoryRestDemo";

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(-2f, 3f, -13f);
            camera.transform.rotation = Quaternion.identity;

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject root = new GameObject(RootName);
            GameObject level = new GameObject("Level");
            level.transform.SetParent(root.transform, false);

            BuildBackdrop(level.transform);
            BuildDemoRoute(level.transform);
            PlayerClimbController player = BuildPlayer(root.transform, camera);
            BuildHudAndInventoryUi(root.transform, player);
            cameraObject.AddComponent<CameraFollowSideView>().Initialize(player.transform);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Inventory/rest demo scene saved to {ScenePath}");
        }

        private static void BuildBackdrop(Transform parent)
        {
            GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backdrop.name = "InventoryRestBackdrop";
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.position = new Vector3(0f, 6.5f, 4f);
            backdrop.transform.localScale = new Vector3(18f, 16f, 0.25f);
            backdrop.GetComponent<Renderer>().sharedMaterial.color = new Color(0.17f, 0.22f, 0.25f);
            Object.DestroyImmediate(backdrop.GetComponent<BoxCollider>());
        }

        private static void BuildDemoRoute(Transform parent)
        {
            GameObject start = CreateBlock(parent, "StartShelf", new Vector3(-4f, -0.5f, 0.8f), new Vector3(4f, 1f, 2f), new Color(0.28f, 0.29f, 0.3f));
            GameObject wall = CreateBlock(parent, "TrainingWall", new Vector3(-0.8f, 2f, 1f), new Vector3(1.3f, 5.4f, 1.2f), new Color(0.33f, 0.36f, 0.37f));
            GameObject shortRest = CreateBlock(parent, "ShortRestShelf", new Vector3(2.1f, 4.4f, 0.8f), new Vector3(2.4f, 0.8f, 2f), new Color(0.22f, 0.43f, 0.52f));
            GameObject longRest = CreateBlock(parent, "LongRestCamp", new Vector3(-1.4f, 8.2f, 0.8f), new Vector3(3f, 0.8f, 2f), new Color(0.42f, 0.28f, 0.18f));
            GameObject resourceShelf = CreateBlock(parent, "ResourceShelf", new Vector3(2.6f, 10.9f, 0.8f), new Vector3(2.4f, 0.8f, 2f), new Color(0.34f, 0.34f, 0.26f));

            ConfigureSurface(start, "Rough rock", 1.2f, 0.6f);
            ConfigureSurface(wall, "Practice wall", 1.5f, 0.85f);
            ConfigureSurface(shortRest, "Short-rest shelf", 1.1f, 0.5f);
            ConfigureSurface(longRest, "Long-rest camp", 1f, 0.45f);
            ConfigureSurface(resourceShelf, "Resource shelf", 1.2f, 0.55f);

            CreateRestPoint(parent, "ShortRestPoint", new Vector3(2.1f, 5.1f, 0f), new Vector3(2.4f, 1.6f, 1.2f), RestPointType.ShortRest, "Short rest: press F", "Pack only, no cooking");
            CreateMarker(parent, "ShortRestMarker", new Vector3(2.1f, 5.8f, 0f), new Color(0.35f, 0.9f, 1f));
            CreateRestPoint(parent, "LongRestPoint", new Vector3(-1.4f, 8.9f, 0f), new Vector3(3f, 1.8f, 1.2f), RestPointType.LongRest, "Long rest camp: press F", "Full pack, cooking, checkpoint");
            CreateMarker(parent, "LongRestMarker", new Vector3(-1.4f, 9.6f, 0f), new Color(1f, 0.58f, 0.18f));

            GameObject resourceNode = CreateMarker(parent, "ResourceNode", new Vector3(2.6f, 11.7f, 0f), new Color(0.95f, 0.86f, 0.3f));
            resourceNode.AddComponent<ResourceNode>();
            CreateGoalPoint(parent, new Vector3(2.6f, 12.4f, 0f), new Vector3(2.5f, 1.5f, 1.2f));
        }

        private static PlayerClimbController BuildPlayer(Transform parent, Camera camera)
        {
            GameObject playerObject = new GameObject("Player");
            playerObject.transform.SetParent(parent, false);
            playerObject.transform.position = new Vector3(-4.2f, 0.2f, 0f);

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
            playerObject.AddComponent<RestSessionController>();
            playerObject.AddComponent<CookingSystem>();
            playerObject.AddComponent<ToolController>();
            PlayerClimbController controller = playerObject.AddComponent<PlayerClimbController>();
            controller.Initialize(camera, playerObject.transform.position, parent);

            GameObject presentationObject = new GameObject("CharacterPresentation");
            presentationObject.transform.SetParent(parent, false);
            presentationObject.AddComponent<CharacterPresentation>().Initialize(controller);
            return controller;
        }

        private static void BuildHudAndInventoryUi(Transform parent, PlayerClimbController player)
        {
            GameObject hud = new GameObject("HUD");
            hud.transform.SetParent(parent, false);
            hud.AddComponent<GameHUDPresenter>().Initialize(player);

            GameObject ui = new GameObject("InventoryAndRestUI");
            ui.transform.SetParent(parent, false);
            ui.AddComponent<InventoryUI>().Initialize(player);
        }

        private static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial.color = color;
            return block;
        }

        private static GameObject CreateMarker(Transform parent, string name, Vector3 position, Color color)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.transform.SetParent(parent, false);
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * 0.45f;
            marker.GetComponent<Renderer>().sharedMaterial.color = color;
            Object.DestroyImmediate(marker.GetComponent<SphereCollider>());
            return marker;
        }

        private static void ConfigureSurface(GameObject target, string label, float grabCost, float drain)
        {
            target.AddComponent<ClimbSurface>().Configure(label, grabCost, drain, 0f, 0f, true, true);
        }

        private static void CreateRestPoint(Transform parent, string name, Vector3 position, Vector3 size, RestPointType type, string prompt, string detail)
        {
            GameObject restObject = new GameObject(name);
            restObject.transform.SetParent(parent, false);
            restObject.transform.position = position;
            BoxCollider collider = restObject.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = true;
            restObject.AddComponent<RestPoint>().Configure(type, prompt, detail);
        }

        private static void CreateGoalPoint(Transform parent, Vector3 position, Vector3 size)
        {
            GameObject goal = new GameObject("GoalPoint");
            goal.transform.SetParent(parent, false);
            goal.transform.position = position;
            BoxCollider collider = goal.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = true;
            goal.AddComponent<GoalPoint>();
        }

        private static void AddToBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (string.Equals(scenes[i].path, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    scenes[i].enabled = true;
                    EditorBuildSettings.scenes = scenes;
                    return;
                }
            }

            Array.Resize(ref scenes, scenes.Length + 1);
            scenes[scenes.Length - 1] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = scenes;
        }
    }
}
