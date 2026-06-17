using UnityEngine;

namespace Aetheria.Combat
{
    /// <summary>
    /// Simple green wind-mana arrow projectile. Damages EnemyController on trigger hit.
    /// </summary>
    public sealed class WindManaArrow : MonoBehaviour
    {
        [SerializeField] private float speed = 26f;
        [SerializeField] private float lifeSeconds = 4f;
        [SerializeField] private float damage = 22f;

        private float _dieAt;

        public void Initialize(float launchSpeed, float launchDamage)
        {
            speed = Mathf.Max(1f, launchSpeed);
            damage = Mathf.Max(1f, launchDamage);
        }

        private void Start()
        {
            _dieAt = Time.time + lifeSeconds;
        }

        private void Update()
        {
            transform.position += transform.forward * (speed * Time.deltaTime);
            if (Time.time >= _dieAt)
                Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHit(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryHit(other);
        }

        private void TryHit(Collider other)
        {
            var enemy = other.GetComponentInParent<EnemyController>();
            if (enemy != null && enemy.IsAlive)
            {
                enemy.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }

            if (!other.isTrigger && !other.CompareTag("Player"))
                Destroy(gameObject);
        }
    }
}
