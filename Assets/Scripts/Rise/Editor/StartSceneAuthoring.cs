using System;
using System.Collections.Generic;
using System.IO;
using Rise;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Rise.Editor
{
    public static class StartSceneAuthoring
    {
        public const string ScenePath = "Assets/Scenes/Start.unity";
        private const string RootName = "StartScene";
        private static readonly string AutorunFlagPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "author-start-scene.flag"));
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

        [MenuItem("Rise/Author Start Scene")]
        public static void CreateScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Start";

            GameObject root = new GameObject(RootName);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.047f, 0.055f, 1f);
            cameraObject.AddComponent<AudioListener>();

            GameObject menuObject = new GameObject("StartMenu");
            menuObject.transform.SetParent(root.transform, false);
            menuObject.AddComponent<StartMenuController>();

            GameObject eventSystemObject = new GameObject("EventSystem");
            EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
            eventSystem.firstSelectedGameObject = null;
            eventSystemObject.AddComponent<InputSystemUIInputModule>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Start scene saved to {ScenePath}");
        }

        private static void AddToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(scenePath, true)
            };

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                scenes.Add(scene);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
