using System.Collections.Generic;
using UnityEngine;

namespace Rise
{
    public sealed class CookingSystem : MonoBehaviour
    {
        private readonly List<CookingRecipeDefinition> recipes = new List<CookingRecipeDefinition>();
        private PlayerInventory inventory;
        private PlayerVitals vitals;

        public IReadOnlyList<CookingRecipeDefinition> Recipes => recipes;
        public string LastMessage { get; private set; }

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            vitals = GetComponent<PlayerVitals>();
            EnsureRecipes();
        }

        public bool TryCook(int index)
        {
            EnsureRecipes();
            if (index < 0 || index >= recipes.Count)
            {
                LastMessage = "No recipe selected";
                return false;
            }

            CookingRecipeDefinition recipe = recipes[index];
            if (!inventory.TryConsumeSmall(recipe.IngredientItemId, recipe.IngredientQuantity) &&
                !inventory.LargePack.TryRemoveItems(recipe.IngredientItemId, recipe.IngredientQuantity))
            {
                LastMessage = $"Missing {recipe.IngredientItemId}";
                return false;
            }

            vitals.RestoreByRecipe(recipe.HealthRestore, recipe.HungerRestore, recipe.WarmthRestore, recipe.SanityRestore);
            inventory.NotifyChanged();
            LastMessage = $"Cooked {recipe.DisplayName}";
            return true;
        }

        private void EnsureRecipes()
        {
            if (recipes.Count > 0)
            {
                return;
            }

            recipes.Add(CookingRecipeDefinition.CreateRuntime("warm_meal", "Warm Meal", "food_ration", 1, 8f, 24f, 16f, 8f));
            recipes.Add(CookingRecipeDefinition.CreateRuntime("fuel_tea", "Fuel Tea", "fuel_canister", 1, 3f, 0f, 28f, 12f));
        }
    }
}
