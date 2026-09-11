using UnityEngine;
using UnityEngine.Rendering;

namespace ArcadeRacing
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NitroSystem))]
    public sealed class NitroVfxController : MonoBehaviour
    {
        [Header("Attachment")]
        [SerializeField, Tooltip("Left exhaust position in vehicle-local space.")]
        private Vector3 leftExhaustPosition =
            new Vector3(-0.58f, -0.25f, -2.4f);

        [SerializeField, Tooltip("Right exhaust position in vehicle-local space.")]
        private Vector3 rightExhaustPosition =
            new Vector3(0.58f, -0.25f, -2.4f);

        [Header("Response")]
        [SerializeField, Tooltip("Approximate time for the VFX to reach full intensity."), Min(0.01f)]
        private float rampUpTime = 0.16f;

        [SerializeField, Tooltip("Approximate time for emission to fade after nitro stops."), Min(0.01f)]
        private float rampDownTime = 0.22f;

        [SerializeField, Tooltip("Clears world-space particles after a respawn or teleport."), Min(0.5f)]
        private float teleportClearDistance = 2.5f;

        [Header("Emission")]
        [SerializeField, Tooltip("Maximum core-flame emission per exhaust."), Min(0f)]
        private float coreEmissionRate = 90f;

        [SerializeField, Tooltip("Maximum outer-glow emission per exhaust."), Min(0f)]
        private float glowEmissionRate = 42f;

        private const int TextureSize = 32;
        private const float ResponsePercentFactor = 3f;
        private const float MinimumVisualNitroAmount = 0.01f;

        private NitroSystem nitroSystem;
        private ParticleSystem[] coreSystems;
        private ParticleSystem[] glowSystems;
        private Material particleMaterial;
        private Texture2D particleTexture;
        private Vector3 previousVehiclePosition;
        private bool hasPreviousVehiclePosition;

        public float VisualIntensity { get; private set; }
        public bool NitroActive =>
            nitroSystem != null &&
            nitroSystem.IsActive &&
            nitroSystem.NormalizedAmount > MinimumVisualNitroAmount;
        public float NitroAmount =>
            nitroSystem != null ? nitroSystem.NormalizedAmount : 0f;
        public int EmitterCount =>
            (coreSystems != null ? coreSystems.Length : 0) +
            (glowSystems != null ? glowSystems.Length : 0);

        public int LiveParticleCount
        {
            get
            {
                int count = 0;
                AddParticleCount(coreSystems, ref count);
                AddParticleCount(glowSystems, ref count);
                return count;
            }
        }

        private void OnValidate()
        {
            rampUpTime = Mathf.Max(0.01f, rampUpTime);
            rampDownTime = Mathf.Max(0.01f, rampDownTime);
            teleportClearDistance = Mathf.Max(0.5f, teleportClearDistance);
            coreEmissionRate = Mathf.Max(0f, coreEmissionRate);
            glowEmissionRate = Mathf.Max(0f, glowEmissionRate);
        }

        private void Awake()
        {
            InitializeRuntime();
        }

        private void OnEnable()
        {
            if (!InitializeRuntime())
            {
                return;
            }

            VisualIntensity = 0f;
            previousVehiclePosition = transform.position;
            hasPreviousVehiclePosition = true;
            SetAllEmission(0f);
        }

        private void OnDisable()
        {
            VisualIntensity = 0f;
            SetAllEmission(0f);
            ClearParticles();
            hasPreviousVehiclePosition = false;
        }

        private void OnDestroy()
        {
            ReleaseRuntimeResources();
        }

        private void Update()
        {
            if (nitroSystem == null)
            {
                VisualIntensity = 0f;
                SetAllEmission(0f);
                return;
            }

            if (
                hasPreviousVehiclePosition &&
                Vector3.Distance(
                    transform.position,
                    previousVehiclePosition
                ) > teleportClearDistance
            )
            {
                ClearParticles();
            }

            previousVehiclePosition = transform.position;
            hasPreviousVehiclePosition = true;

            float targetIntensity = NitroActive ? 1f : 0f;
            float responseTime = targetIntensity > VisualIntensity
                ? rampUpTime
                : rampDownTime;
            float responseBlend = 1f - Mathf.Exp(
                -ResponsePercentFactor * Time.deltaTime / responseTime
            );

            VisualIntensity = Mathf.Lerp(
                VisualIntensity,
                targetIntensity,
                responseBlend
            );

            if (
                targetIntensity <= 0f &&
                VisualIntensity < 0.002f
            )
            {
                VisualIntensity = 0f;
            }

            SetAllEmission(VisualIntensity);
        }

        private bool InitializeRuntime()
        {
            nitroSystem = GetComponent<NitroSystem>();

            if (nitroSystem == null)
            {
                Debug.LogError(
                    "NitroVfxController requires a NitroSystem.",
                    this
                );
                enabled = false;
                return false;
            }

            if (
                SystemsAreReady(coreSystems) &&
                SystemsAreReady(glowSystems) &&
                particleMaterial != null &&
                particleTexture != null
            )
            {
                return true;
            }

            ReleaseRuntimeResources();

            if (!CreateRuntimeMaterial())
            {
                enabled = false;
                return false;
            }

            coreSystems = new[]
            {
                CreateParticleSystem(
                    "Left Nitro Core",
                    leftExhaustPosition,
                    true
                ),
                CreateParticleSystem(
                    "Right Nitro Core",
                    rightExhaustPosition,
                    true
                )
            };

            glowSystems = new[]
            {
                CreateParticleSystem(
                    "Left Nitro Glow",
                    leftExhaustPosition,
                    false
                ),
                CreateParticleSystem(
                    "Right Nitro Glow",
                    rightExhaustPosition,
                    false
                )
            };

            previousVehiclePosition = transform.position;
            hasPreviousVehiclePosition = true;
            return true;
        }

        private bool CreateRuntimeMaterial()
        {
            Shader shader = Shader.Find(
                "Universal Render Pipeline/Particles/Unlit"
            );

            if (shader == null || !shader.isSupported)
            {
                Debug.LogError(
                    "Nitro VFX requires the supported URP Particles/Unlit shader.",
                    this
                );
                return false;
            }

            particleTexture = CreateSoftParticleTexture();
            particleMaterial = new Material(shader)
            {
                name = "Runtime Nitro Additive Material",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent
            };

            particleMaterial.SetOverrideTag("RenderType", "Transparent");
            SetMaterialColor(particleMaterial, Color.white);
            SetMaterialTexture(particleMaterial, particleTexture);

            if (particleMaterial.HasProperty("_Surface"))
            {
                particleMaterial.SetFloat("_Surface", 1f);
            }

            if (particleMaterial.HasProperty("_SrcBlend"))
            {
                particleMaterial.SetFloat(
                    "_SrcBlend",
                    (float)BlendMode.SrcAlpha
                );
            }

            if (particleMaterial.HasProperty("_DstBlend"))
            {
                particleMaterial.SetFloat(
                    "_DstBlend",
                    (float)BlendMode.One
                );
            }

            if (particleMaterial.HasProperty("_ZWrite"))
            {
                particleMaterial.SetFloat("_ZWrite", 0f);
            }

            particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return true;
        }

        private ParticleSystem CreateParticleSystem(
            string objectName,
            Vector3 localPosition,
            bool core
        )
        {
            GameObject particleObject = new GameObject(objectName);
            particleObject.layer = gameObject.layer;
            particleObject.transform.SetParent(transform, false);
            particleObject.transform.localPosition = localPosition;
            particleObject.transform.localRotation =
                Quaternion.Euler(0f, 180f, 0f);

            ParticleSystem system =
                particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            main.startLifetime = core
                ? new ParticleSystem.MinMaxCurve(0.08f, 0.14f)
                : new ParticleSystem.MinMaxCurve(0.15f, 0.24f);
            main.startSpeed = core
                ? new ParticleSystem.MinMaxCurve(6f, 10f)
                : new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
            main.startSize = core
                ? new ParticleSystem.MinMaxCurve(0.1f, 0.17f)
                : new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
            main.startRotation = new ParticleSystem.MinMaxCurve(
                0f,
                Mathf.PI * 2f
            );
            main.startColor = core
                ? new ParticleSystem.MinMaxGradient(
                    new Color(0.55f, 0.95f, 1f, 0.95f),
                    new Color(0.1f, 0.55f, 1f, 0.8f)
                )
                : new ParticleSystem.MinMaxGradient(
                    new Color(0.1f, 0.65f, 1f, 0.42f),
                    new Color(0.02f, 0.25f, 1f, 0.28f)
                );

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = core ? 5f : 11f;
            shape.radius = core ? 0.035f : 0.065f;
            shape.radiusThickness = 1f;

            ParticleSystem.ColorOverLifetimeModule color =
                system.colorOverLifetime;
            color.enabled = true;
            color.color = CreateLifetimeGradient(core);

            ParticleSystem.SizeOverLifetimeModule size =
                system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, core ? 0.65f : 0.45f),
                    new Keyframe(0.25f, 1f),
                    new Keyframe(1f, core ? 0.25f : 0.7f)
                )
            );

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = !core;
            if (!core)
            {
                noise.quality = ParticleSystemNoiseQuality.Low;
                noise.strength = 0.08f;
                noise.frequency = 0.7f;
                noise.scrollSpeed = 0.35f;
            }

            ParticleSystemRenderer renderer =
                particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = core ? 2.4f : 1.7f;
            renderer.velocityScale = core ? 0.08f : 0.05f;
            renderer.sharedMaterial = particleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;

            system.Play(false);
            return system;
        }

        private static Gradient CreateLifetimeGradient(bool core)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(
                        core
                            ? new Color(0.75f, 0.98f, 1f)
                            : new Color(0.15f, 0.75f, 1f),
                        0f
                    ),
                    new GradientColorKey(
                        core
                            ? new Color(0.05f, 0.45f, 1f)
                            : new Color(0.02f, 0.18f, 0.85f),
                        1f
                    )
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(core ? 1f : 0.55f, 0.12f),
                    new GradientAlphaKey(core ? 0.7f : 0.3f, 0.65f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
        }

        private static Texture2D CreateSoftParticleTexture()
        {
            Texture2D texture = new Texture2D(
                TextureSize,
                TextureSize,
                TextureFormat.RGBA32,
                false,
                true
            )
            {
                name = "Runtime Nitro Particle Texture",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float nx = (x + 0.5f) / TextureSize * 2f - 1f;
                    float ny = (y + 0.5f) / TextureSize * 2f - 1f;
                    float distance = Mathf.Sqrt(nx * nx + ny * ny);
                    float alpha = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.Clamp01(1f - distance)
                    );
                    pixels[y * TextureSize + x] = new Color32(
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

        private void SetAllEmission(float intensity)
        {
            SetEmission(coreSystems, coreEmissionRate * intensity);
            SetEmission(glowSystems, glowEmissionRate * intensity);
        }

        private static void SetEmission(
            ParticleSystem[] systems,
            float rate
        )
        {
            if (systems == null)
            {
                return;
            }

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTime = Mathf.Max(0f, rate);

                if (!system.isPlaying)
                {
                    system.Play(false);
                }
            }
        }

        private void ClearParticles()
        {
            ClearParticles(coreSystems);
            ClearParticles(glowSystems);
        }

        private static void ClearParticles(ParticleSystem[] systems)
        {
            if (systems == null)
            {
                return;
            }

            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                {
                    systems[i].Clear(false);
                }
            }
        }

        private static void AddParticleCount(
            ParticleSystem[] systems,
            ref int count
        )
        {
            if (systems == null)
            {
                return;
            }

            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                {
                    count += systems[i].particleCount;
                }
            }
        }

        private static bool SystemsAreReady(ParticleSystem[] systems)
        {
            if (systems == null || systems.Length != 2)
            {
                return false;
            }

            return systems[0] != null && systems[1] != null;
        }

        private void ReleaseRuntimeResources()
        {
            DestroySystems(coreSystems);
            DestroySystems(glowSystems);
            coreSystems = null;
            glowSystems = null;

            DestroyRuntimeObject(particleMaterial);
            DestroyRuntimeObject(particleTexture);
            particleMaterial = null;
            particleTexture = null;
        }

        private static void DestroySystems(ParticleSystem[] systems)
        {
            if (systems == null)
            {
                return;
            }

            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                {
                    DestroyRuntimeObject(systems[i].gameObject);
                }
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
    }
}
