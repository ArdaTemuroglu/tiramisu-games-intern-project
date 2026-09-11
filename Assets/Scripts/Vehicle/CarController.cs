using UnityEngine;

namespace ArcadeRacing
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CarController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private CarConfig config;
        [SerializeField] private NitroSystem nitroSystem;

        [Header("Wheel Colliders")]
        [SerializeField] private WheelCollider frontLeftCollider;
        [SerializeField] private WheelCollider frontRightCollider;
        [SerializeField] private WheelCollider rearLeftCollider;
        [SerializeField] private WheelCollider rearRightCollider;

        [Header("Wheel Models")]
        [SerializeField] private Transform frontLeftModel;
        [SerializeField] private Transform frontRightModel;
        [SerializeField] private Transform rearLeftModel;
        [SerializeField] private Transform rearRightModel;

        [Header("Input")]
        [SerializeField] private string horizontalAxis = "Horizontal";
        [SerializeField] private string verticalAxis = "Vertical";
        [SerializeField] private KeyCode handbrakeKey = KeyCode.Space;
        [SerializeField] private KeyCode nitroKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode alternateNitroKey = KeyCode.RightShift;
        [SerializeField] private KeyCode resetKey = KeyCode.R;

        private Rigidbody body;
        private float rawSteerInput;
        private float rawThrottleInput;
        private float rawBrakeInput;
        private float rawReverseInput;
        private float handbrakeInput;
        private float smoothedSteerInput;
        private float smoothedThrottleInput;
        private float smoothedBrakeInput;
        private float smoothedReverseInput;
        private float currentSteerAngle;
        private bool nitroRequested;
        private float nitroIntensity;
        private bool controlEnabled;
        private float engineMultiplier = 1f;
        private float gripMultiplier = 1f;
        private Vector3 respawnPosition;
        private Quaternion respawnRotation;
        private float wheelBase;
        private float frontTrack;
        private WheelFrictionCurve frontLeftForward;
        private WheelFrictionCurve frontRightForward;
        private WheelFrictionCurve rearLeftForward;
        private WheelFrictionCurve rearRightForward;
        private WheelFrictionCurve frontLeftSideways;
        private WheelFrictionCurve frontRightSideways;
        private WheelFrictionCurve rearLeftSideways;
        private WheelFrictionCurve rearRightSideways;

        public float CurrentSpeedKmh => BodyVelocity.magnitude * 3.6f;
        public float ForwardSpeedKmh => Vector3.Dot(BodyVelocity, transform.forward) * 3.6f;
        public float CurrentSteerAngle => currentSteerAngle;
        public float DriftAmount { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool ControlEnabled => controlEnabled;
        public Rigidbody Body => body;
        public CarConfig Config => config;
        public float HandbrakeInput => handbrakeInput;
        public WheelCollider RearLeftCollider => rearLeftCollider;
        public WheelCollider RearRightCollider => rearRightCollider;

        private Vector3 BodyVelocity
        {
            get
            {
#if UNITY_6000_0_OR_NEWER
                return body.linearVelocity;
#else
                return body.velocity;
#endif
            }
            set
            {
#if UNITY_6000_0_OR_NEWER
                body.linearVelocity = value;
#else
                body.velocity = value;
#endif
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ValidateReferences();
            ConfigureRigidbody();
            ConfigureWheels();
            CacheWheelGeometry();
            CacheFrictionCurves();
            nitroSystem.Initialize(config);
            respawnPosition = transform.position;
            respawnRotation = transform.rotation;
            SetControlEnabled(false);
        }

        private void Update()
        {
            ReadInput();
            UpdateWheelModels();

            if (Input.GetKeyDown(resetKey))
            {
                ResetToRespawn();
            }
        }

        private void FixedUpdate()
        {
            UpdateGroundedState();
            UpdateSmoothedInputs();
            CalculateDriftAmount();
            ApplySteering();
            ApplyDriveAndBrakes();
            ApplyTireFriction();
            ApplyAntiRoll(frontLeftCollider, frontRightCollider, config.frontAntiRoll);
            ApplyAntiRoll(rearLeftCollider, rearRightCollider, config.rearAntiRoll);
            ApplyAerodynamics();
            ApplyStabilityAssist();
        }

        private void ValidateReferences()
        {
            if (config == null)
            {
                throw new MissingReferenceException("CarController requires a CarConfig asset.");
            }

            if (nitroSystem == null)
            {
                throw new MissingReferenceException("CarController requires a NitroSystem reference.");
            }

            if (frontLeftCollider == null || frontRightCollider == null || rearLeftCollider == null || rearRightCollider == null)
            {
                throw new MissingReferenceException("CarController requires four WheelCollider references.");
            }
        }

        private void ConfigureRigidbody()
        {
            body.mass = config.mass;
            body.centerOfMass = config.centerOfMass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = 7f;
            body.solverIterations = 12;
            body.solverVelocityIterations = 12;
        }

        private void ConfigureWheels()
        {
            ConfigureWheel(frontLeftCollider);
            ConfigureWheel(frontRightCollider);
            ConfigureWheel(rearLeftCollider);
            ConfigureWheel(rearRightCollider);
            frontLeftCollider.ConfigureVehicleSubsteps(5f, 12, 15);
        }

        private void ConfigureWheel(WheelCollider wheel)
        {
            wheel.mass = config.wheelMass;
            wheel.wheelDampingRate = config.wheelDampingRate;
            wheel.suspensionDistance = config.suspensionDistance;
            wheel.forceAppPointDistance = config.forceAppPointDistance;
            JointSpring spring = wheel.suspensionSpring;
            spring.spring = config.suspensionSpring;
            spring.damper = config.suspensionDamper;
            spring.targetPosition = config.suspensionTargetPosition;
            wheel.suspensionSpring = spring;
        }

        private void CacheWheelGeometry()
        {
            Vector3 frontLeftLocal = transform.InverseTransformPoint(frontLeftCollider.transform.position);
            Vector3 frontRightLocal = transform.InverseTransformPoint(frontRightCollider.transform.position);
            Vector3 rearLeftLocal = transform.InverseTransformPoint(rearLeftCollider.transform.position);
            wheelBase = Mathf.Max(0.1f, Mathf.Abs(frontLeftLocal.z - rearLeftLocal.z));
            frontTrack = Mathf.Max(0.1f, Mathf.Abs(frontLeftLocal.x - frontRightLocal.x));
        }

        private void CacheFrictionCurves()
        {
            frontLeftForward = frontLeftCollider.forwardFriction;
            frontRightForward = frontRightCollider.forwardFriction;
            rearLeftForward = rearLeftCollider.forwardFriction;
            rearRightForward = rearRightCollider.forwardFriction;
            frontLeftSideways = frontLeftCollider.sidewaysFriction;
            frontRightSideways = frontRightCollider.sidewaysFriction;
            rearLeftSideways = rearLeftCollider.sidewaysFriction;
            rearRightSideways = rearRightCollider.sidewaysFriction;
        }

        private void ReadInput()
        {
            if (!controlEnabled)
            {
                rawSteerInput = 0f;
                rawThrottleInput = 0f;
                rawReverseInput = 0f;
                rawBrakeInput = 1f;
                handbrakeInput = 0f;
                nitroRequested = false;
                return;
            }

            float horizontal = Mathf.Clamp(Input.GetAxisRaw(horizontalAxis), -1f, 1f);
            rawSteerInput = Mathf.Abs(horizontal) < config.steeringDeadZone ? 0f : horizontal;

            float vertical = Mathf.Clamp(Input.GetAxisRaw(verticalAxis), -1f, 1f);
            float forwardSpeed = ForwardSpeedKmh;

            rawThrottleInput = 0f;
            rawReverseInput = 0f;
            rawBrakeInput = 0f;

            if (vertical > 0f)
            {
                if (forwardSpeed < -2f)
                {
                    rawBrakeInput = vertical;
                }
                else
                {
                    rawThrottleInput = vertical;
                }
            }
            else if (vertical < 0f)
            {
                if (forwardSpeed > 2f)
                {
                    rawBrakeInput = -vertical;
                }
                else
                {
                    rawReverseInput = -vertical;
                }
            }

            handbrakeInput = Input.GetKey(handbrakeKey) ? 1f : 0f;
            nitroRequested = Input.GetKey(nitroKey) || Input.GetKey(alternateNitroKey);
        }

        private void UpdateSmoothedInputs()
        {
            float steerRate = Mathf.Abs(rawSteerInput) > 0.001f ? config.steeringInputRiseRate : config.steeringInputReturnRate;
            smoothedSteerInput = Mathf.MoveTowards(smoothedSteerInput, rawSteerInput, steerRate * Time.fixedDeltaTime);

            float throttleRate = rawThrottleInput > smoothedThrottleInput ? config.throttleRiseRate : config.throttleFallRate;
            smoothedThrottleInput = Mathf.MoveTowards(smoothedThrottleInput, rawThrottleInput, throttleRate * Time.fixedDeltaTime);

            float reverseRate = rawReverseInput > smoothedReverseInput ? config.throttleRiseRate : config.throttleFallRate;
            smoothedReverseInput = Mathf.MoveTowards(smoothedReverseInput, rawReverseInput, reverseRate * Time.fixedDeltaTime);

            smoothedBrakeInput = Mathf.MoveTowards(smoothedBrakeInput, rawBrakeInput, config.brakeResponse * Time.fixedDeltaTime);
        }

        private void ApplySteering()
        {
            float speed01 = GetSteeringSpeed01();
            float curveValue = config.steeringBySpeedCurve == null || config.steeringBySpeedCurve.length == 0
                ? 1f - speed01
                : Mathf.Clamp01(config.steeringBySpeedCurve.Evaluate(speed01));

            float steeringLimit = Mathf.Lerp(config.highSpeedSteerAngle, config.maxSteerAngle, curveValue);
            float targetSteerAngle = smoothedSteerInput * steeringLimit;
            float response = Mathf.Abs(smoothedSteerInput) > 0.001f ? config.steeringResponse : config.steeringReturnResponse;
            response *= Mathf.Max(0.1f, config.handling);
            currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetSteerAngle, response * Time.fixedDeltaTime);
            SetAckermannSteering(currentSteerAngle);
        }

        private void SetAckermannSteering(float steeringAngle)
        {
            if (Mathf.Abs(steeringAngle) < 0.01f || config.ackermannStrength <= 0f)
            {
                frontLeftCollider.steerAngle = steeringAngle;
                frontRightCollider.steerAngle = steeringAngle;
                return;
            }

            float sign = Mathf.Sign(steeringAngle);
            float absoluteAngle = Mathf.Abs(steeringAngle) * Mathf.Deg2Rad;
            float turnRadius = wheelBase / Mathf.Max(0.001f, Mathf.Tan(absoluteAngle));
            float innerDenominator = Mathf.Max(0.1f, turnRadius - frontTrack * 0.5f);
            float outerDenominator = Mathf.Max(0.1f, turnRadius + frontTrack * 0.5f);
            float innerAngle = Mathf.Atan(wheelBase / innerDenominator) * Mathf.Rad2Deg;
            float outerAngle = Mathf.Atan(wheelBase / outerDenominator) * Mathf.Rad2Deg;
            float leftAngle = sign > 0f ? outerAngle : innerAngle;
            float rightAngle = sign > 0f ? innerAngle : outerAngle;
            leftAngle *= sign;
            rightAngle *= sign;

            frontLeftCollider.steerAngle = Mathf.Lerp(steeringAngle, leftAngle, config.ackermannStrength);
            frontRightCollider.steerAngle = Mathf.Lerp(steeringAngle, rightAngle, config.ackermannStrength);
        }

        private void ApplyDriveAndBrakes()
        {
            float currentForwardSpeed = ForwardSpeedKmh;
            bool nitroActive = nitroSystem.Process(
                nitroRequested,
                controlEnabled &&
                IsGrounded &&
                smoothedThrottleInput > 0.25f &&
                currentForwardSpeed >= config.nitroMinimumSpeedKmh,
                Time.fixedDeltaTime
            );

            float nitroTarget = nitroActive ? 1f : 0f;
            float nitroResponse = nitroActive ? config.nitroRampUpRate : config.nitroRampDownRate;
            nitroIntensity = Mathf.MoveTowards(
                nitroIntensity,
                nitroTarget,
                nitroResponse * Time.fixedDeltaTime
            );

            float maximumSpeed =
                config.maxSpeedKmh +
                config.nitroMaxSpeedBonusKmh * nitroIntensity;
            float forwardSpeedRatio = Mathf.Clamp01(Mathf.Max(0f, currentForwardSpeed) / Mathf.Max(1f, maximumSpeed));
            float torqueCurveValue = config.engineTorqueCurve == null || config.engineTorqueCurve.length == 0
                ? 1f - forwardSpeedRatio
                : Mathf.Max(0f, config.engineTorqueCurve.Evaluate(forwardSpeedRatio));

            float limiterStart = Mathf.Max(0f, maximumSpeed - config.speedLimiterRangeKmh);
            float speedLimiter = 1f - Mathf.InverseLerp(limiterStart, maximumSpeed, Mathf.Max(0f, currentForwardSpeed));
            float forwardTorque = config.motorTorque * config.acceleration * engineMultiplier * torqueCurveValue * speedLimiter * smoothedThrottleInput;
            float reverseSpeedRatio = Mathf.Clamp01(Mathf.Abs(Mathf.Min(0f, currentForwardSpeed)) / Mathf.Max(1f, config.reverseMaxSpeedKmh));
            float reverseLimiter = 1f - reverseSpeedRatio;
            float reverseTorque = config.reverseTorque * config.acceleration * engineMultiplier * reverseLimiter * smoothedReverseInput;

            if (!IsGrounded)
            {
                forwardTorque *= 0.15f;
                reverseTorque *= 0.15f;
            }

            float requestedTorque = (forwardTorque - reverseTorque) * CalculateTractionFactor();
            ApplyMotorTorque(requestedTorque);

            float serviceBrake = config.brakeTorque * smoothedBrakeInput;
            bool coasting = smoothedThrottleInput < 0.01f && smoothedReverseInput < 0.01f && Mathf.Abs(currentForwardSpeed) > 2f;
            float engineBrake = coasting ? config.engineBrakingTorque : 0f;
            ApplyBrakeTorque(serviceBrake + engineBrake);
            ApplyRearHandbrake(config.handbrakeTorque * handbrakeInput);

            if (nitroIntensity > 0.001f && currentForwardSpeed < maximumSpeed)
            {
                float nitroSpeed01 = Mathf.Clamp01(
                    Mathf.Max(0f, currentForwardSpeed) /
                    Mathf.Max(1f, config.maxSpeedKmh)
                );

                float nitroCurveValue =
                    config.nitroForceBySpeed == null ||
                    config.nitroForceBySpeed.length == 0
                        ? 1f
                        : Mathf.Max(
                            0f,
                            config.nitroForceBySpeed.Evaluate(nitroSpeed01)
                        );

                Vector3 nitroDirection =
                    Vector3.ProjectOnPlane(transform.forward, Vector3.up);

                if (nitroDirection.sqrMagnitude < 0.001f)
                {
                    nitroDirection = transform.forward;
                }
                else
                {
                    nitroDirection.Normalize();
                }

                float appliedNitroForce =
                    config.nitroForce *
                    nitroSystem.PowerMultiplier *
                    nitroIntensity *
                    nitroCurveValue;

                body.AddForce(
                    nitroDirection * appliedNitroForce,
                    ForceMode.Force
                );
            }
        }

        private void ApplyMotorTorque(float totalTorque)
        {
            frontLeftCollider.motorTorque = 0f;
            frontRightCollider.motorTorque = 0f;
            rearLeftCollider.motorTorque = 0f;
            rearRightCollider.motorTorque = 0f;

            if (config.driveLayout == DriveLayout.FrontWheelDrive)
            {
                frontLeftCollider.motorTorque = totalTorque * 0.5f;
                frontRightCollider.motorTorque = totalTorque * 0.5f;
            }
            else if (config.driveLayout == DriveLayout.RearWheelDrive)
            {
                rearLeftCollider.motorTorque = totalTorque * 0.5f;
                rearRightCollider.motorTorque = totalTorque * 0.5f;
            }
            else
            {
                frontLeftCollider.motorTorque = totalTorque * 0.25f;
                frontRightCollider.motorTorque = totalTorque * 0.25f;
                rearLeftCollider.motorTorque = totalTorque * 0.25f;
                rearRightCollider.motorTorque = totalTorque * 0.25f;
            }
        }

        private void ApplyBrakeTorque(float requestedBrake)
        {
            float frontLeftBrake = CalculateAbsBrake(frontLeftCollider, requestedBrake);
            float frontRightBrake = CalculateAbsBrake(frontRightCollider, requestedBrake);
            float rearLeftBrake = CalculateAbsBrake(rearLeftCollider, requestedBrake * 0.8f);
            float rearRightBrake = CalculateAbsBrake(rearRightCollider, requestedBrake * 0.8f);
            frontLeftCollider.brakeTorque = frontLeftBrake;
            frontRightCollider.brakeTorque = frontRightBrake;
            rearLeftCollider.brakeTorque = rearLeftBrake;
            rearRightCollider.brakeTorque = rearRightBrake;
        }

        private float CalculateAbsBrake(WheelCollider wheel, float requestedBrake)
        {
            if (requestedBrake <= 0f || CurrentSpeedKmh < 8f || !wheel.GetGroundHit(out WheelHit hit))
            {
                return requestedBrake;
            }

            float slip = Mathf.Abs(hit.forwardSlip);
            if (slip <= config.absSlipThreshold)
            {
                return requestedBrake;
            }

            float release = Mathf.Clamp01((slip - config.absSlipThreshold) * 2f) * config.absStrength;
            return requestedBrake * (1f - release);
        }

        private void ApplyRearHandbrake(float torque)
        {
            rearLeftCollider.brakeTorque = Mathf.Max(rearLeftCollider.brakeTorque, torque);
            rearRightCollider.brakeTorque = Mathf.Max(rearRightCollider.brakeTorque, torque);
        }

        private float CalculateTractionFactor()
        {
            if (config.tractionControlStrength <= 0f)
            {
                return 1f;
            }

            float largestSlip = 0f;
            int groundedDrivenWheels = 0;
            AccumulateDrivenWheelSlip(frontLeftCollider, true, ref largestSlip, ref groundedDrivenWheels);
            AccumulateDrivenWheelSlip(frontRightCollider, true, ref largestSlip, ref groundedDrivenWheels);
            AccumulateDrivenWheelSlip(rearLeftCollider, false, ref largestSlip, ref groundedDrivenWheels);
            AccumulateDrivenWheelSlip(rearRightCollider, false, ref largestSlip, ref groundedDrivenWheels);

            if (groundedDrivenWheels == 0 || largestSlip <= config.tractionSlipThreshold)
            {
                return 1f;
            }

            float reduction = Mathf.Clamp01((largestSlip - config.tractionSlipThreshold) * 1.2f) * config.tractionControlStrength;
            return Mathf.Clamp01(1f - reduction);
        }

        private void AccumulateDrivenWheelSlip(WheelCollider wheel, bool frontWheel, ref float largestSlip, ref int groundedDrivenWheels)
        {
            bool driven = config.driveLayout == DriveLayout.AllWheelDrive ||
                          config.driveLayout == DriveLayout.FrontWheelDrive && frontWheel ||
                          config.driveLayout == DriveLayout.RearWheelDrive && !frontWheel;

            if (!driven || !wheel.GetGroundHit(out WheelHit hit))
            {
                return;
            }

            groundedDrivenWheels++;
            largestSlip = Mathf.Max(largestSlip, Mathf.Abs(hit.forwardSlip));
        }

        private void ApplyTireFriction()
        {
            float speedBlend = Mathf.InverseLerp(8f, 40f, CurrentSpeedKmh);
            float driftBlend = Mathf.Clamp01(handbrakeInput * speedBlend);
            float handlingMultiplier = Mathf.Max(0.1f, config.handling);
            float frontStiffness = config.frontGrip * handlingMultiplier * gripMultiplier;
            float normalRearStiffness = config.rearGrip * handlingMultiplier * gripMultiplier;
            float driftingRearStiffness = normalRearStiffness * config.driftFactor;
            float rearStiffness = Mathf.Lerp(normalRearStiffness, driftingRearStiffness, driftBlend);

            SetFriction(frontLeftCollider, frontLeftForward, frontLeftSideways, frontStiffness, frontStiffness);
            SetFriction(frontRightCollider, frontRightForward, frontRightSideways, frontStiffness, frontStiffness);
            SetFriction(rearLeftCollider, rearLeftForward, rearLeftSideways, normalRearStiffness, rearStiffness);
            SetFriction(rearRightCollider, rearRightForward, rearRightSideways, normalRearStiffness, rearStiffness);
        }

        private static void SetFriction(WheelCollider wheel, WheelFrictionCurve baseForward, WheelFrictionCurve baseSideways, float forwardStiffness, float sidewaysStiffness)
        {
            WheelFrictionCurve forward = baseForward;
            WheelFrictionCurve sideways = baseSideways;
            forward.stiffness = Mathf.Max(0.05f, forwardStiffness);
            sideways.stiffness = Mathf.Max(0.05f, sidewaysStiffness);
            wheel.forwardFriction = forward;
            wheel.sidewaysFriction = sideways;
        }

        private void ApplyAntiRoll(WheelCollider leftWheel, WheelCollider rightWheel, float antiRollForce)
        {
            float leftTravel = 1f;
            float rightTravel = 1f;
            bool leftGrounded = leftWheel.GetGroundHit(out WheelHit leftHit);
            bool rightGrounded = rightWheel.GetGroundHit(out WheelHit rightHit);

            if (leftGrounded)
            {
                leftTravel = (-leftWheel.transform.InverseTransformPoint(leftHit.point).y - leftWheel.radius) / Mathf.Max(0.01f, leftWheel.suspensionDistance);
            }

            if (rightGrounded)
            {
                rightTravel = (-rightWheel.transform.InverseTransformPoint(rightHit.point).y - rightWheel.radius) / Mathf.Max(0.01f, rightWheel.suspensionDistance);
            }

            float force = (leftTravel - rightTravel) * antiRollForce;

            if (leftGrounded)
            {
                body.AddForceAtPosition(leftWheel.transform.up * -force, leftWheel.transform.position, ForceMode.Force);
            }

            if (rightGrounded)
            {
                body.AddForceAtPosition(rightWheel.transform.up * force, rightWheel.transform.position, ForceMode.Force);
            }
        }

        private void ApplyAerodynamics()
        {
            Vector3 velocity = BodyVelocity;
            float speed = velocity.magnitude;

            if (speed > 0.1f)
            {
                body.AddForce(-velocity.normalized * config.aerodynamicDrag * speed * speed, ForceMode.Force);
                body.AddForce(-velocity.normalized * config.rollingResistance, ForceMode.Force);
            }

            if (IsGrounded)
            {
                body.AddForce(-transform.up * config.downforce * speed * speed, ForceMode.Force);
            }
        }

        private void ApplyStabilityAssist()
        {
            if (!IsGrounded || CurrentSpeedKmh < 8f)
            {
                return;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(BodyVelocity);
            float handbrakeReduction = Mathf.Lerp(1f, 0.08f, handbrakeInput);
            float lateralAcceleration = Mathf.Clamp(
                -localVelocity.x * config.stabilityAssist * handbrakeReduction,
                -config.maximumLateralAssist,
                config.maximumLateralAssist
            );
            body.AddForce(transform.right * lateralAcceleration, ForceMode.Acceleration);

            float forwardSpeedMps = Vector3.Dot(BodyVelocity, transform.forward);
            float desiredYawRate = forwardSpeedMps / Mathf.Max(0.1f, wheelBase) * Mathf.Tan(currentSteerAngle * Mathf.Deg2Rad);
            float speed01 = GetSteeringSpeed01();
            float maximumYawRate = Mathf.Lerp(config.lowSpeedMaximumYawRate, config.highSpeedMaximumYawRate, speed01);
            desiredYawRate = Mathf.Clamp(desiredYawRate, -maximumYawRate, maximumYawRate);

            float actualYawRate = Vector3.Dot(body.angularVelocity, transform.up);
            float yawError = desiredYawRate - actualYawRate;
            float yawAcceleration = Mathf.Clamp(
                yawError * config.yawAssist * handbrakeReduction,
                -config.maximumYawAssistAcceleration,
                config.maximumYawAssistAcceleration
            );
            body.AddTorque(transform.up * yawAcceleration, ForceMode.Acceleration);

            float safetyYawRate = maximumYawRate * 1.25f;
            if (handbrakeInput < 0.5f && Mathf.Abs(actualYawRate) > safetyYawRate)
            {
                float excessiveYaw = actualYawRate - Mathf.Sign(actualYawRate) * safetyYawRate;
                body.AddTorque(transform.up * -excessiveYaw * config.excessiveYawDamping, ForceMode.Acceleration);
            }
        }

        private float GetSteeringSpeed01()
        {
            float speed = Mathf.Abs(ForwardSpeedKmh);
            float start = Mathf.Min(config.fullSteerSpeedKmh, config.minimumSteerSpeedKmh - 0.1f);
            float end = Mathf.Max(start + 0.1f, config.minimumSteerSpeedKmh);
            return Mathf.InverseLerp(start, end, speed);
        }

        private void UpdateGroundedState()
        {
            int groundedWheels = 0;
            if (frontLeftCollider.isGrounded) groundedWheels++;
            if (frontRightCollider.isGrounded) groundedWheels++;
            if (rearLeftCollider.isGrounded) groundedWheels++;
            if (rearRightCollider.isGrounded) groundedWheels++;
            IsGrounded = groundedWheels >= 2;
        }

        private void CalculateDriftAmount()
        {
            Vector3 localVelocity = transform.InverseTransformDirection(BodyVelocity);
            float planarSpeed = new Vector2(localVelocity.x, localVelocity.z).magnitude;
            DriftAmount = planarSpeed < 1f ? 0f : Mathf.Clamp01(Mathf.Abs(localVelocity.x) / planarSpeed);
        }

        private void UpdateWheelModels()
        {
            UpdateWheelModel(frontLeftCollider, frontLeftModel);
            UpdateWheelModel(frontRightCollider, frontRightModel);
            UpdateWheelModel(rearLeftCollider, rearLeftModel);
            UpdateWheelModel(rearRightCollider, rearRightModel);
        }

        private static void UpdateWheelModel(WheelCollider wheelCollider, Transform wheelModel)
        {
            if (wheelModel == null)
            {
                return;
            }

            wheelCollider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            wheelModel.SetPositionAndRotation(position, rotation);
        }

        public void SetControlEnabled(bool enabled)
        {
            controlEnabled = enabled;

            if (!enabled)
            {
                rawThrottleInput = 0f;
                rawReverseInput = 0f;
                rawSteerInput = 0f;
                rawBrakeInput = 1f;
                nitroRequested = false;
                nitroIntensity = 0f;
            }
        }

        public void ApplyUpgradeMultipliers(float newEngineMultiplier, float newGripMultiplier)
        {
            engineMultiplier = Mathf.Max(0.1f, newEngineMultiplier);
            gripMultiplier = Mathf.Max(0.1f, newGripMultiplier);
        }

        public void SetRespawnPose(Vector3 position, Quaternion rotation)
        {
            respawnPosition = position;
            respawnRotation = rotation;
        }

        public void ResetToRespawn()
        {
            body.position = respawnPosition + Vector3.up * 0.6f;
            body.rotation = respawnRotation;
            BodyVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            rawSteerInput = 0f;
            smoothedSteerInput = 0f;
            currentSteerAngle = 0f;
            nitroIntensity = 0f;
            Physics.SyncTransforms();
        }
    }
}
