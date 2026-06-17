using UnityEngine;

namespace Aetheria.Combat
{
    /// <summary>
    /// SAO-style combat VFX. Assign to player or weapon. Hooks into AbilitySystem.
    /// </summary>
    public class AbilityVFX : MonoBehaviour
    {
        [Header("Hit Effects")]
        [SerializeField] private ParticleSystem hitSparks;
        [SerializeField] private Transform vfxSpawnPoint;

        [Header("Skill Effects")]
        [SerializeField] private ParticleSystem lightCutVFX;
        [SerializeField] private ParticleSystem heavyCleaveVFX;
        [SerializeField] private ParticleSystem finisherVFX;
        [SerializeField] private ParticleSystem bloodSpatter;

        [Header("Defaults")]
        [SerializeField] private bool autoCreateDefaultVfx = true;

        private void Awake()
        {
            if (vfxSpawnPoint == null) vfxSpawnPoint = transform;
            if (autoCreateDefaultVfx)
            {
                if (hitSparks == null) hitSparks = CreateBurstSparks("HitSparks", new Color(1f, 0.85f, 0.2f), 8, 0.05f, 0.15f);
                if (lightCutVFX == null) lightCutVFX = CreateBurstSparks("LightCutVFX", new Color(0.5f, 0.9f, 1f), 18, 0.06f, 0.25f);
                if (heavyCleaveVFX == null) heavyCleaveVFX = CreateBurstSparks("HeavyCleaveVFX", new Color(0.9f, 0.4f, 1f), 24, 0.07f, 0.3f);
                if (finisherVFX == null) finisherVFX = CreateBurstSparks("FinisherVFX", new Color(1f, 0.95f, 0.5f), 32, 0.08f, 0.35f);
                ParentVfxToSpawn(hitSparks);
                ParentVfxToSpawn(lightCutVFX);
                ParentVfxToSpawn(heavyCleaveVFX);
                ParentVfxToSpawn(finisherVFX);
            }
        }

        private void ParentVfxToSpawn(ParticleSystem ps)
        {
            if (ps == null || vfxSpawnPoint == null) return;
            ps.transform.SetParent(vfxSpawnPoint, false);
            ps.transform.localPosition = Vector3.zero;
            ps.transform.localRotation = Quaternion.identity;
        }

        private static ParticleSystem CreateBurstSparks(string name, Color color, int burstCount, float size, float lifetime)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.35f;
            main.startLifetime = lifetime;
            main.startSpeed = 2.5f;
            main.startSize = size;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = g;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit");
            if (sh != null)
            {
                rend.material = new Material(sh);
                if (rend.material.HasProperty("_BaseColor"))
                    rend.material.SetColor("_BaseColor", color);
            }

            return ps;
        }

        public void PlayHitSparks(Vector3 position)
        {
            if (hitSparks != null)
            {
                hitSparks.transform.position = position;
                hitSparks.Play();
            }
        }

        public void PlaySkillVFX(WeaponSkill skill)
        {
            Vector3 pos = vfxSpawnPoint != null ? vfxSpawnPoint.position : transform.position;
            Quaternion rot = vfxSpawnPoint != null ? vfxSpawnPoint.rotation : transform.rotation;
            ParticleSystem ps = skill.Id switch
            {
                "light_cut" => lightCutVFX,
                "heavy_cleave" => heavyCleaveVFX,
                "finisher_thrust" => finisherVFX,
                _ => null
            };
            if (ps != null)
            {
                ps.transform.position = pos;
                ps.transform.rotation = rot;
                ps.Play();
            }
        }

        public void PlayBloodSpatter(Vector3 position)
        {
            if (bloodSpatter != null)
            {
                bloodSpatter.transform.position = position;
                bloodSpatter.Play();
            }
        }
    }
}
