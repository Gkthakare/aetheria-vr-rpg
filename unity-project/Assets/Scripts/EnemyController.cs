using UnityEngine;

namespace Aetheria.Combat
{
    /// <summary>
    /// Minimal HP + death. Pair with <see cref="SimpleFleeFromTarget"/> for island chase prototype.
    /// <see cref="AbilitySystem"/> and <see cref="SimpleMeleeDriver"/> call <see cref="TakeDamage"/>.
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] private float maxHp = 50f;
        [SerializeField] private float maxMp = 20f;
        [SerializeField] private float mpRegenPerSecond = 1.5f;

        private float _hp;
        private float _mp;

        public bool IsAlive => _hp > 0;

        public float HealthNormalized => maxHp > 0f ? _hp / maxHp : 0f;
        public float ManaNormalized => maxMp > 0f ? _mp / maxMp : 0f;
        public float CurrentHp => _hp;
        public float MaxHp => maxHp;
        public float CurrentMp => _mp;
        public float MaxMp => maxMp;

        public void ApplyBossStatMultiplier(float hpMul, float mpMul)
        {
            hpMul = Mathf.Max(0.1f, hpMul);
            mpMul = Mathf.Max(0.1f, mpMul);
            maxHp *= hpMul;
            maxMp *= mpMul;
            _hp = Mathf.Min(maxHp, _hp * hpMul);
            _mp = Mathf.Min(maxMp, _mp * mpMul);
        }

        private void Start()
        {
            _hp = maxHp;
            _mp = maxMp;
        }

        private void Update()
        {
            if (_hp <= 0f) return;
            if (maxMp > 0f)
                _mp = Mathf.Min(maxMp, _mp + mpRegenPerSecond * Time.deltaTime);
        }

        public void TakeDamage(float amount)
        {
            if (_hp <= 0f) return;
            _hp -= amount;
            if (_hp <= 0f) Die();
        }

        private void Die()
        {
            var flee = GetComponent<SimpleFleeFromTarget>();
            if (flee != null) flee.enabled = false;
            var ai = GetComponent<EnemyNavMeshAI>();
            if (ai != null) ai.enabled = false;
            Destroy(gameObject);
        }
    }
}
