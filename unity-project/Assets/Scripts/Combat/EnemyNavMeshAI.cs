using UnityEngine;
using UnityEngine.AI;

namespace Aetheria.Combat
{
    /// <summary>
    /// Baseline NavMesh enemy loop: idle -> chase -> short melee hit window.
    /// Keeps behavior simple and stable for early gameplay tests.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyNavMeshAI : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private float chaseDistance = 15f;
        [SerializeField] private float attackRange = 1.7f;
        [SerializeField] private float attackDamage = 8f;
        [SerializeField] private float attackCooldown = 1.15f;
        [SerializeField] private bool stopAndFaceOnAttack = true;

        private NavMeshAgent _agent;
        private float _nextAttackTime;
        private PlayerCombatStats _playerStats;

        /// <summary>
        /// Runtime tuning hook for procedural island enemies.
        /// </summary>
        public void ConfigureChase(float chaseDistanceMeters, float attackRangeMeters)
        {
            chaseDistance = Mathf.Max(3f, chaseDistanceMeters);
            attackRange = Mathf.Clamp(attackRangeMeters, 0.8f, 4f);
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (player == null && Camera.main != null)
                player = Camera.main.transform;
        }

        private void Update()
        {
            if (player == null && Camera.main != null)
                player = Camera.main.transform;
            if (player == null || _agent == null)
                return;
            if (!_agent.isActiveAndEnabled || !_agent.isOnNavMesh)
                return;
            if (_playerStats == null && player != null)
                _playerStats = player.GetComponentInParent<PlayerCombatStats>();

            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            if (dist > chaseDistance)
            {
                _agent.ResetPath();
                return;
            }

            if (dist > attackRange)
            {
                _agent.isStopped = false;
                _agent.SetDestination(player.position);
                return;
            }

            // In range
            _agent.ResetPath();
            _agent.isStopped = true;

            if (stopAndFaceOnAttack && toPlayer.sqrMagnitude > 0.0001f)
            {
                var look = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 8f);
            }

            if (Time.time < _nextAttackTime)
                return;

            _nextAttackTime = Time.time + Mathf.Max(0.2f, attackCooldown);
            if (_playerStats != null)
                _playerStats.TakeDamage(attackDamage);
            Debug.Log($"[EnemyNavMeshAI] {name} attacks player for {attackDamage:0.#}");
        }
    }
}
