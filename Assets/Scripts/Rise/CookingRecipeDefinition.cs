using UnityEngine;

namespace Rise
{
    [CreateAssetMenu(menuName = "Rise/Cooking Recipe")]
    public sealed class CookingRecipeDefinition : ScriptableObject
    {
        [SerializeField] private string recipeId = "meal";
        [SerializeField] private string displayName = "Warm Meal";
        [SerializeField] private string ingredientItemId = "food_ration";
        [SerializeField] private int ingredientQuantity = 1;
        [SerializeField] private float healthRestore = 8f;
        [SerializeField] private float hungerRestore = 22f;
        [SerializeField] private float warmthRestore = 18f;
        [SerializeField] private float sanityRestore = 8f;

        public string DisplayName => displayName;
        public string IngredientItemId => ingredientItemId;
        public int IngredientQuantity => Mathf.Max(1, ingredientQuantity);
        public float HealthRestore => healthRestore;
        public float HungerRestore => hungerRestore;
        public float WarmthRestore => warmthRestore;
        public float SanityRestore => sanityRestore;

        public static CookingRecipeDefinition CreateRuntime(string id, string name, string ingredient, int quantity, float health, float hunger, float warmth, float sanity)
        {
            CookingRecipeDefinition recipe = CreateInstance<CookingRecipeDefinition>();
            recipe.recipeId = id;
            recipe.displayName = name;
            recipe.ingredientItemId = ingredient;
            recipe.ingredientQuantity = quantity;
            recipe.healthRestore = health;
            recipe.hungerRestore = hunger;
            recipe.warmthRestore = warmth;
            recipe.sanityRestore = sanity;
            recipe.hideFlags = HideFlags.DontSave;
            return recipe;
        }
    }
}
