using UnityEngine;

namespace Aetheria.Combat
{
    /// <summary>
    /// Minimal player HP/MP runtime state for combat loop and HUD.
    /// </summary>
    public sealed class PlayerCombatStats : MonoBehaviour
    {
        [SerializeField] private float maxHp = 200f;
        [SerializeField] private float maxMp = 120f;
        [SerializeField] private float hpRegenPerSecond = 1.2f;
        [SerializeField] private float mpRegenPerSecond = 4.0f;

        private float _hp;
        private float _mp;

        public float CurrentHp => _hp;
        public float CurrentMp => _mp;
        public float MaxHp => maxHp;
        public float MaxMp => maxMp;
        public bool IsAlive => _hp > 0f;

        private void Awake()
        {
            _hp = maxHp;
            _mp = maxMp;
        }

        private void Update()
        {
            if (_hp <= 0f) return;
            _hp = Mathf.Min(maxHp, _hp + hpRegenPerSecond * Time.deltaTime);
            _mp = Mathf.Min(maxMp, _mp + mpRegenPerSecond * Time.deltaTime);
        }

        public void TakeDamage(float amount)
        {
            if (_hp <= 0f) return;
            _hp -= Mathf.Max(0f, amount);
            if (_hp <= 0f)
            {
                _hp = 0f;
                Debug.Log("[PlayerCombatStats] Player defeated.");
            }
        }

        public bool TrySpendMp(float amount)
        {
            amount = Mathf.Max(0f, amount);
            if (_mp < amount) return false;
            _mp -= amount;
            return true;
        }
    }
}
