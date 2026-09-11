using UnityEngine;

namespace ArcadeRacing
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CarController))]
    public sealed class VehicleImpactFeedback : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        private float minimumImpactSpeed = 4.5f;

        [SerializeField, Min(0.01f)]
        private float hardImpactSpeed = 18f;

        [SerializeField, Range(0f, 1f)]
        private float maximumGroundNormalY = 0.55f;

        [SerializeField, Min(0f)]
        private float impactCooldown = 0.16f;

        [SerializeField, Min(0f)]
        private float teleportClearDistance = 4f;

        private CarController carController;
        private CameraFollow cameraFollow;
        private Vector3 previousPosition;
        private float cooldownRemaining;

        private void Awake()
        {
            carController = GetComponent<CarController>();
            ResolveCameraFollow();
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
            cooldownRemaining = 0f;
        }

        private void Update()
        {
            cooldownRemaining = Mathf.Max(
                0f,
                cooldownRemaining - Time.unscaledDeltaTime
            );
        }

        private void LateUpdate()
        {
            float clearDistanceSquared =
                teleportClearDistance * teleportClearDistance;

            if (
                (transform.position - previousPosition).sqrMagnitude >
                clearDistanceSquared
            )
            {
                ClearCameraFeedback();
            }

            previousPosition = transform.position;
        }

        private void OnDisable()
        {
            ClearCameraFeedback();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null || cooldownRemaining > 0f)
            {
                return;
            }

            float strongestImpactSpeed = 0f;
            Vector3 strongestImpactNormal = Vector3.zero;

            for (int index = 0; index < collision.contactCount; index++)
            {
                ContactPoint contact = collision.GetContact(index);
                if (Mathf.Abs(contact.normal.y) > maximumGroundNormalY)
                {
                    continue;
                }

                float normalImpactSpeed = Mathf.Abs(
                    Vector3.Dot(collision.relativeVelocity, contact.normal)
                );

                if (normalImpactSpeed > strongestImpactSpeed)
                {
                    strongestImpactSpeed = normalImpactSpeed;
                    strongestImpactNormal = contact.normal;
                }
            }

            if (strongestImpactSpeed < minimumImpactSpeed)
            {
                return;
            }

            if (cameraFollow == null)
            {
                ResolveCameraFollow();
            }

            if (cameraFollow == null)
            {
                return;
            }

            float intensity = Mathf.InverseLerp(
                minimumImpactSpeed,
                Mathf.Max(minimumImpactSpeed + 0.01f, hardImpactSpeed),
                strongestImpactSpeed
            );

            cameraFollow.PlayImpactFeedback(
                carController,
                intensity,
                strongestImpactNormal
            );
            cooldownRemaining = impactCooldown;
        }

        private void ResolveCameraFollow()
        {
            Camera mainCamera = Camera.main;
            cameraFollow = mainCamera != null
                ? mainCamera.GetComponent<CameraFollow>()
                : null;

            if (cameraFollow == null)
            {
                cameraFollow = FindAnyObjectByType<CameraFollow>();
            }
        }

        private void ClearCameraFeedback()
        {
            if (cameraFollow != null)
            {
                cameraFollow.ClearImpactFeedback(carController);
            }
        }
    }
}
