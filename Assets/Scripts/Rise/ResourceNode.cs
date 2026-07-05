using System.Collections.Generic;
using System;
using UnityEngine;

namespace Rise
{
    public sealed class ResourceNode : MonoBehaviour
    {
        [SerializeField] private float revealInterval = 1f;
        [SerializeField] private LootTableDefinition lootTable;
        [SerializeField] private string[] fallbackItemIds = { "anchor_piton", "food_ration", "rare_relic" };
        [SerializeField] private int[] fallbackQuantities = { 2, 2, 1 };

        private readonly List<string> revealedItemIds = new List<string>();
        private readonly List<int> revealedQuantities = new List<int>();
        private readonly List<string> searchedItemIds = new List<string>();
        private readonly List<int> searchedQuantities = new List<int>();
        private readonly List<bool> searchedTaken = new List<bool>();
        private float searchTimer;
        private bool searching;
        private PlayerVitals searcherVitals;
        private int nextRevealIndex;

        public event Action<ResourceNode, string, int, Vector3> ItemRevealed;

        public bool IsSearching => searching;
        public int RevealedCount => revealedItemIds.Count;
        public bool FullyRevealed => nextRevealIndex >= TotalItemCount;
        public int TotalItemCount => GetEntryCount();
        public int RevealedTotalCount => nextRevealIndex;
        public float SearchProgress01
        {
            get
            {
                if (!searching)
                {
                    return revealedItemIds.Count > 0 || FullyRevealed ? 1f : 0f;
                }

                float interval = GetCurrentRevealInterval();
                return interval <= 0.001f ? 1f : Mathf.Clamp01(searchTimer / interval);
            }
        }
        public string SearchLabel => BuildSearchLabel();

        private void Reset()
        {
            EnsureTrigger();
        }

        private void Awake()
        {
            EnsureTrigger();
        }

        private void Update()
        {
            if (!searching)
            {
                return;
            }

            searchTimer += Time.unscaledDeltaTime;
            float interval = GetCurrentRevealInterval();
            if (searchTimer < interval)
            {
                return;
            }

            searchTimer = 0f;
            RevealNext();
        }

        public void StartSearch(MonoBehaviour runner)
        {
            if (!searching && !FullyRevealed && runner != null)
            {
                searching = true;
                searcherVitals = ResolveVitals(runner);
                searchTimer = 0f;
            }
        }

        public bool TryTakeRevealed(PlayerInventory inventory, int index, bool allowLargeFallback = false)
        {
            if (inventory == null || index < 0 || index >= revealedItemIds.Count)
            {
                return false;
            }

            bool added = inventory.AddToSmall(revealedItemIds[index], revealedQuantities[index]);
            if (!added && allowLargeFallback)
            {
                added = inventory.AddToLarge(revealedItemIds[index], revealedQuantities[index]);
            }

            if (added)
            {
                MarkTaken(revealedItemIds[index], revealedQuantities[index]);
                revealedItemIds.RemoveAt(index);
                revealedQuantities.RemoveAt(index);
            }
            return added;
        }

        public bool TryGetRevealed(int index, out string itemId, out int quantity)
        {
            itemId = null;
            quantity = 0;
            if (index < 0 || index >= revealedItemIds.Count)
            {
                return false;
            }

            itemId = revealedItemIds[index];
            quantity = revealedQuantities[index];
            return true;
        }

        public string BuildStatusText()
        {
            if (revealedItemIds.Count == 0)
            {
                if (FullyRevealed)
                {
                    return "Resource node depleted";
                }

                string speed = searcherVitals != null && searcherVitals.ResourceSearchIntervalMultiplier > 1.05f ? " slower" : string.Empty;
                return IsSearching ? $"Searching resource node{speed}..." : "Resource node: press F to search";
            }

            string text = IsSearching ? "Searching. Found:" : "Found:";
            for (int i = 0; i < revealedItemIds.Count; i++)
            {
                text += $" {revealedItemIds[i]} x{revealedQuantities[i]}";
            }
            text += "  Press F to take first";
            return text;
        }

        public string BuildSearchLabel()
        {
            if (revealedItemIds.Count > 0)
            {
                return $"Found {BuildRevealedSummary()}";
            }

            if (FullyRevealed)
            {
                return "Resource node depleted";
            }

            if (searching)
            {
                string speed = searcherVitals != null && searcherVitals.ResourceSearchIntervalMultiplier > 1.05f ? " slower" : string.Empty;
                return $"Searching{speed} {RevealedTotalCount}/{TotalItemCount}";
            }

            return $"Resource node {RevealedTotalCount}/{TotalItemCount}";
        }

        public string BuildRevealedSummary()
        {
            if (revealedItemIds.Count == 0)
            {
                return string.Empty;
            }

            string text = string.Empty;
            for (int i = 0; i < revealedItemIds.Count; i++)
            {
                if (i > 0)
                {
                    text += ", ";
                }

                text += $"{revealedItemIds[i]} x{revealedQuantities[i]}";
            }

            return text;
        }

        public string BuildSlotSummary()
        {
            int total = TotalItemCount;
            if (total <= 0)
            {
                return "No resources";
            }

            string text = string.Empty;
            for (int i = 0; i < total; i++)
            {
                if (i > 0)
                {
                    text += " | ";
                }

                if (i >= searchedItemIds.Count)
                {
                    text += "???";
                    continue;
                }

                text += searchedTaken[i]
                    ? "taken"
                    : $"{searchedItemIds[i]} x{searchedQuantities[i]}";
            }

            return text;
        }

        private float GetCurrentRevealInterval()
        {
            return Mathf.Max(0.05f, revealInterval * (searcherVitals != null ? searcherVitals.ResourceSearchIntervalMultiplier : 1f));
        }

        private void EnsureTrigger()
        {
            SphereCollider sphere = GetComponent<SphereCollider>();
            if (sphere == null)
            {
                sphere = gameObject.AddComponent<SphereCollider>();
            }

            sphere.isTrigger = true;
            sphere.radius = Mathf.Max(sphere.radius, 1.2f);
        }

        private void RevealNext()
        {
            if (FullyRevealed)
            {
                searching = false;
                return;
            }

            int revealIndex = nextRevealIndex;
            nextRevealIndex++;
            GetEntry(revealIndex, out string itemId, out int quantity);
            revealedItemIds.Add(itemId);
            revealedQuantities.Add(quantity);
            searchedItemIds.Add(itemId);
            searchedQuantities.Add(quantity);
            searchedTaken.Add(false);
            ItemRevealed?.Invoke(this, revealedItemIds[revealedItemIds.Count - 1], revealedQuantities[revealedQuantities.Count - 1], transform.position);
            if (FullyRevealed)
            {
                searching = false;
                searcherVitals = null;
            }
        }

        private static PlayerVitals ResolveVitals(MonoBehaviour runner)
        {
            if (runner == null)
            {
                return null;
            }

            if (runner.TryGetComponent(out PlayerVitals vitals))
            {
                return vitals;
            }

            if (runner.TryGetComponent(out PlayerClimbController controller))
            {
                return controller.Vitals != null ? controller.Vitals : controller.GetComponent<PlayerVitals>();
            }

            return runner.GetComponentInParent<PlayerVitals>();
        }

        private int GetEntryCount()
        {
            if (lootTable != null && lootTable.Count > 0)
            {
                return lootTable.Count;
            }

            return fallbackItemIds != null ? fallbackItemIds.Length : 0;
        }

        private void GetEntry(int index, out string itemId, out int quantity)
        {
            if (lootTable != null && lootTable.Count > 0)
            {
                lootTable.GetEntry(index, out itemId, out quantity);
                return;
            }

            itemId = fallbackItemIds[index];
            quantity = fallbackQuantities != null && index < fallbackQuantities.Length ? Mathf.Max(1, fallbackQuantities[index]) : 1;
        }

        private void MarkTaken(string itemId, int quantity)
        {
            for (int i = 0; i < searchedItemIds.Count; i++)
            {
                if (!searchedTaken[i] && searchedItemIds[i] == itemId && searchedQuantities[i] == quantity)
                {
                    searchedTaken[i] = true;
                    return;
                }
            }
        }
    }
}
