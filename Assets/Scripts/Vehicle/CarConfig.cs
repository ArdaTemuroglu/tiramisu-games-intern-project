using UnityEngine;

namespace ArcadeRacing
{
    public enum DriveLayout
    {
        FrontWheelDrive,
        RearWheelDrive,
        AllWheelDrive
    }

    [CreateAssetMenu(fileName = "CarConfig", menuName = "Arcade Racing/Car Config")]
    public sealed class CarConfig : ScriptableObject
    {
        [Header("Core")]
        [Min(100f)] public float mass = 1350f;
        public Vector3 centerOfMass = new Vector3(0f, -0.42f, 0.08f);
        public DriveLayout driveLayout = DriveLayout.AllWheelDrive;
        [Min(0.1f)] public float acceleration = 1f;
        [Min(20f)] public float maxSpeedKmh = 220f;
        [Min(5f)] public float reverseMaxSpeedKmh = 45f;
        [Min(10f)] public float motorTorque = 3000f;
        [Min(10f)] public float reverseTorque = 1400f;
        [Min(10f)] public float brakeTorque = 4200f;
        [Min(10f)] public float handbrakeTorque = 4800f;
        [Min(0f)] public float engineBrakingTorque = 90f;
        [Min(0.1f)] public float throttleRiseRate = 3.5f;
        [Min(0.1f)] public float throttleFallRate = 6f;
        [Min(0.1f)] public float brakeResponse = 10f;
        [Min(0.1f)] public float speedLimiterRangeKmh = 10f;
        public AnimationCurve engineTorqueCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.18f, 1f),
            new Keyframe(0.5f, 0.9f),
            new Keyframe(0.78f, 0.62f),
            new Keyframe(0.95f, 0.25f),
            new Keyframe(1f, 0.06f)
        );

        [Header("Steering")]
        [Min(0.1f)] public float handling = 1f;
        [Range(5f, 45f)] public float maxSteerAngle = 30f;
        [Range(1f, 15f)] public float highSpeedSteerAngle = 4.5f;
        [Min(0f)] public float fullSteerSpeedKmh = 12f;
        [Min(20f)] public float minimumSteerSpeedKmh = 180f;
        public AnimationCurve steeringBySpeedCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.2f, 0.58f),
            new Keyframe(0.45f, 0.28f),
            new Keyframe(0.7f, 0.12f),
            new Keyframe(1f, 0f)
        );
        [Min(0.1f)] public float steeringInputRiseRate = 2.6f;
        [Min(0.1f)] public float steeringInputReturnRate = 4.8f;
        [Min(1f)] public float steeringResponse = 72f;
        [Min(1f)] public float steeringReturnResponse = 115f;
        [Range(0f, 0.5f)] public float steeringDeadZone = 0.04f;
        [Range(0f, 1f)] public float ackermannStrength = 0.82f;
        [Min(0f)] public float stabilityAssist = 1.15f;
        [Min(0f)] public float maximumLateralAssist = 7f;
        [Min(0f)] public float yawAssist = 0.35f;
        [Min(0f)] public float maximumYawAssistAcceleration = 0.7f;
        [Min(0.1f)] public float lowSpeedMaximumYawRate = 1.3f;
        [Min(0.1f)] public float highSpeedMaximumYawRate = 0.55f;
        [Min(0f)] public float excessiveYawDamping = 4f;

        [Header("Tires")]
        [Min(0.1f)] public float frontGrip = 1.25f;
        [Min(0.1f)] public float rearGrip = 1.3f;
        [Range(0.1f, 1f)] public float driftFactor = 0.58f;
        [Range(0f, 1f)] public float tractionControlStrength = 0.35f;
        [Min(0f)] public float tractionSlipThreshold = 0.6f;
        [Range(0f, 1f)] public float absStrength = 0.65f;
        [Min(0f)] public float absSlipThreshold = 0.55f;

        [Header("Chassis")]
        [Min(0f)] public float frontAntiRoll = 6200f;
        [Min(0f)] public float rearAntiRoll = 5600f;
        [Min(0f)] public float downforce = 2.5f;
        [Min(0f)] public float aerodynamicDrag = 0.42f;
        [Min(0f)] public float rollingResistance = 20f;

        [Header("Suspension")]
        [Min(0.01f)] public float suspensionDistance = 0.22f;
        [Min(100f)] public float suspensionSpring = 36000f;
        [Min(100f)] public float suspensionDamper = 4800f;
        [Range(0f, 1f)] public float suspensionTargetPosition = 0.5f;
        [Min(1f)] public float wheelMass = 24f;
        [Min(0f)] public float wheelDampingRate = 0.25f;
        [Min(0f)] public float forceAppPointDistance = 0.12f;

        [Header("Nitro")]
        [Min(0.1f)] public float nitroCapacity = 5f;
        [Min(0.01f)] public float nitroDrainPerSecond = 1f;
        [Min(0f)] public float nitroRechargePerSecond = 0.12f;
        [Min(0f)] public float nitroMinimumSpeedKmh = 30f;
        [Min(0.1f)] public float nitroRampUpRate = 1.6f;
        [Min(0.1f)] public float nitroRampDownRate = 5f;
        [Min(0f)] public float nitroForce = 2800f;
        [Min(0f)] public float nitroMaxSpeedBonusKmh = 25f;
        public AnimationCurve nitroForceBySpeed = new AnimationCurve(
            new Keyframe(0f, 0.25f),
            new Keyframe(0.25f, 0.55f),
            new Keyframe(0.65f, 1f),
            new Keyframe(1f, 0.65f)
        );
    }
}