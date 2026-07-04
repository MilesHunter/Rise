using System;
using UnityEngine;

namespace Rise
{
    public sealed class PlayerVitals : MonoBehaviour
    {
        private const float MaxValue = 100f;

        [SerializeField] private float stamina = MaxValue;
        [SerializeField] private float hunger = MaxValue;
        [SerializeField] private float warmth = MaxValue;
        [SerializeField] private float sanity = MaxValue;
        [SerializeField] private float health = MaxValue;

        public event Action Changed;

        public float Stamina => stamina;
        public float MaxStamina => MaxValue;
        public float Hunger => hunger;
        public float Warmth => warmth;
        public float Sanity => sanity;
        public float Health => health;
        public bool LowStamina => stamina <= 20f;

        public bool TrySpendStamina(float amount)
        {
            if (stamina < amount)
            {
                return false;
            }

            stamina = Mathf.Max(0f, stamina - amount);
            Changed?.Invoke();
            return true;
        }

        public void RestoreStamina(float amount)
        {
            stamina = Mathf.Clamp(stamina + amount, 0f, MaxValue);
            Changed?.Invoke();
        }

        public void RestoreAllForLongRest()
        {
            stamina = MaxValue;
            hunger = Mathf.Min(MaxValue, hunger + 30f);
            warmth = Mathf.Min(MaxValue, warmth + 10f);
            sanity = Mathf.Min(MaxValue, sanity + 20f);
            health = Mathf.Min(MaxValue, health + 15f);
            Changed?.Invoke();
        }

        public void RecoverForShortRest()
        {
            stamina = Mathf.Min(MaxValue, stamina + 35f);
            warmth = Mathf.Min(MaxValue, warmth + 2f);
            sanity = Mathf.Min(MaxValue, sanity + 4f);
            Changed?.Invoke();
        }

        public void RestoreByRecipe(float healthAmount, float hungerAmount, float warmthAmount, float sanityAmount)
        {
            health = Mathf.Min(MaxValue, health + Mathf.Max(0f, healthAmount));
            hunger = Mathf.Min(MaxValue, hunger + Mathf.Max(0f, hungerAmount));
            warmth = Mathf.Min(MaxValue, warmth + Mathf.Max(0f, warmthAmount));
            sanity = Mathf.Min(MaxValue, sanity + Mathf.Max(0f, sanityAmount));
            Changed?.Invoke();
        }

        public void ResetToCheckpoint()
        {
            stamina = Mathf.Max(stamina, 60f);
            Changed?.Invoke();
        }
    }
}
