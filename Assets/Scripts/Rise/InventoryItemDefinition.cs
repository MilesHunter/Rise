using UnityEngine;

namespace Rise
{
    [CreateAssetMenu(menuName = "Rise/Inventory Item")]
    public sealed class InventoryItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId = "item";
        [SerializeField] private string displayName = "Item";
        [SerializeField] private int width = 1;
        [SerializeField] private int height = 1;
        [SerializeField] private int maxStack = 1;
        [SerializeField] private float weight = 1f;
        [SerializeField] private bool consumable;
        [SerializeField] private InventoryUseEffect useEffect;
        [SerializeField] private float useAmount = 10f;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public int Width => Mathf.Max(1, width);
        public int Height => Mathf.Max(1, height);
        public int MaxStack => Mathf.Max(1, maxStack);
        public float Weight => Mathf.Max(0f, weight);
        public bool Consumable => consumable;
        public InventoryUseEffect UseEffect => useEffect;
        public float UseAmount => useAmount;

        public static InventoryItemDefinition CreateRuntime(string id, string name, int width, int height, int maxStack, float weight, bool consumable = false, InventoryUseEffect useEffect = InventoryUseEffect.None, float useAmount = 0f)
        {
            InventoryItemDefinition definition = CreateInstance<InventoryItemDefinition>();
            definition.itemId = id;
            definition.displayName = name;
            definition.width = Mathf.Max(1, width);
            definition.height = Mathf.Max(1, height);
            definition.maxStack = Mathf.Max(1, maxStack);
            definition.weight = Mathf.Max(0f, weight);
            definition.consumable = consumable;
            definition.useEffect = useEffect;
            definition.useAmount = useAmount;
            definition.hideFlags = HideFlags.DontSave;
            return definition;
        }
    }
}
