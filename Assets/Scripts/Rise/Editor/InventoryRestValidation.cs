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
            TestCooking();
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

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
