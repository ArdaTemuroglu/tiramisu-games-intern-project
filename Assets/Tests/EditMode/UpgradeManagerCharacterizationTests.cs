using NUnit.Framework;
using UnityEngine;

namespace ArcadeRacing.Tests
{
    public sealed class UpgradeManagerCharacterizationTests :
        ProgressionCharacterizationTestFixture
    {
        [Test]
        public void UpgradeSession_StartsAtBaseLevelsAndMultipliers()
        {
            UpgradeFixture fixture = CreateUpgradeFixture();

            Assert.That(
                ReadProperty<int>(fixture.SaveManager, "EngineLevel"),
                Is.Zero
            );

            Assert.That(
                ReadProperty<int>(fixture.SaveManager, "TiresLevel"),
                Is.Zero
            );

            Assert.That(
                ReadProperty<int>(fixture.SaveManager, "NitroLevel"),
                Is.Zero
            );

            AssertBaseMultipliers(fixture);
        }

        [Test]
        public void UpgradeManager_PurchasesEngineAndAppliesMultiplier()
        {
            UpgradeFixture fixture = CreateUpgradeFixture();
            int cost = GetUpgradeCost(fixture.UpgradeManager, "Engine");
            InvokePublic(fixture.SaveManager, "AddCash", cost + 1000);

            Assert.That(
                TryPurchase(fixture.UpgradeManager, "Engine"),
                Is.True
            );

            Assert.That(
                GetUpgradeLevel(fixture.UpgradeManager, "Engine"),
                Is.EqualTo(1)
            );

            Assert.That(
                ReadProperty<int>(fixture.SaveManager, "Cash"),
                Is.EqualTo(1000)
            );

            AssertMultiplierForLevel(fixture, "Engine", 1);
        }

        [Test]
        public void UpgradeManager_PurchasesTiresAndAppliesMultiplier()
        {
            UpgradeFixture fixture = CreateUpgradeFixture();
            int cost = GetUpgradeCost(fixture.UpgradeManager, "Tires");
            InvokePublic(fixture.SaveManager, "AddCash", cost + 1000);

            Assert.That(
                TryPurchase(fixture.UpgradeManager, "Tires"),
                Is.True
            );

            Assert.That(
                GetUpgradeLevel(fixture.UpgradeManager, "Tires"),
                Is.EqualTo(1)
            );

            Assert.That(
                ReadProperty<int>(fixture.SaveManager, "Cash"),
                Is.EqualTo(1000)
            );

            AssertMultiplierForLevel(fixture, "Tires", 1);
        }

        [Test]
        public void UpgradeManager_PurchasesNitroAndAppliesBothMultipliers()
        {
            UpgradeFixture fixture = CreateUpgradeFixture();
            int cost = GetUpgradeCost(fixture.UpgradeManager, "Nitro");
            InvokePublic(fixture.SaveManager, "AddCash", cost + 1000);

            Assert.That(
                TryPurchase(fixture.UpgradeManager, "Nitro"),
                Is.True
            );

            Assert.That(
                GetUpgradeLevel(fixture.UpgradeManager, "Nitro"),
                Is.EqualTo(1)
            );

            Assert.That(
                ReadProperty<int>(fixture.SaveManager, "Cash"),
                Is.EqualTo(1000)
            );

            AssertMultiplierForLevel(fixture, "Nitro", 1);
        }

        [Test]
        public void UpgradeManager_ScalesCostFromCurrentLevel()
        {
            UpgradeFixture fixture = CreateUpgradeFixture();
            int baseCost = GetDefinitionInt(
                fixture.UpgradeManager,
                "Engine",
                "baseCost"
            );

            float costMultiplier = GetDefinitionFloat(
                fixture.UpgradeManager,
                "Engine",
                "costMultiplier"
            );

            int levelZeroCost = GetUpgradeCost(
                fixture.UpgradeManager,
                "Engine"
            );

            Assert.That(
                levelZeroCost,
                Is.EqualTo(Mathf.RoundToInt(baseCost))
            );

            InvokePublic(fixture.SaveManager, "AddCash", 100000);
            Assert.That(
                TryPurchase(fixture.UpgradeManager, "Engine"),
                Is.True
            );

            int levelOneCost = GetUpgradeCost(
                fixture.UpgradeManager,
                "Engine"
            );

            Assert.That(
                levelOneCost,
                Is.EqualTo(
                    Mathf.RoundToInt(baseCost * costMultiplier)
                )
            );

            Assert.That(levelOneCost, Is.GreaterThan(levelZeroCost));
        }

        [TestCase("Engine")]
        [TestCase("Tires")]
        [TestCase("Nitro")]
        public void UpgradeManager_DoesNotPurchaseWithoutEnoughCash(
            string typeName
        )
        {
            UpgradeFixture fixture = CreateUpgradeFixture();
            int cost = GetUpgradeCost(fixture.UpgradeManager, typeName);
            Assert.That(cost, Is.GreaterThan(0));
            InvokePublic(fixture.SaveManager, "AddCash", cost - 1);
            int cashBefore = ReadProperty<int>(
                fixture.SaveManager,
                "Cash"
            );

            Assert.That(
                TryPurchase(fixture.UpgradeManager, typeName),
                Is.False
            );

            Assert.That(
                GetUpgradeLevel(fixture.UpgradeManager, typeName),
                Is.Zero
            );

            Assert.That(
                ReadProperty<int>(fixture.SaveManager, "Cash"),
                Is.EqualTo(cashBefore)
            );

            AssertBaseMultipliers(fixture);
        }

        [TestCase("Engine")]
        [TestCase("Tires")]
        [TestCase("Nitro")]
        public void UpgradeManager_DoesNotExceedMaximumLevel(
            string typeName
        )
        {
            UpgradeFixture fixture = CreateUpgradeFixture();
            int maxLevel = GetMaximumLevel(
                fixture.UpgradeManager,
                typeName
            );

            IncrementLevel(
                fixture.SaveManager,
                typeName,
                maxLevel
            );

            InvokePublic(fixture.UpgradeManager, "ApplyUpgrades");
            InvokePublic(fixture.SaveManager, "AddCash", 1000000);
            int cashBefore = ReadProperty<int>(
                fixture.SaveManager,
                "Cash"
            );

            Assert.That(
                TryPurchase(fixture.UpgradeManager, typeName),
                Is.False
            );

            Assert.That(
                GetUpgradeLevel(fixture.UpgradeManager, typeName),
                Is.EqualTo(maxLevel)
            );

            Assert.That(
                ReadProperty<int>(fixture.SaveManager, "Cash"),
                Is.EqualTo(cashBefore)
            );

            AssertMultiplierForLevel(fixture, typeName, maxLevel);
        }

        [Test]
        public void UpgradeSession_PersistsLevelsAcrossReloadAndSerializesBaseLevels()
        {
            string key = CreateTestKey();
            Component firstSaveManager = CreateSaveManager(key);

            InvokePublic(firstSaveManager, "AddCash", 2500);
            InvokePublic(
                firstSaveManager,
                "TrySetBestTimeTrial",
                80f
            );

            InvokePublic(
                firstSaveManager,
                "TrySetBestDriftScore",
                5000
            );

            IncrementLevel(firstSaveManager, "Engine", 2);
            IncrementLevel(firstSaveManager, "Tires", 1);
            IncrementLevel(firstSaveManager, "Nitro", 3);
            InvokePublic(firstSaveManager, "Save");

            string json = PlayerPrefs.GetString(key);
            object persistentData = JsonUtility.FromJson(
                json,
                GetRuntimeType("ArcadeRacing.SaveData")
            );

            Assert.That(
                GetPublicField<int>(persistentData, "engineLevel"),
                Is.Zero
            );

            Assert.That(
                GetPublicField<int>(persistentData, "tiresLevel"),
                Is.Zero
            );

            Assert.That(
                GetPublicField<int>(persistentData, "nitroLevel"),
                Is.Zero
            );

            Component secondSaveManager = CreateSaveManager(key);

            Assert.That(
                ReadProperty<int>(secondSaveManager, "EngineLevel"),
                Is.EqualTo(2)
            );

            Assert.That(
                ReadProperty<int>(secondSaveManager, "TiresLevel"),
                Is.EqualTo(1)
            );

            Assert.That(
                ReadProperty<int>(secondSaveManager, "NitroLevel"),
                Is.EqualTo(3)
            );

            Assert.That(
                ReadProperty<int>(secondSaveManager, "Cash"),
                Is.EqualTo(2500)
            );

            UpgradeFixture reboundVehicle = CreateUpgradeFixture(
                secondSaveManager
            );

            AssertMultiplierForLevel(reboundVehicle, "Engine", 2);
            AssertMultiplierForLevel(reboundVehicle, "Tires", 1);
            AssertMultiplierForLevel(reboundVehicle, "Nitro", 3);
        }

        [Test]
        public void UpgradeSession_NewApplicationResetsLevelsAndMultipliersOnly()
        {
            string key = CreateTestKey();
            Component firstSaveManager = CreateSaveManager(key);

            InvokePublic(firstSaveManager, "AddCash", 2500);
            InvokePublic(
                firstSaveManager,
                "TrySetBestTimeTrial",
                80f
            );

            InvokePublic(
                firstSaveManager,
                "TrySetBestDriftScore",
                5000
            );

            IncrementLevel(firstSaveManager, "Engine", 2);
            IncrementLevel(firstSaveManager, "Tires", 1);
            IncrementLevel(firstSaveManager, "Nitro", 3);
            InvokePublic(firstSaveManager, "Save");

            SimulateNewApplicationSession();
            Component secondSaveManager = CreateSaveManager(key);

            Assert.That(
                ReadProperty<int>(secondSaveManager, "EngineLevel"),
                Is.Zero
            );

            Assert.That(
                ReadProperty<int>(secondSaveManager, "TiresLevel"),
                Is.Zero
            );

            Assert.That(
                ReadProperty<int>(secondSaveManager, "NitroLevel"),
                Is.Zero
            );

            Assert.That(
                ReadProperty<int>(secondSaveManager, "Cash"),
                Is.EqualTo(2500)
            );

            Assert.That(
                ReadProperty<float>(
                    secondSaveManager,
                    "BestTimeTrialRaceTime"
                ),
                Is.EqualTo(80f).Within(0.0001f)
            );

            Assert.That(
                ReadProperty<int>(secondSaveManager, "BestDriftScore"),
                Is.EqualTo(5000)
            );

            UpgradeFixture newSessionVehicle = CreateUpgradeFixture(
                secondSaveManager
            );

            AssertBaseMultipliers(newSessionVehicle);
        }

        private static T GetPublicField<T>(
            object target,
            string fieldName
        )
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(
                fieldName
            );

            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }
    }
}
