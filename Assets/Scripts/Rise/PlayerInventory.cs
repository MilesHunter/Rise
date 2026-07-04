using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rise
{
    [RequireComponent(typeof(PlayerVitals))]
    public sealed class PlayerInventory : MonoBehaviour
    {
        public const string AnchorItemId = "anchor_piton";
        public const string RopeItemId = "climbing_rope";

        private readonly Dictionary<string, InventoryItemDefinition> definitions = new Dictionary<string, InventoryItemDefinition>();

        [SerializeField] private float lightLimit = 14f;
        [SerializeField] private float mediumLimit = 26f;
        [SerializeField] private float heavyLimit = 38f;

        public InventoryGrid SmallPack { get; private set; }
        public InventoryGrid LargePack { get; private set; }
        public event Action Changed;

        public float TotalWeight => (SmallPack?.TotalWeight() ?? 0f) + (LargePack?.TotalWeight() ?? 0f);
        public InventoryWeightClass WeightClass
        {
            get
            {
                float weight = TotalWeight;
                if (weight <= lightLimit) return InventoryWeightClass.Light;
                if (weight <= mediumLimit) return InventoryWeightClass.Medium;
                if (weight <= heavyLimit) return InventoryWeightClass.Heavy;
                return InventoryWeightClass.Overloaded;
            }
        }

        public float HoldDrainMultiplier
        {
            get
            {
                switch (WeightClass)
                {
                    case InventoryWeightClass.Medium: return 1.18f;
                    case InventoryWeightClass.Heavy: return 1.38f;
                    case InventoryWeightClass.Overloaded: return 1.75f;
                    default: return 1f;
                }
            }
        }

        public float KickForceMultiplier
        {
            get
            {
                switch (WeightClass)
                {
                    case InventoryWeightClass.Medium: return 0.92f;
                    case InventoryWeightClass.Heavy: return 0.72f;
                    case InventoryWeightClass.Overloaded: return 0.5f;
                    default: return 1f;
                }
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (SmallPack != null && LargePack != null)
            {
                return;
            }

            SmallPack = new InventoryGrid(4, 4, "Small pack");
            LargePack = new InventoryGrid(6, 6, "Camp pack");
            CreateRuntimeDefinitions();
            SeedDefaultItems();
            Changed?.Invoke();
        }

        public InventoryItemDefinition GetDefinition(string itemId)
        {
            EnsureInitialized();
            return definitions.TryGetValue(itemId, out InventoryItemDefinition definition) ? definition : null;
        }

        public int CountSmall(string itemId)
        {
            EnsureInitialized();
            return SmallPack.CountItem(itemId);
        }

        public bool TryConsumeSmall(string itemId, int quantity)
        {
            EnsureInitialized();
            bool consumed = SmallPack.TryRemoveItems(itemId, quantity);
            if (consumed)
            {
                Changed?.Invoke();
            }
            return consumed;
        }

        public bool AddToSmall(string itemId, int quantity)
        {
            EnsureInitialized();
            InventoryItemDefinition definition = GetDefinition(itemId);
            bool addedAll = definition != null && SmallPack.AddItem(definition, quantity) == 0;
            if (addedAll)
            {
                Changed?.Invoke();
            }
            return addedAll;
        }

        public bool AddToLarge(string itemId, int quantity)
        {
            EnsureInitialized();
            InventoryItemDefinition definition = GetDefinition(itemId);
            bool addedAll = definition != null && LargePack.AddItem(definition, quantity) == 0;
            if (addedAll)
            {
                Changed?.Invoke();
            }
            return addedAll;
        }

        public void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private void CreateRuntimeDefinitions()
        {
            definitions[AnchorItemId] = InventoryItemDefinition.CreateRuntime(AnchorItemId, "Anchor Piton", 1, 1, 8, 0.8f);
            definitions[RopeItemId] = InventoryItemDefinition.CreateRuntime(RopeItemId, "Climbing Rope", 1, 2, 3, 2.2f);
            definitions["food_ration"] = InventoryItemDefinition.CreateRuntime("food_ration", "Food Ration", 1, 1, 6, 0.5f, true, InventoryUseEffect.RestoreHunger, 18f);
            definitions["medkit"] = InventoryItemDefinition.CreateRuntime("medkit", "Medkit", 2, 1, 2, 1.2f, true, InventoryUseEffect.RestoreHealth, 25f);
            definitions["fuel_canister"] = InventoryItemDefinition.CreateRuntime("fuel_canister", "Fuel Canister", 1, 2, 2, 1.8f, true, InventoryUseEffect.RestoreWarmth, 20f);
            definitions["rare_relic"] = InventoryItemDefinition.CreateRuntime("rare_relic", "Rare Relic", 2, 2, 1, 3.2f);
        }

        private void SeedDefaultItems()
        {
            SmallPack.AddItem(definitions[AnchorItemId], 4);
            SmallPack.AddItem(definitions[RopeItemId], 2);
            SmallPack.AddItem(definitions["food_ration"], 2);
            SmallPack.AddItem(definitions["medkit"], 1);
            LargePack.AddItem(definitions[AnchorItemId], 8);
            LargePack.AddItem(definitions[RopeItemId], 2);
            LargePack.AddItem(definitions["fuel_canister"], 2);
            LargePack.AddItem(definitions["food_ration"], 5);
        }
    }
}
