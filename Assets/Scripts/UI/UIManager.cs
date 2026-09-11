using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcadeRacing
{
    public sealed class UIManager : MonoBehaviour
    {
        [Header("Runtime")]
        [SerializeField] private CarController carController;
        [SerializeField] private NitroSystem nitroSystem;
        [SerializeField] private CheckpointManager checkpointManager;
        [SerializeField] private LapTimer lapTimer;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private UpgradeManager upgradeManager;
        [SerializeField] private RaceManager raceManager;
        [SerializeField] private DriftScoreManager driftScoreManager;
        [SerializeField] private GhostReplaySystem ghostReplaySystem;

        [Header("Vehicle Selection")]
        [SerializeField] private GameObject vehicleSelectionPanel;
        [SerializeField] private TextMeshProUGUI selectedVehicleText;

        [Header("Race Mode Selection")]
        [SerializeField] private GameObject raceModeSelectionPanel;
        [SerializeField] private TextMeshProUGUI modeSelectionDescriptionText;

        [Header("HUD - Existing")]
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private TextMeshProUGUI speedText;
        [SerializeField] private TextMeshProUGUI raceTimeText;
        [SerializeField] private TextMeshProUGUI lapTimeText;
        [SerializeField] private TextMeshProUGUI bestLapText;
        [SerializeField] private TextMeshProUGUI lapText;
        [SerializeField] private TextMeshProUGUI checkpointText;
        [SerializeField] private Slider nitroSlider;
        [SerializeField] private TextMeshProUGUI countdownText;

        [Header("HUD - Layout Groups")]
        [SerializeField] private GameObject timeTrialHudGroup;
        [SerializeField] private GameObject driftHudGroup;

        [Header("HUD - Race Feedback")]
        [SerializeField] private CanvasGroup countdownCanvasGroup;
        [SerializeField] private CanvasGroup raceFeedbackCanvasGroup;
        [SerializeField] private TextMeshProUGUI raceFeedbackText;

        [Header("HUD - New Modes")]
        [SerializeField] private TextMeshProUGUI modeText;
        [SerializeField] private TextMeshProUGUI challengeTimeText;
        [SerializeField] private TextMeshProUGUI driftScoreText;
        [SerializeField] private TextMeshProUGUI driftComboText;
        [SerializeField] private TextMeshProUGUI driftAngleText;
        [SerializeField] private TextMeshProUGUI ghostStatusText;

        [Header("Pause")]
        [SerializeField] private GameObject pausePanel;

        [Header("Result - Existing")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TextMeshProUGUI resultRaceTimeText;
        [SerializeField] private TextMeshProUGUI resultBestLapText;
        [SerializeField] private TextMeshProUGUI resultRewardText;
        [SerializeField] private TextMeshProUGUI resultCashText;

        [Header("Result - New Modes")]
        [SerializeField] private TextMeshProUGUI resultModeText;
        [SerializeField] private TextMeshProUGUI resultDriftScoreText;
        [SerializeField] private TextMeshProUGUI resultRecordText;

        [Header("Garage")]
        [SerializeField] private GameObject garagePanel;
        [SerializeField] private TextMeshProUGUI cashText;
        [SerializeField] private TextMeshProUGUI engineLevelText;
        [SerializeField] private TextMeshProUGUI tiresLevelText;
        [SerializeField] private TextMeshProUGUI nitroLevelText;
        [SerializeField] private TextMeshProUGUI engineCostText;
        [SerializeField] private TextMeshProUGUI tiresCostText;
        [SerializeField] private TextMeshProUGUI nitroCostText;
        [SerializeField] private Button engineUpgradeButton;
        [SerializeField] private Button tiresUpgradeButton;
        [SerializeField] private Button nitroUpgradeButton;

        private const float CountdownScaleInDuration = 0.16f;
        private const float CountdownHoldDuration = 0.50f;
        private const float CountdownFadeDuration = 0.28f;
        private const float GoHoldDuration = 0.32f;
        private const float GoFadeDuration = 0.20f;

        private const float FeedbackPunchDuration = 0.14f;
        private const float FeedbackSettleDuration = 0.10f;
        private const float FeedbackHoldDuration = 0.50f;
        private const float FeedbackFadeDuration = 0.24f;

        private static readonly Color CountdownGoColor =
            new Color(0.20f, 0.90f, 1f, 1f);

        private static readonly Color CheckpointFeedbackColor =
            new Color(0.30f, 0.88f, 1f, 1f);

        private static readonly Color LapFeedbackColor =
            new Color(0.95f, 0.98f, 1f, 1f);

        private static readonly Color NewBestFeedbackColor =
            new Color(1f, 0.78f, 0.16f, 1f);

        private Coroutine countdownAnimation;
        private Coroutine feedbackAnimation;
        private RectTransform countdownRect;
        private RectTransform raceFeedbackRect;
        private Vector3 countdownBaseScale = Vector3.one;
        private Vector3 raceFeedbackBaseScale = Vector3.one;
        private Color countdownBaseColor = Color.white;
        private int currentFeedbackPriority;

        private void Start()
        {
            ValidateCoreReferences();
            PrepareFeedbackUi();

            saveManager.DataChanged += RefreshGarage;
            upgradeManager.UpgradesChanged += RefreshGarage;
            checkpointManager.CheckpointPassed += HandleCheckpointPassed;
            lapTimer.LapCompleted += HandleLapCompleted;

            HideCountdown();
            HideResultsOnly();
            HideGarage();
            HidePauseMenu();
            HideRaceModeSelection();
            RefreshGarage();

            if (vehicleSelectionPanel != null)
            {
                ShowVehicleSelection();
            }
            else
            {
                ShowHud();
            }
        }

        private void OnDestroy()
        {
            if (saveManager != null)
            {
                saveManager.DataChanged -= RefreshGarage;
            }

            if (upgradeManager != null)
            {
                upgradeManager.UpgradesChanged -= RefreshGarage;
            }

            if (checkpointManager != null)
            {
                checkpointManager.CheckpointPassed -=
                    HandleCheckpointPassed;
            }

            if (lapTimer != null)
            {
                lapTimer.LapCompleted -= HandleLapCompleted;
            }
        }

        private void Update()
        {
            RefreshHud();
        }

        public void BindVehicle(
            CarController newCarController,
            NitroSystem newNitroSystem
        )
        {
            carController = newCarController;
            nitroSystem = newNitroSystem;
            RefreshHud();
        }

        public void SetSelectedVehicleName(string vehicleName)
        {
            if (selectedVehicleText != null)
            {
                selectedVehicleText.text = vehicleName;
            }
        }

        public void ShowVehicleSelection()
        {
            HideCountdown();
            HideHud();
            HideResultsOnly();
            HideGarage();
            HidePauseMenu();
            HideRaceModeSelection();

            if (vehicleSelectionPanel != null)
            {
                vehicleSelectionPanel.SetActive(true);
            }
        }

        public void HideVehicleSelection()
        {
            if (vehicleSelectionPanel != null)
            {
                vehicleSelectionPanel.SetActive(false);
            }
        }

        public void ShowRaceModeSelection()
        {
            HideCountdown();
            HideHud();
            HideResultsOnly();
            HideGarage();
            HidePauseMenu();
            HideVehicleSelection();

            if (raceModeSelectionPanel == null)
            {
                Debug.LogError(
                    "Race Mode Selection Panel is not assigned. " +
                    "Verify the City scene setup.",
                    this
                );
                return;
            }

            raceModeSelectionPanel.SetActive(true);
            raceModeSelectionPanel.transform.SetAsLastSibling();

            if (modeSelectionDescriptionText != null)
            {
                modeSelectionDescriptionText.text =
                    "TIME TRIAL: Complete 3 laps as fast as possible.\n" +
                    "DRIFT CHALLENGE: Score as many drift points as possible before time runs out.";
            }
        }

        public void HideRaceModeSelection()
        {
            if (raceModeSelectionPanel != null)
            {
                raceModeSelectionPanel.SetActive(false);
            }
        }

        public void SelectTimeTrial()
        {
            raceManager.SelectTimeTrialMode();
        }

        public void SelectDriftChallenge()
        {
            raceManager.SelectDriftChallengeMode();
        }

        public void ShowHud()
        {
            HideRaceModeSelection();

            if (hudPanel != null)
            {
                hudPanel.SetActive(true);
            }
        }

        public void HideHud()
        {
            HideRaceFeedback();

            if (hudPanel != null)
            {
                hudPanel.SetActive(false);
            }
        }

        public void ShowCountdown(string value)
        {
            if (countdownText == null)
            {
                return;
            }

            PrepareFeedbackUi();

            if (countdownAnimation != null)
            {
                StopCoroutine(countdownAnimation);
            }

            countdownText.gameObject.SetActive(true);
            countdownText.text = value;
            countdownAnimation = StartCoroutine(
                AnimateCountdown(value == "GO!")
            );
        }

        public void HideCountdown()
        {
            if (countdownAnimation != null)
            {
                StopCoroutine(countdownAnimation);
                countdownAnimation = null;
            }

            if (countdownCanvasGroup != null)
            {
                countdownCanvasGroup.alpha = 0f;
            }

            if (countdownRect != null)
            {
                countdownRect.localScale = countdownBaseScale;
            }

            if (countdownText != null)
            {
                countdownText.color = countdownBaseColor;
                countdownText.gameObject.SetActive(false);
            }
        }

        private IEnumerator AnimateCountdown(bool isGo)
        {
            if (
                countdownCanvasGroup == null ||
                countdownRect == null ||
                countdownText == null
            )
            {
                yield break;
            }

            countdownText.color = isGo
                ? CountdownGoColor
                : countdownBaseColor;

            float punchScale = isGo ? 1.18f : 1.08f;
            countdownCanvasGroup.alpha = 0f;
            countdownRect.localScale = countdownBaseScale * 0.55f;

            float elapsed = 0f;
            while (elapsed < CountdownScaleInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(
                    elapsed / CountdownScaleInDuration
                );

                float eased = Mathf.SmoothStep(0f, 1f, progress);
                countdownCanvasGroup.alpha = eased;
                countdownRect.localScale = Vector3.LerpUnclamped(
                    countdownBaseScale * 0.55f,
                    countdownBaseScale * punchScale,
                    eased
                );

                yield return null;
            }

            float holdDuration = isGo
                ? GoHoldDuration
                : CountdownHoldDuration;

            elapsed = 0f;
            while (elapsed < holdDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / holdDuration);
                countdownCanvasGroup.alpha = 1f;
                countdownRect.localScale = Vector3.LerpUnclamped(
                    countdownBaseScale * punchScale,
                    countdownBaseScale,
                    Mathf.SmoothStep(0f, 1f, progress)
                );

                yield return null;
            }

            float fadeDuration = isGo
                ? GoFadeDuration
                : CountdownFadeDuration;

            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeDuration);
                countdownCanvasGroup.alpha = 1f - progress;
                countdownRect.localScale = Vector3.LerpUnclamped(
                    countdownBaseScale,
                    countdownBaseScale * 0.94f,
                    progress
                );

                yield return null;
            }

            countdownCanvasGroup.alpha = 0f;
            countdownRect.localScale = countdownBaseScale;
            countdownAnimation = null;
        }

        private void HandleCheckpointPassed(
            int completedCheckpointCount,
            int checkpointCount
        )
        {
            if (raceManager == null || !raceManager.IsRacing)
            {
                return;
            }

            ShowRaceFeedback(
                "CHECKPOINT!",
                1,
                CheckpointFeedbackColor
            );
        }

        private void HandleLapCompleted(
            int lapNumber,
            float lapTime,
            bool isNewBest
        )
        {
            if (
                raceManager == null ||
                !raceManager.IsRacing ||
                raceManager.ActiveMode != RaceMode.TimeTrial
            )
            {
                return;
            }

            bool isFinalLap =
                checkpointManager != null &&
                checkpointManager.CurrentLap >=
                checkpointManager.TotalLaps;

            if (isFinalLap)
            {
                return;
            }

            string message = $"LAP {lapNumber} COMPLETE";
            int priority = 2;
            Color color = LapFeedbackColor;

            if (isNewBest)
            {
                message += "\nNEW BEST LAP!";
                priority = 3;
                color = NewBestFeedbackColor;
            }

            ShowRaceFeedback(message, priority, color);
        }

        private void ShowRaceFeedback(
            string message,
            int priority,
            Color color
        )
        {
            PrepareFeedbackUi();

            if (
                raceFeedbackCanvasGroup == null ||
                raceFeedbackText == null ||
                raceFeedbackRect == null ||
                priority < currentFeedbackPriority
            )
            {
                return;
            }

            if (feedbackAnimation != null)
            {
                StopCoroutine(feedbackAnimation);
            }

            currentFeedbackPriority = priority;
            raceFeedbackText.text = message;
            raceFeedbackText.color = color;
            raceFeedbackCanvasGroup.gameObject.SetActive(true);
            feedbackAnimation = StartCoroutine(
                AnimateRaceFeedback()
            );
        }

        private IEnumerator AnimateRaceFeedback()
        {
            raceFeedbackCanvasGroup.alpha = 0f;
            raceFeedbackRect.localScale =
                raceFeedbackBaseScale * 0.82f;

            float elapsed = 0f;
            while (elapsed < FeedbackPunchDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(
                    elapsed / FeedbackPunchDuration
                );

                float eased = Mathf.SmoothStep(0f, 1f, progress);
                raceFeedbackCanvasGroup.alpha = eased;
                raceFeedbackRect.localScale = Vector3.LerpUnclamped(
                    raceFeedbackBaseScale * 0.82f,
                    raceFeedbackBaseScale * 1.08f,
                    eased
                );

                yield return null;
            }

            elapsed = 0f;
            while (elapsed < FeedbackSettleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(
                    elapsed / FeedbackSettleDuration
                );

                raceFeedbackCanvasGroup.alpha = 1f;
                raceFeedbackRect.localScale = Vector3.LerpUnclamped(
                    raceFeedbackBaseScale * 1.08f,
                    raceFeedbackBaseScale,
                    Mathf.SmoothStep(0f, 1f, progress)
                );

                yield return null;
            }

            elapsed = 0f;
            while (elapsed < FeedbackHoldDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < FeedbackFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(
                    elapsed / FeedbackFadeDuration
                );

                raceFeedbackCanvasGroup.alpha = 1f - progress;
                raceFeedbackRect.localScale = Vector3.LerpUnclamped(
                    raceFeedbackBaseScale,
                    raceFeedbackBaseScale * 0.96f,
                    progress
                );

                yield return null;
            }

            raceFeedbackCanvasGroup.alpha = 0f;
            raceFeedbackRect.localScale = raceFeedbackBaseScale;
            raceFeedbackText.text = string.Empty;
            currentFeedbackPriority = 0;
            feedbackAnimation = null;
        }

        private void HideRaceFeedback()
        {
            if (feedbackAnimation != null)
            {
                StopCoroutine(feedbackAnimation);
                feedbackAnimation = null;
            }

            if (raceFeedbackCanvasGroup != null)
            {
                raceFeedbackCanvasGroup.alpha = 0f;
            }

            if (raceFeedbackRect != null)
            {
                raceFeedbackRect.localScale = raceFeedbackBaseScale;
            }

            if (raceFeedbackText != null)
            {
                raceFeedbackText.text = string.Empty;
            }

            currentFeedbackPriority = 0;
        }

        public void ShowPauseMenu()
        {
            if (pausePanel != null)
            {
                pausePanel.SetActive(true);
                pausePanel.transform.SetAsLastSibling();
            }
        }

        public void HidePauseMenu()
        {
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }
        }

        public void ShowResults(
            RaceMode mode,
            float raceTime,
            float bestLap,
            int driftScore,
            bool isNewRecord,
            int reward,
            int totalCash
        )
        {
            HideCountdown();
            HideHud();
            HideGarage();
            HideVehicleSelection();
            HideRaceModeSelection();
            HidePauseMenu();

            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
            }

            if (resultModeText != null)
            {
                resultModeText.text = RaceModeUtility.GetDisplayName(mode);
            }

            if (resultRaceTimeText != null)
            {
                resultRaceTimeText.text =
                    $"Race Time: {LapTimer.FormatTime(raceTime)}";
            }

            if (resultBestLapText != null)
            {
                resultBestLapText.text = bestLap > 0f
                    ? $"Best Lap: {LapTimer.FormatTime(bestLap)}"
                    : "Best Lap: --:--.---";
            }

            if (resultDriftScoreText != null)
            {
                resultDriftScoreText.text =
                    $"Drift Score: {driftScore:N0}";
            }

            if (resultRecordText != null)
            {
                resultRecordText.text = isNewRecord
                    ? "NEW RECORD!"
                    : string.Empty;
            }

            if (resultRewardText != null)
            {
                resultRewardText.text = $"Reward: {reward:N0}";
            }

            if (resultCashText != null)
            {
                resultCashText.text = $"Cash: {totalCash:N0}";
            }

            RefreshGarage();
        }

        public void HideResults()
        {
            HideResultsOnly();
        }

        public void ShowGarage()
        {
            HideResultsOnly();
            HideHud();
            HideVehicleSelection();
            HideRaceModeSelection();
            HidePauseMenu();

            if (garagePanel != null)
            {
                garagePanel.SetActive(true);
            }

            RefreshGarage();
        }

        public void HideGarage()
        {
            if (garagePanel != null)
            {
                garagePanel.SetActive(false);
            }
        }

        public void CloseGarageAndShowHud()
        {
            HideGarage();
            ShowHud();
        }

        public void UpgradeEngine()
        {
            upgradeManager.TryUpgradeEngine();
        }

        public void UpgradeTires()
        {
            upgradeManager.TryUpgradeTires();
        }

        public void UpgradeNitro()
        {
            upgradeManager.TryUpgradeNitro();
        }

        public void RefreshGarage()
        {
            if (saveManager == null || upgradeManager == null)
            {
                return;
            }

            if (cashText != null)
            {
                cashText.text = $"Cash: {saveManager.Cash:N0}";
            }

            RefreshUpgrade(
                UpgradeType.Engine,
                engineLevelText,
                engineCostText,
                engineUpgradeButton
            );

            RefreshUpgrade(
                UpgradeType.Tires,
                tiresLevelText,
                tiresCostText,
                tiresUpgradeButton
            );

            RefreshUpgrade(
                UpgradeType.Nitro,
                nitroLevelText,
                nitroCostText,
                nitroUpgradeButton
            );
        }

        private void RefreshHud()
        {
            bool isDriftChallenge =
                raceManager != null &&
                raceManager.ActiveMode == RaceMode.DriftChallenge;

            SetActiveIfNeeded(timeTrialHudGroup, !isDriftChallenge);
            SetActiveIfNeeded(driftHudGroup, isDriftChallenge);

            if (speedText != null)
            {
                int speed = carController != null
                    ? Mathf.RoundToInt(carController.CurrentSpeedKmh)
                    : 0;

                speedText.text =
                    $"<b>{speed:000}</b><size=24> KM/H</size>";
            }

            if (lapTimer != null)
            {
                if (raceTimeText != null)
                {
                    raceTimeText.text =
                        "RACE  <b>" +
                        LapTimer.FormatTime(lapTimer.RaceTime) +
                        "</b>";
                }

                if (lapTimeText != null)
                {
                    lapTimeText.text =
                        "LAP   <b>" +
                        LapTimer.FormatTime(lapTimer.CurrentLapTime) +
                        "</b>";
                }

                if (bestLapText != null)
                {
                    bestLapText.text = lapTimer.BestLapTime > 0f
                        ? "BEST  <b>" +
                            LapTimer.FormatTime(lapTimer.BestLapTime) +
                            "</b>"
                        : "BEST  <b>--:--.---</b>";
                }
            }

            if (checkpointManager != null)
            {
                if (lapText != null)
                {
                    lapText.text =
                        $"LAP  <b>{checkpointManager.CurrentLap}</b> / " +
                        checkpointManager.TotalLaps;
                }

                if (checkpointText != null)
                {
                    checkpointText.text = checkpointManager.WaitingForFinish
                        ? "CHECKPOINT  <b>FINISH</b>"
                        : $"CHECKPOINT  <b>{checkpointManager.CompletedCheckpointCount}</b> / " +
                            checkpointManager.CheckpointCount;
                }
            }

            if (nitroSlider != null)
            {
                float nitroAmount = nitroSystem != null
                    ? nitroSystem.NormalizedAmount
                    : 0f;

                nitroSlider.SetValueWithoutNotify(nitroAmount);
            }

            if (raceManager != null)
            {
                if (modeText != null)
                {
                    modeText.text =
                        RaceModeUtility.GetDisplayName(
                            raceManager.ActiveMode
                        );
                }

                if (challengeTimeText != null)
                {
                    bool showChallengeTimer = isDriftChallenge;

                    SetActiveIfNeeded(
                        challengeTimeText.gameObject,
                        showChallengeTimer
                    );

                    if (showChallengeTimer)
                    {
                        challengeTimeText.text =
                            "<size=18>TIME LEFT</size>\n<b>" +
                            LapTimer.FormatTime(
                                raceManager.ChallengeTimeRemaining
                            ) +
                            "</b>";
                    }
                }
            }

            if (driftScoreManager != null)
            {
                if (driftScoreText != null)
                {
                    driftScoreText.text =
                        "<size=20>DRIFT SCORE</size>\n<b>" +
                        driftScoreManager.TotalScore.ToString(
                            "N0",
                            CultureInfo.InvariantCulture
                        ) +
                        "</b>";
                }

                if (driftComboText != null)
                {
                    driftComboText.text =
                        "<size=18>COMBO</size>\n<b>" +
                        "x" +
                        driftScoreManager.ComboMultiplier.ToString(
                            "0.0",
                            CultureInfo.InvariantCulture
                        ) +
                        "</b>  <size=20>+" +
                        driftScoreManager.CurrentComboScore.ToString(
                            "N0",
                            CultureInfo.InvariantCulture
                        ) +
                        "</size>";
                }

                if (driftAngleText != null)
                {
                    driftAngleText.text =
                        "<size=16>ANGLE</size>\n<b>" +
                        $"{driftScoreManager.DriftAngle:0}°" +
                        "</b>";
                }
            }

            if (ghostStatusText != null && ghostReplaySystem != null)
            {
                ghostStatusText.text = ghostReplaySystem.HasBestLap
                    ? "<size=14>GHOST</size>\n" +
                        LapTimer.FormatTime(ghostReplaySystem.BestLapTime)
                    : "<size=14>GHOST</size>\n--:--.---";
            }
        }

        private static void SetActiveIfNeeded(
            GameObject target,
            bool shouldBeActive
        )
        {
            if (target != null && target.activeSelf != shouldBeActive)
            {
                target.SetActive(shouldBeActive);
            }
        }

        private void RefreshUpgrade(
            UpgradeType type,
            TextMeshProUGUI levelText,
            TextMeshProUGUI costText,
            Button button
        )
        {
            int level = upgradeManager.GetLevel(type);
            int maxLevel = upgradeManager.GetMaxLevel(type);
            bool maxed = level >= maxLevel;
            int cost = maxed ? 0 : upgradeManager.GetCost(type);

            if (levelText != null)
            {
                levelText.text = $"Level {level}/{maxLevel}";
            }

            if (costText != null)
            {
                costText.text = maxed ? "MAX" : $"Cost {cost:N0}";
            }

            if (button != null)
            {
                button.interactable =
                    !maxed &&
                    saveManager.Cash >= cost;
            }
        }

        private void PrepareFeedbackUi()
        {
            if (countdownText != null)
            {
                if (countdownCanvasGroup == null)
                {
                    countdownCanvasGroup =
                        countdownText.GetComponent<CanvasGroup>();

                    if (countdownCanvasGroup == null)
                    {
                        countdownCanvasGroup =
                            countdownText.gameObject.AddComponent<
                                CanvasGroup
                            >();
                    }
                }

                if (countdownRect == null)
                {
                    countdownRect = countdownText.rectTransform;
                    countdownBaseScale = countdownRect.localScale;
                    countdownBaseColor = countdownText.color;
                }

                countdownCanvasGroup.interactable = false;
                countdownCanvasGroup.blocksRaycasts = false;
            }

            if (
                raceFeedbackCanvasGroup == null &&
                raceFeedbackText != null
            )
            {
                raceFeedbackCanvasGroup =
                    raceFeedbackText.GetComponentInParent<CanvasGroup>();
            }

            if (raceFeedbackCanvasGroup != null)
            {
                raceFeedbackCanvasGroup.interactable = false;
                raceFeedbackCanvasGroup.blocksRaycasts = false;

                if (raceFeedbackRect == null)
                {
                    raceFeedbackRect =
                        raceFeedbackCanvasGroup.GetComponent<RectTransform>();
                    raceFeedbackBaseScale =
                        raceFeedbackRect != null
                            ? raceFeedbackRect.localScale
                            : Vector3.one;
                }
            }
        }

        private void HideResultsOnly()
        {
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }
        }

        private void ValidateCoreReferences()
        {
            if (
                checkpointManager == null ||
                lapTimer == null ||
                saveManager == null ||
                upgradeManager == null ||
                raceManager == null ||
                driftScoreManager == null ||
                ghostReplaySystem == null
            )
            {
                throw new MissingReferenceException(
                    "UIManager requires CheckpointManager, LapTimer, " +
                    "SaveManager, UpgradeManager, RaceManager, " +
                    "DriftScoreManager and GhostReplaySystem references. " +
                    "Verify the City scene setup."
                );
            }
        }
    }
}
