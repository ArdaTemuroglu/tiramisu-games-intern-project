using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ArcadeRacing.Tests
{
    public abstract class GhostReplayCharacterizationTestFixture
    {
        private const BindingFlags InstanceMembers =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private const BindingFlags StaticPrivate =
            BindingFlags.Static | BindingFlags.NonPublic;

        private readonly List<GameObject> createdObjects =
            new List<GameObject>();

        private readonly List<UnityEngine.Object> createdAssets =
            new List<UnityEngine.Object>();

        private readonly HashSet<string> createdFilePaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        protected Type GhostReplaySystemType { get; private set; }
        protected Type GhostFrameType { get; private set; }
        protected Type GhostLapDataType { get; private set; }
        protected Type RaceModeType { get; private set; }
        protected Component GhostReplaySystem { get; private set; }
        protected Component RaceManager { get; private set; }
        protected Component LapTimer { get; private set; }
        protected Component PlayerCar { get; private set; }
        protected string UniqueToken { get; private set; }
        protected string UniqueProfileId { get; private set; }
        protected string TemporaryRoot { get; private set; }

        protected int CurrentFormatVersion
        {
            get
            {
                object data = Activator.CreateInstance(
                    GhostLapDataType,
                    true
                );

                return ReadField<int>(data, "version");
            }
        }

        [SetUp]
        public void SetUpGhostFixture()
        {
            UniqueToken =
                "R1_Ghost_" + Guid.NewGuid().ToString("N");

            UniqueProfileId = UniqueToken + "_Vehicle";
            TemporaryRoot = Path.Combine(
                Path.GetTempPath(),
                UniqueToken
            );

            GhostReplaySystemType = GetRuntimeType(
                "ArcadeRacing.GhostReplaySystem"
            );

            GhostFrameType = GhostReplaySystemType.GetNestedType(
                "GhostFrame",
                BindingFlags.NonPublic
            );

            GhostLapDataType = GhostReplaySystemType.GetNestedType(
                "GhostLapData",
                BindingFlags.NonPublic
            );

            RaceModeType = GetRuntimeType("ArcadeRacing.RaceMode");
            Type raceManagerType = GetRuntimeType(
                "ArcadeRacing.RaceManager"
            );

            Type lapTimerType = GetRuntimeType(
                "ArcadeRacing.LapTimer"
            );

            GameObject systemsObject = CreateTemporaryObject(
                UniqueToken + "_Systems"
            );

            RaceManager = systemsObject.AddComponent(raceManagerType);
            LapTimer = systemsObject.AddComponent(lapTimerType);

            GameObject ghostObject = CreateTemporaryObject(
                UniqueToken + "_GhostSystem"
            );

            GhostReplaySystem = ghostObject.AddComponent(
                GhostReplaySystemType
            );

            PlayerCar = CreatePlayer(UniqueProfileId + "_Car");

            SetPrivateField(
                GhostReplaySystem,
                "raceManager",
                RaceManager
            );

            SetPrivateField(
                GhostReplaySystem,
                "lapTimer",
                LapTimer
            );

            SetPrivateField(
                GhostReplaySystem,
                "saveGhostBetweenSessions",
                true
            );

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Hidden/InternalErrorShader");

            Assert.That(shader, Is.Not.Null, "Ghost test shader");

            Material material = new Material(shader)
            {
                name = UniqueToken + "_Material",
                hideFlags = HideFlags.HideAndDontSave
            };

            createdAssets.Add(material);
            SetPrivateField(GhostReplaySystem, "ghostMaterial", material);
        }

        [TearDown]
        public void TearDownGhostFixture()
        {
            if (GhostReplaySystem != null)
            {
                InvokePrivate(
                    GhostReplaySystem,
                    "DestroyGhostVisual"
                );
            }

            foreach (string path in createdFilePaths)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            string fullTemporaryRoot = string.IsNullOrWhiteSpace(
                TemporaryRoot
            )
                ? string.Empty
                : Path.GetFullPath(TemporaryRoot);

            string systemTemporaryRoot = Path.GetFullPath(
                Path.GetTempPath()
            );

            if (
                !string.IsNullOrWhiteSpace(fullTemporaryRoot) &&
                fullTemporaryRoot.StartsWith(
                    systemTemporaryRoot,
                    StringComparison.OrdinalIgnoreCase
                ) &&
                Path.GetFileName(fullTemporaryRoot).StartsWith(
                    "R1_Ghost_",
                    StringComparison.Ordinal
                ) &&
                Directory.Exists(fullTemporaryRoot)
            )
            {
                Directory.Delete(fullTemporaryRoot, true);
            }

            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        createdObjects[i]
                    );
                }
            }

            for (int i = createdAssets.Count - 1; i >= 0; i--)
            {
                if (createdAssets[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        createdAssets[i]
                    );
                }
            }

            createdObjects.Clear();
            createdAssets.Clear();
            createdFilePaths.Clear();
        }

        protected object RaceMode(string modeName)
        {
            return Enum.Parse(RaceModeType, modeName);
        }

        protected string BuildSavePath(
            string sceneName,
            string profileId,
            string modeName
        )
        {
            return (string)InvokePrivateStatic(
                GhostReplaySystemType,
                "BuildSavePath",
                sceneName,
                profileId,
                RaceMode(modeName)
            );
        }

        protected string BuildLegacySavePath(
            string sceneName,
            string profileId
        )
        {
            return (string)InvokePrivateStatic(
                GhostReplaySystemType,
                "BuildLegacySavePath",
                sceneName,
                profileId
            );
        }

        protected void BindPlayer(
            string profileId = null,
            Component playerCar = null
        )
        {
            InvokePublic(
                GhostReplaySystem,
                "BindPlayer",
                playerCar ?? PlayerCar,
                profileId ?? UniqueProfileId
            );

            RegisterCreatedPath(
                ReadField<string>(GhostReplaySystem, "savePath")
            );

            RegisterCreatedPath(
                ReadField<string>(GhostReplaySystem, "legacySavePath")
            );
        }

        protected void SetRaceMode(string modeName)
        {
            InvokePublic(
                GhostReplaySystem,
                "SetRaceMode",
                RaceMode(modeName)
            );

            RegisterCreatedPath(
                ReadField<string>(GhostReplaySystem, "savePath")
            );

            RegisterCreatedPath(
                ReadField<string>(GhostReplaySystem, "legacySavePath")
            );
        }

        protected void SetRaceManagerMode(string modeName)
        {
            SetPrivateField(
                RaceManager,
                "<ActiveMode>k__BackingField",
                RaceMode(modeName)
            );
        }

        protected string ConfigureStorage(
            string relativePath,
            string legacyRelativePath = null
        )
        {
            string savePath = SafeTemporaryPath(relativePath);
            string legacyPath = SafeTemporaryPath(
                legacyRelativePath ??
                Path.Combine(
                    "Legacy",
                    Path.GetFileName(relativePath)
                )
            );

            SetPrivateField(GhostReplaySystem, "savePath", savePath);
            SetPrivateField(
                GhostReplaySystem,
                "legacySavePath",
                legacyPath
            );

            RegisterCreatedPath(savePath);
            RegisterCreatedPath(savePath + ".tmp");
            RegisterCreatedPath(legacyPath);
            RegisterCreatedPath(legacyPath + ".tmp");
            return savePath;
        }

        protected string CurrentSavePath =>
            ReadField<string>(GhostReplaySystem, "savePath");

        protected string CurrentLegacySavePath =>
            ReadField<string>(GhostReplaySystem, "legacySavePath");

        protected GameObject GhostRoot =>
            ReadField<GameObject>(GhostReplaySystem, "ghostRoot");

        protected bool HasBestLap =>
            ReadProperty<bool>(GhostReplaySystem, "HasBestLap");

        protected bool IsPlaying =>
            ReadProperty<bool>(GhostReplaySystem, "IsPlaying");

        protected bool IsRecording =>
            ReadProperty<bool>(GhostReplaySystem, "IsRecording");

        protected string ActiveModeName =>
            ReadProperty<object>(GhostReplaySystem, "ActiveMode")
                .ToString();

        protected float BestLapTime =>
            ReadProperty<float>(GhostReplaySystem, "BestLapTime");

        protected IList RecordingFrames =>
            ReadField<IList>(GhostReplaySystem, "recordingFrames");

        protected object BestLapData =>
            ReadField<object>(GhostReplaySystem, "bestLapData");

        protected void ClearBestLap()
        {
            SetPrivateField(GhostReplaySystem, "bestLapData", null);
        }

        protected object CreateFrame(
            float time,
            Vector3 position,
            Quaternion rotation
        )
        {
            object frame = Activator.CreateInstance(
                GhostFrameType,
                true
            );

            SetField(frame, "time", time);
            SetField(frame, "position", position);
            SetField(frame, "rotation", rotation);
            return frame;
        }

        protected object CreateLapData(
            float lapTime,
            string sceneName,
            string vehicleId,
            string modeName,
            IEnumerable<object> frames,
            int? version = null
        )
        {
            object data = Activator.CreateInstance(
                GhostLapDataType,
                true
            );

            SetField(
                data,
                "version",
                version ?? CurrentFormatVersion
            );

            SetField(data, "sceneName", sceneName);
            SetField(data, "vehicleId", vehicleId);
            SetField(data, "raceMode", modeName);
            SetField(data, "lapTime", lapTime);

            FieldInfo framesField = GhostLapDataType.GetField(
                "frames",
                InstanceMembers
            );

            IList frameList = (IList)Activator.CreateInstance(
                framesField.FieldType
            );

            if (frames != null)
            {
                foreach (object frame in frames)
                {
                    frameList.Add(frame);
                }
            }

            framesField.SetValue(data, frameList);
            return data;
        }

        protected void SaveLapData(object data)
        {
            SetPrivateField(GhostReplaySystem, "bestLapData", data);
            InvokePrivate(GhostReplaySystem, "SaveBestLap");
        }

        protected void LoadLapData()
        {
            ClearBestLap();
            InvokePrivate(GhostReplaySystem, "LoadBestLap");
        }

        protected void WriteText(string path, string content)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, content);
            RegisterCreatedPath(path);
        }

        protected string Serialize(object data)
        {
            return JsonUtility.ToJson(data, false);
        }

        protected object Deserialize(string json)
        {
            return JsonUtility.FromJson(json, GhostLapDataType);
        }

        protected IList Frames(object data)
        {
            return ReadField<IList>(data, "frames");
        }

        protected float FrameTime(object frame)
        {
            return ReadField<float>(frame, "time");
        }

        protected Vector3 FramePosition(object frame)
        {
            return ReadField<Vector3>(frame, "position");
        }

        protected Quaternion FrameRotation(object frame)
        {
            return ReadField<Quaternion>(frame, "rotation");
        }

        protected void SetPlaybackData(object data)
        {
            SetPrivateField(GhostReplaySystem, "bestLapData", data);
            SetPrivateField(GhostReplaySystem, "playbackFrameIndex", 0);
            SetPrivateField(GhostReplaySystem, "playing", true);

            Assert.That(GhostRoot, Is.Not.Null, "Ghost visual root");
            GhostRoot.SetActive(true);
        }

        protected void UpdatePlayback(float time)
        {
            InvokePrivate(GhostReplaySystem, "UpdatePlayback", time);
        }

        protected void StartRecording(int lapNumber = 1)
        {
            InvokePrivate(
                GhostReplaySystem,
                "HandleLapStarted",
                lapNumber
            );
        }

        protected void CompleteRecording(
            float lapTime,
            bool isNewBest,
            int lapNumber = 1
        )
        {
            InvokePrivate(
                GhostReplaySystem,
                "HandleLapCompleted",
                lapNumber,
                lapTime,
                isNewBest
            );
        }

        protected void RecordAt(float lapTime)
        {
            SetPrivateField(
                LapTimer,
                "<CurrentLapTime>k__BackingField",
                lapTime
            );

            InvokePrivate(GhostReplaySystem, "RecordAtCurrentTime");
        }

        protected Component CreateAdditionalPlayer(string name)
        {
            return CreatePlayer(name);
        }

        protected Mesh AddSimpleMeshToPlayer(Component playerCar)
        {
            GameObject visual = CreateTemporaryObject(
                UniqueToken + "_PlayerVisual"
            );

            visual.transform.SetParent(
                playerCar.transform,
                false
            );

            Mesh mesh = new Mesh
            {
                name = UniqueToken + "_Triangle",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up
                },
                triangles = new[] { 0, 1, 2 }
            };

            mesh.RecalculateNormals();
            createdAssets.Add(mesh);

            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            visual.AddComponent<MeshRenderer>();
            return mesh;
        }

        protected static T ReadField<T>(object target, string fieldName)
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

            return (T)field.GetValue(target);
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

        protected static void SetPrivateField(
            object target,
            string fieldName,
            object value
        )
        {
            SetField(target, fieldName, value);
        }

        protected static void SetField(
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

        protected static object InvokePublic(
            object target,
            string methodName,
            params object[] arguments
        )
        {
            return FindMethod(
                target.GetType(),
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                arguments.Length
            ).Invoke(target, arguments);
        }

        protected static object InvokePrivate(
            object target,
            string methodName,
            params object[] arguments
        )
        {
            return FindMethod(
                target.GetType(),
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic,
                arguments.Length
            ).Invoke(target, arguments);
        }

        protected static object InvokePrivateStatic(
            Type type,
            string methodName,
            params object[] arguments
        )
        {
            return FindMethod(
                type,
                methodName,
                StaticPrivate,
                arguments.Length
            ).Invoke(null, arguments);
        }

        private static MethodInfo FindMethod(
            Type type,
            string methodName,
            BindingFlags flags,
            int parameterCount
        )
        {
            MethodInfo[] methods = type.GetMethods(flags);

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
                type.FullName + "." + methodName +
                " with " + parameterCount +
                " parameters was not found."
            );

            return null;
        }

        private Component CreatePlayer(string name)
        {
            Type carControllerType = GetRuntimeType(
                "ArcadeRacing.CarController"
            );

            GameObject playerObject = CreateTemporaryObject(name);
            return playerObject.AddComponent(carControllerType);
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

        private string SafeTemporaryPath(string relativePath)
        {
            string fullRoot = Path.GetFullPath(TemporaryRoot);
            string fullPath = Path.GetFullPath(
                Path.Combine(fullRoot, relativePath)
            );

            string requiredPrefix = fullRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            ) + Path.DirectorySeparatorChar;

            Assert.That(
                fullPath.StartsWith(
                    requiredPrefix,
                    StringComparison.OrdinalIgnoreCase
                ),
                Is.True,
                "Ghost test path escaped its temporary root."
            );

            return fullPath;
        }

        private void RegisterCreatedPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string fullPath = Path.GetFullPath(path);
            string fullTemporaryRoot = Path.GetFullPath(TemporaryRoot);

            bool isTemporary = fullPath.StartsWith(
                fullTemporaryRoot,
                StringComparison.OrdinalIgnoreCase
            );

            bool isUniqueProductionPath =
                Path.GetFileName(fullPath).IndexOf(
                    UniqueToken,
                    StringComparison.OrdinalIgnoreCase
                ) >= 0;

            Assert.That(
                isTemporary || isUniqueProductionPath,
                Is.True,
                "Refusing to track a non-test ghost file: " + fullPath
            );

            createdFilePaths.Add(fullPath);
        }

        private static Type GetRuntimeType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail(fullName + " was not found in loaded assemblies.");
            return null;
        }
    }
}
