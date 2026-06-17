using System.Text;
using Aetheria.Combat;
using Aetheria.Dungeons;
using Aetheria.Voice;
using UnityEngine;

namespace Aetheria.Gameplay
{
    /// <summary>
    /// One-shot final scene readiness validator for VR combat loop:
    /// player -> bow -> mana -> fire -> enemy defeat -> HUD/status.
    /// </summary>
    /// <remarks>
    /// Runs after <see cref="Aetheria.World.IslandWorldBuilder"/> (<c>-50</c>) so
    /// <c>EnsureGameplayRuntimeSystems</c> has added voice/HUD/dungeon helpers first.
    /// </remarks>
    [DefaultExecutionOrder(50)]
    public sealed class FinalVrSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private bool validateOnStart = true;
        [SerializeField] private bool logPassAsInfo = true;
        [Tooltip("Before validation, force WindManaBow to spawn with bow in hand (not ground test mode).")]
        [SerializeField] private bool finalizeHandBowOnAwake = true;

        public bool LastPassed { get; private set; }
        public string LastReport { get; private set; } = "Not validated.";

        private void Start()
        {
            if (finalizeHandBowOnAwake)
            {
                foreach (var bow in FindObjectsOfType<WindManaBow>())
                {
                    if (bow != null)
                        bow.EnsureHandBowForDeployment();
                }
            }

            if (validateOnStart)
                ValidateReadiness();
        }

        [ContextMenu("Validate Final VR Scene Readiness")]
        public void ValidateReadiness()
        {
            var sb = new StringBuilder();
            bool ok = true;

            var xr = GameObject.Find("XR Origin (XR Rig)");
            bool hasXr = xr != null;
            ok &= hasXr;
            sb.AppendLine(hasXr ? "XR Rig: OK" : "XR Rig: MISSING");

            var playerStats = FindObjectOfType<PlayerCombatStats>();
            bool hasPlayerStats = playerStats != null;
            ok &= hasPlayerStats;
            sb.AppendLine(hasPlayerStats ? "PlayerCombatStats: OK" : "PlayerCombatStats: MISSING");

            var bow = FindObjectOfType<WindManaBow>();
            bool hasBow = bow != null;
            ok &= hasBow;
            sb.AppendLine(hasBow ? "WindManaBow: OK" : "WindManaBow: MISSING");

            var voice = FindObjectOfType<VoiceIntegrationSystem>();
            bool hasVoice = voice != null;
            ok &= hasVoice;
            sb.AppendLine(hasVoice ? "VoiceIntegrationSystem: OK" : "VoiceIntegrationSystem: MISSING");

            var screens = FindObjectOfType<PlayerEnemyStatusScreens>();
            bool hasScreens = screens != null;
            ok &= hasScreens;
            sb.AppendLine(hasScreens ? "Player/Enemy Screens: OK" : "Player/Enemy Screens: MISSING");

            var objectiveHud = FindObjectOfType<DungeonRunObjectiveHud>();
            bool hasObjectiveHud = objectiveHud != null;
            ok &= hasObjectiveHud;
            sb.AppendLine(hasObjectiveHud ? "Dungeon Objective HUD: OK" : "Dungeon Objective HUD: MISSING");

            var dungeonController = FindObjectOfType<DungeonFloorCombatController>();
            bool hasDungeon = dungeonController != null;
            ok &= hasDungeon;
            sb.AppendLine(hasDungeon ? "DungeonFloorCombatController: OK" : "DungeonFloorCombatController: MISSING");

            var visualizer = FindObjectOfType<DungeonRoomVisualizer>();
            bool hasVisualizer = visualizer != null;
            ok &= hasVisualizer;
            sb.AppendLine(hasVisualizer ? "DungeonRoomVisualizer: OK" : "DungeonRoomVisualizer: MISSING");

            var enemies = FindObjectsOfType<EnemyController>();
            bool hasEnemy = enemies != null && enemies.Length > 0;
            sb.AppendLine(hasEnemy ? $"Enemies Spawned: OK ({enemies.Length})" : "Enemies Spawned: NONE");

            var enemyVitals = FindObjectOfType<EnemyBillboardVitals>();
            bool hasEnemyVitals = enemyVitals != null;
            sb.AppendLine(hasEnemyVitals ? "Enemy Billboard Vitals: OK" : "Enemy Billboard Vitals: MISSING");

            var navValidator = FindObjectOfType<NavMeshSetupValidator>();
            bool hasNavValidator = navValidator != null;
            ok &= hasNavValidator;
            sb.AppendLine(hasNavValidator ? "NavMeshSetupValidator: OK" : "NavMeshSetupValidator: MISSING");
            if (hasNavValidator)
            {
                navValidator.ValidateNow();
                bool navOk = navValidator.LastNavMeshOk && navValidator.LastEnemyTagOk;
                ok &= navOk;
                sb.AppendLine(navOk ? "NavMesh/EnemyTag: OK" : "NavMesh/EnemyTag: NOT READY");
                ok &= navValidator.LastImportedAssetsOk;
                sb.AppendLine(navValidator.LastImportedAssetsOk ? "Imported Assets: OK" : "Imported Assets: MISSING");
            }

            bool combatLoopLikelyReady = hasXr && hasPlayerStats && hasBow && hasVoice && hasScreens && hasObjectiveHud && hasDungeon && hasVisualizer;
            ok &= combatLoopLikelyReady;
            sb.AppendLine(combatLoopLikelyReady
                ? "Combat Loop Path: READY (Bow -> Mana -> Fire -> Enemy/HUD)"
                : "Combat Loop Path: INCOMPLETE");

            LastPassed = ok;
            LastReport = sb.ToString().TrimEnd();
            if (ok)
            {
                if (logPassAsInfo)
                    Debug.Log($"[FinalVrSceneBootstrap] PASS\n{LastReport}");
            }
            else
            {
                Debug.LogWarning($"[FinalVrSceneBootstrap] FAIL\n{LastReport}");
            }
        }
    }
}
