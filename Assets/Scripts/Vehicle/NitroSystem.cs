using System;
using UnityEngine;

namespace ArcadeRacing
{
    public sealed class NitroSystem : MonoBehaviour
    {
        [SerializeField] private bool startFull = true;

        private float baseCapacity;
        private float drainPerSecond;
        private float rechargePerSecond;
        private float capacityMultiplier = 1f;
        private float powerMultiplier = 1f;
        private bool initialized;

        public float CurrentAmount { get; private set; }
        public float Capacity => baseCapacity * capacityMultiplier;
        public float NormalizedAmount => Capacity <= 0f ? 0f : Mathf.Clamp01(CurrentAmount / Capacity);
        public float PowerMultiplier => powerMultiplier;
        public bool IsActive { get; private set; }

        public event Action<float> AmountChanged;

        public void Initialize(CarConfig config)
        {
            baseCapacity = Mathf.Max(0.1f, config.nitroCapacity);
            drainPerSecond = Mathf.Max(0.01f, config.nitroDrainPerSecond);
            rechargePerSecond = Mathf.Max(0f, config.nitroRechargePerSecond);
            initialized = true;
            CurrentAmount = startFull ? Capacity : 0f;
            AmountChanged?.Invoke(NormalizedAmount);
        }

        public bool Process(bool requested, bool canUse, float deltaTime)
        {
            if (!initialized)
            {
                IsActive = false;
                return false;
            }

            float previous = CurrentAmount;
            IsActive = requested && canUse && CurrentAmount > 0f;

            if (IsActive)
            {
                CurrentAmount = Mathf.Max(0f, CurrentAmount - drainPerSecond * deltaTime);
            }
            else if (rechargePerSecond > 0f)
            {
                CurrentAmount = Mathf.Min(Capacity, CurrentAmount + rechargePerSecond * deltaTime);
            }

            if (!Mathf.Approximately(previous, CurrentAmount))
            {
                AmountChanged?.Invoke(NormalizedAmount);
            }

            return IsActive;
        }

        public void ApplyUpgradeMultipliers(float newCapacityMultiplier, float newPowerMultiplier)
        {
            float previousNormalized = NormalizedAmount;
            capacityMultiplier = Mathf.Max(0.1f, newCapacityMultiplier);
            powerMultiplier = Mathf.Max(0.1f, newPowerMultiplier);
            CurrentAmount = Mathf.Clamp(previousNormalized * Capacity, 0f, Capacity);
            AmountChanged?.Invoke(NormalizedAmount);
        }

        public void Refill()
        {
            CurrentAmount = Capacity;
            AmountChanged?.Invoke(NormalizedAmount);
        }
    }
}
