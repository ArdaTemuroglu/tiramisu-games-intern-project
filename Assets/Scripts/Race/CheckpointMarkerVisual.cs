using UnityEngine;

namespace ArcadeRacing
{
    [DisallowMultipleComponent]
    public sealed class CheckpointMarkerVisual : MonoBehaviour
    {
        [SerializeField] private Transform rotatingVisual;
        [SerializeField, Min(0f)] private float bobAmplitude = 0.25f;
        [SerializeField, Min(0f)] private float bobFrequency = 0.85f;
        [SerializeField] private float rotationSpeed = 38f;
        [SerializeField, Range(0f, 0.25f)] private float pulseAmount = 0.07f;

        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private bool hasCachedPose;

        private void Awake()
        {
            CachePose();
        }

        private void OnEnable()
        {
            CachePose();
            ApplyAnimatedPose(0f);
        }

        private void Update()
        {
            if (!hasCachedPose)
            {
                CachePose();
            }

            float phase = Time.time * bobFrequency * Mathf.PI * 2f;
            ApplyAnimatedPose(phase);

            if (rotatingVisual != null)
            {
                rotatingVisual.Rotate(
                    Vector3.up,
                    rotationSpeed * Time.deltaTime,
                    Space.Self
                );
            }
        }

        private void OnDisable()
        {
            if (!hasCachedPose)
            {
                return;
            }

            transform.localPosition = baseLocalPosition;
            transform.localScale = baseLocalScale;
        }

        private void CachePose()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalScale = transform.localScale;
            hasCachedPose = true;
        }

        private void ApplyAnimatedPose(float phase)
        {
            float bob = Mathf.Sin(phase) * bobAmplitude;
            float pulse = 1f + Mathf.Sin(phase * 0.75f) * pulseAmount;

            transform.localPosition =
                baseLocalPosition + Vector3.up * bob;
            transform.localScale = baseLocalScale * pulse;
        }
    }
}
