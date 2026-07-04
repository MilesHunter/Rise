using UnityEngine;

namespace Rise
{
    [CreateAssetMenu(menuName = "Rise/Loot Table")]
    public sealed class LootTableDefinition : ScriptableObject
    {
        [SerializeField] private string[] itemIds = { "anchor_piton", "food_ration", "rare_relic" };
        [SerializeField] private int[] quantities = { 2, 1, 1 };

        public int Count => itemIds != null ? itemIds.Length : 0;

        public void GetEntry(int index, out string itemId, out int quantity)
        {
            itemId = itemIds[index];
            quantity = quantities != null && index < quantities.Length ? Mathf.Max(1, quantities[index]) : 1;
        }
    }
}
