using UnityEngine;

namespace Aetheria.World
{
    /// <summary>
    /// Ambient world effects: birds, wind, falling leaves.
    /// Assign AudioClips for birds. Leaves use a particle system.
    /// Auto-creates leaves particle if none assigned.
    /// </summary>
    public class AmbientWorld : MonoBehaviour
    {
        [Header("Birds")]
        [SerializeField] private AudioClip[] birdClips;
        [SerializeField] private float birdIntervalMin = 3f;
        [SerializeField] private float birdIntervalMax = 8f;
        [SerializeField] private float birdVolume = 0.4f;

        [Header("Wind")]
        [SerializeField] private WindZone windZone;
        [SerializeField] private float windBaseStrength = 0.3f;
        [SerializeField] private float windGustStrength = 0.8f;
        [SerializeField] private float windGustInterval = 5f;

        [Header("Leaves")]
        [SerializeField] private ParticleSystem leavesParticle;
        [SerializeField] private int leavesEmitRate = 8;
        [SerializeField] private bool autoCreateLeaves = true;
        [SerializeField] private Vector3 leavesAreaSize = new Vector3(30f, 10f, 30f);

        private AudioSource _birdSource;
        private float _nextBirdTime;
        private float _nextGustTime;

        void Start()
        {
            if (birdClips != null && birdClips.Length > 0)
            {
                _birdSource = gameObject.AddComponent<AudioSource>();
                _birdSource.playOnAwake = false;
                _birdSource.spatialBlend = 0f;
                _nextBirdTime = Time.time + Random.Range(2f, 5f);
            }

            if (windZone == null)
                windZone = FindObjectOfType<WindZone>();
            if (windZone != null)
                windZone.windMain = windBaseStrength;
            _nextGustTime = Time.time + windGustInterval;

            if (leavesParticle == null && autoCreateLeaves)
                leavesParticle = CreateLeavesParticle();
            if (leavesParticle != null && !leavesParticle.isPlaying)
                leavesParticle.Play();
        }

        private ParticleSystem CreateLeavesParticle()
        {
            var go = new GameObject("LeavesParticle");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(0f, 5f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = 4f;
            main.startSpeed = 0.5f;
            main.startSize = 0.15f;
            main.gravityModifier = 0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.4f, 0.6f, 0.2f, 0.8f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = leavesAreaSize;

            var emission = ps.emission;
            emission.rateOverTime = leavesEmitRate;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.5f, 0.4f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            if (shader != null)
                renderer.material = new Material(shader);
            return ps;
        }

        void Update()
        {
            if (_birdSource != null && birdClips != null && birdClips.Length > 0 && Time.time >= _nextBirdTime)
            {
                var clip = birdClips[Random.Range(0, birdClips.Length)];
                if (clip != null)
                {
                    _birdSource.PlayOneShot(clip, birdVolume);
                    _nextBirdTime = Time.time + Random.Range(birdIntervalMin, birdIntervalMax);
                }
            }

            if (windZone != null && Time.time >= _nextGustTime)
            {
                windZone.windMain = windBaseStrength + Random.Range(0f, windGustStrength);
                _nextGustTime = Time.time + windGustInterval;
            }
        }
    }
}
