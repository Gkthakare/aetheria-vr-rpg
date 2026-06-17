using UnityEngine;

namespace Aetheria.Combat
{
    /// <summary>
    /// Editor / desktop test melee: sphere-cast forward from <see cref="strikeOrigin"/> (defaults to main camera).
    /// Uses left mouse or Unity "Fire1". Add to XR Origin or an empty alongside the player rig.
    /// Enemy needs a <see cref="Collider"/> and <see cref="EnemyController"/> on self or parent.
    /// </summary>
    public class SimpleMeleeDriver : MonoBehaviour
    {
        [SerializeField] private Transform strikeOrigin;

        [SerializeField] private float range = 2.5f;

        [SerializeField] private float sphereRadius = 0.35f;

        [SerializeField] private float damage = 20f;

        [SerializeField] private float cooldown = 0.35f;

        [SerializeField] private KeyCode primaryKey = KeyCode.Mouse0;

        private float _nextStrikeTime;

        private void Awake()
        {
            if (strikeOrigin == null && Camera.main != null)
                strikeOrigin = Camera.main.transform;
        }

        private void Update()
        {
            if (strikeOrigin == null) return;

            bool fire = Input.GetKeyDown(primaryKey) || Input.GetButtonDown("Fire1");
            if (!fire || Time.time < _nextStrikeTime) return;

            _nextStrikeTime = Time.time + cooldown;

            Vector3 origin = strikeOrigin.position;
            Vector3 dir = strikeOrigin.forward;

            if (Physics.SphereCast(origin, sphereRadius, dir, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
            {
                var enemy = hit.collider.GetComponentInParent<EnemyController>();
                if (enemy != null)
                {
                    enemy.TakeDamage(damage);
                    Debug.Log($"[SimpleMeleeDriver] Hit {enemy.name} for {damage:0.#} (dist {hit.distance:0.##}m)");
                }
            }
        }
    }
}
