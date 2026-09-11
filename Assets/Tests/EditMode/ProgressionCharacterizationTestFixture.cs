using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ArcadeRacing.Tests
{
    public abstract class ProgressionCharacterizationTestFixture
    {
        protected const BindingFlags InstanceMembers =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private const BindingFlags StaticMembers =
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private readonly List<GameObject> createdObjects =
            new List<GameObject>();

        private readonly List<string> playerPrefsKeys =
            new List<string>();

        private SessionState originalSessionState;

        [SetUp]
        public void SetUpProgressionFixture()
        {
            originalSessionState = CaptureSessionState();
            SimulateNewApplicationSession();
        }

        [TearDown]
        public void TearDownProgressionFixture()
        {
            for (int i = 0; i < playerPrefsKeys.Count; i++)
            {
                PlayerPrefs.DeleteKey(playerPrefsKeys[i]);
            }

            PlayerPrefs.Save();

            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
            playerPrefsKeys.Clear();
            RestoreSessionState(originalSessionState);
        }

        protected string CreateTestKey()
        {
            string key =
                "ArcadeRacing_R1_Progression_" +
                Guid.NewGuid().ToString("N");

            playerPrefsKeys.Add(key);
            return key;
        }

        protected Component CreateSaveManager(
            string key,
            bool load = true
        )
        {
            GameObject saveObject = CreateTemporaryObject(
                "R1_Progression_SaveManager"
            );

            Component saveManager = saveObject.AddComponent(
                GetRuntimeType("ArcadeRacing.SaveManager")
            );

            SetPrivateField(saveManager, "saveKey", key);
            SetPrivateField(saveManager, "loadOnAwake", false);
            SetPrivateField(
                saveManager,
                "saveOnApplicationPause",
                false
            );

            if (load)
            {
                InvokePublic(saveManager, "Load");
            }

            return saveManager;
        }

        protected UpgradeFixture CreateUpgradeFixture(
            Component saveManager = null
        )
        {
            if (saveManager == null)
            {
                saveManager = CreateSaveManager(CreateTestKey());
            }

            GameObject vehicleObject = CreateTemporaryObject(
                "R1_Progression_Vehicle"
            );

            Component nitroSystem = vehicleObject.AddComponent(
                GetRuntimeType("ArcadeRacing.NitroSystem")
            );

            Component carController = vehicleObject.AddComponent(
                GetRuntimeType("ArcadeRacing.CarController")
            );

            SetPrivateField(
                carController,
                "nitroSystem",
                nitroSystem
            );

            GameObject upgradeObject = CreateTemporaryObject(
                "R1_Progression_UpgradeManager"
            );

            Component upgradeManager = upgradeObject.AddComponent(
                GetRuntimeType("ArcadeRacing.UpgradeManager")
            );

            SetPrivateField(
                upgradeManager,
                "saveManager",
                saveManager
            );

            SetPrivateField(
                upgradeManager,
                "carController",
                carController
            );

            SetPrivateField(
                upgradeManager,
                "nitroSystem",
                nitroSystem
            );

            InvokePublic(upgradeManager, "ApplyUpgrades");

            return new UpgradeFixture
            {
                SaveManager = saveManager,
                UpgradeManager = upgradeManager,
                CarController = carController,
                NitroSystem = nitroSystem
            };
        }

        protected void SimulateNewApplicationSession()
        {
            Type saveManagerType = GetRuntimeType(
                "ArcadeRacing.SaveManager"
            );

            MethodInfo resetSession = saveManagerType.GetMethod(
                "BeginNewApplicationSession",
                StaticMembers
            );

            Assert.That(resetSession, Is.Not.Null);
            resetSession.Invoke(null, null);
        }

        protected object GetUpgradeType(string typeName)
        {
            return Enum.Parse(
                GetRuntimeType("ArcadeRacing.UpgradeType"),
                typeName
            );
        }

        protected int GetUpgradeLevel(
            Component upgradeManager,
            string typeName
        )
        {
            return (int)InvokePublic(
                upgradeManager,
                "GetLevel",
                GetUpgradeType(typeName)
            );
        }

        protected int GetUpgradeCost(
            Component upgradeManager,
            string typeName
        )
        {
            return (int)InvokePublic(
                upgradeManager,
                "GetCost",
                GetUpgradeType(typeName)
            );
        }

        protected int GetMaximumLevel(
            Component upgradeManager,
            string typeName
        )
        {
            return (int)InvokePublic(
                upgradeManager,
                "GetMaxLevel",
                GetUpgradeType(typeName)
            );
        }

        protected bool TryPurchase(
            Component upgradeManager,
            string typeName
        )
        {
            return (bool)InvokePublic(
                upgradeManager,
                "TryUpgrade",
                GetUpgradeType(typeName)
            );
        }

        protected void IncrementLevel(
            Component saveManager,
            string typeName,
            int amount = 1
        )
        {
            for (int i = 0; i < amount; i++)
            {
                InvokePublic(
                    saveManager,
                    "IncrementUpgradeLevel",
                    GetUpgradeType(typeName)
                );
            }
        }

        protected float GetDefinitionFloat(
            Component upgradeManager,
            string typeName,
            string fieldName
        )
        {
            object definition = GetPrivateField<object>(
                upgradeManager,
                typeName.ToLowerInvariant()
            );

            FieldInfo field = definition.GetType().GetField(fieldName);
            Assert.That(field, Is.Not.Null, fieldName);
            return (float)field.GetValue(definition);
        }

        protected int GetDefinitionInt(
            Component upgradeManager,
            string typeName,
            string fieldName
        )
        {
            object definition = GetPrivateField<object>(
                upgradeManager,
                typeName.ToLowerInvariant()
            );

            FieldInfo field = definition.GetType().GetField(fieldName);
            Assert.That(field, Is.Not.Null, fieldName);
            return (int)field.GetValue(definition);
        }

        protected void AssertBaseMultipliers(UpgradeFixture fixture)
        {
            Assert.That(
                GetPrivateField<float>(
                    fixture.CarController,
                    "engineMultiplier"
                ),
                Is.EqualTo(1f).Within(0.0001f)
            );

            Assert.That(
                GetPrivateField<float>(
                    fixture.CarController,
                    "gripMultiplier"
                ),
                Is.EqualTo(1f).Within(0.0001f)
            );

            Assert.That(
                GetPrivateField<float>(
                    fixture.NitroSystem,
                    "capacityMultiplier"
                ),
                Is.EqualTo(1f).Within(0.0001f)
            );

            Assert.That(
                ReadProperty<float>(
                    fixture.NitroSystem,
                    "PowerMultiplier"
                ),
                Is.EqualTo(1f).Within(0.0001f)
            );
        }

        protected void AssertMultiplierForLevel(
            UpgradeFixture fixture,
            string typeName,
            int level
        )
        {
            float primaryEffect = GetDefinitionFloat(
                fixture.UpgradeManager,
                typeName,
                "primaryEffectPerLevel"
            );

            float expectedPrimary = 1f + level * primaryEffect;

            if (typeName == "Engine")
            {
                Assert.That(
                    GetPrivateField<float>(
                        fixture.CarController,
                        "engineMultiplier"
                    ),
                    Is.EqualTo(expectedPrimary).Within(0.0001f)
                );

                return;
            }

            if (typeName == "Tires")
            {
                Assert.That(
                    GetPrivateField<float>(
                        fixture.CarController,
                        "gripMultiplier"
                    ),
                    Is.EqualTo(expectedPrimary).Within(0.0001f)
                );

                return;
            }

            float secondaryEffect = GetDefinitionFloat(
                fixture.UpgradeManager,
                typeName,
                "secondaryEffectPerLevel"
            );

            Assert.That(
                GetPrivateField<float>(
                    fixture.NitroSystem,
                    "capacityMultiplier"
                ),
                Is.EqualTo(expectedPrimary).Within(0.0001f)
            );

            Assert.That(
                ReadProperty<float>(
                    fixture.NitroSystem,
                    "PowerMultiplier"
                ),
                Is.EqualTo(1f + level * secondaryEffect)
                    .Within(0.0001f)
            );
        }

        protected static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName + " was not found.");
            return type;
        }

        protected static T ReadProperty<T>(
            object target,
            string propertyName
        )
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                InstanceMembers
            );

            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(target);
        }

        protected static T GetPrivateField<T>(
            object target,
            string fieldName
        )
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                InstanceMembers
            );

            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        protected static object InvokePublic(
            object target,
            string methodName,
            params object[] arguments
        )
        {
            MethodInfo method = FindMethod(
                target,
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                arguments.Length
            );

            return method.Invoke(target, arguments);
        }

        protected static void SetPrivateField(
            object target,
            string fieldName,
            object value
        )
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                InstanceMembers
            );

            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static MethodInfo FindMethod(
            object target,
            string methodName,
            BindingFlags flags,
            int parameterCount
        )
        {
            MethodInfo[] methods = target.GetType().GetMethods(flags);

            for (int i = 0; i < methods.Length; i++)
            {
                if (
                    methods[i].Name == methodName &&
                    methods[i].GetParameters().Length == parameterCount
                )
                {
                    return methods[i];
                }
            }

            Assert.Fail(
                target.GetType().FullName + "." + methodName +
                " with " + parameterCount + " parameters was not found."
            );

            return null;
        }

        private GameObject CreateTemporaryObject(string name)
        {
            GameObject created = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            createdObjects.Add(created);
            return created;
        }

        private static SessionState CaptureSessionState()
        {
            Type saveManagerType = GetRuntimeType(
                "ArcadeRacing.SaveManager"
            );

            return new SessionState
            {
                Initialized = ReadStaticField<bool>(
                    saveManagerType,
                    "sessionUpgradeLevelsInitialized"
                ),
                EngineLevel = ReadStaticField<int>(
                    saveManagerType,
                    "sessionEngineLevel"
                ),
                TiresLevel = ReadStaticField<int>(
                    saveManagerType,
                    "sessionTiresLevel"
                ),
                NitroLevel = ReadStaticField<int>(
                    saveManagerType,
                    "sessionNitroLevel"
                )
            };
        }

        private static void RestoreSessionState(SessionState state)
        {
            Type saveManagerType = GetRuntimeType(
                "ArcadeRacing.SaveManager"
            );

            SetStaticField(
                saveManagerType,
                "sessionUpgradeLevelsInitialized",
                state.Initialized
            );

            SetStaticField(
                saveManagerType,
                "sessionEngineLevel",
                state.EngineLevel
            );

            SetStaticField(
                saveManagerType,
                "sessionTiresLevel",
                state.TiresLevel
            );

            SetStaticField(
                saveManagerType,
                "sessionNitroLevel",
                state.NitroLevel
            );
        }

        private static T ReadStaticField<T>(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, StaticMembers);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(null);
        }

        private static void SetStaticField(
            Type type,
            string fieldName,
            object value
        )
        {
            FieldInfo field = type.GetField(fieldName, StaticMembers);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(null, value);
        }

        protected sealed class UpgradeFixture
        {
            public Component SaveManager;
            public Component UpgradeManager;
            public Component CarController;
            public Component NitroSystem;
        }

        private struct SessionState
        {
            public bool Initialized;
            public int EngineLevel;
            public int TiresLevel;
            public int NitroLevel;
        }
    }
}
