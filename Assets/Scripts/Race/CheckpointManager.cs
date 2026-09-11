using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcadeRacing
{
    public sealed class CheckpointManager : MonoBehaviour
    {
        private const int RequiredCheckpointCount = 4;

        [Header("Checkpoints")]
        [SerializeField] private Checkpoint startFinishCheckpoint;
        [SerializeField] private Checkpoint checkpoint1;
        [SerializeField] private Checkpoint checkpoint2;
        [SerializeField] private Checkpoint checkpoint3;
        [SerializeField] private Checkpoint checkpoint4;

        [Header("Race")]
        [SerializeField, Min(1)] private int totalLaps = 3;
        [SerializeField] private RaceManager raceManager;
        [SerializeField] private LapTimer lapTimer;

        private Checkpoint[] lapCheckpoints = Array.Empty<Checkpoint>();
        private bool raceActive;
        private bool initialized;
        private bool waitingForFinish;
        private int nextCheckpointIndex;

        public int CurrentLap { get; private set; } = 1;
        public int TotalLaps => totalLaps;
        public int CheckpointCount => RequiredCheckpointCount;
        public int CompletedCheckpointCount => Mathf.Clamp(
            nextCheckpointIndex,
            0,
            RequiredCheckpointCount
        );

        public int ExpectedCheckpointNumber =>
            waitingForFinish ? 0 : nextCheckpointIndex + 1;

        public bool WaitingForFinish => waitingForFinish;
        public bool RaceActive => raceActive;
        public bool IsInitialized => initialized;

        public event Action<int, int> CheckpointPassed;

        private void Reset()
        {
            ResolveMissingReferences();
        }

        private void OnValidate()
        {
            totalLaps = Mathf.Max(1, totalLaps);
            ResolveMissingReferences();
            lapCheckpoints = CreateLapCheckpointArray();
            ValidateReferences();
        }

        private void Awake()
        {
            initialized = InitializeManager();
            ResetProgress();
        }

        public void BeginRace()
        {
            if (!EnsureInitialized())
            {
                raceActive = false;
                RefreshCheckpointVisuals();
                return;
            }

            CurrentLap = 1;
            nextCheckpointIndex = 0;
            waitingForFinish = false;
            raceActive = true;

            RefreshCheckpointVisuals();
        }

        public void EndRace()
        {
            raceActive = false;
            RefreshCheckpointVisuals();
        }

        public void ResetProgress()
        {
            CurrentLap = 1;
            nextCheckpointIndex = 0;
            waitingForFinish = false;
            raceActive = false;

            RefreshCheckpointVisuals();
        }

        public void TryPassCheckpoint(
            Checkpoint checkpoint,
            CarController carController
        )
        {
            if (
                !EnsureInitialized() ||
                !raceActive ||
                checkpoint == null ||
                carController == null ||
                carController != raceManager.PlayerCar
            )
            {
                return;
            }

            if (checkpoint == startFinishCheckpoint)
            {
                TryCompleteLap(checkpoint, carController);
                return;
            }

            if (
                waitingForFinish ||
                nextCheckpointIndex >= lapCheckpoints.Length
            )
            {
                return;
            }

            Checkpoint expectedCheckpoint =
                lapCheckpoints[nextCheckpointIndex];

            if (checkpoint != expectedCheckpoint)
            {
                return;
            }

            carController.SetRespawnPose(
                checkpoint.RespawnPosition,
                checkpoint.RespawnRotation
            );

            nextCheckpointIndex++;
            waitingForFinish =
                nextCheckpointIndex >= lapCheckpoints.Length;

            RefreshCheckpointVisuals();
            CheckpointPassed?.Invoke(
                CompletedCheckpointCount,
                CheckpointCount
            );
        }

        private void TryCompleteLap(
            Checkpoint checkpoint,
            CarController carController
        )
        {
            if (
                !waitingForFinish ||
                nextCheckpointIndex < RequiredCheckpointCount
            )
            {
                return;
            }

            carController.SetRespawnPose(
                checkpoint.RespawnPosition,
                checkpoint.RespawnRotation
            );

            lapTimer.CompleteLap();

            bool timeTrialFinished =
                raceManager.ActiveMode == RaceMode.TimeTrial &&
                CurrentLap >= totalLaps;

            if (timeTrialFinished)
            {
                raceActive = false;
                waitingForFinish = false;
                RefreshCheckpointVisuals();
                raceManager.FinishRace();
                return;
            }

            CurrentLap++;
            nextCheckpointIndex = 0;
            waitingForFinish = false;
            lapTimer.BeginNextLap();

            RefreshCheckpointVisuals();
        }

        private bool EnsureInitialized()
        {
            if (initialized)
            {
                return true;
            }

            initialized = InitializeManager();
            return initialized;
        }

        private bool InitializeManager()
        {
            ResolveMissingReferences();

            lapCheckpoints = CreateLapCheckpointArray();

            if (!ValidateReferences())
            {
                return false;
            }

            startFinishCheckpoint.Initialize(this, -1, true);

            for (int i = 0; i < lapCheckpoints.Length; i++)
            {
                lapCheckpoints[i].Initialize(this, i, false);
            }

            return true;
        }

        private Checkpoint[] CreateLapCheckpointArray()
        {
            return new[]
            {
                checkpoint1,
                checkpoint2,
                checkpoint3,
                checkpoint4
            };
        }

        private void RefreshCheckpointVisuals()
        {
            if (startFinishCheckpoint != null)
            {
                startFinishCheckpoint.SetExpected(
                    initialized &&
                    raceActive &&
                    waitingForFinish
                );
            }

            if (lapCheckpoints == null)
            {
                return;
            }

            for (int i = 0; i < lapCheckpoints.Length; i++)
            {
                Checkpoint currentCheckpoint = lapCheckpoints[i];

                if (currentCheckpoint == null)
                {
                    continue;
                }

                currentCheckpoint.SetExpected(
                    initialized &&
                    raceActive &&
                    !waitingForFinish &&
                    i == nextCheckpointIndex
                );
            }
        }

        private void ResolveMissingReferences()
        {
            Checkpoint[] childCheckpoints =
                GetComponentsInChildren<Checkpoint>(true);

            if (childCheckpoints.Length == 0)
            {
                childCheckpoints = FindObjectsByType<Checkpoint>(
                    FindObjectsInactive.Include
                );
            }

            if (startFinishCheckpoint == null)
            {
                startFinishCheckpoint = FindCheckpointByName(
                    childCheckpoints,
                    "Checkpoint_00_StartFinish",
                    "Checkpoint_00_Finish",
                    "Checkpoint_00",
                    "StartFinish"
                );
            }

            if (checkpoint1 == null)
            {
                checkpoint1 = FindCheckpointByName(
                    childCheckpoints,
                    "Checkpoint_01",
                    "Checkpoint"
                );
            }

            if (checkpoint2 == null)
            {
                checkpoint2 = FindCheckpointByName(
                    childCheckpoints,
                    "Checkpoint_02",
                    "Checkpoint (1)"
                );
            }

            if (checkpoint3 == null)
            {
                checkpoint3 = FindCheckpointByName(
                    childCheckpoints,
                    "Checkpoint_03",
                    "Checkpoint (2)"
                );
            }

            if (checkpoint4 == null)
            {
                checkpoint4 = FindCheckpointByName(
                    childCheckpoints,
                    "Checkpoint_04",
                    "Checkpoint (3)"
                );
            }
        }

        private static Checkpoint FindCheckpointByName(
            Checkpoint[] source,
            params string[] acceptedNames
        )
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null)
                {
                    continue;
                }

                for (int j = 0; j < acceptedNames.Length; j++)
                {
                    if (
                        string.Equals(
                            source[i].gameObject.name,
                            acceptedNames[j],
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        return source[i];
                    }
                }
            }

            return null;
        }

        private bool ValidateReferences()
        {
            List<string> missingReferences = new List<string>();

            if (startFinishCheckpoint == null)
            {
                missingReferences.Add("Start Finish Checkpoint");
            }

            if (checkpoint1 == null) missingReferences.Add("Checkpoint 1");
            if (checkpoint2 == null) missingReferences.Add("Checkpoint 2");
            if (checkpoint3 == null) missingReferences.Add("Checkpoint 3");
            if (checkpoint4 == null) missingReferences.Add("Checkpoint 4");
            if (raceManager == null) missingReferences.Add("Race Manager");
            if (lapTimer == null) missingReferences.Add("Lap Timer");

            if (missingReferences.Count > 0)
            {
                Debug.LogError(
                    "CheckpointManager missing references: " +
                    string.Join(", ", missingReferences),
                    this
                );

                return false;
            }

            HashSet<Checkpoint> uniqueCheckpoints =
                new HashSet<Checkpoint> { startFinishCheckpoint };

            for (int i = 0; i < lapCheckpoints.Length; i++)
            {
                if (!uniqueCheckpoints.Add(lapCheckpoints[i]))
                {
                    Debug.LogError(
                        "The same Checkpoint is assigned more than once.",
                        this
                    );

                    return false;
                }
            }

#if UNITY_EDITOR
            WarnIfMissingRespawn(
                startFinishCheckpoint,
                "Start Finish Checkpoint"
            );

            for (int i = 0; i < lapCheckpoints.Length; i++)
            {
                WarnIfMissingRespawn(
                    lapCheckpoints[i],
                    $"Checkpoint {i + 1}"
                );
            }
#endif

            return true;
        }

#if UNITY_EDITOR
        private static void WarnIfMissingRespawn(
            Checkpoint checkpoint,
            string label
        )
        {
            if (checkpoint == null)
            {
                return;
            }

            UnityEditor.SerializedObject serializedCheckpoint =
                new UnityEditor.SerializedObject(checkpoint);

            UnityEditor.SerializedProperty respawnProperty =
                serializedCheckpoint.FindProperty("respawnPoint");

            if (
                respawnProperty != null &&
                respawnProperty.objectReferenceValue != null
            )
            {
                return;
            }

            Debug.LogWarning(
                $"{label} has no respawn point. " +
                "The checkpoint transform will be used as a fallback.",
                checkpoint
            );
        }
#endif
    }
}
