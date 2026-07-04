using System;
using UnityEngine;

namespace Rise
{
    public sealed class PlayerVitals : MonoBehaviour
    {
        private const float MaxValue = 100f;

        [SerializeField] private float stamina = MaxValue;
        [SerializeField] private float hunger = MaxValue;
        [SerializeField] private float cold = 15f;
        [SerializeField] private float sanity = MaxValue;
        [SerializeField] private float health = MaxValue;

        public event Action Changed;

        public float Stamina => stamina;
        public float MaxStamina => MaxValue;
        public float Hunger => hunger;
        public float Cold => cold;
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
            cold = Mathf.Max(0f, cold - 10f);
            sanity = Mathf.Min(MaxValue, sanity + 20f);
            health = Mathf.Min(MaxValue, health + 15f);
            Changed?.Invoke();
        }

        public void RecoverForShortRest()
        {
            stamina = Mathf.Min(MaxValue, stamina + 35f);
            cold = Mathf.Max(0f, cold - 1.5f);
            sanity = Mathf.Min(MaxValue, sanity + 4f);
            Changed?.Invoke();
        }

        public void ResetToCheckpoint()
        {
            stamina = Mathf.Max(stamina, 60f);
            Changed?.Invoke();
        }
    }
}
