using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcadeRacing.Tests
{
    public abstract class RacingCharacterizationTestFixture
    {
        protected const BindingFlags InstanceMembers =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private readonly List<GameObject> createdObjects =
            new List<GameObject>();

        private readonly List<string> playerPrefsKeys =
            new List<string>();

        private bool previousIgnoreFailingMessages;

        protected int CheckpointEventCount { get; private set; }
        protected int LastCheckpointProgress { get; private set; }
        protected int LastCheckpointTotal { get; private set; }
        protected int LapCompletedEventCount { get; private set; }
        protected int LastCompletedLapNumber { get; private set; }

        [SetUp]
        public void SetUpCharacterizationFixture()
        {
            previousIgnoreFailingMessages =
                LogAssert.ignoreFailingMessages;

            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            AudioListener.pause = false;

            CheckpointEventCount = 0;
            LastCheckpointProgress = 0;
            LastCheckpointTotal = 0;
            LapCompletedEventCount = 0;
            LastCompletedLapNumber = 0;
        }

        [TearDown]
        public void TearDownCharacterizationFixture()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;

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
            LogAssert.ignoreFailingMessages =
                previousIgnoreFailingMessages;
        }

        protected RacingFixture CreateRacingFixture(
            string modeName = "TimeTrial"
        )
        {
            LogAssert.ignoreFailingMessages = true;

            Type raceManagerType = GetRuntimeType(
                "ArcadeRacing.RaceManager"
            );

            Type checkpointManagerType = GetRuntimeType(
                "ArcadeRacing.CheckpointManager"
            );

            Type checkpointType = GetRuntimeType(
                "ArcadeRacing.Checkpoint"
            );

            Type lapTimerType = GetRuntimeType(
                "ArcadeRacing.LapTimer"
            );

            Type saveManagerType = GetRuntimeType(
                "ArcadeRacing.SaveManager"
            );

            Type uiManagerType = GetRuntimeType(
                "ArcadeRacing.UIManager"
            );

            Type driftScoreManagerType = GetRuntimeType(
                "ArcadeRacing.DriftScoreManager"
            );

            Type ghostReplaySystemType = GetRuntimeType(
                "ArcadeRacing.GhostReplaySystem"
            );

            Type carControllerType = GetRuntimeType(
                "ArcadeRacing.CarController"
            );

            GameObject systemsObject = CreateTemporaryObject(
                "R1_RaceSystems"
            );

            Component raceManager = systemsObject.AddComponent(
                raceManagerType
            );

            Component lapTimer = systemsObject.AddComponent(
                lapTimerType
            );

            Component saveManager = systemsObject.AddComponent(
                saveManagerType
            );

            Component uiManager = systemsObject.AddComponent(
                uiManagerType
            );

            Component driftScoreManager = systemsObject.AddComponent(
                driftScoreManagerType
            );

            Component ghostReplaySystem = systemsObject.AddComponent(
                ghostReplaySystemType
            );

            GameObject carObject = CreateTemporaryObject("R1_PlayerCar");
            Component playerCar = carObject.AddComponent(carControllerType);

            Component startFinish = CreateCheckpoint(
                "R1_StartFinish",
                checkpointType
            );

            Component[] lapCheckpoints =
            {
                CreateCheckpoint("R1_Checkpoint_1", checkpointType),
                CreateCheckpoint("R1_Checkpoint_2", checkpointType),
                CreateCheckpoint("R1_Checkpoint_3", checkpointType),
                CreateCheckpoint("R1_Checkpoint_4", checkpointType)
            };

            GameObject checkpointManagerObject = CreateTemporaryObject(
                "R1_CheckpointManager"
            );

            Component checkpointManager =
                checkpointManagerObject.AddComponent(checkpointManagerType);

            SetPrivateField(
                checkpointManager,
                "startFinishCheckpoint",
                startFinish
            );

            for (int i = 0; i < lapCheckpoints.Length; i++)
            {
                SetPrivateField(
                    checkpointManager,
                    "checkpoint" + (i + 1),
                    lapCheckpoints[i]
                );
            }

            SetPrivateField(checkpointManager, "raceManager", raceManager);
            SetPrivateField(checkpointManager, "lapTimer", lapTimer);

            SetPrivateField(raceManager, "playerCar", playerCar);
            SetPrivateField(
                raceManager,
                "checkpointManager",
                checkpointManager
            );

            SetPrivateField(raceManager, "lapTimer", lapTimer);
            SetPrivateField(raceManager, "saveManager", saveManager);
            SetPrivateField(raceManager, "uiManager", uiManager);
            SetPrivateField(
                raceManager,
                "driftScoreManager",
                driftScoreManager
            );

            SetPrivateField(
                raceManager,
                "ghostReplaySystem",
                ghostReplaySystem
            );

            SetPrivateField(raceManager, "startAutomatically", false);
            SetPrivateField(raceManager, "requireModeSelection", false);
            SetPrivateField(raceManager, "modeSelected", true);

            string saveKey =
                "ArcadeRacing_R1_Characterization_" +
                Guid.NewGuid().ToString("N");

            playerPrefsKeys.Add(saveKey);
            SetPrivateField(saveManager, "saveKey", saveKey);
            SetPrivateField(saveManager, "loadOnAwake", false);
            SetPrivateField(
                saveManager,
                "saveOnApplicationPause",
                false
            );

            InvokePublic(saveManager, "Load");

            object mode = Enum.Parse(
                GetRuntimeType("ArcadeRacing.RaceMode"),
                modeName
            );

            SetAutoProperty(raceManager, "ActiveMode", mode);

            bool initialized = (bool)InvokePrivate(
                checkpointManager,
                "InitializeManager"
            );

            Assert.That(
                initialized,
                Is.True,
                "The isolated checkpoint fixture must initialize."
            );

            SetPrivateField(checkpointManager, "initialized", true);
            LogAssert.ignoreFailingMessages =
                previousIgnoreFailingMessages;

            return new RacingFixture
            {
                RaceManager = raceManager,
                CheckpointManager = checkpointManager,
                LapTimer = lapTimer,
                SaveManager = saveManager,
                UIManager = uiManager,
                DriftScoreManager = driftScoreManager,
                GhostReplaySystem = ghostReplaySystem,
                PlayerCar = playerCar,
                PauseManagerType = GetRuntimeType(
                    "ArcadeRacing.PauseManager"
                ),
                StartFinish = startFinish,
                LapCheckpoints = lapCheckpoints
            };
        }

        protected void BeginRacing(
            RacingFixture fixture,
            string modeName = "TimeTrial"
        )
        {
            object mode = Enum.Parse(
                GetRuntimeType("ArcadeRacing.RaceMode"),
                modeName
            );

            object racingState = Enum.Parse(
                GetRuntimeType("ArcadeRacing.RaceState"),
                "Racing"
            );

            SetAutoProperty(fixture.RaceManager, "ActiveMode", mode);
            SetAutoProperty(fixture.RaceManager, "State", racingState);
            InvokePublic(fixture.CheckpointManager, "BeginRace");
            InvokePublic(fixture.LapTimer, "BeginRace", 0f);
            InvokePublic(fixture.DriftScoreManager, "BeginRun");
            InvokePublic(fixture.PlayerCar, "SetControlEnabled", true);
        }

        protected void CompleteValidLap(RacingFixture fixture)
        {
            for (int i = 0; i < fixture.LapCheckpoints.Length; i++)
            {
                PassCheckpoint(fixture, fixture.LapCheckpoints[i]);
            }

            PassCheckpoint(fixture, fixture.StartFinish);
        }

        protected void PassCheckpoint(
            RacingFixture fixture,
            Component checkpoint
        )
        {
            InvokePublic(
                fixture.CheckpointManager,
                "TryPassCheckpoint",
                checkpoint,
                fixture.PlayerCar
            );
        }

        protected IEnumerator CreateCountdownEnumerator(
            RacingFixture fixture
        )
        {
            return (IEnumerator)InvokePrivate(
                fixture.RaceManager,
                "CountdownRoutine"
            );
        }

        protected void SubscribeToProgressEvents(RacingFixture fixture)
        {
            SubscribeEvent(
                fixture.CheckpointManager,
                "CheckpointPassed",
                nameof(CaptureCheckpointPassed)
            );

            SubscribeEvent(
                fixture.LapTimer,
                "LapCompleted",
                nameof(CaptureLapCompleted)
            );
        }

        protected Component CreatePauseManager(RacingFixture fixture)
        {
            GameObject pauseObject = CreateTemporaryObject(
                "R1_PauseManager"
            );

            Component pauseManager = pauseObject.AddComponent(
                fixture.PauseManagerType
            );

            SetPrivateField(
                pauseManager,
                "raceManager",
                fixture.RaceManager
            );

            SetPrivateField(pauseManager, "uiManager", fixture.UIManager);
            SetPrivateField(pauseManager, "pauseAudio", false);
            return pauseManager;
        }

        protected static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName + " was not found.");
            return type;
        }

        protected GameObject CreateTemporaryObject(string name)
        {
            GameObject created = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            createdObjects.Add(created);
            return created;
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

            Assert.That(
                field,
                Is.Not.Null,
                target.GetType().FullName + "." + fieldName
            );

            field.SetValue(target, value);
        }

        protected static void SetAutoProperty(
            object target,
            string propertyName,
            object value
        )
        {
            SetPrivateField(
                target,
                "<" + propertyName + ">k__BackingField",
                value
            );
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

            Assert.That(
                property,
                Is.Not.Null,
                target.GetType().FullName + "." + propertyName
            );

            return (T)property.GetValue(target);
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

        protected static object InvokePrivate(
            object target,
            string methodName,
            params object[] arguments
        )
        {
            MethodInfo method = FindMethod(
                target,
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic,
                arguments.Length
            );

            return method.Invoke(target, arguments);
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

        private Component CreateCheckpoint(string name, Type checkpointType)
        {
            GameObject checkpointObject = CreateTemporaryObject(name);
            checkpointObject.AddComponent<BoxCollider>();

            Component checkpoint = checkpointObject.AddComponent(
                checkpointType
            );

            GameObject respawnObject = CreateTemporaryObject(
                name + "_RespawnPoint"
            );

            respawnObject.transform.SetParent(
                checkpointObject.transform,
                false
            );

            SetPrivateField(
                checkpoint,
                "respawnPoint",
                respawnObject.transform
            );

            return checkpoint;
        }

        private void SubscribeEvent(
            object source,
            string eventName,
            string handlerName
        )
        {
            EventInfo eventInfo = source.GetType().GetEvent(
                eventName,
                InstanceMembers
            );

            Assert.That(eventInfo, Is.Not.Null, eventName);

            MethodInfo handler = typeof(RacingCharacterizationTestFixture)
                .GetMethod(
                handlerName,
                InstanceMembers
            );

            Assert.That(handler, Is.Not.Null, handlerName);

            Delegate callback = Delegate.CreateDelegate(
                eventInfo.EventHandlerType,
                this,
                handler
            );

            eventInfo.AddEventHandler(source, callback);
        }

        private void CaptureCheckpointPassed(int completed, int total)
        {
            CheckpointEventCount++;
            LastCheckpointProgress = completed;
            LastCheckpointTotal = total;
        }

        private void CaptureLapCompleted(
            int lapNumber,
            float lapTime,
            bool isNewBest
        )
        {
            LapCompletedEventCount++;
            LastCompletedLapNumber = lapNumber;
        }

        protected sealed class RacingFixture
        {
            public Component RaceManager;
            public Component CheckpointManager;
            public Component LapTimer;
            public Component SaveManager;
            public Component UIManager;
            public Component DriftScoreManager;
            public Component GhostReplaySystem;
            public Component PlayerCar;
            public Type PauseManagerType;
            public Component StartFinish;
            public Component[] LapCheckpoints;
        }
    }
}
