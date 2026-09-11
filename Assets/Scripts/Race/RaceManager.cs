using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcadeRacing
{
    public enum RaceState
    {
        Waiting,
        Countdown,
        Racing,
        Finished
    }

    public sealed class RaceManager : MonoBehaviour
    {
        [Header("Runtime")]
        [SerializeField] private CarController playerCar;
        [SerializeField] private CheckpointManager checkpointManager;
        [SerializeField] private LapTimer lapTimer;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private DriftScoreManager driftScoreManager;
        [SerializeField] private GhostReplaySystem ghostReplaySystem;

        [Header("Race Settings")]
        [SerializeField] private bool startAutomatically = true;
        [SerializeField, Min(1)] private int countdownSeconds = 3;
        [SerializeField, Min(0)] private int baseRaceReward = 1000;
        [SerializeField, Min(0)] private int rewardPerLap = 250;

        [Header("Mode Selection")]
        [SerializeField] private bool requireModeSelection = true;
        [SerializeField] private RaceMode defaultMode = RaceMode.TimeTrial;
        [SerializeField, Min(10f)] private float driftChallengeDuration = 90f;
        [SerializeField, Min(0.01f)] private float driftPointsPerCash = 10f;

        private Coroutine countdownRoutine;
        private bool controlWasEnabledBeforePause;
        private bool modeSelected;

        public RaceState State { get; private set; } = RaceState.Waiting;
        public RaceMode ActiveMode { get; private set; } = RaceMode.TimeTrial;
        public int LastReward { get; private set; }
        public int LastDriftScore { get; private set; }
        public bool LastResultWasRecord { get; private set; }
        public bool IsRacing => State == RaceState.Racing;
        public bool IsPaused { get; private set; }
        public bool HasModeSelection => !requireModeSelection || modeSelected;
        public CarController PlayerCar => playerCar;

        public float ChallengeTimeRemaining =>
            ActiveMode == RaceMode.DriftChallenge
                ? Mathf.Max(0f, driftChallengeDuration - lapTimer.RaceTime)
                : 0f;

        private void OnValidate()
        {
            countdownSeconds = Mathf.Max(1, countdownSeconds);
            driftChallengeDuration = Mathf.Max(10f, driftChallengeDuration);
            driftPointsPerCash = Mathf.Max(0.01f, driftPointsPerCash);
        }

        private void Start()
        {
            ValidateCoreReferences();

            ActiveMode = defaultMode;
            modeSelected = !requireModeSelection;

            checkpointManager.ResetProgress();
            lapTimer.ResetTimers();
            driftScoreManager.ResetRun();
            ghostReplaySystem.StopReplay();

            uiManager.HideCountdown();
            uiManager.HideResults();
            uiManager.HideRaceModeSelection();

            if (playerCar != null)
            {
                playerCar.SetControlEnabled(false);
            }

            bool canAutoStart =
                startAutomatically &&
                playerCar != null &&
                playerCar.gameObject.activeInHierarchy;

            if (canAutoStart)
            {
                RequestRaceStart();
            }
            else
            {
                State = RaceState.Waiting;
            }
        }

        private void Update()
        {
            if (
                State == RaceState.Racing &&
                ActiveMode == RaceMode.DriftChallenge &&
                ChallengeTimeRemaining <= 0f
            )
            {
                FinishRace();
            }
        }

        public void SetPlayerCar(
            CarController newPlayerCar,
            string vehicleProfileId = null
        )
        {
            if (playerCar != null && playerCar != newPlayerCar)
            {
                playerCar.SetControlEnabled(false);
            }

            playerCar = newPlayerCar;
            driftScoreManager?.BindVehicle(playerCar);

            if (ghostReplaySystem != null)
            {
                ghostReplaySystem.BindPlayer(
                    playerCar,
                    vehicleProfileId
                );
            }

            if (playerCar != null)
            {
                playerCar.SetControlEnabled(false);
            }

            State = RaceState.Waiting;
            IsPaused = false;
            controlWasEnabledBeforePause = false;
        }

        public void RequestRaceStart()
        {
            if (playerCar == null || !playerCar.gameObject.activeInHierarchy)
            {
                Debug.LogError(
                    "RaceManager cannot start because no active player vehicle is selected.",
                    this
                );
                return;
            }

            if (requireModeSelection && !modeSelected)
            {
                uiManager.ShowRaceModeSelection();
                return;
            }

            StartRaceSequence();
        }

        public void SelectTimeTrialMode()
        {
            SelectMode(RaceMode.TimeTrial);
        }

        public void SelectDriftChallengeMode()
        {
            SelectMode(RaceMode.DriftChallenge);
        }

        public void SelectMode(RaceMode mode)
        {
            if (State == RaceState.Countdown || State == RaceState.Racing)
            {
                return;
            }

            ActiveMode = mode;
            modeSelected = true;
            uiManager.HideRaceModeSelection();

            if (playerCar != null && playerCar.gameObject.activeInHierarchy)
            {
                StartRaceSequence();
            }
        }

        public void StartRaceSequence()
        {
            if (playerCar == null || !playerCar.gameObject.activeInHierarchy)
            {
                Debug.LogError(
                    "RaceManager cannot start because no active player vehicle is selected.",
                    this
                );
                return;
            }

            if (requireModeSelection && !modeSelected)
            {
                uiManager.ShowRaceModeSelection();
                return;
            }

            ghostReplaySystem.SetRaceMode(ActiveMode);

            if (countdownRoutine != null)
            {
                StopCoroutine(countdownRoutine);
            }

            IsPaused = false;
            countdownRoutine = StartCoroutine(CountdownRoutine());
        }

        private IEnumerator CountdownRoutine()
        {
            State = RaceState.Countdown;
            playerCar.SetControlEnabled(false);
            checkpointManager.ResetProgress();
            lapTimer.ResetTimers();
            driftScoreManager.ResetRun();
            ghostReplaySystem.StopReplay();

            uiManager.HideResults();
            uiManager.HideRaceModeSelection();
            uiManager.ShowHud();

            for (int i = countdownSeconds; i > 0; i--)
            {
                uiManager.ShowCountdown(i.ToString());
                yield return new WaitForSeconds(1f);
            }

            uiManager.ShowCountdown("GO!");
            State = RaceState.Racing;

            checkpointManager.BeginRace();
            driftScoreManager.BeginRun();
            lapTimer.BeginRace(ghostReplaySystem.BestLapTime);
            playerCar.SetControlEnabled(true);

            yield return new WaitForSeconds(0.75f);

            uiManager.HideCountdown();
            countdownRoutine = null;
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused)
            {
                return;
            }

            IsPaused = paused;

            if (playerCar == null)
            {
                return;
            }

            if (paused)
            {
                controlWasEnabledBeforePause = playerCar.ControlEnabled;
                playerCar.SetControlEnabled(false);
                return;
            }

            bool shouldRestoreControl =
                State == RaceState.Racing &&
                controlWasEnabledBeforePause;

            playerCar.SetControlEnabled(shouldRestoreControl);
            controlWasEnabledBeforePause = false;
        }

        public void FinishRace()
        {
            if (State != RaceState.Racing)
            {
                return;
            }

            State = RaceState.Finished;
            IsPaused = false;

            if (playerCar != null)
            {
                playerCar.SetControlEnabled(false);
            }

            checkpointManager.EndRace();
            lapTimer.StopRace();
            ghostReplaySystem.StopReplay();

            LastDriftScore = driftScoreManager.EndRun();

            if (ActiveMode == RaceMode.TimeTrial)
            {
                LastReward =
                    baseRaceReward +
                    checkpointManager.TotalLaps * rewardPerLap;

                LastResultWasRecord =
                    saveManager.TrySetBestTimeTrial(lapTimer.RaceTime);
            }
            else
            {
                LastReward =
                    baseRaceReward +
                    Mathf.RoundToInt(
                        LastDriftScore / driftPointsPerCash
                    );

                LastResultWasRecord =
                    saveManager.TrySetBestDriftScore(LastDriftScore);
            }

            saveManager.AddCash(LastReward);
            saveManager.Save();

            uiManager.ShowResults(
                ActiveMode,
                lapTimer.RaceTime,
                lapTimer.BestLapTime,
                LastDriftScore,
                LastResultWasRecord,
                LastReward,
                saveManager.Cash
            );
        }

        public void RestartRace()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void QuitApplication()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Application.Quit();
        }

        private void ValidateCoreReferences()
        {
            if (
                checkpointManager == null ||
                lapTimer == null ||
                saveManager == null ||
                uiManager == null ||
                driftScoreManager == null ||
                ghostReplaySystem == null
            )
            {
                throw new MissingReferenceException(
                    "RaceManager requires CheckpointManager, LapTimer, " +
                    "SaveManager, UIManager, DriftScoreManager and " +
                    "GhostReplaySystem references. Verify the City " +
                    "scene setup."
                );
            }
        }
    }
}
