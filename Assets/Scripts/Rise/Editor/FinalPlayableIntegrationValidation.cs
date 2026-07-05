using System;
using Rise;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Rise.Editor
{
    public static class FinalPlayableIntegrationValidation
    {
        private const string ScenePath = "Assets/Scenes/FinalPlayable.unity";

        [MenuItem("Rise/Run Final Playable Integration Validation")]
        public static void RunAll()
        {
            EditorSceneManager.OpenScene(ScenePath);
            InventoryRestValidation.RunAll();
            TestSceneSystems();
            TestHudConfiguration();
            TestResourceNodeConfiguration();
            TestRestAndInventoryConfiguration();
            TestAudioConfiguration();
            Debug.Log("Final playable integration validation passed.");
        }

        private static void TestSceneSystems()
        {
            GameObject player = RequireObject("RisePrototypeWorld/Player");
            Require(player.GetComponent<PlayerVitals>() != null, "PlayerVitals missing");
            Require(player.GetComponent<PlayerInventory>() != null, "PlayerInventory missing");
            Require(player.GetComponent<RestSessionController>() != null, "RestSessionController missing");
            ValidateRestSessionController(player.GetComponent<RestSessionController>());
            Require(player.GetComponent<CookingSystem>() != null, "CookingSystem missing");
            Require(player.GetComponent<ToolController>() != null, "ToolController missing");
            Require(player.GetComponent<PlayerClimbController>() != null, "PlayerClimbController missing");
            Require(player.GetComponent<PlayerAudioBridge>() != null, "PlayerAudioBridge missing");
        }

        private static void TestHudConfiguration()
        {
            GameObject hud = RequireObject("RisePrototypeWorld/HUD");
            Require(hud.GetComponent<GameHUDPresenter>() != null, "GameHUDPresenter missing");
            Require(hud.transform.Find("TopPanel/StaminaBg/StaminaFill") != null, "stamina fill missing");
            RequireText(hud.transform, "TopPanel/StaminaValue");

            RectTransform resourceIcons = RequireRect(hud.transform, "ResourceIcons");
            Require(resourceIcons.anchorMin == Vector2.zero && resourceIcons.anchorMax == Vector2.zero, "ResourceIcons should be anchored bottom-left");
            Require(resourceIcons.anchoredPosition.x <= 40f && resourceIcons.anchoredPosition.y <= 40f, "ResourceIcons should remain in lower-left corner");

            RequireResourceDisplay(hud.transform, "Health");
            RequireResourceDisplay(hud.transform, "Hunger");
            RequireResourceDisplay(hud.transform, "Warmth");
            RequireResourceDisplay(hud.transform, "Sanity");

            Require(hud.transform.Find("StatusOverlay/HealthOverlay") != null, "health overlay missing");
            Require(hud.transform.Find("StatusOverlay/HungerOverlay") != null, "hunger overlay missing");
            Require(hud.transform.Find("StatusOverlay/WarmthOverlay") != null, "warmth overlay missing");
            Require(hud.transform.Find("StatusOverlay/SanityOverlay") != null, "sanity overlay missing");
            Require(hud.transform.Find("ResourceSearchPanel/ProgressBg/ProgressFill") != null, "resource search fill missing");
            RequireText(hud.transform, "ResourceSearchPanel/Label");
        }

        private static void TestResourceNodeConfiguration()
        {
            AuditResourceNode("ResourceNode_Left_01", "anchor_piton", 3);
            AuditResourceNode("ResourceNode_Right_01", "food_ration", 3);
            AuditResourceNode("ResourceNode_Final_01", "fuel_canister", 2);
        }

        private static void TestRestAndInventoryConfiguration()
        {
            GameObject ui = RequireObject("RisePrototypeWorld/InventoryAndRestUI");
            Require(ui.GetComponent<InventoryUI>() != null, "InventoryUI missing");
            Require(ui.transform.Find("InventoryPanel/SmallGrid") != null, "small pack grid missing");
            Require(ui.transform.Find("InventoryPanel/LargeGrid") != null, "large pack grid missing");
            RequireText(ui.transform, "RestPanel/RestMenu");

            RestPoint[] restPoints = UnityEngine.Object.FindObjectsByType<RestPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int shortRestCount = 0;
            int longRestCount = 0;
            for (int i = 0; i < restPoints.Length; i++)
            {
                if (restPoints[i].RestType == RestPointType.ShortRest) shortRestCount++;
                if (restPoints[i].RestType == RestPointType.LongRest) longRestCount++;
            }

            Require(shortRestCount >= 2, "expected at least two short-rest points");
            Require(longRestCount >= 2, "expected at least two long-rest points");
        }

        private static void TestAudioConfiguration()
        {
            AudioCueDatabase database = Resources.Load<AudioCueDatabase>("RiseAudioCueDatabase");
            Require(database != null, "RiseAudioCueDatabase should load from Resources");
            RequireCue(database, AudioCueId.ResourceSearchStart);
            RequireCue(database, AudioCueId.ResourceFound);
            RequireCue(database, AudioCueId.ResourceTake);
            RequireCue(database, AudioCueId.ItemUse);
            RequireCue(database, AudioCueId.CookSuccess);
            RequireCue(database, AudioCueId.CookFail);
            RequireCue(database, AudioCueId.BreathingLightLoop);
            RequireCue(database, AudioCueId.BreathingHeavyLoop);
            RequireCue(database, AudioCueId.CampfireLoop);
            RequireCue(database, AudioCueId.WindLoop);
            Require(typeof(AudioService).GetMethod("SetRestMixActive") != null, "AudioService rest mix hook missing");

            PlayerAudioBridge bridge = RequireObject("RisePrototypeWorld/Player").GetComponent<PlayerAudioBridge>();
            Require(bridge != null, "PlayerAudioBridge missing for status audio");
        }

        private static void ValidateRestSessionController(RestSessionController rest)
        {
            SerializedObject serialized = new SerializedObject(rest);
            Require(serialized.FindProperty("enterCameraDuration").floatValue > 0f, "enter rest camera duration should be positive");
            Require(serialized.FindProperty("exitCameraDuration").floatValue > 0f, "exit rest camera duration should be positive");
        }

        private static void AuditResourceNode(string nodeName, string expectedFirstItem, int expectedFirstQuantity)
        {
            GameObject nodeObject = RequireObject("RisePrototypeWorld/Level/InteractionMarkers/" + nodeName);
            ResourceNode node = nodeObject.GetComponent<ResourceNode>();
            Require(node != null, nodeName + " ResourceNode missing");
            Require(node.TotalItemCount == 3, nodeName + " should expose three configured entries");

            SerializedObject serialized = new SerializedObject(node);
            LootTableDefinition table = serialized.FindProperty("lootTable").objectReferenceValue as LootTableDefinition;
            Require(table != null, nodeName + " loot table missing");
            Require(table.Count == 3, nodeName + " loot table should have three entries");
            table.GetEntry(0, out string itemId, out int quantity);
            Require(itemId == expectedFirstItem, nodeName + " first loot item mismatch");
            Require(quantity == expectedFirstQuantity, nodeName + " first loot quantity mismatch");
        }

        private static void RequireResourceDisplay(Transform hud, string name)
        {
            Image icon = RequireImage(hud, "ResourceIcons/" + name + "/Icon");
            Text value = RequireText(hud, "ResourceIcons/" + name + "/Value");
            Require(icon.sprite != null, name + " resource icon should have a sprite");
            Require(int.TryParse(value.text, out _), name + " resource value should be Arabic numerals");
        }

        private static void RequireCue(AudioCueDatabase database, AudioCueId cueId)
        {
            Require(database.TryGetCue(cueId, out AudioCue cue), cueId + " cue missing");
            Require(cue.TryPickClip(out _), cueId + " cue should have at least one clip");
        }

        private static GameObject RequireObject(string path)
        {
            GameObject found = GameObject.Find(path);
            Require(found != null, path + " missing");
            return found;
        }

        private static RectTransform RequireRect(Transform root, string path)
        {
            Transform found = root.Find(path);
            Require(found != null, path + " missing");
            RectTransform rect = found.GetComponent<RectTransform>();
            Require(rect != null, path + " RectTransform missing");
            return rect;
        }

        private static Image RequireImage(Transform root, string path)
        {
            Transform found = root.Find(path);
            Require(found != null, path + " missing");
            Image image = found.GetComponent<Image>();
            Require(image != null, path + " Image missing");
            return image;
        }

        private static Text RequireText(Transform root, string path)
        {
            Transform found = root.Find(path);
            Require(found != null, path + " missing");
            Text text = found.GetComponent<Text>();
            Require(text != null, path + " Text missing");
            return text;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
