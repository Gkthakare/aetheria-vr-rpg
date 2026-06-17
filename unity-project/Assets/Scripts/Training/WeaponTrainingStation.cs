using UnityEngine;
using UnityEngine.Events;
using Aetheria.Combat;

namespace Aetheria.Training
{
    public enum WeaponSkillType { LightCut, HeavyCleave, FinisherThrust }

    /// <summary>
    /// Weapon training: perform the requested skill (from AbilitySystem) for VFX feedback.
    /// Focus on perfect execution and clear visual response.
    /// </summary>
    public class WeaponTrainingStation : MonoBehaviour
    {
        [Header("Target skill")]
        [SerializeField] private WeaponSkillType skillType = WeaponSkillType.LightCut;
        [SerializeField] private KeyCode triggerKey = KeyCode.J;

        [Header("Dependencies")]
        [SerializeField] private AbilitySystem abilitySystem;
        [SerializeField] private TrainingVFX vfx;

        [Header("Events")]
        public UnityEvent OnSuccess;
        public UnityEvent OnFail;

        private void Awake()
        {
            if (abilitySystem == null) abilitySystem = FindFirstObjectByType<AbilitySystem>();
            if (vfx == null) vfx = GetComponentInChildren<TrainingVFX>();
        }

        private WeaponSkill ResolveSkill()
        {
            switch (skillType)
            {
                case WeaponSkillType.LightCut: return WeaponSkills.LightCut;
                case WeaponSkillType.HeavyCleave: return WeaponSkills.HeavyCleave;
                case WeaponSkillType.FinisherThrust: return WeaponSkills.FinisherThrust;
                default: return WeaponSkills.LightCut;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(triggerKey))
            {
                var skill = ResolveSkill();
                bool ok = abilitySystem != null && abilitySystem.TryUseSkill(skill);
                if (vfx != null)
                {
                    if (ok) vfx.PlaySuccess();
                    else vfx.PlayFail();
                }
                if (ok) OnSuccess?.Invoke();
                else OnFail?.Invoke();
            }
        }

        public void SetTargetSkillType(WeaponSkillType type) => skillType = type;
    }
}
