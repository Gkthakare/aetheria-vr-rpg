using UnityEngine;

namespace Aetheria.Dungeons
{
    /// <summary>
    /// Realistic portal VFX: swirling particles + pulsing glow.
    /// Attach to portal GameObject. Works with TeleportPortal.
    /// Auto-creates particles and light if none assigned.
    /// </summary>
    [RequireComponent(typeof(TeleportPortal))]
    public class PortalVFX : MonoBehaviour
    {
        [Header("Particles")]
        [SerializeField] private ParticleSystem swirlParticles;
        [SerializeField] private ParticleSystem edgeParticles;
        [SerializeField] private float swirlSpeed = 2f;
        [SerializeField] private bool autoCreateParticles = true;

        [Header("Glow")]
        [SerializeField] private Renderer portalSurface;
        [SerializeField] private Light glowLight;
        [SerializeField] private Color glowColor = new Color(0.2f, 0.5f, 1f);
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseMin = 0.3f;
        [SerializeField] private float pulseMax = 1f;
        [SerializeField] private bool autoCreateLight = true;
        [Tooltip("Uncheck to stop the glow light and surface from pulsing every frame.")]
        [SerializeField] private bool animateGlowPulse = false;

        private Material _portalMat;
        private float _phase;
        private float _baseLightIntensity;

        void Start()
        {
            if (portalSurface != null && portalSurface.material != null)
                _portalMat = portalSurface.material;
            else
                portalSurface = GetComponentInChildren<Renderer>();

            if (glowLight == null && autoCreateLight)
                CreateGlowLight();
            if (glowLight != null)
                _baseLightIntensity = glowLight.intensity;

            if (swirlParticles == null && autoCreateParticles)
                swirlParticles = CreateSwirlParticles();
            if (swirlParticles != null && !swirlParticles.isPlaying)
                swirlParticles.Play();
            if (edgeParticles != null && !edgeParticles.isPlaying)
                edgeParticles.Play();
        }

        private void CreateGlowLight()
        {
            var go = new GameObject("PortalGlow");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            glowLight = go.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = glowColor;
            glowLight.intensity = 1f;
            glowLight.range = 4f;
            _baseLightIntensity = 1f;
        }

        private ParticleSystem CreateSwirlParticles()
        {
            var go = new GameObject("PortalSwirl");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = 2f;
            main.startSpeed = 0.3f;
            main.startSize = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0.6f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;

            var emission = ps.emission;
            emission.rateOverTime = 15f;

            main.simulationSpeed = swirlSpeed;

            var rend = go.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Particles/Additive");
            if (shader != null)
            {
                rend.material = new Material(shader);
                if (rend.material.HasProperty("_BaseColor"))
                    rend.material.SetColor("_BaseColor", glowColor);
                else if (rend.material.HasProperty("_TintColor"))
                    rend.material.SetColor("_TintColor", new Color(glowColor.r, glowColor.g, glowColor.b, 0.8f));
            }
            return ps;
        }

        void Update()
        {
            if (!animateGlowPulse)
            {
                if (glowLight != null)
                {
                    glowLight.intensity = _baseLightIntensity;
                    glowLight.color = glowColor;
                }

                if (_portalMat != null && _portalMat.HasProperty("_BaseColor"))
                    _portalMat.SetColor("_BaseColor", glowColor);
                return;
            }

            _phase += Time.deltaTime * pulseSpeed;
            float t = (Mathf.Sin(_phase) + 1f) * 0.5f;
            float intensity = Mathf.Lerp(pulseMin, pulseMax, t);
            if (glowLight != null)
            {
                glowLight.intensity = _baseLightIntensity * intensity;
                glowLight.color = glowColor;
            }
            if (_portalMat != null && _portalMat.HasProperty("_BaseColor"))
                _portalMat.SetColor("_BaseColor", Color.Lerp(glowColor * 0.3f, glowColor, intensity));
        }
    }
}
