using UnityEngine;

namespace Aetheria.World
{
    /// <summary>
    /// Simple animal wandering. Add to a simple creature (cube/capsule).
    /// Moves randomly within radius.
    /// </summary>
    public class AnimalWander : MonoBehaviour
    {
        [SerializeField] private float wanderRadius = 15f;
        [SerializeField] private float moveSpeed = 1f;
        [SerializeField] private float idleTimeMin = 2f;
        [SerializeField] private float idleTimeMax = 5f;

        private Vector3 _target;
        private float _idleUntil;

        void Start()
        {
            PickNewTarget();
        }

        void Update()
        {
            if (Time.time < _idleUntil)
                return;

            transform.position = Vector3.MoveTowards(transform.position, _target, moveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, _target) < 0.5f)
            {
                PickNewTarget();
                _idleUntil = Time.time + Random.Range(idleTimeMin, idleTimeMax);
            }
        }

        private void PickNewTarget()
        {
            _target = transform.position + new Vector3(
                Random.Range(-wanderRadius, wanderRadius),
                0f,
                Random.Range(-wanderRadius, wanderRadius)
            );
        }
    }
}
