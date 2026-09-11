using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace ArcadeRacing.Tests
{
    public sealed class GhostReplayCharacterizationTests :
        GhostReplayCharacterizationTestFixture
    {
        private const float FloatTolerance = 0.0001f;
        private const float RotationToleranceDegrees = 0.05f;

        [Test]
        public void GhostPaths_IsolateRaceModesAndLoadedData()
        {
            string scene = UniqueToken + "_Track";
            string timeTrialBuiltPath = BuildSavePath(
                scene,
                UniqueProfileId,
                "TimeTrial"
            );

            string driftBuiltPath = BuildSavePath(
                scene,
                UniqueProfileId,
                "DriftChallenge"
            );

            Assert.That(
                driftBuiltPath,
                Is.Not.EqualTo(timeTrialBuiltPath)
            );

            BindPlayer();
            string timeTrialPath = ConfigureStorage(
                "Modes/TimeTrial.json"
            );

            object timeTrialData = CreateTwoFrameLap(
                "TimeTrial",
                UniqueProfileId
            );

            SaveLapData(timeTrialData);
            Assert.That(File.Exists(timeTrialPath), Is.True);

            SetRaceMode("DriftChallenge");
            string driftPath = ConfigureStorage(
                "Modes/DriftChallenge.json"
            );

            LoadLapData();
            Assert.That(HasBestLap, Is.False);

            ConfigureStorage("Modes/TimeTrial.json");
            LoadLapData();
            Assert.That(
                HasBestLap,
                Is.False,
                "Time Trial data must not load in Drift Challenge."
            );

            ConfigureStorage("Modes/DriftChallenge.json");
            object driftData = CreateTwoFrameLap(
                "DriftChallenge",
                UniqueProfileId
            );

            SaveLapData(driftData);

            Assert.That(timeTrialPath, Is.Not.EqualTo(driftPath));
            Assert.That(File.Exists(timeTrialPath), Is.True);
            Assert.That(File.Exists(driftPath), Is.True);
        }

        [Test]
        public void GhostPaths_IsolateVehiclesAndLoadedData()
        {
            string firstVehicle = UniqueToken + "_Vehicle1";
            string secondVehicle = UniqueToken + "_Vehicle2";
            string scene = UniqueToken + "_Track";

            Assert.That(
                BuildSavePath(scene, firstVehicle, "TimeTrial"),
                Is.Not.EqualTo(
                    BuildSavePath(scene, secondVehicle, "TimeTrial")
                )
            );

            BindPlayer(firstVehicle);
            string firstPath = ConfigureStorage(
                "Vehicles/Vehicle1.json"
            );

            SaveLapData(CreateTwoFrameLap("TimeTrial", firstVehicle));
            Assert.That(File.Exists(firstPath), Is.True);

            BindPlayer(secondVehicle);
            string secondPath = ConfigureStorage(
                "Vehicles/Vehicle2.json"
            );

            LoadLapData();
            Assert.That(HasBestLap, Is.False);

            SaveLapData(CreateTwoFrameLap("TimeTrial", secondVehicle));

            Assert.That(secondPath, Is.Not.EqualTo(firstPath));
            Assert.That(File.Exists(firstPath), Is.True);
            Assert.That(File.Exists(secondPath), Is.True);
        }

        [Test]
        public void GhostPaths_IsolateScenes()
        {
            string firstScene = UniqueToken + "_TrackA";
            string secondScene = UniqueToken + "_TrackB";

            string firstPath = BuildSavePath(
                firstScene,
                UniqueProfileId,
                "TimeTrial"
            );

            string secondPath = BuildSavePath(
                secondScene,
                UniqueProfileId,
                "TimeTrial"
            );

            Assert.That(secondPath, Is.Not.EqualTo(firstPath));
            Assert.That(
                Path.GetFileName(firstPath),
                Does.Contain(firstScene)
            );

            Assert.That(
                Path.GetFileName(secondPath),
                Does.Contain(secondScene)
            );
        }

        [Test]
        public void GhostPersistence_RoundTripsFramesAndMetadata()
        {
            BindPlayer();
            string path = ConfigureStorage("RoundTrip/ghost.json");
            Quaternion middleRotation = Quaternion.Euler(0f, 45f, 0f);
            Quaternion finalRotation = Quaternion.Euler(0f, 90f, 0f);

            object data = CreateLapData(
                0.1f,
                UniqueToken + "_Track",
                UniqueProfileId,
                "TimeTrial",
                new[]
                {
                    CreateFrame(0f, Vector3.zero, Quaternion.identity),
                    CreateFrame(
                        0.05f,
                        new Vector3(2f, 1f, -3f),
                        middleRotation
                    ),
                    CreateFrame(
                        0.1f,
                        new Vector3(4f, 2f, -6f),
                        finalRotation
                    )
                }
            );

            SaveLapData(data);
            ClearBestLap();
            LoadLapData();

            Assert.That(File.Exists(path), Is.True);
            Assert.That(HasBestLap, Is.True);
            Assert.That(
                ReadField<int>(BestLapData, "version"),
                Is.EqualTo(CurrentFormatVersion)
            );

            Assert.That(
                ReadField<string>(BestLapData, "sceneName"),
                Is.EqualTo(UniqueToken + "_Track")
            );

            Assert.That(
                ReadField<string>(BestLapData, "vehicleId"),
                Is.EqualTo(UniqueProfileId)
            );

            Assert.That(
                ReadField<string>(BestLapData, "raceMode"),
                Is.EqualTo("TimeTrial")
            );

            Assert.That(BestLapTime, Is.EqualTo(0.1f).Within(FloatTolerance));

            IList frames = Frames(BestLapData);
            Assert.That(frames.Count, Is.EqualTo(3));
            AssertFrame(
                frames[0],
                0f,
                Vector3.zero,
                Quaternion.identity
            );

            AssertFrame(
                frames[1],
                0.05f,
                new Vector3(2f, 1f, -3f),
                middleRotation
            );

            AssertFrame(
                frames[2],
                0.1f,
                new Vector3(4f, 2f, -6f),
                finalRotation
            );
        }

        [Test]
        public void GhostPersistence_MissingFileRemainsUnavailable()
        {
            BindPlayer();
            string path = ConfigureStorage("Missing/not-present.json");

            Assert.That(File.Exists(path), Is.False);
            LoadLapData();

            Assert.That(HasBestLap, Is.False);
            Assert.That(BestLapTime, Is.Zero);
            Assert.That(IsPlaying, Is.False);
            Assert.That(GhostRoot.activeSelf, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GhostPersistence_CreatesMissingDirectory()
        {
            BindPlayer();
            string path = ConfigureStorage(
                "Directory/Does/Not/Exist/ghost.json"
            );

            string directory = Path.GetDirectoryName(path);
            Assert.That(Directory.Exists(directory), Is.False);

            SaveLapData(
                CreateTwoFrameLap("TimeTrial", UniqueProfileId)
            );

            Assert.That(Directory.Exists(directory), Is.True);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void GhostPersistence_SerializesCurrentRuntimeVersion()
        {
            BindPlayer();
            string path = ConfigureStorage("Version/current.json");
            SaveLapData(
                CreateTwoFrameLap("TimeTrial", UniqueProfileId)
            );

            object serialized = Deserialize(File.ReadAllText(path));
            Assert.That(
                ReadField<int>(serialized, "version"),
                Is.EqualTo(CurrentFormatVersion)
            );
        }

        [Test]
        public void GhostPersistence_MalformedJsonWarnsAndRemainsUnavailable()
        {
            BindPlayer();
            string path = ConfigureStorage("Corruption/malformed.json");
            WriteText(path, "{ definitely-not-valid-json");

            LogAssert.Expect(
                LogType.Warning,
                new Regex("^Ghost data could not be loaded:")
            );

            LoadLapData();
            Assert.That(HasBestLap, Is.False);
            Assert.That(IsPlaying, Is.False);
        }

        [Test]
        public void GhostPersistence_EmptyFileRemainsUnavailableWithoutWarning()
        {
            BindPlayer();
            string path = ConfigureStorage("Corruption/empty.json");
            WriteText(path, string.Empty);

            LoadLapData();
            Assert.That(HasBestLap, Is.False);
            Assert.That(IsPlaying, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GhostPersistence_NullFramesRemainUnavailable()
        {
            BindPlayer();
            string path = ConfigureStorage("Corruption/null-frames.json");

            string json =
                "{\"version\":" + CurrentFormatVersion +
                ",\"sceneName\":\"Track\"" +
                ",\"vehicleId\":\"Vehicle\"" +
                ",\"raceMode\":\"TimeTrial\"" +
                ",\"lapTime\":1.0,\"frames\":null}";

            WriteText(path, json);
            LoadLapData();

            Assert.That(HasBestLap, Is.False);
            Assert.That(BestLapTime, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GhostPersistence_EmptyFramesRemainUnavailable()
        {
            BindPlayer();
            string path = ConfigureStorage("Corruption/empty-frames.json");

            object data = CreateLapData(
                1f,
                "Track",
                UniqueProfileId,
                "TimeTrial",
                Array.Empty<object>()
            );

            WriteText(path, Serialize(data));
            LoadLapData();

            Assert.That(HasBestLap, Is.False);
            Assert.That(BestLapTime, Is.Zero);
            Assert.That(GhostRoot.activeSelf, Is.False);
        }

        [Test]
        public void GhostPersistence_SingleFrameRemainsUnavailableAndPlaybackStops()
        {
            BindPlayer();
            string path = ConfigureStorage("Corruption/single-frame.json");

            object data = CreateLapData(
                1f,
                "Track",
                UniqueProfileId,
                "TimeTrial",
                new[]
                {
                    CreateFrame(
                        0f,
                        new Vector3(3f, 2f, 1f),
                        Quaternion.Euler(0f, 30f, 0f)
                    )
                }
            );

            WriteText(path, Serialize(data));
            LoadLapData();
            Assert.That(HasBestLap, Is.False);

            SetPlaybackData(data);
            Assert.DoesNotThrow(() => UpdatePlayback(0.5f));
            Assert.That(IsPlaying, Is.False);
            Assert.That(GhostRoot.activeSelf, Is.False);
        }

        [Test]
        public void GhostPersistence_AcceptsStructurallyValidUnknownVersion()
        {
            BindPlayer();
            string path = ConfigureStorage("Version/unknown.json");

            object data = CreateLapData(
                1f,
                "Track",
                UniqueProfileId,
                "TimeTrial",
                TwoFrames(),
                CurrentFormatVersion + 77
            );

            WriteText(path, Serialize(data));
            LoadLapData();

            Assert.That(HasBestLap, Is.True);
            Assert.That(
                ReadField<int>(BestLapData, "version"),
                Is.EqualTo(CurrentFormatVersion + 77)
            );
        }

        [Test]
        public void GhostPersistence_LoadsModeLessLegacyDataForTimeTrialOnly()
        {
            BindPlayer();
            ConfigureStorage(
                "Legacy/current.json",
                "Legacy/without-mode.json"
            );

            object legacyData = CreateLapData(
                1f,
                "Track",
                UniqueProfileId,
                string.Empty,
                TwoFrames()
            );

            WriteText(CurrentLegacySavePath, Serialize(legacyData));
            LoadLapData();
            Assert.That(HasBestLap, Is.True);

            SetRaceMode("DriftChallenge");
            ConfigureStorage(
                "Legacy/current.json",
                "Legacy/without-mode.json"
            );

            LoadLapData();
            Assert.That(HasBestLap, Is.False);
        }

        [Test]
        public void GhostPlayback_InterpolatesPositionAtMidpoint()
        {
            BindPlayer();
            object data = CreateLapData(
                1f,
                "Track",
                UniqueProfileId,
                "TimeTrial",
                new[]
                {
                    CreateFrame(0f, Vector3.zero, Quaternion.identity),
                    CreateFrame(
                        1f,
                        new Vector3(10f, 0f, 0f),
                        Quaternion.identity
                    )
                }
            );

            SetPlaybackData(data);
            UpdatePlayback(0.5f);

            AssertVector(
                GhostRoot.transform.position,
                new Vector3(5f, 0f, 0f)
            );
        }

        [Test]
        public void GhostPlayback_SlerpsRotationAtMidpoint()
        {
            BindPlayer();
            Quaternion from = Quaternion.Euler(0f, 0f, 0f);
            Quaternion to = Quaternion.Euler(0f, 120f, 0f);

            object data = CreateLapData(
                1f,
                "Track",
                UniqueProfileId,
                "TimeTrial",
                new[]
                {
                    CreateFrame(0f, Vector3.zero, from),
                    CreateFrame(1f, Vector3.zero, to)
                }
            );

            SetPlaybackData(data);
            UpdatePlayback(0.5f);

            AssertQuaternion(
                GhostRoot.transform.rotation,
                Quaternion.Slerp(from, to, 0.5f)
            );
        }

        [Test]
        public void GhostPlayback_ClampsToFirstPoseAtStartBoundary()
        {
            BindPlayer();
            Vector3 firstPosition = new Vector3(3f, 4f, 5f);
            Quaternion firstRotation = Quaternion.Euler(10f, 20f, 30f);

            object data = CreateLapData(
                2f,
                "Track",
                UniqueProfileId,
                "TimeTrial",
                new[]
                {
                    CreateFrame(1f, firstPosition, firstRotation),
                    CreateFrame(
                        2f,
                        new Vector3(8f, 9f, 10f),
                        Quaternion.Euler(40f, 50f, 60f)
                    )
                }
            );

            SetPlaybackData(data);
            UpdatePlayback(0.25f);
            AssertVector(GhostRoot.transform.position, firstPosition);
            AssertQuaternion(GhostRoot.transform.rotation, firstRotation);

            UpdatePlayback(1f);
            AssertVector(GhostRoot.transform.position, firstPosition);
            AssertQuaternion(GhostRoot.transform.rotation, firstRotation);
        }

        [Test]
        public void GhostPlayback_DisablesAtEndBoundaryWithoutException()
        {
            BindPlayer();
            object data = CreateTwoFrameLap(
                "TimeTrial",
                UniqueProfileId
            );

            SetPlaybackData(data);
            Assert.DoesNotThrow(() => UpdatePlayback(1f));

            Assert.That(IsPlaying, Is.False);
            Assert.That(GhostRoot.activeSelf, Is.False);
        }

        [Test]
        public void GhostRecording_StartClearsBufferAndCapturesInitialPose()
        {
            BindPlayer();
            SetRaceManagerMode("TimeTrial");
            PlayerCar.transform.SetPositionAndRotation(
                new Vector3(7f, 1f, -2f),
                Quaternion.Euler(0f, 35f, 0f)
            );

            RecordingFrames.Add(
                CreateFrame(99f, Vector3.one, Quaternion.identity)
            );

            StartRecording();

            Assert.That(IsRecording, Is.True);
            Assert.That(RecordingFrames.Count, Is.EqualTo(1));
            AssertFrame(
                RecordingFrames[0],
                0f,
                PlayerCar.transform.position,
                PlayerCar.transform.rotation
            );
        }

        [Test]
        public void GhostRecording_ProducesMonotonicSampleTimes()
        {
            BindPlayer();
            SetRaceManagerMode("TimeTrial");
            StartRecording();

            RecordAt(0.05f);
            RecordAt(0.1f);
            RecordAt(0.15f);

            Assert.That(RecordingFrames.Count, Is.EqualTo(4));

            for (int i = 1; i < RecordingFrames.Count; i++)
            {
                Assert.That(
                    FrameTime(RecordingFrames[i]),
                    Is.GreaterThanOrEqualTo(
                        FrameTime(RecordingFrames[i - 1])
                    )
                );
            }
        }

        [Test]
        public void GhostRecording_NewBestFinishStopsAndSaves()
        {
            BindPlayer();
            SetRaceManagerMode("TimeTrial");
            string path = ConfigureStorage("Lifecycle/new-best.json");

            StartRecording();
            PlayerCar.transform.SetPositionAndRotation(
                new Vector3(12f, 0f, 4f),
                Quaternion.Euler(0f, 90f, 0f)
            );

            CompleteRecording(1f, true);

            Assert.That(IsRecording, Is.False);
            Assert.That(IsPlaying, Is.False);
            Assert.That(HasBestLap, Is.True);
            Assert.That(BestLapTime, Is.EqualTo(1f));
            Assert.That(Frames(BestLapData).Count, Is.EqualTo(2));
            Assert.That(File.Exists(path), Is.True);
        }

        [Test]
        public void GhostRecording_WorseRunDoesNotOverwriteBest()
        {
            BindPlayer();
            SetRaceManagerMode("TimeTrial");
            string path = ConfigureStorage("Lifecycle/best-policy.json");

            StartRecording();
            PlayerCar.transform.position = new Vector3(5f, 0f, 0f);
            CompleteRecording(1f, true);
            string firstSave = File.ReadAllText(path);

            StartRecording();
            PlayerCar.transform.position = new Vector3(20f, 0f, 0f);
            CompleteRecording(2f, false);

            Assert.That(File.ReadAllText(path), Is.EqualTo(firstSave));
            Assert.That(BestLapTime, Is.EqualTo(1f));
        }

        [Test]
        public void GhostLifecycle_ModeSwitchClearsActiveReplayState()
        {
            BindPlayer();
            SetPlaybackData(
                CreateTwoFrameLap("TimeTrial", UniqueProfileId)
            );

            RecordingFrames.Add(
                CreateFrame(0f, Vector3.zero, Quaternion.identity)
            );

            SetPrivateField(GhostReplaySystem, "recording", true);
            SetRaceMode("DriftChallenge");

            Assert.That(ActiveModeName, Is.EqualTo("DriftChallenge"));
            Assert.That(IsRecording, Is.False);
            Assert.That(IsPlaying, Is.False);
            Assert.That(RecordingFrames.Count, Is.Zero);
            Assert.That(HasBestLap, Is.False);
            Assert.That(GhostRoot.activeSelf, Is.False);
        }

        [Test]
        public void GhostLifecycle_VehicleSwitchRebindsAndClearsReplayState()
        {
            string firstProfile = UniqueToken + "_FirstVehicle";
            string secondProfile = UniqueToken + "_SecondVehicle";
            BindPlayer(firstProfile);
            GameObject firstGhostRoot = GhostRoot;

            RecordingFrames.Add(
                CreateFrame(0f, Vector3.zero, Quaternion.identity)
            );

            SetPrivateField(GhostReplaySystem, "recording", true);
            SetPrivateField(GhostReplaySystem, "playing", true);
            firstGhostRoot.SetActive(true);

            Component secondPlayer = CreateAdditionalPlayer(
                secondProfile + "_Car"
            );

            BindPlayer(secondProfile, secondPlayer);

            Assert.That(
                ReadField<Component>(GhostReplaySystem, "playerCar"),
                Is.SameAs(secondPlayer)
            );

            Assert.That(firstGhostRoot == null, Is.True);
            Assert.That(GhostRoot, Is.Not.Null);
            Assert.That(IsRecording, Is.False);
            Assert.That(IsPlaying, Is.False);
            Assert.That(RecordingFrames.Count, Is.Zero);
            Assert.That(HasBestLap, Is.False);
        }

        [Test]
        public void GhostVisual_HasNoPhysicsComponentsAndStartsHidden()
        {
            AddSimpleMeshToPlayer(PlayerCar);
            BindPlayer();

            Assert.That(GhostRoot, Is.Not.Null);
            Assert.That(GhostRoot.activeSelf, Is.False);
            Assert.That(
                GhostRoot.GetComponentsInChildren<Collider>(true),
                Is.Empty
            );

            Assert.That(
                GhostRoot.GetComponentsInChildren<Rigidbody>(true),
                Is.Empty
            );

            MeshRenderer[] renderers =
                GhostRoot.GetComponentsInChildren<MeshRenderer>(true);

            Assert.That(renderers, Has.Length.EqualTo(1));
            Assert.That(
                renderers[0].shadowCastingMode,
                Is.EqualTo(ShadowCastingMode.Off)
            );

            Assert.That(renderers[0].receiveShadows, Is.False);
        }

        private object CreateTwoFrameLap(
            string modeName,
            string vehicleId
        )
        {
            return CreateLapData(
                1f,
                UniqueToken + "_Track",
                vehicleId,
                modeName,
                TwoFrames()
            );
        }

        private object[] TwoFrames()
        {
            return new[]
            {
                CreateFrame(0f, Vector3.zero, Quaternion.identity),
                CreateFrame(
                    1f,
                    new Vector3(10f, 0f, 0f),
                    Quaternion.Euler(0f, 90f, 0f)
                )
            };
        }

        private void AssertFrame(
            object frame,
            float time,
            Vector3 position,
            Quaternion rotation
        )
        {
            Assert.That(
                FrameTime(frame),
                Is.EqualTo(time).Within(FloatTolerance)
            );

            AssertVector(FramePosition(frame), position);
            AssertQuaternion(FrameRotation(frame), rotation);
        }

        private static void AssertVector(
            Vector3 actual,
            Vector3 expected
        )
        {
            Assert.That(
                Vector3.Distance(actual, expected),
                Is.LessThan(FloatTolerance)
            );
        }

        private static void AssertQuaternion(
            Quaternion actual,
            Quaternion expected
        )
        {
            Assert.That(
                Quaternion.Angle(actual, expected),
                Is.LessThan(RotationToleranceDegrees)
            );
        }
    }
}
