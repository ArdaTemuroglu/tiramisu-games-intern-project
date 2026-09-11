using UnityEngine;

namespace ArcadeRacing
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private CarController target;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 3.2f, -7.5f);
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1.2f, 0f);
        [SerializeField] private float lookAheadDistance = 2.5f;
        [SerializeField] private float positionSmoothTime = 0.12f;
        [SerializeField] private float rotationSharpness = 10f;
        [SerializeField] private float minimumFieldOfView = 60f;
        [SerializeField] private float maximumFieldOfView = 75f;
        [SerializeField] private float fieldOfViewSpeed = 190f;
        [SerializeField] private float fieldOfViewSharpness = 4f;

        [Header("Nitro FOV")]
        [SerializeField, Range(0f, 12f)]
        private float nitroFieldOfViewBonus = 6f;

        [SerializeField, Min(0.01f)]
        private float nitroFieldOfViewRampUpTime = 0.22f;

        [SerializeField, Min(0.01f)]
        private float nitroFieldOfViewRampDownTime = 0.38f;

        [Header("Impact Feedback")]
        [SerializeField, Min(0f)]
        private float mediumImpactPositionAmplitude = 0.035f;

        [SerializeField, Min(0f)]
        private float hardImpactPositionAmplitude = 0.22f;

        [SerializeField, Min(0.01f)]
        private float mediumImpactDuration = 0.1f;

        [SerializeField, Min(0.01f)]
        private float hardImpactDuration = 0.25f;

        [SerializeField, Min(1f)]
        private float impactOscillationFrequency = 24f;

        private Camera attachedCamera;
        private Vector3 positionVelocity;
        private NitroSystem targetNitroSystem;
        private float currentNitroFieldOfViewBonus;
        private Vector3 appliedImpactWorldOffset;
        private Vector3 impactDirectionLocal;
        private float impactElapsed;
        private float impactDuration;
        private float impactPositionAmplitude;
        private float impactPhase;

        private const float ResponsePercentFactor = 3f;
        private const float MinimumVisualNitroAmount = 0.01f;

        public CarController Target => target;
        public float NormalTargetFieldOfView { get; private set; }
        public float CurrentNitroFieldOfViewBonus =>
            currentNitroFieldOfViewBonus;
        public float FinalTargetFieldOfView =>
            NormalTargetFieldOfView + currentNitroFieldOfViewBonus;

        private void Awake()
        {
            attachedCamera = GetComponent<Camera>();
            ResolveTargetNitroSystem();
        }

        private void Start()
        {
            if (target != null)
            {
                SnapToTarget();
            }
        }

        private void LateUpdate()
        {
            RemoveAppliedImpactOffset();

            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = target.transform.TransformPoint(localOffset);
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                positionSmoothTime
            );

            float speedBlend = Mathf.Clamp01(
                target.CurrentSpeedKmh / Mathf.Max(1f, fieldOfViewSpeed)
            );

            Vector3 lookTarget =
                target.transform.position +
                lookOffset +
                target.transform.forward * lookAheadDistance * speedBlend;

            Vector3 lookDirection = lookTarget - transform.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(
                    lookDirection,
                    Vector3.up
                );

                float rotationBlend = 1f - Mathf.Exp(
                    -rotationSharpness * Time.deltaTime
                );

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    desiredRotation,
                    rotationBlend
                );
            }

            NormalTargetFieldOfView = Mathf.Lerp(
                minimumFieldOfView,
                maximumFieldOfView,
                speedBlend
            );

            float fieldOfViewBlend = 1f - Mathf.Exp(
                -fieldOfViewSharpness * Time.deltaTime
            );

            float currentSpeedFieldOfView =
                attachedCamera.fieldOfView -
                currentNitroFieldOfViewBonus;

            currentSpeedFieldOfView = Mathf.Lerp(
                currentSpeedFieldOfView,
                NormalTargetFieldOfView,
                fieldOfViewBlend
            );

            float targetNitroBonus =
                targetNitroSystem != null &&
                targetNitroSystem.IsActive &&
                targetNitroSystem.NormalizedAmount >
                MinimumVisualNitroAmount
                    ? nitroFieldOfViewBonus
                    : 0f;

            float responseTime =
                targetNitroBonus > currentNitroFieldOfViewBonus
                    ? nitroFieldOfViewRampUpTime
                    : nitroFieldOfViewRampDownTime;
            float nitroBlend = 1f - Mathf.Exp(
                -ResponsePercentFactor * Time.deltaTime / responseTime
            );

            currentNitroFieldOfViewBonus = Mathf.Lerp(
                currentNitroFieldOfViewBonus,
                targetNitroBonus,
                nitroBlend
            );

            if (
                targetNitroBonus <= 0f &&
                currentNitroFieldOfViewBonus < 0.002f
            )
            {
                currentNitroFieldOfViewBonus = 0f;
            }

            attachedCamera.fieldOfView =
                currentSpeedFieldOfView +
                currentNitroFieldOfViewBonus;

            UpdateImpactFeedback();
        }

        public void SetTarget(CarController newTarget, bool snapImmediately = true)
        {
            ClearImpactFeedback();
            RemoveCurrentNitroBonus();
            target = newTarget;
            positionVelocity = Vector3.zero;
            ResolveTargetNitroSystem();

            if (target != null && snapImmediately)
            {
                SnapToTarget();
            }
        }

        public void ClearTarget()
        {
            ClearImpactFeedback();
            RemoveCurrentNitroBonus();
            target = null;
            targetNitroSystem = null;
            positionVelocity = Vector3.zero;
        }

        public void SnapToTarget()
        {
            ClearImpactFeedback();

            if (target == null)
            {
                return;
            }

            transform.position = target.transform.TransformPoint(localOffset);

            Vector3 lookTarget = target.transform.position + lookOffset;
            Vector3 lookDirection = lookTarget - transform.position;

            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(
                    lookDirection,
                    Vector3.up
                );
            }
        }

        public void PlayImpactFeedback(
            CarController source,
            float normalizedIntensity,
            Vector3 impactNormal
        )
        {
            if (source == null || source != target)
            {
                return;
            }

            float intensity = Mathf.Clamp01(normalizedIntensity);
            const float hardBandStart = 0.55f;

            if (intensity < hardBandStart)
            {
                float mediumBlend = intensity / hardBandStart;
                impactDuration = Mathf.Lerp(
                    mediumImpactDuration,
                    0.16f,
                    mediumBlend
                );
                impactPositionAmplitude = Mathf.Lerp(
                    mediumImpactPositionAmplitude,
                    0.105f,
                    mediumBlend
                );
            }
            else
            {
                float hardBlend = Mathf.InverseLerp(
                    hardBandStart,
                    1f,
                    intensity
                );
                impactDuration = Mathf.Lerp(
                    0.18f,
                    hardImpactDuration,
                    hardBlend
                );
                impactPositionAmplitude = Mathf.Lerp(
                    0.13f,
                    hardImpactPositionAmplitude,
                    hardBlend
                );
            }

            Vector3 localNormal = source.transform.InverseTransformDirection(
                impactNormal
            );
            localNormal.y = 0f;
            impactDirectionLocal = localNormal.sqrMagnitude > 0.0001f
                ? localNormal.normalized
                : Vector3.right;
            impactElapsed = 0f;
            impactPhase = Time.unscaledTime * 19f;
        }

        public void ClearImpactFeedback(CarController source = null)
        {
            if (source != null && source != target)
            {
                return;
            }

            RemoveAppliedImpactOffset();
            impactElapsed = 0f;
            impactDuration = 0f;
            impactPositionAmplitude = 0f;
            impactDirectionLocal = Vector3.zero;
        }

        private void ResolveTargetNitroSystem()
        {
            targetNitroSystem = target != null
                ? target.GetComponent<NitroSystem>()
                : null;
        }

        private void RemoveCurrentNitroBonus()
        {
            if (
                attachedCamera != null &&
                currentNitroFieldOfViewBonus > 0f
            )
            {
                attachedCamera.fieldOfView = Mathf.Max(
                    1f,
                    attachedCamera.fieldOfView -
                    currentNitroFieldOfViewBonus
                );
            }

            currentNitroFieldOfViewBonus = 0f;
        }

        private void UpdateImpactFeedback()
        {
            if (impactDuration <= 0f)
            {
                return;
            }

            impactElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(impactElapsed / impactDuration);
            float envelope = 1f - progress;
            float angle =
                impactPhase +
                impactElapsed * impactOscillationFrequency * Mathf.PI * 2f;

            float horizontalNoise = Mathf.Sin(angle * 1.13f);
            float verticalNoise = Mathf.Sin(angle * 1.71f + 1.2f);

            Vector3 localOffset = new Vector3(
                horizontalNoise * 0.45f + impactDirectionLocal.x * 0.7f,
                verticalNoise * 0.28f,
                impactDirectionLocal.z * 0.18f
            ) * (impactPositionAmplitude * envelope);

            appliedImpactWorldOffset = transform.TransformVector(localOffset);
            transform.position += appliedImpactWorldOffset;

            if (progress >= 1f)
            {
                impactDuration = 0f;
            }
        }

        private void RemoveAppliedImpactOffset()
        {
            transform.position -= appliedImpactWorldOffset;
            appliedImpactWorldOffset = Vector3.zero;
        }
    }
}
