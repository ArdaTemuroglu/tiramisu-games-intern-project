using UnityEngine;

namespace ArcadeRacing
{
    public sealed class DriftScoreManager : MonoBehaviour
    {
        [Header("Runtime")]
        [SerializeField] private RaceManager raceManager;
        [SerializeField] private CarController playerCar;

        [Header("Drift Detection")]
        [SerializeField, Min(0f)] private float minimumSpeedKmh = 25f;
        [SerializeField, Range(1f, 89f)] private float minimumDriftAngle = 12f;
        [SerializeField, Range(1f, 89f)] private float maximumDriftAngle = 75f;
        [SerializeField, Min(0f)] private float comboGraceSeconds = 0.65f;
        [SerializeField] private bool scoreDuringTimeTrial = true;

        [Header("Scoring")]
        [SerializeField, Min(1f)] private float basePointsPerSecond = 100f;
        [SerializeField, Min(0f)] private float maximumSpeedBonus = 1.25f;
        [SerializeField, Min(0f)] private float comboGrowthPerSecond = 0.35f;
        [SerializeField, Min(1f)] private float maximumComboMultiplier = 5f;

        private float bankedScore;
        private float currentComboScore;
        private float comboDuration;
        private float graceRemaining;
        private bool runActive;

        public int TotalScore => Mathf.Max(
            0,
            Mathf.RoundToInt(bankedScore + currentComboScore)
        );

        public int CurrentComboScore => Mathf.Max(
            0,
            Mathf.RoundToInt(currentComboScore)
        );

        public float ComboMultiplier { get; private set; } = 1f;
        public float DriftAngle { get; private set; }
        public bool IsDrifting { get; private set; }
        public bool RunActive => runActive;

        private void Reset()
        {
            raceManager = FindAnyObjectByType<RaceManager>();
        }

        private void OnValidate()
        {
            minimumDriftAngle = Mathf.Clamp(
                minimumDriftAngle,
                1f,
                88f
            );

            maximumDriftAngle = Mathf.Clamp(
                maximumDriftAngle,
                minimumDriftAngle + 1f,
                89f
            );

            maximumComboMultiplier = Mathf.Max(
                1f,
                maximumComboMultiplier
            );
        }

        private void Update()
        {
            if (!CanScore())
            {
                EndDriftFrame();
                return;
            }

            Vector3 velocity = GetVelocity(playerCar.Body);
            Vector3 planarVelocity = Vector3.ProjectOnPlane(
                velocity,
                Vector3.up
            );

            float speedKmh = planarVelocity.magnitude * 3.6f;
            float forwardDot = Vector3.Dot(
                planarVelocity,
                playerCar.transform.forward
            );

            DriftAngle = planarVelocity.sqrMagnitude > 0.01f
                ? Vector3.Angle(
                    playerCar.transform.forward,
                    planarVelocity.normalized
                )
                : 0f;

            bool validDrift =
                playerCar.IsGrounded &&
                forwardDot > 0f &&
                speedKmh >= minimumSpeedKmh &&
                DriftAngle >= minimumDriftAngle &&
                DriftAngle <= maximumDriftAngle;

            if (!validDrift)
            {
                EndDriftFrame();
                return;
            }

            IsDrifting = true;
            graceRemaining = comboGraceSeconds;
            comboDuration += Time.deltaTime;

            ComboMultiplier = Mathf.Min(
                maximumComboMultiplier,
                1f + comboDuration * comboGrowthPerSecond
            );

            float angleQuality = Mathf.InverseLerp(
                minimumDriftAngle,
                maximumDriftAngle,
                DriftAngle
            );

            float speedQuality = Mathf.Clamp01(
                (speedKmh - minimumSpeedKmh) / 100f
            );

            float angleFactor = Mathf.Lerp(0.55f, 1.25f, angleQuality);
            float speedFactor = 1f + speedQuality * maximumSpeedBonus;

            currentComboScore +=
                basePointsPerSecond *
                angleFactor *
                speedFactor *
                ComboMultiplier *
                Time.deltaTime;
        }

        public void BindVehicle(CarController newPlayerCar)
        {
            playerCar = newPlayerCar;
            ResetRun();
        }

        public void BeginRun()
        {
            ResetRun();
            runActive = true;
        }

        public int EndRun()
        {
            BankCurrentCombo();
            runActive = false;
            IsDrifting = false;
            DriftAngle = 0f;
            return TotalScore;
        }

        public void ResetRun()
        {
            bankedScore = 0f;
            currentComboScore = 0f;
            comboDuration = 0f;
            graceRemaining = 0f;
            ComboMultiplier = 1f;
            DriftAngle = 0f;
            IsDrifting = false;
            runActive = false;
        }

        private bool CanScore()
        {
            if (
                !runActive ||
                playerCar == null ||
                raceManager == null ||
                !raceManager.IsRacing ||
                raceManager.IsPaused
            )
            {
                return false;
            }

            return
                raceManager.ActiveMode == RaceMode.DriftChallenge ||
                scoreDuringTimeTrial;
        }

        private void EndDriftFrame()
        {
            IsDrifting = false;
            DriftAngle = 0f;

            if (currentComboScore <= 0f)
            {
                return;
            }

            graceRemaining -= Time.deltaTime;

            if (graceRemaining <= 0f)
            {
                BankCurrentCombo();
            }
        }

        private void BankCurrentCombo()
        {
            bankedScore += currentComboScore;
            currentComboScore = 0f;
            comboDuration = 0f;
            graceRemaining = 0f;
            ComboMultiplier = 1f;
        }

        private static Vector3 GetVelocity(Rigidbody rigidbody)
        {
            if (rigidbody == null)
            {
                return Vector3.zero;
            }

#if UNITY_6000_0_OR_NEWER
            return rigidbody.linearVelocity;
#else
            return rigidbody.velocity;
#endif
        }
    }
}
