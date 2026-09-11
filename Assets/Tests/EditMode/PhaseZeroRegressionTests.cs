using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcadeRacing.Tests
{
    public sealed class PhaseZeroRegressionTests
    {
        private const BindingFlags InstancePrivate =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private const BindingFlags StaticPrivate =
            BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void GhostSavePath_IsFilesystemSafeAndModeSpecific()
        {
            Type ghostType = GetRuntimeType(
                "ArcadeRacing.GhostReplaySystem"
            );

            Type raceModeType = GetRuntimeType(
                "ArcadeRacing.RaceMode"
            );

            MethodInfo buildSavePath = ghostType.GetMethod(
                "BuildSavePath",
                StaticPrivate
            );

            Assert.That(buildSavePath, Is.Not.Null);

            object timeTrial = Enum.Parse(
                raceModeType,
                "TimeTrial"
            );

            object driftChallenge = Enum.Parse(
                raceModeType,
                "DriftChallenge"
            );

            string timeTrialPath = (string)buildSavePath.Invoke(
                null,
                new[]
                {
                    "City:/Track*?",
                    "Player Car<>:\"/\\|?*",
                    timeTrial
                }
            );

            string driftPath = (string)buildSavePath.Invoke(
                null,
                new[]
                {
                    "City:/Track*?",
                    "Player Car<>:\"/\\|?*",
                    driftChallenge
                }
            );

            Assert.That(driftPath, Is.Not.EqualTo(timeTrialPath));
            Assert.That(
                Path.GetFileName(timeTrialPath),
                Does.EndWith("_TimeTrial.json")
            );

            Assert.That(
                Path.GetFileName(driftPath),
                Does.EndWith("_DriftChallenge.json")
            );

            Assert.That(
                Path.GetFileName(timeTrialPath).IndexOfAny(
                    Path.GetInvalidFileNameChars()
                ),
                Is.EqualTo(-1)
            );
        }

        [Test]
        public void CheckpointManager_IgnoresOutOfOrderCheckpoint()
        {
            bool previousIgnoreState = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            List<GameObject> createdObjects = new List<GameObject>();

            try
            {
                Type checkpointType = GetRuntimeType(
                    "ArcadeRacing.Checkpoint"
                );

                Type checkpointManagerType = GetRuntimeType(
                    "ArcadeRacing.CheckpointManager"
                );

                Type raceManagerType = GetRuntimeType(
                    "ArcadeRacing.RaceManager"
                );

                Type lapTimerType = GetRuntimeType(
                    "ArcadeRacing.LapTimer"
                );

                Type carControllerType = GetRuntimeType(
                    "ArcadeRacing.CarController"
                );

                GameObject raceObject = CreateTemporaryObject(
                    "PhaseZero_RaceSystems",
                    createdObjects
                );

                Component raceManager = raceObject.AddComponent(
                    raceManagerType
                );

                Component lapTimer = raceObject.AddComponent(
                    lapTimerType
                );

                GameObject carObject = CreateTemporaryObject(
                    "PhaseZero_PlayerCar",
                    createdObjects
                );

                Component playerCar = carObject.AddComponent(
                    carControllerType
                );

                Component startFinish = CreateCheckpoint(
                    "StartFinish",
                    checkpointType,
                    createdObjects
                );

                Component checkpoint1 = CreateCheckpoint(
                    "Checkpoint",
                    checkpointType,
                    createdObjects
                );

                Component checkpoint2 = CreateCheckpoint(
                    "Checkpoint (1)",
                    checkpointType,
                    createdObjects
                );

                Component checkpoint3 = CreateCheckpoint(
                    "Checkpoint (2)",
                    checkpointType,
                    createdObjects
                );

                Component checkpoint4 = CreateCheckpoint(
                    "Checkpoint (3)",
                    checkpointType,
                    createdObjects
                );

                GameObject managerObject = CreateTemporaryObject(
                    "PhaseZero_CheckpointManager",
                    createdObjects
                );

                Component checkpointManager =
                    managerObject.AddComponent(checkpointManagerType);

                SetPrivateField(
                    checkpointManager,
                    "startFinishCheckpoint",
                    startFinish
                );

                SetPrivateField(
                    checkpointManager,
                    "checkpoint1",
                    checkpoint1
                );

                SetPrivateField(
                    checkpointManager,
                    "checkpoint2",
                    checkpoint2
                );

                SetPrivateField(
                    checkpointManager,
                    "checkpoint3",
                    checkpoint3
                );

                SetPrivateField(
                    checkpointManager,
                    "checkpoint4",
                    checkpoint4
                );

                SetPrivateField(
                    checkpointManager,
                    "raceManager",
                    raceManager
                );

                SetPrivateField(
                    checkpointManager,
                    "lapTimer",
                    lapTimer
                );

                SetPrivateField(
                    raceManager,
                    "playerCar",
                    playerCar
                );

                MethodInfo initializeManager =
                    checkpointManagerType.GetMethod(
                        "InitializeManager",
                        InstancePrivate
                    );

                Assert.That(initializeManager, Is.Not.Null);
                Assert.That(
                    (bool)initializeManager.Invoke(
                        checkpointManager,
                        null
                    ),
                    Is.True
                );

                checkpointManagerType.GetMethod("BeginRace").Invoke(
                    checkpointManager,
                    null
                );

                MethodInfo tryPassCheckpoint =
                    checkpointManagerType.GetMethod(
                        "TryPassCheckpoint"
                    );

                Assert.That(tryPassCheckpoint, Is.Not.Null);

                tryPassCheckpoint.Invoke(
                    checkpointManager,
                    new object[] { checkpoint2, playerCar }
                );

                Assert.That(
                    GetPublicIntProperty(
                        checkpointManager,
                        "CompletedCheckpointCount"
                    ),
                    Is.EqualTo(0)
                );

                Assert.That(
                    GetPublicIntProperty(
                        checkpointManager,
                        "ExpectedCheckpointNumber"
                    ),
                    Is.EqualTo(1)
                );

                tryPassCheckpoint.Invoke(
                    checkpointManager,
                    new object[] { checkpoint1, playerCar }
                );

                Assert.That(
                    GetPublicIntProperty(
                        checkpointManager,
                        "CompletedCheckpointCount"
                    ),
                    Is.EqualTo(1)
                );
            }
            finally
            {
                for (int i = createdObjects.Count - 1; i >= 0; i--)
                {
                    if (createdObjects[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(
                            createdObjects[i]
                        );
                    }
                }

                LogAssert.ignoreFailingMessages = previousIgnoreState;
            }
        }

        [Test]
        public void SaveManager_RoundTripsExistingDataShape()
        {
            Type saveManagerType = GetRuntimeType(
                "ArcadeRacing.SaveManager"
            );

            string testKey =
                "ArcadeRacing_PhaseZero_Test_" +
                Guid.NewGuid().ToString("N");

            GameObject writerObject = null;
            GameObject readerObject = null;

            try
            {
                writerObject = new GameObject("PhaseZero_SaveWriter")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                Component writer = writerObject.AddComponent(
                    saveManagerType
                );

                SetPrivateField(writer, "saveKey", testKey);
                saveManagerType.GetMethod("Load").Invoke(writer, null);
                saveManagerType.GetMethod("AddCash").Invoke(
                    writer,
                    new object[] { 1250 }
                );

                saveManagerType.GetMethod("TrySetBestTimeTrial").Invoke(
                    writer,
                    new object[] { 87.5f }
                );

                saveManagerType.GetMethod("TrySetBestDriftScore").Invoke(
                    writer,
                    new object[] { 4321 }
                );

                saveManagerType.GetMethod("Save").Invoke(writer, null);

                readerObject = new GameObject("PhaseZero_SaveReader")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                Component reader = readerObject.AddComponent(
                    saveManagerType
                );

                SetPrivateField(reader, "saveKey", testKey);
                saveManagerType.GetMethod("Load").Invoke(reader, null);

                Assert.That(
                    GetPublicIntProperty(reader, "Cash"),
                    Is.EqualTo(1250)
                );

                Assert.That(
                    GetPublicIntProperty(reader, "BestDriftScore"),
                    Is.EqualTo(4321)
                );

                float bestTime = (float)saveManagerType.GetProperty(
                    "BestTimeTrialRaceTime"
                ).GetValue(reader);

                Assert.That(bestTime, Is.EqualTo(87.5f));
            }
            finally
            {
                PlayerPrefs.DeleteKey(testKey);
                PlayerPrefs.Save();

                if (writerObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(writerObject);
                }

                if (readerObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(readerObject);
                }
            }
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName + " was not found.");
            return type;
        }

        private static GameObject CreateTemporaryObject(
            string name,
            ICollection<GameObject> createdObjects
        )
        {
            GameObject created = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            createdObjects.Add(created);
            return created;
        }

        private static Component CreateCheckpoint(
            string name,
            Type checkpointType,
            ICollection<GameObject> createdObjects
        )
        {
            GameObject checkpointObject = CreateTemporaryObject(
                name,
                createdObjects
            );

            checkpointObject.AddComponent<BoxCollider>();

            Component checkpoint = checkpointObject.AddComponent(
                checkpointType
            );

            GameObject respawnObject = CreateTemporaryObject(
                name + "_RespawnPoint",
                createdObjects
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

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value
        )
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                InstancePrivate
            );

            Assert.That(
                field,
                Is.Not.Null,
                target.GetType().FullName + "." + fieldName
            );

            field.SetValue(target, value);
        }

        private static int GetPublicIntProperty(
            object target,
            string propertyName
        )
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName
            );

            Assert.That(property, Is.Not.Null);
            return (int)property.GetValue(target);
        }
    }
}
