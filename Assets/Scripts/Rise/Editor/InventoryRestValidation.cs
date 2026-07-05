using System;
using Rise;
using UnityEditor;
using UnityEngine;

namespace Rise.Editor
{
    public static class InventoryRestValidation
    {
        [MenuItem("Rise/Run Inventory Rest Validation")]
        public static void RunAll()
        {
            TestGridPlacement();
            TestStackingAndTransfer();
            TestInventoryWeightAndConsume();
            TestPassiveResourceDecay();
            TestLowResourcesDoNotAffectGameplayMultipliers();
            TestRestRecoversAllLongTermResources();
            TestFreeHandsStaminaRecovery();
            TestCooking();
            TestResourceCollection();
            TestLootTableResourceCollection();
            TestAudioCueDatabase();
            Debug.Log("Inventory/rest validation passed.");
        }

        public static void RunAllBatch()
        {
            try
            {
                RunAll();
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
                else
                {
                    throw;
                }
            }
        }

        private static void TestGridPlacement()
        {
            InventoryItemDefinition item = InventoryItemDefinition.CreateRuntime("test_tool", "Test Tool", 2, 1, 4, 1f);
            InventoryGrid grid = new InventoryGrid(4, 4, "test");
            InventoryItemStack stack = new InventoryItemStack(item, 1);
            Require(grid.TryPlace(stack, 0, 0), "inside placement should succeed");
            Require(!grid.CanPlace(new InventoryItemStack(item, 1), 3, 0), "out-of-bounds placement should fail");
            Require(!grid.CanPlace(new InventoryItemStack(item, 1), 1, 0), "overlap placement should fail");
            Require(grid.TryRotate(stack), "rotation in bounds should succeed");
            Require(stack.Width == 1 && stack.Height == 2, "rotation should swap occupied dimensions");
        }

        private static void TestStackingAndTransfer()
        {
            InventoryItemDefinition item = InventoryItemDefinition.CreateRuntime("piton", "Piton", 1, 1, 4, 0.5f);
            InventoryGrid source = new InventoryGrid(4, 4, "source");
            InventoryGrid target = new InventoryGrid(4, 4, "target");
            Require(source.AddItem(item, 7) == 0, "source should accept stacked items");
            Require(source.Stacks.Count == 2, "stacking should stop at max stack and create second stack");
            Require(target.AddItem(item, 3) == 0, "target seed should succeed");
            Require(source.TryQuickTransferTo(target, source.Stacks[0]), "quick transfer should move items");
            Require(target.CountItem("piton") == 7, "quick transfer should merge before new slot");
        }

        private static void TestInventoryWeightAndConsume()
        {
            GameObject player = new GameObject("InventoryValidationPlayer");
            try
            {
                PlayerVitals vitals = player.AddComponent<PlayerVitals>();
                PlayerInventory inventory = player.AddComponent<PlayerInventory>();
                inventory.EnsureInitialized();
                int before = inventory.CountSmall(PlayerInventory.AnchorItemId);
                Require(before > 0, "default small pack should contain anchors");
                Require(inventory.TryConsumeSmall(PlayerInventory.AnchorItemId, 1), "anchor consume should succeed");
                Require(inventory.CountSmall(PlayerInventory.AnchorItemId) == before - 1, "consume should subtract one anchor");
                Require(inventory.HoldDrainMultiplier >= 1f && inventory.KickForceMultiplier <= 1f, "weight multipliers should be valid");
                Require(vitals != null, "vitals should exist for inventory validation");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void TestCooking()
        {
            GameObject player = new GameObject("CookingValidationPlayer");
            try
            {
                player.AddComponent<PlayerVitals>();
                player.AddComponent<PlayerInventory>();
                CookingSystem cooking = player.AddComponent<CookingSystem>();
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                inventory.EnsureInitialized();
                int before = inventory.CountSmall("food_ration");
                Require(cooking.TryCook(0), "warm meal should cook with seeded food ration");
                Require(inventory.CountSmall("food_ration") == before - 1, "cooking should consume material");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void TestPassiveResourceDecay()
        {
            GameObject player = new GameObject("PassiveResourceDecayValidationPlayer");
            try
            {
                PlayerVitals vitals = player.AddComponent<PlayerVitals>();
                SerializedObject serialized = new SerializedObject(vitals);
                serialized.FindProperty("passiveDecayIntervalRange").vector2Value = new Vector2(0.01f, 0.01f);
                serialized.FindProperty("passiveHungerDecayRange").vector2Value = new Vector2(1f, 1f);
                serialized.FindProperty("passiveWarmthDecayRange").vector2Value = new Vector2(1f, 1f);
                serialized.FindProperty("passiveSanityDecayRange").vector2Value = new Vector2(1f, 1f);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                InvokePassiveDecayTick(vitals);
                Require(vitals.Health == 100f, "passive decay should not reduce health");
                Require(vitals.Hunger < 100f, "passive decay should reduce hunger");
                Require(vitals.Warmth < 100f, "passive decay should reduce warmth");
                Require(vitals.Sanity < 100f, "passive decay should reduce sanity");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void TestLowResourcesDoNotAffectGameplayMultipliers()
        {
            GameObject player = new GameObject("LowResourceNeutralGameplayValidationPlayer");
            try
            {
                PlayerVitals vitals = player.AddComponent<PlayerVitals>();
                vitals.ModifyHealth(-90f);
                vitals.ModifyHunger(-90f);
                vitals.ModifyWarmth(-90f);
                vitals.ModifySanity(-90f);

                Require(vitals.LowHealth && vitals.LowHunger && vitals.LowWarmth && vitals.LowSanity, "test should put all long-term resources into low state");
                Require(Mathf.Approximately(vitals.GrabCostMultiplier, 1f), "low resources should not change grab cost");
                Require(Mathf.Approximately(vitals.HoldDrainMultiplier, 1f), "low resources should not change hold drain");
                Require(Mathf.Approximately(vitals.StaminaRecoveryMultiplier, 1f), "low resources should not change stamina recovery");
                Require(Mathf.Approximately(vitals.KickForceMultiplier, 1f), "low resources should not change kick force");
                Require(Mathf.Approximately(vitals.ResourceSearchIntervalMultiplier, 1f), "low resources should not change resource search speed");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void TestRestRecoversAllLongTermResources()
        {
            GameObject player = new GameObject("RestRecoveryValidationPlayer");
            try
            {
                PlayerVitals vitals = player.AddComponent<PlayerVitals>();
                vitals.ModifyHealth(-50f);
                vitals.ModifyHunger(-50f);
                vitals.ModifyWarmth(-50f);
                vitals.ModifySanity(-50f);

                float healthBefore = vitals.Health;
                float hungerBefore = vitals.Hunger;
                float warmthBefore = vitals.Warmth;
                float sanityBefore = vitals.Sanity;
                vitals.RecoverForShortRest();
                Require(vitals.Health > healthBefore, "short rest should recover health");
                Require(vitals.Hunger > hungerBefore, "short rest should recover hunger");
                Require(vitals.Warmth > warmthBefore, "short rest should recover warmth");
                Require(vitals.Sanity > sanityBefore, "short rest should recover sanity");

                vitals.ModifyHealth(-50f);
                vitals.ModifyHunger(-50f);
                vitals.ModifyWarmth(-50f);
                vitals.ModifySanity(-50f);
                healthBefore = vitals.Health;
                hungerBefore = vitals.Hunger;
                warmthBefore = vitals.Warmth;
                sanityBefore = vitals.Sanity;
                vitals.RestoreAllForLongRest();
                Require(vitals.Health > healthBefore, "long rest should recover health");
                Require(vitals.Hunger > hungerBefore, "long rest should recover hunger");
                Require(vitals.Warmth > warmthBefore, "long rest should recover warmth");
                Require(vitals.Sanity > sanityBefore, "long rest should recover sanity");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void TestFreeHandsStaminaRecovery()
        {
            GameObject player = new GameObject("FreeHandsStaminaRecoveryValidationPlayer");
            try
            {
                player.AddComponent<Rigidbody>();
                player.AddComponent<CapsuleCollider>();
                PlayerVitals vitals = player.AddComponent<PlayerVitals>();
                player.AddComponent<PlayerInventory>();
                player.AddComponent<ToolController>();
                PlayerClimbController controller = player.AddComponent<PlayerClimbController>();

                Require(vitals.TrySpendStamina(20f), "test should reduce stamina before free-hands recovery");
                float depleted = vitals.Stamina;

                InvokeFreeHandsStaminaRecovery(controller, 2.9f);
                Require(Mathf.Approximately(vitals.Stamina, depleted), "free hands should not recover stamina before the delay");

                InvokeFreeHandsStaminaRecovery(controller, 0.2f);
                Require(vitals.Stamina > depleted, "free hands should start recovering after the delay");

                float afterDelay = vitals.Stamina;
                InvokeFreeHandsStaminaRecovery(controller, 1f);
                Require(Mathf.Abs(vitals.Stamina - (afterDelay + 2f)) <= 0.01f, "free hands should recover stamina at two points per second");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void TestResourceCollection()
        {
            GameObject player = new GameObject("ResourceCollectionValidationPlayer");
            GameObject nodeObject = new GameObject("ResourceCollectionValidationNode");
            try
            {
                player.AddComponent<PlayerVitals>();
                PlayerInventory inventory = player.AddComponent<PlayerInventory>();
                inventory.EnsureInitialized();

                ResourceNode node = nodeObject.AddComponent<ResourceNode>();
                SerializedObject serializedNode = new SerializedObject(node);
                serializedNode.FindProperty("revealInterval").floatValue = 0.001f;
                serializedNode.ApplyModifiedPropertiesWithoutUndo();
                int foundEvents = 0;
                node.ItemRevealed += (source, itemId, quantity, origin) => foundEvents++;
                node.StartSearch(player.AddComponent<ValidationRunner>());
                InvokePrivateReveal(node);

                Require(foundEvents > 0, "resource search should reveal at least one item");
                Require(node.RevealedCount > 0, "resource node should expose revealed item");
                Require(node.TryGetRevealed(0, out string itemId, out int quantity), "revealed item should be queryable");
                Require(!string.IsNullOrEmpty(itemId) && quantity > 0, "revealed item data should be valid");
                Require(node.BuildSlotSummary().Contains(itemId), "slot summary should show revealed item");

                int before = inventory.CountSmall(itemId);
                Require(node.TryTakeRevealed(inventory, 0), "revealed item should be collectable into small pack");
                Require(inventory.CountSmall(itemId) >= before + quantity, "collection should add item quantity");
                Require(node.BuildSlotSummary().Contains("taken"), "slot summary should mark collected item as taken");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(nodeObject);
            }
        }

        private static void TestLootTableResourceCollection()
        {
            LootTableDefinition table = ScriptableObject.CreateInstance<LootTableDefinition>();
            GameObject player = new GameObject("LootTableResourceValidationPlayer");
            GameObject nodeObject = new GameObject("LootTableResourceValidationNode");
            try
            {
                SerializedObject serializedTable = new SerializedObject(table);
                SerializedProperty ids = serializedTable.FindProperty("itemIds");
                SerializedProperty quantities = serializedTable.FindProperty("quantities");
                ids.arraySize = 2;
                quantities.arraySize = 2;
                ids.GetArrayElementAtIndex(0).stringValue = "fuel_canister";
                quantities.GetArrayElementAtIndex(0).intValue = 2;
                ids.GetArrayElementAtIndex(1).stringValue = "rare_relic";
                quantities.GetArrayElementAtIndex(1).intValue = 1;
                serializedTable.ApplyModifiedPropertiesWithoutUndo();

                player.AddComponent<PlayerVitals>();
                PlayerInventory inventory = player.AddComponent<PlayerInventory>();
                inventory.EnsureInitialized();

                ResourceNode node = nodeObject.AddComponent<ResourceNode>();
                SerializedObject serializedNode = new SerializedObject(node);
                serializedNode.FindProperty("lootTable").objectReferenceValue = table;
                serializedNode.ApplyModifiedPropertiesWithoutUndo();

                Require(node.TotalItemCount == 2, "loot table should drive resource node total count");
                Require(node.BuildSlotSummary().Contains("???"), "loot table node should show unknown slots before reveal");
                node.StartSearch(player.AddComponent<ValidationRunner>());
                InvokePrivateReveal(node);
                Require(node.TryGetRevealed(0, out string itemId, out int quantity), "loot table item should reveal");
                Require(itemId == "fuel_canister" && quantity == 2, "first loot table entry should be revealed");
                Require(node.BuildSlotSummary().Contains("fuel_canister x2"), "loot table slot summary should show revealed entry");
                Require(node.TryTakeRevealed(inventory, 0, true), "loot table item should collect with large-pack fallback");
                Require(node.BuildSlotSummary().Contains("taken"), "loot table slot summary should mark taken entry");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(table);
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(nodeObject);
            }
        }

        private static void TestAudioCueDatabase()
        {
            AudioCueDatabase database = Resources.Load<AudioCueDatabase>("RiseAudioCueDatabase");
            Require(database != null, "RiseAudioCueDatabase should load from Resources");
            Require(database.TryGetCue(AudioCueId.ResourceSearchStart, out AudioCue searchCue) && searchCue.TryPickClip(out _), "resource search cue should have a clip");
            Require(database.TryGetCue(AudioCueId.ResourceFound, out AudioCue foundCue) && foundCue.TryPickClip(out _), "resource found cue should have a clip");
            Require(database.TryGetCue(AudioCueId.ResourceTake, out AudioCue takeCue) && takeCue.TryPickClip(out _), "resource take cue should have a clip");
            Require(database.TryGetCue(AudioCueId.ItemUse, out AudioCue itemUseCue) && itemUseCue.TryPickClip(out _), "item use cue should have a clip");
            Require(database.TryGetCue(AudioCueId.CookSuccess, out AudioCue cookSuccessCue) && cookSuccessCue.TryPickClip(out _), "cook success cue should have a clip");
            Require(database.TryGetCue(AudioCueId.CookFail, out AudioCue cookFailCue) && cookFailCue.TryPickClip(out _), "cook fail cue should have a clip");
        }

        private static void InvokePrivateReveal(ResourceNode node)
        {
            System.Reflection.MethodInfo reveal = typeof(ResourceNode).GetMethod("RevealNext", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            reveal.Invoke(node, null);
        }

        private static void InvokePassiveDecayTick(PlayerVitals vitals)
        {
            System.Reflection.MethodInfo tick = typeof(PlayerVitals).GetMethod("ApplyPassiveDecayTick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            tick.Invoke(vitals, null);
        }

        private static void InvokeFreeHandsStaminaRecovery(PlayerClimbController controller, float deltaTime)
        {
            System.Reflection.MethodInfo tick = typeof(PlayerClimbController).GetMethod("UpdateFreeHandsStaminaRecovery", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            tick.Invoke(controller, new object[] { deltaTime });
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class ValidationRunner : MonoBehaviour
        {
        }
    }
}
