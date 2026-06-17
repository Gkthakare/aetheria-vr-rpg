using UnityEngine;

namespace Aetheria.Combat
{
    /// <summary>
    /// Trigger-based sword damage with per-target hit cooldown and velocity-based bonus for VR swings.
    /// Put on the sword object that has a trigger collider.
    /// </summary>
    public sealed class VRSwordDamage : MonoBehaviour
    {
        [SerializeField] private float baseDamage = 18f;
        [SerializeField] private float maxDamage = 44f;
        [SerializeField] private float velocityForMaxDamage = 4.5f;
        [SerializeField] private float perTargetCooldown = 0.28f;
        [SerializeField] private bool requireMinimumSwingSpeed = true;
        [SerializeField] private float minSwingSpeed = 0.45f;

        private Vector3 _lastPos;
        private float _swingSpeed;
        private readonly System.Collections.Generic.Dictionary<int, float> _hitCooldowns = new System.Collections.Generic.Dictionary<int, float>();

        private void Start()
        {
            _lastPos = transform.position;
        }

        private void Update()
        {
            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            _swingSpeed = Vector3.Distance(transform.position, _lastPos) / dt;
            _lastPos = transform.position;
        }

        private void OnTriggerEnter(Collider other)
        {
            var enemy = other.GetComponentInParent<EnemyController>();
            if (enemy == null)
                return;

            if (requireMinimumSwingSpeed && _swingSpeed < minSwingSpeed)
                return;

            int key = enemy.GetInstanceID();
            if (_hitCooldowns.TryGetValue(key, out var t) && Time.time < t)
                return;
            _hitCooldowns[key] = Time.time + Mathf.Max(0.05f, perTargetCooldown);

            float alpha = Mathf.Clamp01(_swingSpeed / Mathf.Max(0.1f, velocityForMaxDamage));
            float dmg = Mathf.Lerp(baseDamage, maxDamage, alpha);
            enemy.TakeDamage(dmg);
        }
    }
}
