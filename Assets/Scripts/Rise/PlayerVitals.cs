using System;
using UnityEngine;

namespace Rise
{
    public sealed class PlayerVitals : MonoBehaviour
    {
        private const float MaxValue = 100f;
        private const float WarningThreshold = 66f;
        private const float LowThreshold = 33f;
        private const float CriticalThreshold = 0.01f;

        [SerializeField] private float stamina = MaxValue;
        [SerializeField] private float hunger = MaxValue;
        [SerializeField] private float warmth = MaxValue;
        [SerializeField] private float sanity = MaxValue;
        [SerializeField] private float health = MaxValue;
        [SerializeField] private Vector2 passiveDecayIntervalRange = new Vector2(8f, 14f);
        [SerializeField] private Vector2 passiveHungerDecayRange = new Vector2(0.4f, 0.9f);
        [SerializeField] private Vector2 passiveWarmthDecayRange = new Vector2(0.15f, 0.45f);
        [SerializeField] private Vector2 passiveSanityDecayRange = new Vector2(0.08f, 0.28f);

        public event Action Changed;
        private RestSessionController restSession;
        private float passiveDecayTimer;
        private float nextPassiveDecayInterval;

        public float Stamina => stamina;
        public float MaxStamina => MaxValue;
        public float Hunger => hunger;
        public float Warmth => warmth;
        public float Sanity => sanity;
        public float Health => health;
        public bool LowStamina => stamina <= 20f;
        public bool LowHealth => health <= LowThreshold;
        public bool LowHunger => hunger <= LowThreshold;
        public bool LowWarmth => warmth <= LowThreshold;
        public bool LowSanity => sanity <= LowThreshold;
        public bool WarningHealth => health <= WarningThreshold;
        public bool WarningHunger => hunger <= WarningThreshold;
        public bool WarningWarmth => warmth <= WarningThreshold;
        public bool WarningSanity => sanity <= WarningThreshold;

        public float GrabCostMultiplier => 1f;
        public float HoldDrainMultiplier => 1f;
        public float StaminaRecoveryMultiplier => 1f;
        public float KickForceMultiplier => 1f;
        public float ResourceSearchIntervalMultiplier => 1f;

        private void Awake()
        {
            restSession = GetComponent<RestSessionController>();
            ScheduleNextPassiveDecay();
        }

        private void Update()
        {
            UpdatePassiveDecay();
        }

        public bool TrySpendStamina(float amount)
        {
            amount = Mathf.Max(0f, amount);
            if (stamina < amount)
            {
                return false;
            }

            stamina = Mathf.Max(0f, stamina - amount);
            Changed?.Invoke();
            return true;
        }

        public bool TrySpendAdjustedStamina(float baseAmount)
        {
            return TrySpendStamina(baseAmount * GrabCostMultiplier);
        }

        public void RestoreStamina(float amount)
        {
            ModifyStamina(amount * StaminaRecoveryMultiplier);
        }

        public void ModifyStamina(float delta)
        {
            stamina = ClampVital(stamina + delta);
            Changed?.Invoke();
        }

        public void ModifyHealth(float delta)
        {
            health = ClampVital(health + delta);
            Changed?.Invoke();
        }

        public void ModifyHunger(float delta)
        {
            hunger = ClampVital(hunger + delta);
            Changed?.Invoke();
        }

        public void ModifyWarmth(float delta)
        {
            warmth = ClampVital(warmth + delta);
            Changed?.Invoke();
        }

        public void ModifySanity(float delta)
        {
            sanity = ClampVital(sanity + delta);
            Changed?.Invoke();
        }

        public void RestoreAllForLongRest()
        {
            stamina = MaxValue;
            health = ClampVital(health + 30f);
            hunger = ClampVital(hunger + 35f);
            warmth = ClampVital(warmth + 25f);
            sanity = ClampVital(sanity + 30f);
            Changed?.Invoke();
        }

        public void RecoverForShortRest()
        {
            stamina = ClampVital(stamina + 35f);
            health = ClampVital(health + 8f);
            hunger = ClampVital(hunger + 12f);
            warmth = ClampVital(warmth + 8f);
            sanity = ClampVital(sanity + 10f);
            Changed?.Invoke();
        }

        public void RestoreByRecipe(float healthAmount, float hungerAmount, float warmthAmount, float sanityAmount)
        {
            health = ClampVital(health + Mathf.Max(0f, healthAmount));
            hunger = ClampVital(hunger + Mathf.Max(0f, hungerAmount));
            warmth = ClampVital(warmth + Mathf.Max(0f, warmthAmount));
            sanity = ClampVital(sanity + Mathf.Max(0f, sanityAmount));
            Changed?.Invoke();
        }

        public void ApplyItemEffect(InventoryUseEffect effect, float amount)
        {
            switch (effect)
            {
                case InventoryUseEffect.RestoreHealth:
                    ModifyHealth(Mathf.Max(0f, amount));
                    break;
                case InventoryUseEffect.RestoreWarmth:
                    ModifyWarmth(Mathf.Max(0f, amount));
                    break;
                case InventoryUseEffect.RestoreSanity:
                    ModifySanity(Mathf.Max(0f, amount));
                    break;
                case InventoryUseEffect.RestoreHunger:
                    ModifyHunger(Mathf.Max(0f, amount));
                    break;
            }
        }

        public void ApplyClimbExposureTick(bool isHardClimbing, bool highAltitude)
        {
            ModifyHunger(-1f);
            if (highAltitude)
            {
                ModifyWarmth(-1f);
            }

            if (isHardClimbing && LowStamina)
            {
                ModifySanity(-2f);
            }

        }

        public void ResetToCheckpoint()
        {
            stamina = Mathf.Max(stamina, 60f);
            Changed?.Invoke();
        }

        private void UpdatePassiveDecay()
        {
            if (restSession != null && restSession.IsResting)
            {
                ScheduleNextPassiveDecay();
                return;
            }

            passiveDecayTimer += Time.deltaTime;
            if (passiveDecayTimer < nextPassiveDecayInterval)
            {
                return;
            }

            ApplyPassiveDecayTick();
        }

        private void ScheduleNextPassiveDecay()
        {
            passiveDecayTimer = 0f;
            float min = Mathf.Max(0.1f, passiveDecayIntervalRange.x);
            float max = Mathf.Max(min, passiveDecayIntervalRange.y);
            nextPassiveDecayInterval = UnityEngine.Random.Range(min, max);
        }

        private void ApplyPassiveDecayTick()
        {
            passiveDecayTimer = 0f;
            hunger = ClampVital(hunger - UnityEngine.Random.Range(passiveHungerDecayRange.x, passiveHungerDecayRange.y));
            warmth = ClampVital(warmth - UnityEngine.Random.Range(passiveWarmthDecayRange.x, passiveWarmthDecayRange.y));
            sanity = ClampVital(sanity - UnityEngine.Random.Range(passiveSanityDecayRange.x, passiveSanityDecayRange.y));
            ScheduleNextPassiveDecay();
            Changed?.Invoke();
        }

        private static float ClampVital(float value)
        {
            return Mathf.Clamp(value, 0f, MaxValue);
        }
    }
}
