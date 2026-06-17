using System.Collections.Generic;
using Aetheria.Combat;
using Aetheria.Dungeons;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Aetheria.Gameplay
{
    /// <summary>
    /// Runs a minimum multi-floor dungeon combat loop (default 3 floors):
    /// regenerate dungeon floor, spawn enemy wave, clear wave -> next floor.
    /// </summary>
    public sealed class DungeonFloorCombatController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DungeonGenerator dungeonGenerator;
        [SerializeField] private DungeonRoomVisualizer dungeonVisualizer;
        [SerializeField] private SpawnDirector spawnDirector;
        [SerializeField] private Transform player;
        [SerializeField] private GameObject enemyPrefab;

        [Header("Imported enemy (scary monster)")]
        [Tooltip("When set, wave spawns use this FBX before Meshy catalog / enemy prefab.")]
        [SerializeField] private bool useImportedScaryMonsterForWaves = true;
        [SerializeField] private GameObject scaryMonsterPrefab;
#pragma warning disable 0414 // Editor-only binding path (TryBindScaryMonsterPrefabFromPath).
        [SerializeField] private string scaryMonsterAssetPath = "Assets/ImportedContent/Creatures/scary-monster/source/Monster.fbx";
#pragma warning restore 0414
        [SerializeField] private float scaryMonsterWaveScale = 0.4f;
        [SerializeField] private float scaryMonsterBossScale = 0.55f;

        [Header("Floor progression")]
        [SerializeField] private int startFloor = 1;
        [SerializeField] private int floorCount = 3;
        [SerializeField] private int dungeonSeed = 12345;
        [Tooltip("When off (default), the floating dungeon does not start on Play — use island/beach or context menu Begin Run. Enable for automatic multi-floor dungeon on Play.")]
        [SerializeField] private bool autoStartRunOnPlay;
        [SerializeField] private float floatingDungeonY = 22f;
        [SerializeField] private bool requirePortalToAdvance = true;
        [SerializeField] private Vector3 portalOffsetFromPlayer = new Vector3(0f, 0.6f, 2.6f);
        [SerializeField] private string floor3BossCatalogId = "creature.dragon.v1";
        [SerializeField] private float floor3BossScale = 0.85f;
        [Tooltip("Per-floor Meshy catalog IDs for normal enemies. Last entry repeats for remaining floors.")]
        [SerializeField] private string[] floorEnemyCatalogIds = { "creature.wolf.v1", "creature.wolf.v1", "creature.dragon.v1" };

        [Header("Enemy waves")]
        [SerializeField] private int baseEnemiesPerFloor = 5;
        [SerializeField] private int extraEnemiesPerFloor = 2;
        [SerializeField] private float spawnRadiusMin = 6f;
        [SerializeField] private float spawnRadiusMax = 16f;
        [SerializeField] private float navMeshSampleRadius = 8f;

        private readonly List<GameObject> _alive = new List<GameObject>();
        private int _currentFloor;
        private bool _runActive;
        private bool _awaitingPortal;
        private GameObject _advancePortal;
        private string _currentObjective = "Initializing...";
        private static bool _warnedEnemyTagMissing;

        public int CurrentFloor => _currentFloor;
        public int FloorCount => Mathf.Max(1, floorCount);
        public int RemainingEnemies => _alive.Count;
        public bool AwaitingPortal => _awaitingPortal;
        public string CurrentObjective => _currentObjective;
        public bool IsRunActive => _runActive;

        /// <summary>IslandWorldBuilder calls this after AddComponent so auto-start matches scene policy.</summary>
        public void SetAutoStartRunOnPlay(bool autoStart) => autoStartRunOnPlay = autoStart;

        private void Awake()
        {
#if UNITY_EDITOR
            TryBindScaryMonsterPrefabFromPath();
#endif
        }

        private void Start()
        {
            if (dungeonGenerator == null) dungeonGenerator = FindObjectOfType<DungeonGenerator>();
            if (dungeonVisualizer == null) dungeonVisualizer = FindObjectOfType<DungeonRoomVisualizer>();
            if (spawnDirector == null) spawnDirector = FindObjectOfType<SpawnDirector>();
            if (player == null) player = Camera.main != null ? Camera.main.transform : transform;
            if (dungeonVisualizer != null) dungeonVisualizer.SetAutoBuildOnStart(false);

            if (autoStartRunOnPlay)
                BeginRun();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            TryBindScaryMonsterPrefabFromPath();
        }

        private void TryBindScaryMonsterPrefabFromPath()
        {
            if (scaryMonsterPrefab != null || string.IsNullOrWhiteSpace(scaryMonsterAssetPath))
                return;
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(scaryMonsterAssetPath);
            if (p != null)
                scaryMonsterPrefab = p;
        }
#endif

        private void Update()
        {
            if (!_runActive) return;
            CleanupDead();
            if (_alive.Count > 0 || _awaitingPortal) return;

            if (_currentFloor >= startFloor + Mathf.Max(1, floorCount) - 1)
            {
                _runActive = false;
                _currentObjective = "Dungeon complete! Victory!";
                Debug.Log("[DungeonFloorCombat] Dungeon run complete! All floors cleared.");
                return;
            }

            if (requirePortalToAdvance)
            {
                SpawnAdvancePortal();
                _awaitingPortal = true;
                _currentObjective = $"Floor {_currentFloor} clear. Enter portal for Floor {_currentFloor + 1}.";
            }
            else
            {
                _currentFloor++;
                SetupFloorAndWave(_currentFloor);
            }
        }

        [ContextMenu("Begin 3-floor dungeon run")]
        public void BeginRun()
        {
            _alive.Clear();
            _currentFloor = Mathf.Max(1, startFloor);
            _runActive = true;
            _awaitingPortal = false;
            if (_advancePortal != null) Destroy(_advancePortal);
            _currentObjective = $"Floor {_currentFloor} starting...";
            Debug.Log(
                $"[DungeonFloorCombat] Dungeon run starting: rooms and wave spawns use floating world Y≈{floatingDungeonY:F1} " +
                $"(XZ ~50m from origin). Beach/island enemies are under Island_Enemies near sea level. " +
                $"Auto-start is off by default; enable **Auto Start Dungeon Combat On Play** on IslandWorldManager or **Auto Start Run On Play** here, or use this component's context menu **Begin 3-floor dungeon run** when you want the dungeon.",
                this);
            SetupFloorAndWave(_currentFloor);
        }

        public void OnAdvancePortalEntered()
        {
            if (!_awaitingPortal) return;
            _awaitingPortal = false;
            if (_advancePortal != null) Destroy(_advancePortal);
            _currentFloor++;
            _currentObjective = $"Floor {_currentFloor} starting...";
            SetupFloorAndWave(_currentFloor);
        }

        private void SetupFloorAndWave(int floor)
        {
            if (dungeonGenerator != null)
                dungeonGenerator.SetSeedAndFloor(dungeonSeed, floor);
            if (dungeonVisualizer != null)
                dungeonVisualizer.SetFloatingHeightWorldY(floatingDungeonY);
            if (dungeonVisualizer != null)
                dungeonVisualizer.SetFloorVisualTheme(floor);
            if (dungeonVisualizer != null)
                dungeonVisualizer.Build();

            int waveCount = Mathf.Max(1, baseEnemiesPerFloor + (floor - startFloor) * extraEnemiesPerFloor);
            SpawnWave(waveCount);
            _currentObjective = $"Floor {floor}: defeat {waveCount} enemies.";

            if (floor >= startFloor + Mathf.Max(1, floorCount) - 1)
                SpawnBossForFinalFloor();
            Debug.Log($"[DungeonFloorCombat] Floor {floor} started with {waveCount} enemies.");
        }

        private void SpawnWave(int count)
        {
            if (player == null)
            {
                Debug.LogWarning("[DungeonFloorCombat] Missing player.");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 p = FindSpawnPoint(i);
                GameObject go = null;
                if (useImportedScaryMonsterForWaves && scaryMonsterPrefab != null)
                {
                    go = Instantiate(scaryMonsterPrefab, p, Quaternion.identity);
                    if (!Mathf.Approximately(scaryMonsterWaveScale, 1f))
                        go.transform.localScale = Vector3.one * scaryMonsterWaveScale;
                    EnsureEnemyHitCollider(go);
                }

                if (go == null)
                    go = TrySpawnCatalogEnemyForCurrentFloor(p);
                if (go == null && enemyPrefab != null)
                    go = Instantiate(enemyPrefab, p, Quaternion.identity);
                if (go == null)
                    continue;
                go.name = $"Floor{_currentFloor}_Enemy_{i}";
                TrySetEnemyTag(go);

                if (go.GetComponent<EnemyController>() == null)
                    go.AddComponent<EnemyController>();
                var hasNavMesh = NavMesh.CalculateTriangulation().vertices.Length > 0;
                if (hasNavMesh)
                {
                    if (go.GetComponent<NavMeshAgent>() == null)
                        go.AddComponent<NavMeshAgent>();
                    if (go.GetComponent<EnemyNavMeshAI>() == null)
                        go.AddComponent<EnemyNavMeshAI>();
                }
                if (go.GetComponent<SimpleFleeFromTarget>() == null)
                    go.AddComponent<SimpleFleeFromTarget>();
                if (go.GetComponent<EnemyBillboardVitals>() == null)
                    go.AddComponent<EnemyBillboardVitals>();

                _alive.Add(go);
            }
        }

        private GameObject TrySpawnCatalogEnemyForCurrentFloor(Vector3 position)
        {
            if (spawnDirector == null || floorEnemyCatalogIds == null || floorEnemyCatalogIds.Length == 0)
                return null;
            int idx = Mathf.Clamp(_currentFloor - startFloor, 0, floorEnemyCatalogIds.Length - 1);
            var id = floorEnemyCatalogIds[idx];
            if (string.IsNullOrWhiteSpace(id))
                return null;
            return spawnDirector.TrySpawnFromCatalogId(id, position, Quaternion.identity, out var spawned) ? spawned : null;
        }

        private void SpawnBossForFinalFloor()
        {
            if (player == null)
                return;

            Vector3 pos = FindSpawnPoint(9000);
            GameObject boss = null;

            // Prefer Meshy catalog spawn for boss if available in SpawnDirector.
            if (spawnDirector != null && !string.IsNullOrEmpty(floor3BossCatalogId))
            {
                spawnDirector.TrySpawnFromCatalogId(
                    floor3BossCatalogId,
                    pos,
                    Quaternion.identity,
                    out boss,
                    floor3BossScale);
            }

            if (boss == null && useImportedScaryMonsterForWaves && scaryMonsterPrefab != null)
            {
                boss = Instantiate(scaryMonsterPrefab, pos, Quaternion.identity);
                boss.transform.localScale = Vector3.one * scaryMonsterBossScale;
                EnsureEnemyHitCollider(boss);
            }

            if (boss == null && enemyPrefab != null)
                boss = Instantiate(enemyPrefab, pos, Quaternion.identity);

            if (boss == null) return;

            boss.name = $"Floor{_currentFloor}_Boss";
            TrySetEnemyTag(boss);

            var enemy = boss.GetComponent<EnemyController>();
            if (enemy == null) enemy = boss.AddComponent<EnemyController>();
            enemy.ApplyBossStatMultiplier(3.2f, 2.3f);
            var hasNavMesh = NavMesh.CalculateTriangulation().vertices.Length > 0;
            if (hasNavMesh)
            {
                if (boss.GetComponent<NavMeshAgent>() == null) boss.AddComponent<NavMeshAgent>();
                var ai = boss.GetComponent<EnemyNavMeshAI>();
                if (ai == null) ai = boss.AddComponent<EnemyNavMeshAI>();
            }
            if (boss.GetComponent<SimpleFleeFromTarget>() == null) boss.AddComponent<SimpleFleeFromTarget>();
            if (boss.GetComponent<EnemyBillboardVitals>() == null) boss.AddComponent<EnemyBillboardVitals>();

            // Make final boss visibly stronger even if prefab is generic.
            boss.transform.localScale *= 1.25f;
            _alive.Add(boss);
            Debug.Log($"[DungeonFloorCombat] Final floor boss spawned via {(spawnDirector != null ? "Meshy catalog/fallback" : "fallback prefab")}.");
        }

        private void SpawnAdvancePortal()
        {
            if (player == null) return;
            if (_advancePortal != null) Destroy(_advancePortal);

            _advancePortal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _advancePortal.name = "DungeonAdvancePortal";
            _advancePortal.transform.position = player.position + player.forward * portalOffsetFromPlayer.z + Vector3.up * portalOffsetFromPlayer.y;
            _advancePortal.transform.localScale = new Vector3(0.8f, 0.2f, 0.8f);

            var col = _advancePortal.GetComponent<Collider>();
            if (col == null) col = _advancePortal.AddComponent<CapsuleCollider>();
            col.isTrigger = true;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.28f, 0.9f, 1f, 0.85f));
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0.28f, 0.9f, 1f, 0.85f));
            var r = _advancePortal.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;

            var p = _advancePortal.AddComponent<DungeonAdvancePortal>();
            p.Configure(this, player);

            Debug.Log("[DungeonFloorCombat] Floor cleared. Enter cyan portal to start next floor.");
        }

        private Vector3 FindSpawnPoint(int salt)
        {
            float angle = (salt * 0.6180339f + Random.value) * Mathf.PI * 2f;
            float radius = Random.Range(spawnRadiusMin, spawnRadiusMax);
            Vector3 raw = player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            raw.y = player.position.y;

            if (NavMesh.SamplePosition(raw, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
                return hit.position;
            return raw;
        }

        private void CleanupDead()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] == null)
                    _alive.RemoveAt(i);
            }
        }

        /// <summary>Imported meshes may ship without colliders; arrows need a physics body to hit.</summary>
        private static void EnsureEnemyHitCollider(GameObject go)
        {
            if (go == null) return;
            if (go.GetComponentInChildren<Collider>(true) != null)
                return;
            var cap = go.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 1f, 0f);
            cap.height = 2.2f;
            cap.radius = 0.45f;
        }

        private static void TrySetEnemyTag(GameObject go)
        {
            if (go == null) return;
            try
            {
                if (string.IsNullOrEmpty(go.tag) || go.tag == "Untagged")
                    go.tag = "Enemy";
            }
            catch (UnityException)
            {
                if (_warnedEnemyTagMissing) return;
                _warnedEnemyTagMissing = true;
                Debug.LogWarning("[DungeonFloorCombat] Tag 'Enemy' is not defined in Tags & Layers. Using component checks only.");
            }
        }
    }
}
