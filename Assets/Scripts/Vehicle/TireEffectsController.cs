using UnityEngine;
using UnityEngine.Rendering;

namespace ArcadeRacing
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CarController))]
    public sealed class TireEffectsController : MonoBehaviour
    {
        [Header("Slip Detection")]
        [SerializeField, Tooltip("Minimum vehicle speed required before tire effects can appear."), Min(0f)]
        private float minimumSpeedKmh = 15f;

        [SerializeField, Tooltip("Combined rear-wheel slip that starts tire smoke."), Min(0f)]
        private float smokeSlipThreshold = 0.18f;

        [SerializeField, Tooltip("Combined rear-wheel slip that starts a skid mark."), Min(0f)]
        private float skidSlipThreshold = 0.28f;

        [SerializeField, Tooltip("Slip value treated as maximum visual intensity."), Min(0.01f)]
        private float fullEffectSlip = 0.65f;

        [SerializeField, Tooltip("Lowers slip thresholds while the handbrake is held, but slip is still required."), Range(0.5f, 1f)]
        private float handbrakeThresholdMultiplier = 0.8f;

        [Header("Smoke")]
        [SerializeField, Tooltip("Maximum particles emitted per second by each rear tire."), Min(0f)]
        private float maximumSmokeEmission = 45f;

        [Header("Skid Marks")]
        [SerializeField, Tooltip("Width of each rear tire mark in metres."), Range(0.05f, 0.35f)]
        private float skidMarkWidth = 0.18f;

        [SerializeField, Tooltip("Raises marks slightly above the contacted surface to avoid z-fighting."), Range(0.002f, 0.05f)]
        private float surfaceOffset = 0.015f;

        [SerializeField, Tooltip("Maximum number of recycled skid segments shared by both rear tires."), Range(256, 6000)]
        private int maximumSkidSegments = 3000;

        [SerializeField, Tooltip("Minimum contact-point movement before another segment is added."), Min(0.02f)]
        private float minimumSegmentDistance = 0.08f;

        [SerializeField, Tooltip("Breaks continuity after a jump or respawn so no long ribbon is drawn."), Min(0.5f)]
        private float maximumSegmentDistance = 1.5f;

        private const float ForwardSlipWeight = 0.35f;
        private const int SmokeTextureSize = 32;

        private sealed class WheelEffectState
        {
            public bool hasPreviousPoint;
            public Vector3 previousPoint;
            public Vector3 previousNormal;

            public void Reset()
            {
                hasPreviousPoint = false;
            }
        }

        private CarController carController;
        private WheelCollider rearLeftWheel;
        private WheelCollider rearRightWheel;
        private ParticleSystem rearLeftSmoke;
        private ParticleSystem rearRightSmoke;
        private Material smokeMaterial;
        private Material skidMaterial;
        private Texture2D smokeTexture;
        private SkidMeshBuffer skidMesh;
        private readonly WheelEffectState leftState = new WheelEffectState();
        private readonly WheelEffectState rightState = new WheelEffectState();
        private Vector3 previousVehiclePosition;
        private bool hasPreviousVehiclePosition;

        public float LeftSlip { get; private set; }
        public float RightSlip { get; private set; }
        public float LeftSmokeIntensity { get; private set; }
        public float RightSmokeIntensity { get; private set; }
        public bool LeftGrounded { get; private set; }
        public bool RightGrounded { get; private set; }
        public int SkidSegmentCount => skidMesh != null ? skidMesh.SegmentCount : 0;

        private void OnValidate()
        {
            minimumSpeedKmh = Mathf.Max(0f, minimumSpeedKmh);
            smokeSlipThreshold = Mathf.Max(0f, smokeSlipThreshold);
            skidSlipThreshold = Mathf.Max(smokeSlipThreshold, skidSlipThreshold);
            fullEffectSlip = Mathf.Max(skidSlipThreshold + 0.01f, fullEffectSlip);
            maximumSkidSegments = Mathf.Clamp(maximumSkidSegments, 256, 6000);
            minimumSegmentDistance = Mathf.Max(0.02f, minimumSegmentDistance);
            maximumSegmentDistance = Mathf.Max(
                minimumSegmentDistance + 0.1f,
                maximumSegmentDistance
            );
        }

        private void Awake()
        {
            InitializeRuntime();
        }

        private bool InitializeRuntime()
        {
            carController = GetComponent<CarController>();
            rearLeftWheel = carController.RearLeftCollider;
            rearRightWheel = carController.RearRightCollider;

            if (rearLeftWheel == null || rearRightWheel == null)
            {
                Debug.LogError(
                    "TireEffectsController requires both rear WheelCollider references.",
                    this
                );
                enabled = false;
                return false;
            }

            if (
                rearLeftSmoke != null &&
                rearRightSmoke != null &&
                smokeMaterial != null &&
                skidMaterial != null &&
                smokeTexture != null &&
                skidMesh != null
            )
            {
                return true;
            }

            ReleaseRuntimeResources();

            if (!CreateRuntimeMaterials())
            {
                enabled = false;
                return false;
            }

            rearLeftSmoke = CreateSmokeSystem("Rear Left Tire Smoke");
            rearRightSmoke = CreateSmokeSystem("Rear Right Tire Smoke");
            skidMesh = new SkidMeshBuffer(
                name + " Skid Marks",
                maximumSkidSegments,
                skidMaterial,
                gameObject.layer
            );

            previousVehiclePosition = transform.position;
            hasPreviousVehiclePosition = true;
            return true;
        }

        private void OnEnable()
        {
            if (!InitializeRuntime())
            {
                return;
            }

            ResetContinuity();
            previousVehiclePosition = transform.position;
            hasPreviousVehiclePosition = true;
        }

        private void OnDisable()
        {
            SetSmokeEmission(rearLeftSmoke, 0f);
            SetSmokeEmission(rearRightSmoke, 0f);
            ResetContinuity();
        }

        private void OnDestroy()
        {
            ReleaseRuntimeResources();
        }

        private void ReleaseRuntimeResources()
        {
            if (skidMesh != null)
            {
                skidMesh.Dispose();
                skidMesh = null;
            }

            if (rearLeftSmoke != null)
            {
                DestroyRuntimeObject(rearLeftSmoke.gameObject);
                rearLeftSmoke = null;
            }

            if (rearRightSmoke != null)
            {
                DestroyRuntimeObject(rearRightSmoke.gameObject);
                rearRightSmoke = null;
            }

            DestroyRuntimeObject(smokeMaterial);
            DestroyRuntimeObject(skidMaterial);
            DestroyRuntimeObject(smokeTexture);
            smokeMaterial = null;
            skidMaterial = null;
            smokeTexture = null;
        }

        private void FixedUpdate()
        {
            if (carController.Body == null)
            {
                StopAllEffects();
                return;
            }

            if (
                hasPreviousVehiclePosition &&
                Vector3.Distance(
                    transform.position,
                    previousVehiclePosition
                ) > maximumSegmentDistance
            )
            {
                ResetContinuity();
            }

            previousVehiclePosition = transform.position;
            hasPreviousVehiclePosition = true;

            float speedKmh = carController.CurrentSpeedKmh;
            float thresholdMultiplier = Mathf.Lerp(
                1f,
                handbrakeThresholdMultiplier,
                Mathf.Clamp01(carController.HandbrakeInput)
            );

            ProcessWheel(
                rearLeftWheel,
                rearLeftSmoke,
                leftState,
                speedKmh,
                thresholdMultiplier,
                true
            );

            ProcessWheel(
                rearRightWheel,
                rearRightSmoke,
                rightState,
                speedKmh,
                thresholdMultiplier,
                false
            );
        }

        private void ProcessWheel(
            WheelCollider wheel,
            ParticleSystem smoke,
            WheelEffectState state,
            float speedKmh,
            float thresholdMultiplier,
            bool leftWheel
        )
        {
            bool grounded = wheel.GetGroundHit(out WheelHit hit);

            if (leftWheel)
            {
                LeftGrounded = grounded;
            }
            else
            {
                RightGrounded = grounded;
            }

            if (!grounded || speedKmh < minimumSpeedKmh)
            {
                SetWheelTelemetry(leftWheel, 0f, 0f);
                SetSmokeEmission(smoke, 0f);
                state.Reset();
                return;
            }

            float slip = Mathf.Max(
                Mathf.Abs(hit.sidewaysSlip),
                Mathf.Abs(hit.forwardSlip) * ForwardSlipWeight
            );

            float smokeThreshold =
                smokeSlipThreshold * thresholdMultiplier;
            float skidThreshold =
                skidSlipThreshold * thresholdMultiplier;

            float smokeIntensity = slip > smokeThreshold
                ? Mathf.InverseLerp(
                    smokeThreshold,
                    fullEffectSlip,
                    slip
                )
                : 0f;

            float skidIntensity = slip > skidThreshold
                ? Mathf.InverseLerp(
                    skidThreshold,
                    fullEffectSlip,
                    slip
                )
                : 0f;

            SetWheelTelemetry(leftWheel, slip, smokeIntensity);
            PositionSmoke(smoke, hit.point, hit.normal);
            SetSmokeEmission(
                smoke,
                smokeIntensity * maximumSmokeEmission
            );

            if (skidIntensity <= 0f)
            {
                state.Reset();
                return;
            }

            AddSkidPoint(state, hit, skidIntensity);
        }

        private void AddSkidPoint(
            WheelEffectState state,
            WheelHit hit,
            float intensity
        )
        {
            Vector3 point = hit.point + hit.normal * surfaceOffset;

            if (!state.hasPreviousPoint)
            {
                state.hasPreviousPoint = true;
                state.previousPoint = point;
                state.previousNormal = hit.normal;
                return;
            }

            float distance = Vector3.Distance(
                state.previousPoint,
                point
            );

            if (distance > maximumSegmentDistance)
            {
                state.previousPoint = point;
                state.previousNormal = hit.normal;
                return;
            }

            if (distance < minimumSegmentDistance)
            {
                return;
            }

            skidMesh.AddSegment(
                state.previousPoint,
                state.previousNormal,
                point,
                hit.normal,
                skidMarkWidth,
                intensity
            );

            state.previousPoint = point;
            state.previousNormal = hit.normal;
        }

        private void SetWheelTelemetry(
            bool leftWheel,
            float slip,
            float smokeIntensity
        )
        {
            if (leftWheel)
            {
                LeftSlip = slip;
                LeftSmokeIntensity = smokeIntensity;
                return;
            }

            RightSlip = slip;
            RightSmokeIntensity = smokeIntensity;
        }

        private void StopAllEffects()
        {
            SetWheelTelemetry(true, 0f, 0f);
            SetWheelTelemetry(false, 0f, 0f);
            LeftGrounded = false;
            RightGrounded = false;
            SetSmokeEmission(rearLeftSmoke, 0f);
            SetSmokeEmission(rearRightSmoke, 0f);
            ResetContinuity();
        }

        private void ResetContinuity()
        {
            leftState.Reset();
            rightState.Reset();
        }

        private bool CreateRuntimeMaterials()
        {
            Shader particleShader = Shader.Find(
                "Universal Render Pipeline/Particles/Unlit"
            );
            Shader unlitShader = Shader.Find(
                "Universal Render Pipeline/Unlit"
            );

            if (
                particleShader == null ||
                !particleShader.isSupported ||
                unlitShader == null ||
                !unlitShader.isSupported
            )
            {
                Debug.LogError(
                    "Tire effects require supported URP Particles/Unlit and Unlit shaders.",
                    this
                );
                return false;
            }

            smokeTexture = CreateSmokeTexture();
            smokeMaterial = new Material(particleShader)
            {
                name = "Runtime Tire Smoke Material",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent
            };

            smokeMaterial.SetOverrideTag("RenderType", "Transparent");
            SetMaterialColor(smokeMaterial, Color.white);
            SetMaterialTexture(smokeMaterial, smokeTexture);

            if (smokeMaterial.HasProperty("_Surface"))
            {
                smokeMaterial.SetFloat("_Surface", 1f);
            }

            if (smokeMaterial.HasProperty("_SrcBlend"))
            {
                smokeMaterial.SetFloat(
                    "_SrcBlend",
                    (float)BlendMode.SrcAlpha
                );
            }

            if (smokeMaterial.HasProperty("_DstBlend"))
            {
                smokeMaterial.SetFloat(
                    "_DstBlend",
                    (float)BlendMode.OneMinusSrcAlpha
                );
            }

            if (smokeMaterial.HasProperty("_ZWrite"))
            {
                smokeMaterial.SetFloat("_ZWrite", 0f);
            }

            smokeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            skidMaterial = new Material(unlitShader)
            {
                name = "Runtime Skid Mark Material",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Geometry + 1
            };

            SetMaterialColor(
                skidMaterial,
                new Color(0.025f, 0.03f, 0.035f, 1f)
            );

            return true;
        }

        private ParticleSystem CreateSmokeSystem(string objectName)
        {
            GameObject smokeObject = new GameObject(objectName);
            smokeObject.layer = gameObject.layer;
            smokeObject.transform.SetParent(transform, false);

            ParticleSystem system = smokeObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
            main.startRotation = new ParticleSystem.MinMaxCurve(
                0f,
                Mathf.PI * 2f
            );
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.72f, 0.74f, 0.76f, 0.38f),
                new Color(0.93f, 0.94f, 0.95f, 0.55f)
            );
            main.gravityModifier = -0.04f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.1f;
            shape.radiusThickness = 1f;

            ParticleSystem.ColorOverLifetimeModule color =
                system.colorOverLifetime;
            color.enabled = true;
            Gradient smokeGradient = new Gradient();
            smokeGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(
                        new Color(0.92f, 0.93f, 0.95f),
                        0f
                    ),
                    new GradientColorKey(
                        new Color(0.62f, 0.65f, 0.68f),
                        1f
                    )
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.45f, 0.12f),
                    new GradientAlphaKey(0.25f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            color.color = smokeGradient;

            ParticleSystem.SizeOverLifetimeModule size =
                system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.55f),
                    new Keyframe(0.35f, 1f),
                    new Keyframe(1f, 1.8f)
                )
            );

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.1f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.2f;
            noise.damping = true;

            ParticleSystemRenderer renderer =
                smokeObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = smokeMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;

            system.Play(false);
            return system;
        }

        private static Texture2D CreateSmokeTexture()
        {
            Texture2D texture = new Texture2D(
                SmokeTextureSize,
                SmokeTextureSize,
                TextureFormat.RGBA32,
                false,
                true
            )
            {
                name = "Runtime Tire Smoke Texture",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] pixels = new Color32[
                SmokeTextureSize * SmokeTextureSize
            ];

            for (int y = 0; y < SmokeTextureSize; y++)
            {
                for (int x = 0; x < SmokeTextureSize; x++)
                {
                    float normalizedX =
                        (x + 0.5f) / SmokeTextureSize * 2f - 1f;
                    float normalizedY =
                        (y + 0.5f) / SmokeTextureSize * 2f - 1f;
                    float distance = Mathf.Sqrt(
                        normalizedX * normalizedX +
                        normalizedY * normalizedY
                    );
                    float alpha = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.Clamp01(1f - distance)
                    );
                    alpha *= alpha;
                    pixels[y * SmokeTextureSize + x] = new Color32(
                        255,
                        255,
                        255,
                        (byte)Mathf.RoundToInt(alpha * 255f)
                    );
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void PositionSmoke(
            ParticleSystem smoke,
            Vector3 point,
            Vector3 normal
        )
        {
            if (smoke == null)
            {
                return;
            }

            smoke.transform.SetPositionAndRotation(
                point + normal * 0.035f,
                Quaternion.LookRotation(normal)
            );
        }

        private static void SetSmokeEmission(
            ParticleSystem smoke,
            float rate
        )
        {
            if (smoke == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = smoke.emission;
            emission.rateOverTime = Mathf.Max(0f, rate);

            if (!smoke.isPlaying)
            {
                smoke.Play(false);
            }
        }

        private static void SetMaterialColor(
            Material material,
            Color color
        )
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static void SetMaterialTexture(
            Material material,
            Texture texture
        )
        {
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private sealed class SkidMeshBuffer
        {
            private readonly GameObject meshObject;
            private readonly Mesh mesh;
            private readonly Vector3[] vertices;
            private readonly Vector3[] normals;
            private readonly Vector2[] uvs;
            private readonly Color32[] colors;
            private readonly int maximumSegments;
            private int nextSegment;
            private bool initialized;

            public int SegmentCount { get; private set; }

            public SkidMeshBuffer(
                string objectName,
                int segmentBudget,
                Material material,
                int layer
            )
            {
                maximumSegments = Mathf.Clamp(
                    segmentBudget,
                    256,
                    6000
                );
                int vertexCount = maximumSegments * 4;

                vertices = new Vector3[vertexCount];
                normals = new Vector3[vertexCount];
                uvs = new Vector2[vertexCount];
                colors = new Color32[vertexCount];
                int[] triangles = new int[maximumSegments * 6];

                for (int segment = 0; segment < maximumSegments; segment++)
                {
                    int vertex = segment * 4;
                    int triangle = segment * 6;
                    triangles[triangle] = vertex;
                    triangles[triangle + 1] = vertex + 2;
                    triangles[triangle + 2] = vertex + 1;
                    triangles[triangle + 3] = vertex + 2;
                    triangles[triangle + 4] = vertex + 3;
                    triangles[triangle + 5] = vertex + 1;

                    uvs[vertex] = new Vector2(0f, 0f);
                    uvs[vertex + 1] = new Vector2(1f, 0f);
                    uvs[vertex + 2] = new Vector2(0f, 1f);
                    uvs[vertex + 3] = new Vector2(1f, 1f);
                }

                meshObject = new GameObject(objectName)
                {
                    layer = layer
                };
                meshObject.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity
                );

                MeshFilter filter = meshObject.AddComponent<MeshFilter>();
                MeshRenderer renderer =
                    meshObject.AddComponent<MeshRenderer>();

                mesh = new Mesh
                {
                    name = objectName + " Mesh",
                    hideFlags = HideFlags.HideAndDontSave
                };
                mesh.MarkDynamic();
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.uv = uvs;
                mesh.colors32 = colors;
                mesh.triangles = triangles;
                filter.sharedMesh = mesh;

                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode =
                    MotionVectorGenerationMode.ForceNoMotion;
            }

            public void AddSegment(
                Vector3 previousPoint,
                Vector3 previousNormal,
                Vector3 point,
                Vector3 normal,
                float markWidth,
                float intensity
            )
            {
                Vector3 direction = point - previousPoint;

                if (direction.sqrMagnitude < 0.0001f)
                {
                    return;
                }

                Vector3 surfaceNormal =
                    (previousNormal + normal).normalized;

                if (surfaceNormal.sqrMagnitude < 0.1f)
                {
                    surfaceNormal = Vector3.up;
                }

                Vector3 side = Vector3.Cross(
                    surfaceNormal,
                    direction.normalized
                ).normalized;

                if (side.sqrMagnitude < 0.1f)
                {
                    return;
                }

                float halfWidth =
                    markWidth * Mathf.Lerp(0.3f, 0.5f, intensity);

                if (!initialized)
                {
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        vertices[i] = previousPoint;
                        normals[i] = surfaceNormal;
                        colors[i] = new Color32(255, 255, 255, 0);
                    }

                    initialized = true;
                }

                int vertex = nextSegment * 4;
                vertices[vertex] = previousPoint - side * halfWidth;
                vertices[vertex + 1] = previousPoint + side * halfWidth;
                vertices[vertex + 2] = point - side * halfWidth;
                vertices[vertex + 3] = point + side * halfWidth;

                normals[vertex] = previousNormal;
                normals[vertex + 1] = previousNormal;
                normals[vertex + 2] = normal;
                normals[vertex + 3] = normal;

                byte alpha = (byte)Mathf.RoundToInt(
                    Mathf.Lerp(115f, 235f, intensity)
                );
                Color32 color = new Color32(255, 255, 255, alpha);
                colors[vertex] = color;
                colors[vertex + 1] = color;
                colors[vertex + 2] = color;
                colors[vertex + 3] = color;

                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.colors32 = colors;
                mesh.RecalculateBounds();

                nextSegment = (nextSegment + 1) % maximumSegments;
                SegmentCount = Mathf.Min(
                    maximumSegments,
                    SegmentCount + 1
                );
            }

            public void Dispose()
            {
                DestroyRuntimeObject(mesh);
                DestroyRuntimeObject(meshObject);
            }
        }
    }
}
