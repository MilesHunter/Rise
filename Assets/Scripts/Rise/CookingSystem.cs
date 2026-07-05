using System.Collections.Generic;
using System;
using UnityEngine;

namespace Rise
{
    public sealed class CookingSystem : MonoBehaviour
    {
        private readonly List<CookingRecipeDefinition> recipes = new List<CookingRecipeDefinition>();
        private PlayerInventory inventory;
        private PlayerVitals vitals;
        private RestSessionController restSession;

        public IReadOnlyList<CookingRecipeDefinition> Recipes => recipes;
        public string LastMessage { get; private set; }
        public event Action<bool, string, Vector3> CookingFinished;

        private void Awake()
        {
            EnsureInitialized();
        }

        public bool TryCook(int index, bool requireLongRest = false)
        {
            EnsureInitialized();
            EnsureRecipes();
            if (requireLongRest && (restSession == null || !restSession.AllowsCooking))
            {
                LastMessage = "Cooking is only available at long rest";
                CookingFinished?.Invoke(false, LastMessage, transform.position);
                return false;
            }

            if (index < 0 || index >= recipes.Count)
            {
                LastMessage = "No recipe selected";
                CookingFinished?.Invoke(false, LastMessage, transform.position);
                return false;
            }

            if (inventory == null || vitals == null)
            {
                LastMessage = "Cooking requires player inventory and vitals";
                CookingFinished?.Invoke(false, LastMessage, transform.position);
                return false;
            }

            CookingRecipeDefinition recipe = recipes[index];
            if (!inventory.TryConsumeSmall(recipe.IngredientItemId, recipe.IngredientQuantity) &&
                !inventory.LargePack.TryRemoveItems(recipe.IngredientItemId, recipe.IngredientQuantity))
            {
                LastMessage = $"Missing {recipe.IngredientItemId}";
                CookingFinished?.Invoke(false, LastMessage, transform.position);
                return false;
            }

            vitals.RestoreByRecipe(recipe.HealthRestore, recipe.HungerRestore, recipe.WarmthRestore, recipe.SanityRestore);
            inventory.NotifyChanged();
            LastMessage = $"Cooked {recipe.DisplayName}";
            CookingFinished?.Invoke(true, LastMessage, transform.position);
            return true;
        }

        private void EnsureInitialized()
        {
            if (inventory == null)
            {
                inventory = GetComponent<PlayerInventory>();
            }

            if (vitals == null)
            {
                vitals = GetComponent<PlayerVitals>();
            }

            if (restSession == null)
            {
                restSession = GetComponent<RestSessionController>();
            }

            if (inventory != null)
            {
                inventory.EnsureInitialized();
            }

            EnsureRecipes();
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
