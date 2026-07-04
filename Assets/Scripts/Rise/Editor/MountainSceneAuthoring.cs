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
    public static class MountainSceneAuthoring
    {
        private const string ScenePath = "Assets/Scenes/Mountain.unity";
        private const string LongClimbScenePath = "Assets/Scenes/MountainLongClimb.unity";
        private const string RootName = "RisePrototypeWorld";
        private const float LongClimbWallCenterX = 0f;
        private const float LongClimbWallCenterY = 55f;
        private const float LongClimbWallWidth = 28f;
        private const float LongClimbWallHeight = 122f;
        private const float LongClimbWallFrontZ = 0.12f;
        private const float LongClimbRockFrontZ = -0.06f;
        private const float LongClimbRockFrontTolerance = 0.035f;
        private static readonly string AutorunFlagPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "author-mountain-scene.flag"));
        private static bool autorunQueued;

        [InitializeOnLoadMethod]
        private static void MaybeAutorun()
        {
            if (!File.Exists(AutorunFlagPath) || autorunQueued)
            {
                return;
            }

            autorunQueued = true;
            Debug.Log($"MountainSceneAuthoring detected autorun flag at {AutorunFlagPath}");
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
                if (File.Exists(AutorunFlagPath))
                {
                    File.Delete(AutorunFlagPath);
                }

                ApplyToMountainScene();

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

        [MenuItem("Rise/Author Mountain Scene")]
        public static void ApplyToMountainScene()
        {
            Scene scene = ResolveTargetScene();

            RemoveExistingPrototype(scene);
            EnsureCamera();

            GameObject root = new GameObject(RootName);
            GameObject levelRoot = new GameObject("Level");
            levelRoot.transform.SetParent(root.transform, false);

            BuildBackdrop(levelRoot.transform);
            BuildRoute(levelRoot.transform);
            PlayerClimbController player = BuildPlayer(root.transform);
            BuildHud(root.transform);
            ConfigureCamera(player.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Mountain scene authored successfully.");
        }

        [MenuItem("Rise/Author Mountain Long Climb Scene")]
        public static void ApplyToMountainLongClimbScene()
        {
            if (File.Exists(Path.Combine(Application.dataPath, "..", LongClimbScenePath)))
            {
                AssetDatabase.DeleteAsset(LongClimbScenePath);
            }

            if (!AssetDatabase.CopyAsset(ScenePath, LongClimbScenePath))
            {
                throw new InvalidOperationException($"Unable to copy {ScenePath} to {LongClimbScenePath}");
            }

            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.OpenScene(LongClimbScenePath, OpenSceneMode.Single);

            RemoveExistingPrototype(scene);
            RemoveGeneratedLongClimbObjects(scene);
            EnsureCameraForLongClimb();

            GameObject root = new GameObject(RootName);
            GameObject levelRoot = new GameObject("Level");
            levelRoot.transform.SetParent(root.transform, false);

            BuildLongClimbBackdrop(levelRoot.transform);
            BuildLongClimbRoute(levelRoot.transform);
            PlayerClimbController player = BuildPlayer(root.transform);
            player.transform.position = new Vector3(-5.6f, 0.35f, 0f);
            BuildHud(root.transform);
            ConfigureCamera(player.transform);

            ValidateLongClimbRocks(levelRoot.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Mountain long climb scene authored successfully: {LongClimbScenePath}");
        }

        private static Scene ResolveTargetScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && string.Equals(activeScene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
            {
                return activeScene;
            }

            Scene loadedScene = SceneManager.GetSceneByPath(ScenePath);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                SceneManager.SetActiveScene(loadedScene);
                return loadedScene;
            }

            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static void RemoveExistingPrototype(Scene scene)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == RootName || rootObject.name == "HUD" || rootObject.name == "Player")
                {
                    Object.DestroyImmediate(rootObject);
                }
            }
        }

        private static void RemoveGeneratedLongClimbObjects(Scene scene)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == "LongClimbRockwall" || rootObject.name == "Cube")
                {
                    Object.DestroyImmediate(rootObject);
                }
            }
        }

        private static void EnsureCamera()
        {
            Camera sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                sceneCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            sceneCamera.transform.position = new Vector3(-1.2f, 2.5f, -13f);
            sceneCamera.transform.rotation = Quaternion.identity;
        }

        private static void EnsureCameraForLongClimb()
        {
            EnsureCamera();
            if (Camera.main != null)
            {
                Camera.main.transform.position = new Vector3(-3.2f, 2.6f, -18f);
            }
        }

        private static void BuildBackdrop(Transform parent)
        {
            GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backdrop.name = "Backdrop";
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.position = new Vector3(0f, 8f, 4f);
            backdrop.transform.localScale = new Vector3(22f, 20f, 0.25f);
            backdrop.GetComponent<Renderer>().sharedMaterial.color = new Color(0.21f, 0.28f, 0.34f);
            Object.DestroyImmediate(backdrop.GetComponent<BoxCollider>());
        }

        private static void BuildRoute(Transform parent)
        {
            GameObject startPlatform = CreatePlatform(parent, "StartPlatform", new Vector3(-5f, -0.5f, 0.8f), new Vector3(5f, 1f, 2f), new Color(0.32f, 0.32f, 0.34f));
            GameObject shortRestLedge = CreatePlatform(parent, "ShortRestLedge", new Vector3(2.1f, 4.4f, 0.8f), new Vector3(2.2f, 0.8f, 2f), new Color(0.28f, 0.43f, 0.5f));
            GameObject longRestCamp = CreatePlatform(parent, "LongRestCamp", new Vector3(-1.4f, 9.3f, 0.8f), new Vector3(2.8f, 0.8f, 2f), new Color(0.45f, 0.31f, 0.18f));
            GameObject summit = CreatePlatform(parent, "Summit", new Vector3(2.3f, 15.6f, 0.8f), new Vector3(3f, 0.8f, 2f), new Color(0.62f, 0.58f, 0.44f));

            GameObject wallA = CreateWallColumn(parent, "WallA", new Vector3(-0.6f, 2f, 1f), new Vector3(1.8f, 5.5f, 1.2f), new Color(0.28f, 0.29f, 0.31f));
            GameObject wallB = CreateWallColumn(parent, "WallB", new Vector3(1.6f, 7f, 1f), new Vector3(1.8f, 4f, 1.2f), new Color(0.36f, 0.42f, 0.46f));
            GameObject wallC = CreateWallColumn(parent, "WallC", new Vector3(0.3f, 12.2f, 1f), new Vector3(1.8f, 5f, 1.2f), new Color(0.4f, 0.44f, 0.5f));

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

        private static void BuildLongClimbBackdrop(Transform parent)
        {
            GameObject rockwall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rockwall.name = "LongClimbRockwall";
            rockwall.transform.SetParent(parent, false);
            rockwall.transform.position = new Vector3(LongClimbWallCenterX, LongClimbWallCenterY, 0.18f);
            rockwall.transform.localScale = new Vector3(LongClimbWallWidth, LongClimbWallHeight, 0.12f);
            rockwall.GetComponent<Renderer>().sharedMaterial.color = new Color(0.2f, 0.23f, 0.24f);
            Object.DestroyImmediate(rockwall.GetComponent<BoxCollider>());
        }

        private static void BuildLongClimbRoute(Transform parent)
        {
            GameObject[] normalRocks = LoadPrefabs(
                "Assets/Prefab/RockNormal/Rock_Normal_1.prefab",
                "Assets/Prefab/RockNormal/Rock_Normal_2.prefab",
                "Assets/Prefab/RockNormal/Rock_Normal_3.prefab",
                "Assets/Prefab/RockNormal/Rock_Normal_4.prefab",
                "Assets/Prefab/RockNormal/Rock_Normal_5.prefab",
                "Assets/Prefab/RockNormal/Rock_Normal_6.prefab",
                "Assets/Prefab/RockNormal/Rock_Normal_7.prefab");
            GameObject[] mossRocks = LoadPrefabs(
                "Assets/Prefab/RockWithMoss/Rcok_With_Moss_1.prefab",
                "Assets/Prefab/RockWithMoss/Rcok_With_Moss_2.prefab",
                "Assets/Prefab/RockWithMoss/Rcok_With_Moss_3.prefab",
                "Assets/Prefab/RockWithMoss/Rcok_With_Moss_4.prefab",
                "Assets/Prefab/RockWithMoss/Rcok_With_Moss_5.prefab",
                "Assets/Prefab/RockWithMoss/Rcok_With_Moss_6.prefab");

            Transform mainRoute = CreateGroup(parent, "MainZigzagRoute");
            Transform branches = CreateGroup(parent, "RestAndResourceBranches");
            Transform markers = CreateGroup(parent, "InteractionMarkers");

            CreateRockShelf(mainRoute, normalRocks, "StartShelf", new Vector2(-5.2f, 0.1f), 11, 0.48f, 0.24f);

            Vector3[] route = BuildZigzagPath();
            for (int i = 0; i < route.Length; i++)
            {
                bool isMoss = i % 11 == 5 || i % 17 == 9;
                GameObject[] bank = isMoss ? mossRocks : normalRocks;
                Vector3 scale = RockScaleForIndex(i, isMoss, false);
                Vector3 euler = new Vector3(0f, 0f, (i % 9 - 4) * 7.5f);
                GameObject rock = PlaceRock(mainRoute, bank[i % bank.Length], $"MainRock_{i:000}", route[i], euler, scale);
                ConfigureRockClimb(rock, i < 42 ? "Lower mountain rock" : i < 84 ? "Exposed mountain rock" : "Summit ridge rock", i);
            }

            BuildLongClimbRestStop(markers, normalRocks, mossRocks, new Vector2(-8.15f, 22.2f), RestPointType.ShortRest, "ShortRest_01", "Short rest: press F", "+35 stamina, side shelf");
            BuildLongClimbRestStop(markers, normalRocks, mossRocks, new Vector2(7.9f, 43.2f), RestPointType.LongRest, "LongRest_01", "Long rest camp: press F", "Full recovery and checkpoint");
            BuildLongClimbRestStop(markers, normalRocks, mossRocks, new Vector2(-7.8f, 66.8f), RestPointType.ShortRest, "ShortRest_02", "Short rest: press F", "+35 stamina, wind break");
            BuildLongClimbRestStop(markers, normalRocks, mossRocks, new Vector2(8.15f, 86.6f), RestPointType.LongRest, "LongRest_02", "Long rest camp: press F", "Full recovery before final pitch");

            BuildResourceBranch(branches, normalRocks, mossRocks, "ResourceBranch_Left_01", new[]
            {
                new Vector3(-2.6f, 29.5f, 0f),
                new Vector3(-4.2f, 31.2f, 0f),
                new Vector3(-5.8f, 32.4f, 0f),
                new Vector3(-7.1f, 33.5f, 0f)
            });
            CreateResourceNode(markers, "ResourceNode_Left_01", new Vector3(-7.7f, 34.3f, 0f));

            BuildResourceBranch(branches, normalRocks, mossRocks, "ResourceBranch_Right_01", new[]
            {
                new Vector3(3.2f, 58.8f, 0f),
                new Vector3(4.8f, 60.2f, 0f),
                new Vector3(6.3f, 61.1f, 0f),
                new Vector3(7.4f, 62.5f, 0f)
            });
            CreateResourceNode(markers, "ResourceNode_Right_01", new Vector3(8.0f, 63.4f, 0f));

            BuildResourceBranch(branches, normalRocks, mossRocks, "ResourceBranch_Final_01", new[]
            {
                new Vector3(-4.4f, 94.0f, 0f),
                new Vector3(-6.0f, 95.2f, 0f),
                new Vector3(-7.4f, 96.5f, 0f)
            });
            CreateResourceNode(markers, "ResourceNode_Final_01", new Vector3(-8.0f, 97.4f, 0f));

            CreateRockShelf(mainRoute, normalRocks, "SummitShelf", new Vector2(-6.6f, 111.4f), 12, 0.5f, 0.26f);
            CreateGoalPoint(markers, new Vector3(-6.6f, 112.8f, 0f), new Vector3(4.2f, 2.2f, 1.2f));
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject[] LoadPrefabs(params string[] paths)
        {
            GameObject[] prefabs = new GameObject[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (prefabs[i] == null)
                {
                    throw new FileNotFoundException($"Missing prefab at {paths[i]}");
                }
            }

            return prefabs;
        }

        private static Vector3[] BuildZigzagPath()
        {
            const int count = 118;
            Vector3[] points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float y = Mathf.Lerp(1.8f, 109.0f, t);
                float x;
                if (t < 0.34f)
                {
                    x = Mathf.Lerp(-4.8f, -7.2f, t / 0.34f);
                }
                else if (t < 0.68f)
                {
                    x = Mathf.Lerp(-7.2f, 7.1f, (t - 0.34f) / 0.34f);
                }
                else
                {
                    x = Mathf.Lerp(7.1f, -6.6f, (t - 0.68f) / 0.32f);
                }

                x += Mathf.Sin(i * 0.83f) * 0.62f;
                y += Mathf.Sin(i * 0.37f) * 0.24f;
                points[i] = new Vector3(x, y, 0f);
            }

            return points;
        }

        private static Vector3 RockScaleForIndex(int index, bool moss, bool shelf)
        {
            float baseScale = shelf ? 0.34f : 0.2f;
            float x = baseScale + (index % 5) * 0.025f;
            float y = baseScale + ((index + 2) % 4) * 0.022f;
            float z = baseScale + ((index + 4) % 3) * 0.018f;
            float sizeMultiplier = RockSizeMultiplierForIndex(index, shelf);
            if (moss)
            {
                x += 0.025f;
                y += 0.018f;
            }

            return new Vector3(x * sizeMultiplier, y * sizeMultiplier, z * sizeMultiplier);
        }

        private static GameObject PlaceRock(Transform parent, GameObject prefab, string name, Vector3 position, Vector3 euler, Vector3 scale)
        {
            GameObject rock = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            rock.name = name;
            rock.transform.SetParent(parent, false);
            rock.transform.position = new Vector3(position.x, position.y, 0f);
            rock.transform.rotation = Quaternion.Euler(euler);
            rock.transform.localScale = scale;
            NormalizeRockVisualSize(rock, Mathf.Clamp(Mathf.Max(scale.x, scale.y) * 4f, 0.65f, 4.2f));
            ClampRockToWallBounds(rock);
            AlignRockFrontPlane(rock);
            return rock;
        }

        private static float RockSizeMultiplierForIndex(int index, bool shelf)
        {
            if (shelf)
            {
                return Mathf.Lerp(0.92f, 1.45f, Hash01(index, 311));
            }

            float roll = Hash01(index, 137);
            if (roll < 0.52f)
            {
                return Mathf.Lerp(0.82f, 1.35f, Hash01(index, 149));
            }

            if (roll < 0.84f)
            {
                return Mathf.Lerp(1.45f, 2.65f, Hash01(index, 163));
            }

            return Mathf.Lerp(2.8f, 4f, Hash01(index, 179));
        }

        private static void NormalizeRockVisualSize(GameObject rock, float targetMaxDimension)
        {
            Renderer[] renderers = rock.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float maxDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (maxDimension <= 0.0001f)
            {
                return;
            }

            float factor = targetMaxDimension / maxDimension;
            rock.transform.localScale *= factor;
        }

        private static void ClampRockToWallBounds(GameObject rock)
        {
            if (!TryGetRendererBounds(rock, out Bounds bounds))
            {
                return;
            }

            float halfWidth = LongClimbWallWidth * 0.5f;
            float halfHeight = LongClimbWallHeight * 0.5f;
            float minX = LongClimbWallCenterX - halfWidth;
            float maxX = LongClimbWallCenterX + halfWidth;
            float minY = LongClimbWallCenterY - halfHeight;
            float maxY = LongClimbWallCenterY + halfHeight;
            Vector3 offset = Vector3.zero;

            if (bounds.min.x < minX)
            {
                offset.x = minX - bounds.min.x;
            }
            else if (bounds.max.x > maxX)
            {
                offset.x = maxX - bounds.max.x;
            }

            if (bounds.min.y < minY)
            {
                offset.y = minY - bounds.min.y;
            }
            else if (bounds.max.y > maxY)
            {
                offset.y = maxY - bounds.max.y;
            }

            if (offset != Vector3.zero)
            {
                rock.transform.position += offset;
            }
        }

        private static void AlignRockFrontPlane(GameObject rock)
        {
            if (!TryGetRendererBounds(rock, out Bounds bounds))
            {
                return;
            }

            rock.transform.position += new Vector3(0f, 0f, LongClimbRockFrontZ - bounds.min.z);
        }

        private static bool TryGetRendererBounds(GameObject rock, out Bounds bounds)
        {
            Renderer[] renderers = rock.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return true;
        }

        private static void ConfigureRockClimb(GameObject rock, string label, int index)
        {
            ClimbSurface surface = rock.GetComponent<ClimbSurface>();
            if (surface == null)
            {
                return;
            }

            float grabCost = index < 42 ? 1.55f : index < 84 ? 1.75f : 1.95f;
            float drain = index < 42 ? 0.82f : index < 84 ? 0.98f : 1.12f;
            float slipInterval = index < 80 ? 0f : 1.8f;
            float slipChance = index < 80 ? 0f : 0.08f;
            surface.Configure(label, grabCost, drain, slipInterval, slipChance, true, true);
        }

        private static void CreateRockShelf(Transform parent, GameObject[] rocks, string name, Vector2 center, int count, float spacing, float scaleBase)
        {
            Transform shelf = CreateGroup(parent, name);
            for (int i = 0; i < count; i++)
            {
                float x = center.x + (i - (count - 1) * 0.5f) * spacing;
                float y = center.y + Mathf.Sin(i * 0.7f) * 0.12f;
                Vector3 scale = new Vector3(scaleBase + (i % 3) * 0.04f, scaleBase * 0.68f + (i % 2) * 0.03f, scaleBase);
                GameObject rock = PlaceRock(shelf, rocks[i % rocks.Length], $"{name}_Rock_{i:00}", new Vector3(x, y, 0f), new Vector3(0f, 0f, i * 11f), scale);
                ConfigureRockClimb(rock, "Wide ledge rock", 20);
            }
        }

        private static void BuildLongClimbRestStop(Transform parent, GameObject[] normalRocks, GameObject[] mossRocks, Vector2 center, RestPointType type, string name, string prompt, string detail)
        {
            Transform restGroup = CreateGroup(parent, name);
            GameObject[] shelfBank = type == RestPointType.ShortRest ? mossRocks : normalRocks;
            CreateRockShelf(restGroup, shelfBank, $"{name}_Shelf", center, type == RestPointType.ShortRest ? 7 : 9, 0.48f, type == RestPointType.ShortRest ? 0.26f : 0.28f);
            CreateRestPoint(restGroup, $"{name}_Trigger", new Vector3(center.x, center.y + 1.0f, 0f), new Vector3(3.8f, 2.0f, 1.2f), type, prompt, detail);
            CreateRestMarker(restGroup, new Vector3(center.x, center.y + 0.65f, 0f), type);
        }

        private static void BuildResourceBranch(Transform parent, GameObject[] normalRocks, GameObject[] mossRocks, string name, Vector3[] points)
        {
            Transform branch = CreateGroup(parent, name);
            for (int i = 0; i < points.Length; i++)
            {
                GameObject[] bank = i == points.Length - 1 ? mossRocks : normalRocks;
                GameObject rock = PlaceRock(branch, bank[i % bank.Length], $"{name}_Rock_{i:00}", points[i], new Vector3(0f, 0f, -18f + i * 12f), RockScaleForIndex(i + 50, i == points.Length - 1, false));
                ConfigureRockClimb(rock, "Resource branch rock", 45);
            }
        }

        private static void CreateResourceNode(Transform parent, string name, Vector3 position)
        {
            GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            node.name = name;
            node.transform.SetParent(parent, false);
            node.transform.position = new Vector3(position.x, position.y, 0f);
            node.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            node.GetComponent<Renderer>().sharedMaterial.color = new Color(0.92f, 0.78f, 0.23f);
            SphereCollider collider = node.GetComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 1.2f;
            node.AddComponent<ResourceNode>();
        }

        private static void ValidateLongClimbRocks(Transform levelRoot)
        {
            foreach (Transform child in levelRoot.GetComponentsInChildren<Transform>())
            {
                if (child.name == "LongClimbRockwall" || child.name.IndexOf("Rock", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                Vector3 position = child.position;
                if (!Mathf.Approximately(position.z, 0f))
                {
                    AlignRockFrontPlane(child.gameObject);
                }

                ClampRockToWallBounds(child.gameObject);
                ValidateRockBounds(child.gameObject);
            }
        }

        private static void ValidateRockBounds(GameObject rock)
        {
            if (!TryGetRendererBounds(rock, out Bounds bounds))
            {
                return;
            }

            float halfWidth = LongClimbWallWidth * 0.5f;
            float halfHeight = LongClimbWallHeight * 0.5f;
            float minX = LongClimbWallCenterX - halfWidth;
            float maxX = LongClimbWallCenterX + halfWidth;
            float minY = LongClimbWallCenterY - halfHeight;
            float maxY = LongClimbWallCenterY + halfHeight;

            if (bounds.min.x < minX || bounds.max.x > maxX || bounds.min.y < minY || bounds.max.y > maxY)
            {
                Debug.LogError($"{rock.name} is outside LongClimbRockwall after scaling. Bounds: {bounds}");
            }

            if (Mathf.Abs(bounds.min.z - LongClimbRockFrontZ) > LongClimbRockFrontTolerance)
            {
                Debug.LogError($"{rock.name} front plane mismatch. Expected {LongClimbRockFrontZ:0.###}, got {bounds.min.z:0.###}");
            }

            if (bounds.max.z < LongClimbWallFrontZ)
            {
                Debug.LogError($"{rock.name} does not reach the wall after front-plane alignment. Back z: {bounds.max.z:0.###}");
            }
        }

        private static float Hash01(int index, int salt)
        {
            uint value = (uint)(index + 1) * 747796405u + (uint)salt * 2891336453u;
            value = (value >> ((int)(value >> 28) + 4)) ^ value;
            value *= 277803737u;
            value = (value >> 22) ^ value;
            return value / (float)uint.MaxValue;
        }

        private static PlayerClimbController BuildPlayer(Transform parent)
        {
            GameObject playerObject = new GameObject("Player");
            playerObject.transform.SetParent(parent, false);
            playerObject.transform.position = new Vector3(-4.5f, 0.2f, 0f);

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
            return playerObject.AddComponent<PlayerClimbController>();
        }

        private static void BuildHud(Transform parent)
        {
            GameObject hud = new GameObject("HUD");
            hud.transform.SetParent(parent, false);
            hud.AddComponent<GameHUDPresenter>();

            GameObject inventoryUi = new GameObject("InventoryAndRestUI");
            inventoryUi.transform.SetParent(parent, false);
            inventoryUi.AddComponent<InventoryUI>();
        }

        private static void ConfigureCamera(Transform player)
        {
            Camera sceneCamera = Camera.main;
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
            platform.GetComponent<Renderer>().sharedMaterial.color = color;
            return platform;
        }

        private static GameObject CreateWallColumn(Transform parent, string objectName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = objectName;
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial.color = color;
            return wall;
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

        private static void CreateRestMarker(Transform parent, Vector3 position, RestPointType type)
        {
            if (type == RestPointType.ShortRest)
            {
                GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beacon.name = "ShortRestBeacon";
                beacon.transform.SetParent(parent, false);
                beacon.transform.position = position + new Vector3(0f, 0.55f, 0f);
                beacon.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);
                beacon.GetComponent<Renderer>().sharedMaterial.color = new Color(0.32f, 0.78f, 0.96f);

                GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crystal.name = "ShortRestCrystal";
                crystal.transform.SetParent(parent, false);
                crystal.transform.position = position + new Vector3(0f, 1.3f, 0f);
                crystal.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                crystal.GetComponent<Renderer>().sharedMaterial.color = new Color(0.66f, 0.9f, 1f);
            }
            else
            {
                GameObject fireBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fireBase.name = "LongRestFireBase";
                fireBase.transform.SetParent(parent, false);
                fireBase.transform.position = position + new Vector3(0f, 0.25f, 0f);
                fireBase.transform.localScale = new Vector3(0.55f, 0.12f, 0.55f);
                fireBase.GetComponent<Renderer>().sharedMaterial.color = new Color(0.34f, 0.2f, 0.14f);

                GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flame.name = "LongRestFlame";
                flame.transform.SetParent(parent, false);
                flame.transform.position = position + new Vector3(0f, 0.8f, 0f);
                flame.transform.localScale = new Vector3(0.38f, 0.55f, 0.38f);
                flame.GetComponent<Renderer>().sharedMaterial.color = new Color(1f, 0.58f, 0.18f);

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
                banner.GetComponent<Renderer>().sharedMaterial.color = new Color(0.52f, 0.34f, 0.14f);
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
