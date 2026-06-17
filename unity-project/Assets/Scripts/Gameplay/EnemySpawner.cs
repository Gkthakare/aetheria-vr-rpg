using System.Collections.Generic;
using Aetheria.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Aetheria.Gameplay
{
    /// <summary>
    /// Controlled enemy spawner around the player (caps count, spawn interval, NavMesh sample).
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Transform player;
        [SerializeField] private float spawnRadius = 20f;
        [SerializeField] private int maxEnemies = 8;
        [SerializeField] private float spawnInterval = 1.5f;
        [SerializeField] private float navMeshSampleRadius = 7f;
        [SerializeField] private bool addCombatComponentsIfMissing = true;

        private readonly List<GameObject> _alive = new List<GameObject>();
        private float _nextSpawnTime;

        private void Awake()
        {
            if (player == null && Camera.main != null)
                player = Camera.main.transform;
        }

        private void Update()
        {
            CleanupDeadRefs();
            if (enemyPrefab == null || player == null)
                return;
            if (_alive.Count >= Mathf.Max(1, maxEnemies))
                return;
            if (Time.time < _nextSpawnTime)
                return;

            _nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnInterval);
            TrySpawn();
        }

        private void TrySpawn()
        {
            Vector3 random = player.position + Random.insideUnitSphere * spawnRadius;
            random.y = player.position.y;

            if (!NavMesh.SamplePosition(random, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
                return;

            var go = Instantiate(enemyPrefab, hit.position, Quaternion.identity, transform);
            if (string.IsNullOrEmpty(go.tag) || go.tag == "Untagged")
                go.tag = "Enemy";

            if (addCombatComponentsIfMissing)
            {
                if (go.GetComponent<EnemyController>() == null)
                    go.AddComponent<EnemyController>();
                if (go.GetComponent<EnemyNavMeshAI>() == null)
                    go.AddComponent<EnemyNavMeshAI>();
                if (go.GetComponent<NavMeshAgent>() == null)
                    go.AddComponent<NavMeshAgent>();
            }

            _alive.Add(go);
        }

        private void CleanupDeadRefs()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] == null)
                    _alive.RemoveAt(i);
            }
        }
    }
}
