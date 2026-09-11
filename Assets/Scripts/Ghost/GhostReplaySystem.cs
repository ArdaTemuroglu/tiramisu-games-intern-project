using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ArcadeRacing
{
    public sealed class GhostReplaySystem : MonoBehaviour
    {
        [Serializable]
        private sealed class GhostFrame
        {
            public float time;
            public Vector3 position;
            public Quaternion rotation;
        }

        [Serializable]
        private sealed class GhostLapData
        {
            public int version = 2;
            public string sceneName;
            public string vehicleId;
            public string raceMode;
            public float lapTime;
            public List<GhostFrame> frames = new List<GhostFrame>();
        }

        [Header("Runtime")]
        [SerializeField] private RaceManager raceManager;
        [SerializeField] private LapTimer lapTimer;

        [Header("Recording")]
        [SerializeField, Range(0.02f, 0.25f)] private float sampleInterval = 0.05f;
        [SerializeField] private bool saveGhostBetweenSessions = true;

        [Header("Appearance")]
        [SerializeField] private Material ghostMaterial;
        [SerializeField] private Color ghostColor = new Color(0.1f, 0.8f, 1f, 0.35f);
        [SerializeField] private bool showGhostInDriftChallenge = true;

        private readonly List<GhostFrame> recordingFrames =
            new List<GhostFrame>();

        private readonly List<Mesh> generatedMeshes = new List<Mesh>();

        private CarController playerCar;
        private GameObject ghostRoot;
        private Material runtimeGhostMaterial;
        private GhostLapData bestLapData;
        private string vehicleId;
        private string savePath;
        private string legacySavePath;
        private RaceMode activeMode = RaceMode.TimeTrial;
        private float nextSampleTime;
        private int playbackFrameIndex;
        private bool recording;
        private bool playing;

        public bool HasBestLap =>
            bestLapData != null &&
            bestLapData.frames != null &&
            bestLapData.frames.Count >= 2;

        public float BestLapTime => HasBestLap
            ? Mathf.Max(0f, bestLapData.lapTime)
            : 0f;

        public bool IsPlaying => playing;
        public bool IsRecording => recording;
        public RaceMode ActiveMode => activeMode;

        private void Reset()
        {
            raceManager = FindAnyObjectByType<RaceManager>();
            lapTimer = FindAnyObjectByType<LapTimer>();
        }

        private void OnEnable()
        {
            SubscribeToLapTimer();
        }

        private void OnDisable()
        {
            UnsubscribeFromLapTimer();
            StopReplay();
        }

        private void OnDestroy()
        {
            DestroyGhostVisual();

            if (runtimeGhostMaterial != null)
            {
                Destroy(runtimeGhostMaterial);
            }

            for (int i = 0; i < generatedMeshes.Count; i++)
            {
                if (generatedMeshes[i] != null)
                {
                    Destroy(generatedMeshes[i]);
                }
            }
        }

        private void Update()
        {
            if (
                raceManager == null ||
                lapTimer == null ||
                !raceManager.IsRacing ||
                raceManager.IsPaused
            )
            {
                return;
            }

            if (recording)
            {
                RecordAtCurrentTime();
            }

            if (playing)
            {
                UpdatePlayback(lapTimer.CurrentLapTime);
            }
        }

        public void BindPlayer(
            CarController newPlayerCar,
            string profileId = null
        )
        {
            StopReplay();
            DestroyGhostVisual();

            playerCar = newPlayerCar;
            bestLapData = null;

            if (playerCar == null)
            {
                vehicleId = string.Empty;
                savePath = string.Empty;
                legacySavePath = string.Empty;
                return;
            }

            vehicleId = string.IsNullOrWhiteSpace(profileId)
                ? playerCar.gameObject.name
                : profileId.Trim();

            if (
                raceManager != null &&
                IsSupportedRaceMode(raceManager.ActiveMode)
            )
            {
                activeMode = raceManager.ActiveMode;
            }

            RefreshStorageContext();
            LoadBestLap();
            BuildGhostVisual();
        }

        public void SetRaceMode(RaceMode mode)
        {
            if (!IsSupportedRaceMode(mode))
            {
                Debug.LogWarning(
                    $"Unsupported ghost race mode '{mode}'. Falling back to Time Trial.",
                    this
                );

                mode = RaceMode.TimeTrial;
            }

            if (
                activeMode == mode &&
                !string.IsNullOrWhiteSpace(savePath)
            )
            {
                return;
            }

            StopReplay();
            activeMode = mode;
            bestLapData = null;

            RefreshStorageContext();
            LoadBestLap();
        }

        public void StopReplay()
        {
            recording = false;
            playing = false;
            recordingFrames.Clear();

            if (ghostRoot != null)
            {
                ghostRoot.SetActive(false);
            }
        }

        [ContextMenu("Delete Best Ghost For This Vehicle")]
        public void DeleteBestGhost()
        {
            StopReplay();
            bestLapData = null;

            if (
                !string.IsNullOrWhiteSpace(savePath) &&
                File.Exists(savePath)
            )
            {
                File.Delete(savePath);
            }

            if (
                activeMode == RaceMode.TimeTrial &&
                !string.IsNullOrWhiteSpace(legacySavePath) &&
                File.Exists(legacySavePath)
            )
            {
                File.Delete(legacySavePath);
            }
        }

        private void SubscribeToLapTimer()
        {
            if (lapTimer == null)
            {
                return;
            }

            lapTimer.LapStarted -= HandleLapStarted;
            lapTimer.LapCompleted -= HandleLapCompleted;
            lapTimer.RaceStopped -= StopReplay;

            lapTimer.LapStarted += HandleLapStarted;
            lapTimer.LapCompleted += HandleLapCompleted;
            lapTimer.RaceStopped += StopReplay;
        }

        private void UnsubscribeFromLapTimer()
        {
            if (lapTimer == null)
            {
                return;
            }

            lapTimer.LapStarted -= HandleLapStarted;
            lapTimer.LapCompleted -= HandleLapCompleted;
            lapTimer.RaceStopped -= StopReplay;
        }

        private void HandleLapStarted(int lapNumber)
        {
            if (playerCar == null)
            {
                StopReplay();
                return;
            }

            recordingFrames.Clear();
            nextSampleTime = sampleInterval;
            recording = true;
            CaptureFrame(0f);

            bool allowedInMode =
                raceManager.ActiveMode == RaceMode.TimeTrial ||
                showGhostInDriftChallenge;

            playing =
                allowedInMode &&
                HasBestLap &&
                ghostRoot != null;

            playbackFrameIndex = 0;

            if (ghostRoot != null)
            {
                ghostRoot.SetActive(playing);
            }

            if (playing)
            {
                ApplyFrame(bestLapData.frames[0]);
            }
        }

        private void HandleLapCompleted(
            int lapNumber,
            float lapTime,
            bool isNewBest
        )
        {
            if (!recording)
            {
                return;
            }

            CaptureFrame(lapTime);
            recording = false;
            playing = false;

            if (ghostRoot != null)
            {
                ghostRoot.SetActive(false);
            }

            if (
                !isNewBest ||
                lapTime <= 0f ||
                recordingFrames.Count < 2
            )
            {
                return;
            }

            bestLapData = new GhostLapData
            {
                version = 2,
                sceneName = SceneManager.GetActiveScene().name,
                vehicleId = vehicleId,
                raceMode = activeMode.ToString(),
                lapTime = lapTime,
                frames = new List<GhostFrame>(recordingFrames)
            };

            if (saveGhostBetweenSessions)
            {
                SaveBestLap();
            }
        }

        private void RecordAtCurrentTime()
        {
            float lapTime = lapTimer.CurrentLapTime;

            if (lapTime + 0.0001f < nextSampleTime)
            {
                return;
            }

            CaptureFrame(lapTime);
            nextSampleTime = lapTime + sampleInterval;
        }

        private void CaptureFrame(float time)
        {
            if (playerCar == null)
            {
                return;
            }

            recordingFrames.Add(
                new GhostFrame
                {
                    time = Mathf.Max(0f, time),
                    position = playerCar.transform.position,
                    rotation = playerCar.transform.rotation
                }
            );
        }

        private void UpdatePlayback(float playbackTime)
        {
            if (!HasBestLap || ghostRoot == null)
            {
                StopReplay();
                return;
            }

            List<GhostFrame> frames = bestLapData.frames;

            while (
                playbackFrameIndex < frames.Count - 2 &&
                frames[playbackFrameIndex + 1].time <= playbackTime
            )
            {
                playbackFrameIndex++;
            }

            if (playbackTime >= frames[frames.Count - 1].time)
            {
                playing = false;
                ghostRoot.SetActive(false);
                return;
            }

            GhostFrame from = frames[playbackFrameIndex];
            GhostFrame to = frames[playbackFrameIndex + 1];

            float blend = Mathf.InverseLerp(
                from.time,
                to.time,
                playbackTime
            );

            ghostRoot.transform.SetPositionAndRotation(
                Vector3.Lerp(from.position, to.position, blend),
                Quaternion.Slerp(from.rotation, to.rotation, blend)
            );
        }

        private void ApplyFrame(GhostFrame frame)
        {
            ghostRoot.transform.SetPositionAndRotation(
                frame.position,
                frame.rotation
            );
        }

        private void BuildGhostVisual()
        {
            if (playerCar == null)
            {
                return;
            }

            ghostRoot = new GameObject(
                $"Ghost_{SanitizeFileName(vehicleId)}"
            );

            ghostRoot.transform.SetPositionAndRotation(
                playerCar.transform.position,
                playerCar.transform.rotation
            );

            ghostRoot.transform.localScale = Vector3.one;
            Material material = GetGhostMaterial();

            MeshRenderer[] meshRenderers =
                playerCar.GetComponentsInChildren<MeshRenderer>(true);

            for (int i = 0; i < meshRenderers.Length; i++)
            {
                MeshFilter sourceFilter =
                    meshRenderers[i].GetComponent<MeshFilter>();

                if (
                    sourceFilter == null ||
                    sourceFilter.sharedMesh == null ||
                    !meshRenderers[i].gameObject.activeSelf
                )
                {
                    continue;
                }

                GameObject visual = CreateVisualObject(
                    meshRenderers[i].transform,
                    sourceFilter.sharedMesh,
                    material
                );

                visual.name = $"Ghost_{meshRenderers[i].name}";
            }

            SkinnedMeshRenderer[] skinnedRenderers =
                playerCar.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                if (!skinnedRenderers[i].gameObject.activeSelf)
                {
                    continue;
                }

                Mesh bakedMesh = new Mesh
                {
                    name = $"GhostMesh_{skinnedRenderers[i].name}"
                };

                skinnedRenderers[i].BakeMesh(bakedMesh);
                generatedMeshes.Add(bakedMesh);

                GameObject visual = CreateVisualObject(
                    skinnedRenderers[i].transform,
                    bakedMesh,
                    material
                );

                visual.name = $"Ghost_{skinnedRenderers[i].name}";
            }

            ghostRoot.SetActive(false);
        }

        private GameObject CreateVisualObject(
            Transform source,
            Mesh mesh,
            Material material
        )
        {
            GameObject visual = new GameObject();
            visual.transform.SetParent(ghostRoot.transform, false);

            visual.transform.localPosition =
                playerCar.transform.InverseTransformPoint(source.position);

            visual.transform.localRotation =
                Quaternion.Inverse(playerCar.transform.rotation) *
                source.rotation;

            Vector3 rootScale = playerCar.transform.lossyScale;
            Vector3 sourceScale = source.lossyScale;

            visual.transform.localScale = new Vector3(
                SafeDivide(sourceScale.x, rootScale.x),
                SafeDivide(sourceScale.y, rootScale.y),
                SafeDivide(sourceScale.z, rootScale.z)
            );

            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            return visual;
        }

        private Material GetGhostMaterial()
        {
            if (ghostMaterial != null)
            {
                return ghostMaterial;
            }

            if (runtimeGhostMaterial != null)
            {
                return runtimeGhostMaterial;
            }

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color");

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            runtimeGhostMaterial = new Material(shader)
            {
                name = "Runtime Ghost Material",
                color = ghostColor,
                renderQueue = (int)RenderQueue.Transparent
            };

            runtimeGhostMaterial.SetOverrideTag(
                "RenderType",
                "Transparent"
            );

            SetMaterialFloat("_Surface", 1f);
            SetMaterialFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            SetMaterialFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetMaterialFloat("_ZWrite", 0f);

            runtimeGhostMaterial.DisableKeyword("_ALPHATEST_ON");
            runtimeGhostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            if (runtimeGhostMaterial.HasProperty("_BaseColor"))
            {
                runtimeGhostMaterial.SetColor("_BaseColor", ghostColor);
            }

            return runtimeGhostMaterial;
        }

        private void SetMaterialFloat(string propertyName, float value)
        {
            if (runtimeGhostMaterial.HasProperty(propertyName))
            {
                runtimeGhostMaterial.SetFloat(propertyName, value);
            }
        }

        private void DestroyGhostVisual()
        {
            if (ghostRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(ghostRoot);
            }
            else
            {
                DestroyImmediate(ghostRoot);
            }

            ghostRoot = null;
        }

        private void LoadBestLap()
        {
            if (
                !saveGhostBetweenSessions ||
                string.IsNullOrWhiteSpace(savePath)
            )
            {
                return;
            }

            string pathToLoad = File.Exists(savePath)
                ? savePath
                : GetLegacyPathToLoad();

            if (string.IsNullOrWhiteSpace(pathToLoad))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(pathToLoad);
                GhostLapData loaded =
                    JsonUtility.FromJson<GhostLapData>(json);

                if (
                    loaded != null &&
                    loaded.frames != null &&
                    loaded.frames.Count >= 2 &&
                    loaded.lapTime > 0f &&
                    IsMatchingMode(loaded)
                )
                {
                    bestLapData = loaded;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Ghost data could not be loaded: {exception.Message}",
                    this
                );
            }
        }

        private string GetLegacyPathToLoad()
        {
            if (
                activeMode != RaceMode.TimeTrial ||
                string.IsNullOrWhiteSpace(legacySavePath) ||
                !File.Exists(legacySavePath)
            )
            {
                return string.Empty;
            }

            return legacySavePath;
        }

        private bool IsMatchingMode(GhostLapData loaded)
        {
            if (string.IsNullOrWhiteSpace(loaded.raceMode))
            {
                return activeMode == RaceMode.TimeTrial;
            }

            return string.Equals(
                loaded.raceMode,
                activeMode.ToString(),
                StringComparison.Ordinal
            );
        }

        private void SaveBestLap()
        {
            if (bestLapData == null || string.IsNullOrWhiteSpace(savePath))
            {
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(savePath);

                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(bestLapData, false);
                string temporaryPath = savePath + ".tmp";

                File.WriteAllText(temporaryPath, json);

                if (File.Exists(savePath))
                {
                    File.Delete(savePath);
                }

                File.Move(temporaryPath, savePath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Ghost data could not be saved: {exception.Message}",
                    this
                );
            }
        }

        private void RefreshStorageContext()
        {
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                savePath = string.Empty;
                legacySavePath = string.Empty;
                return;
            }

            string sceneName = SceneManager.GetActiveScene().name;
            savePath = BuildSavePath(sceneName, vehicleId, activeMode);
            legacySavePath = BuildLegacySavePath(sceneName, vehicleId);
        }

        private static string BuildSavePath(
            string sceneName,
            string profileId,
            RaceMode mode
        )
        {
            string fileName =
                $"{SanitizeFileName(sceneName)}_" +
                $"{SanitizeFileName(profileId)}_" +
                $"{SanitizeFileName(mode.ToString())}.json";

            return Path.Combine(
                Application.persistentDataPath,
                "Ghosts",
                fileName
            );
        }

        private static string BuildLegacySavePath(
            string sceneName,
            string profileId
        )
        {
            string fileName =
                $"{SanitizeFileName(sceneName)}_" +
                $"{SanitizeFileName(profileId)}.json";

            return Path.Combine(
                Application.persistentDataPath,
                "Ghosts",
                fileName
            );
        }

        private static bool IsSupportedRaceMode(RaceMode mode)
        {
            return
                mode == RaceMode.TimeTrial ||
                mode == RaceMode.DriftChallenge;
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Vehicle";
            }

            char[] invalidCharacters = Path.GetInvalidFileNameChars();

            for (int i = 0; i < invalidCharacters.Length; i++)
            {
                value = value.Replace(invalidCharacters[i], '_');
            }

            return value.Replace(' ', '_');
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) > 0.0001f
                ? value / divisor
                : value;
        }
    }
}
