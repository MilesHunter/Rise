using System.Collections.Generic;
using UnityEngine;

namespace Rise
{
    public sealed class ResourceNode : MonoBehaviour
    {
        [SerializeField] private float revealInterval = 1f;
        [SerializeField] private string[] fallbackItemIds = { "anchor_piton", "food_ration", "rare_relic" };
        [SerializeField] private int[] fallbackQuantities = { 2, 2, 1 };

        private readonly List<string> revealedItemIds = new List<string>();
        private readonly List<int> revealedQuantities = new List<int>();
        private float searchTimer;
        private bool searching;

        public bool IsSearching => searching;
        public int RevealedCount => revealedItemIds.Count;
        public bool FullyRevealed => RevealedCount >= fallbackItemIds.Length;

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
            if (searchTimer < revealInterval)
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
                searchTimer = 0f;
            }
        }

        public bool TryTakeRevealed(PlayerInventory inventory, int index)
        {
            if (inventory == null || index < 0 || index >= revealedItemIds.Count)
            {
                return false;
            }

            bool added = inventory.AddToSmall(revealedItemIds[index], revealedQuantities[index]);
            if (added)
            {
                revealedItemIds.RemoveAt(index);
                revealedQuantities.RemoveAt(index);
            }
            return added;
        }

        public string BuildStatusText()
        {
            if (revealedItemIds.Count == 0)
            {
                return IsSearching ? "Searching resource node..." : "Resource node: press F to search";
            }

            string text = IsSearching ? "Searching. Found:" : "Found:";
            for (int i = 0; i < revealedItemIds.Count; i++)
            {
                text += $" {revealedItemIds[i]} x{revealedQuantities[i]}";
            }
            text += "  Press F to take first";
            return text;
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

            revealedItemIds.Add(fallbackItemIds[revealedItemIds.Count]);
            revealedQuantities.Add(fallbackQuantities != null && revealedItemIds.Count - 1 < fallbackQuantities.Length ? fallbackQuantities[revealedItemIds.Count - 1] : 1);
            if (FullyRevealed)
            {
                searching = false;
            }
        }
    }
}
