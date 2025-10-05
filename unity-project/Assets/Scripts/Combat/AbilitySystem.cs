using UnityEngine;
using System.Collections.Generic;

namespace Aetheria.Combat
{
    /// <summary>
    /// Minimal ability system scaffold: stats, stamina, cooldowns, and queued skills.
    /// Designed to be deterministic on the server by consuming inputs and timestamps.
    /// </summary>
    public class AbilitySystem : MonoBehaviour
    {
        [Header("Stats")]
        public float maxStamina = 100f;
        public float staminaRegenPerSec = 10f;
        public float weaponWeight = 1.0f; // affects stamina cost

        [Header("Runtime")]
        public float stamina;

        private readonly Dictionary<string, float> cooldownUntil = new Dictionary<string, float>();

        private void Awake()
        {
            stamina = maxStamina;
        }

        private void Update()
        {
            stamina = Mathf.Min(maxStamina, stamina + staminaRegenPerSec * Time.deltaTime);

            // Example keyboard inputs to demo logic in Editor
            if (Input.GetKeyDown(KeyCode.J)) TryUseSkill(WeaponSkills.LightCut);
            if (Input.GetKeyDown(KeyCode.K)) TryUseSkill(WeaponSkills.HeavyCleave);
            if (Input.GetKeyDown(KeyCode.L)) TryUseSkill(WeaponSkills.FinisherThrust);
        }

        public bool TryUseSkill(WeaponSkill skill)
        {
            string key = skill.Id;
            float now = Time.time;

            if (cooldownUntil.TryGetValue(key, out float until) && now < until)
            {
                Debug.Log($"[Ability] {key} on CD {(until - now):0.00}s");
                return false;
            }

            float cost = skill.BaseStaminaCost * weaponWeight;
            if (stamina < cost)
            {
                Debug.Log($"[Ability] Not enough stamina for {key}");
                return false;
            }

            stamina -= cost;
            cooldownUntil[key] = now + skill.Cooldown;
            Debug.Log($"[Ability] Cast {key} cost {cost:0} stamina. Remaining {stamina:0}");
            // TODO: trigger animation/events/damage windows
            return true;
        }
    }

    public static class WeaponSkills
    {
        public static readonly WeaponSkill LightCut = new WeaponSkill("light_cut", 10f, 0.6f);
        public static readonly WeaponSkill HeavyCleave = new WeaponSkill("heavy_cleave", 20f, 1.2f);
        public static readonly WeaponSkill FinisherThrust = new WeaponSkill("finisher_thrust", 30f, 2.0f);
    }

    public sealed class WeaponSkill
    {
        public string Id { get; }
        public float BaseStaminaCost { get; }
        public float Cooldown { get; }
        public WeaponSkill(string id, float cost, float cd)
        { Id = id; BaseStaminaCost = cost; Cooldown = cd; }
    }
}


