using System;
using UnityEngine;

namespace ArcadeRacing
{
    public enum UpgradeType
    {
        Engine,
        Tires,
        Nitro
    }

    [Serializable]
    public sealed class UpgradeDefinition
    {
        [Min(0)] public int baseCost = 500;
        [Min(1f)] public float costMultiplier = 1.65f;
        [Min(1)] public int maxLevel = 5;
        [Min(0f)] public float primaryEffectPerLevel = 0.08f;
        [Min(0f)] public float secondaryEffectPerLevel = 0.04f;
    }

    public sealed class UpgradeManager : MonoBehaviour
    {
        [Header("Runtime")]
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private CarController carController;
        [SerializeField] private NitroSystem nitroSystem;

        [Header("Definitions")]
        [SerializeField] private UpgradeDefinition engine = new UpgradeDefinition();
        [SerializeField] private UpgradeDefinition tires = new UpgradeDefinition();
        [SerializeField] private UpgradeDefinition nitro = new UpgradeDefinition();

        private bool hasStarted;

        public event Action UpgradesChanged;

        private void Start()
        {
            if (saveManager == null)
            {
                throw new MissingReferenceException(
                    "UpgradeManager requires a SaveManager reference."
                );
            }

            if (!saveManager.IsLoaded)
            {
                saveManager.Load();
            }

            hasStarted = true;
            ApplyUpgrades();
        }

        public void BindVehicle(
            CarController newCarController,
            NitroSystem newNitroSystem
        )
        {
            carController = newCarController;
            nitroSystem = newNitroSystem;

            if (hasStarted)
            {
                ApplyUpgrades();
            }
        }

        public int GetLevel(UpgradeType type)
        {
            return saveManager != null
                ? saveManager.GetUpgradeLevel(type)
                : 0;
        }

        public int GetMaxLevel(UpgradeType type)
        {
            return GetDefinition(type).maxLevel;
        }

        public int GetCost(UpgradeType type)
        {
            UpgradeDefinition definition = GetDefinition(type);
            int level = GetLevel(type);

            return Mathf.RoundToInt(
                definition.baseCost *
                Mathf.Pow(definition.costMultiplier, level)
            );
        }

        public bool TryUpgradeEngine()
        {
            return TryUpgrade(UpgradeType.Engine);
        }

        public bool TryUpgradeTires()
        {
            return TryUpgrade(UpgradeType.Tires);
        }

        public bool TryUpgradeNitro()
        {
            return TryUpgrade(UpgradeType.Nitro);
        }

        public bool TryUpgrade(UpgradeType type)
        {
            if (saveManager == null)
            {
                return false;
            }

            int level = GetLevel(type);
            UpgradeDefinition definition = GetDefinition(type);

            if (level >= definition.maxLevel)
            {
                return false;
            }

            int cost = GetCost(type);
            if (!saveManager.SpendCash(cost))
            {
                return false;
            }

            saveManager.IncrementUpgradeLevel(type);
            ApplyUpgrades();
            saveManager.Save();
            UpgradesChanged?.Invoke();
            return true;
        }

        public void ApplyUpgrades()
        {
            if (saveManager == null)
            {
                return;
            }

            int engineLevel = saveManager.GetUpgradeLevel(UpgradeType.Engine);
            int tiresLevel = saveManager.GetUpgradeLevel(UpgradeType.Tires);
            int nitroLevel = saveManager.GetUpgradeLevel(UpgradeType.Nitro);

            float engineMultiplier =
                1f + engineLevel * engine.primaryEffectPerLevel;

            float gripMultiplier =
                1f + tiresLevel * tires.primaryEffectPerLevel;

            float nitroCapacityMultiplier =
                1f + nitroLevel * nitro.primaryEffectPerLevel;

            float nitroPowerMultiplier =
                1f + nitroLevel * nitro.secondaryEffectPerLevel;

            if (carController != null)
            {
                carController.ApplyUpgradeMultipliers(
                    engineMultiplier,
                    gripMultiplier
                );
            }

            if (nitroSystem != null)
            {
                nitroSystem.ApplyUpgradeMultipliers(
                    nitroCapacityMultiplier,
                    nitroPowerMultiplier
                );
            }

            UpgradesChanged?.Invoke();
        }

        private UpgradeDefinition GetDefinition(UpgradeType type)
        {
            if (type == UpgradeType.Engine)
            {
                return engine;
            }

            if (type == UpgradeType.Tires)
            {
                return tires;
            }

            return nitro;
        }
    }
}
