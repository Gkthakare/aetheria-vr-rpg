using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Aetheria.Combat;
using Aetheria.Gameplay;
using Aetheria.Voice;
using Aetheria.VR;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Aetheria.World
{
    /// <summary>
    /// Builds a simple island with forest, grass, road, and mountains.
    /// Call Build() at Start. Player spawns on road at roadStartPosition.
    /// Runs at default execution order -50 so runtime gameplay components on IslandWorldManager
    /// (dungeon auto-start, etc.) are configured before their <c>Start</c>.
    /// For a more natural look: tune coastline noise, assign beach rock prefabs, add Suimono shoreline/foam,
    /// and eventually replace the flat cylinders with Terrain or sculpted mesh for height variation.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class IslandWorldBuilder : MonoBehaviour
    {
        [Header("Island Size")]
        [Tooltip("World footprint X/Z of the ground disc (cylinder top). E.g. 100 = 100×100 area.")]
        [SerializeField] private float groundDiameter = 100f;
        [SerializeField] private float groundY = 0f;
        [Tooltip("Raises land above sea level (world = SeaLevelWorldY + this). Higher = clearer beach above Suimono water, less z-fighting.")]
        [SerializeField] private float islandClearanceAboveSea = 4f;
        [Tooltip("Sand ring between grass and open ocean. Outer edge of sand is groundDiameter; grass disc is this much narrower on each side.")]
        [SerializeField] private float beachWidth = 14f;
        [Tooltip("Cylinder localScale.y for sand/grass. Unity cylinder top = center.y + scale.y; keep this small so you get a thin island slab, not a tall wall/pillar visible under the hub.")]
        [SerializeField] private float groundSlabThicknessY = 0.22f;

        [Header("Road")]
        [Tooltip("Local space under this transform. Set Y ≈ islandClearanceAboveSea + 0.45 so the fallback road cube sits on the grass/sand.")]
        [SerializeField] private Vector3 roadStartPosition = new Vector3(0f, 4.45f, 0f);
        [SerializeField] private float roadWidth = 3f;
        [SerializeField] private float roadLength = 80f;
        [SerializeField] private Material roadMaterial;
        [Tooltip("Preferred: Meshy brick road segment prefab (FBX/GLB imported prefab). If assigned, repeated segments build a structured road.")]
        [SerializeField] private GameObject roadSegmentPrefab;
        [SerializeField] private float roadSegmentLength = 2.2f;
        [Tooltip("Creates extra connector roads from the main road to make the island feel structured.")]
        [SerializeField] private bool createStructuredRoadNetwork = true;
        [SerializeField] private int roadBranchCount = 3;
        [SerializeField] private float roadBranchLength = 22f;

        [Header("Ground")]
        [SerializeField] private Material grassMaterial;
        [SerializeField] private Material sandMaterial;

        [Header("Trees")]
        [SerializeField] private int treeCount = 75;
        [SerializeField] private float treeMinDistFromRoad = 4f;
        [Tooltip("Assign ALP Oak, Mountain pack tree_01, etc. Leave empty for simple procedural trees.")]
        [SerializeField] private GameObject treePrefab;
        [Tooltip("Optional pack/ALP tree prefabs. When set, each tree chooses one from this list.")]
        [SerializeField] private GameObject[] treePrefabs;
        [SerializeField] private float treePrefabScale = 0.4f;
        [SerializeField] private Material treeTrunkMaterial;
        [SerializeField] private Material treeFoliageMaterial;

        [Tooltip("Same seed = same forest layout. Change to reshuffle groves.")]
        [SerializeField] private int treePlacementSeed = 4201;

        [Tooltip("Trees stay off the sand ring — max distance from center is (grass disc radius − this).")]
        [SerializeField] private float treeInlandEdgePadding = 3.5f;

        [Tooltip("No trees inside this radius (hub / road readability).")]
        [SerializeField] private float treeClearingRadiusAroundCenter = 11f;

        [Tooltip("Higher = patchier forest (groves). Lower = more uniform cover.")]
        [SerializeField] [Range(0f, 0.55f)] private float treeDensityNoiseThreshold = 0.22f;

        [Tooltip("Radial bias: <1 pushes trees slightly toward mid-forest; >1 toward inner clearing edge.")]
        [SerializeField] [Range(0.35f, 1.35f)] private float treeRadialBiasExponent = 0.78f;

        [Header("Mountains (distant)")]
        [SerializeField] private int mountainCount = 7;
        [Tooltip("Ring distance from center. Auto-clamped to stay outside the island disc.")]
        [SerializeField] private float mountainDistance = 65f;
        [SerializeField] private Material mountainMaterial;
        [Tooltip("Optional: Mountain Terrain pack mountain_terrain_01 — replaces primitive cylinders when set.")]
        [SerializeField] private GameObject mountainTerrainPrefab;
        [SerializeField] private float mountainPrefabScale = 1f;

        [Tooltip("Same seed = same mountain jitter.")]
        [SerializeField] private int mountainPlacementSeed = 9001;

        [Tooltip("Breaks perfect hexagonal spacing (radians, max deviation).")]
        [SerializeField] private float mountainAngleJitterRad = 0.42f;

        [Tooltip("Fractional distance variation ± around mountainDistance.")]
        [SerializeField] [Range(0f, 0.25f)] private float mountainDistanceJitter = 0.09f;

        [Tooltip("Leave a wider gap toward the VR beach/ocean view (same azimuth as beach spawn). 0 = full 360° ring.")]
        [SerializeField] [Range(0f, 75f)] private float oceanViewMountainSparseHalfAngleDegrees = 34f;

        [Tooltip("If off, mountains use (Island Radius + Coastal Gap) so peaks hug the disc. If on, uses Mountain Distance (distant ring).")]
        [SerializeField] private bool mountainRingUseAbsoluteDistance = false;

        [Tooltip("Meters beyond the outer sand edge. Keep small (2–8) so terrain tiles read as coastal hills, not floating islands.")]
        [SerializeField] private float coastalMountainGap = 4f;

        [Tooltip("Align mountain prefab bottoms to land top (beach/grass level) instead of sea level.")]
        [SerializeField] private bool snapMountainPrefabsToLandTop = true;

        [Tooltip("After align, sink into ground to hide square tile bases (meters).")]
        [SerializeField] private float mountainPrefabGroundSink = 0.55f;

        [Tooltip("Adds this yaw to each mountain (e.g. 45 breaks axis-aligned square tiles).")]
        [SerializeField] private float mountainExtraYawOffsetDegrees = 45f;

        [Header("Grass Patches")]
        [SerializeField] private int grassPatchCount = 160;
        [SerializeField] private Material grassPatchMaterial;

        [Header("Island realism (organic coast + beach detail)")]
        [Tooltip("0 = perfect circle. Higher = trees/grass/rocks deviate from a ring so the coast reads less synthetic.")]
        [SerializeField] [Range(0f, 0.2f)] private float coastlineNoiseStrength = 0.1f;
        [SerializeField] private float coastNoiseScale = 2.6f;
        [SerializeField] private float coastNoiseSeed = 42f;
        [Tooltip("Small rocks / debris on the sand ring (optional). Uses same pack rocks as ChunkManager if assigned.")]
        [SerializeField] private GameObject[] beachRockPrefabs;
        [SerializeField] private int beachRockCount = 28;
        [SerializeField] private float beachRockScaleMin = 0.35f;
        [SerializeField] private float beachRockScaleMax = 0.9f;

        [Header("Animals")]
        [SerializeField] private int animalCount = 8;
        [SerializeField] private Material animalMaterial;

        [Header("Enemies on island/roads")]
        [SerializeField] private GameObject enemyPrefab;
        [Tooltip("When true and prefab resolves, island spawns use scary Monster.fbx before enemyPrefab / capsule (same asset as dungeon waves).")]
        [SerializeField] private bool useImportedScaryMonsterOnIsland = true;
        [SerializeField] private GameObject scaryMonsterIslandPrefab;
#pragma warning disable 0414 // Editor-only binding path; read in TryBindScaryMonsterIslandPrefabFromPath (#if UNITY_EDITOR).
        [SerializeField] private string scaryMonsterIslandAssetPath = "Assets/ImportedContent/Creatures/scary-monster/source/Monster.fbx";
#pragma warning restore 0414
        [SerializeField] private float scaryMonsterIslandScale = 0.45f;
        [SerializeField] private int enemyCount = 14;
        [SerializeField] [Range(0f, 1f)] private float roadEnemyBias = 0.6f;
        [SerializeField] private float minEnemyDistanceFromRoad = 1.4f;
        [SerializeField] private float enemySpawnHeightOffset = 0.6f;

        [Header("AI Navigation")]
        [Tooltip("If enabled, IslandWorldBuilder creates/updates a NavMeshSurface and bakes it at runtime after ground/roads are built.")]
        [SerializeField] private bool autoBakeIslandNavMesh = true;
        [SerializeField] private NavMeshSurface islandNavMeshSurface;

        [Header("VR view")]
        [Tooltip("Moves XR Origin to a deterministic island spawn point at play.")]
        [SerializeField] private bool repositionVrOriginOnStart = true;
        [SerializeField] private Transform xrOrigin;
        [Tooltip("When enabled, XR Origin spawns at island center (recommended for combat test scene).")]
        [SerializeField] private bool spawnVrAtIslandCenter = true;
        [Tooltip("Azimuth on XZ around island center (degrees). Adjust so you look toward open ocean, not into the hub.")]
        [SerializeField] private float beachSpawnAngleDegrees = 200f;
        [Tooltip("Spawn this far inside the outer sand edge (meters) so you stay on sand, not in Suimono water.")]
        [SerializeField] private float beachSpawnRadiusMarginFromOuterEdge = 4f;
        [Tooltip("If true, sets XR Origin yaw to the configured start facing direction.")]
        [SerializeField] private bool orientVrOriginYawTowardOpenOcean = true;
        [SerializeField] private float centerSpawnYawDegrees = 180f;

        [Header("VR avatar (optional)")]
        [Tooltip("Optional humanoid avatar prefab to instantiate under XR Origin for full-body presence.")]
        [SerializeField] private GameObject vrPlayerAvatarPrefab;
        [Tooltip("Auto-resolve avatar prefab from ImportedContent if explicit prefab is not assigned.")]
        [SerializeField] private bool autoResolveAvatarFromImportedContent = true;
        [Tooltip("External avatar source GLB. If present and prefab isn't linked, editor auto-copies/imports this into Assets/ImportedContent/Characters.")]
#pragma warning disable 0414 // Editor-only paths (TryImportExternalAvatarIfNeeded / ResolveAvatarPrefabIfNeeded).
        [SerializeField] private string externalAvatarSourcePath = @"C:\Users\intru\Downloads\full-body-cyberpunk-male\source\readyplayerme_cyberpunk.glb";
        [SerializeField] private string importedAvatarAssetPath = "Assets/ImportedContent/Characters/full-body-cyberpunk-male/readyplayerme_cyberpunk.glb";
#pragma warning restore 0414
        [SerializeField] private bool spawnFallbackRuntimeAvatarIfMissing = true;
        [SerializeField] private Vector3 vrAvatarLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 vrAvatarLocalEuler = Vector3.zero;
        [SerializeField] private Vector3 vrAvatarLocalScale = Vector3.one;
        [SerializeField] private bool replaceExistingRuntimeAvatar = true;
        [Tooltip("Optional: Resources.Load<GameObject>(this path) when VR Avatar Prefab is still empty (after editor auto-resolve if any). E.g. Assets/Resources/Characters/MyAvatar.prefab → set to \"Characters/MyAvatar\".")]
        [SerializeField] private string runtimeAvatarResourcesPath = "";
        [Tooltip("Lowers enabled XR rig cameras' near clip so legs/torso are not culled when you look down.")]
        [SerializeField] private bool adjustXrCameraNearClipForBody = true;
        [SerializeField] private float xrCameraNearClipBody = 0.09f;
        [Tooltip("Assigns Default layer to the whole avatar so HMD cameras (culling Default) always draw your body.")]
        [SerializeField] private bool putRuntimeAvatarOnDefaultLayer = true;
        [Tooltip("Raycast down at spawn XZ and place the rig on the hit surface (island/terrain colliders). Fixes huge BodyPose Y when IslandWorldManager Y does not match real ground.")]
        [SerializeField] private bool snapVrSpawnYToGround = true;
        [SerializeField] private float vrSpawnGroundProbeStartAbove = 140f;
        [SerializeField] private float vrSpawnGroundRayMaxDistance = 600f;
        [SerializeField] private float vrSpawnHeightAboveGround = 0.08f;
        [SerializeField] private LayerMask vrSpawnGroundLayers = ~0;

        [Header("VR camera (when WorldManager / Suimono bootstrap is off)")]
        [Tooltip("HelloVR’s WorldManager runs SuimonoInfinityWaterBootstrap, which sets far clip, Forward path, and Skybox clear on the HMD camera. If you disable WorldManager to avoid duplicate Chunk/Dungeon systems, enable this so the Game view / headset is not a grey void.")]
        [SerializeField] private bool applyVrCameraHorizonDefaultsWhenNoSuimono = true;
        [SerializeField] private float vrCameraFarClipForHorizon = 5000f;
        [Tooltip("Matches Suimono bootstrap: Built-in–style water and some URP setups behave better with Forward on the eye camera.")]
        [SerializeField] private bool forceVrCamerasForwardRendering = true;
        [Tooltip("When RenderSettings.skybox is set, set MainCamera clear flags to Skybox (avoids solid grey “empty” Game view).")]
        [SerializeField] private bool useSkyboxClearOnMainCameraWhenSkyboxAssigned = true;

        [Header("Dungeon (optional)")]
        [Tooltip("When true, runtime-added DungeonFloorCombatController starts the floating dungeon on Play. Leave off for island, beach, and bow tests.")]
        [SerializeField] private bool autoStartDungeonCombatOnPlay;

        private Transform _groundRoot;
        private Transform _roadRoot;
        private Transform _treesRoot;
        private Transform _mountainsRoot;
        private Transform _beachDecorRoot;
        private Transform _enemiesRoot;

        private Renderer _islandSandRenderer;
        private Renderer _islandGrassRenderer;
        private static bool _warnedEnemyTagMissing;

        /// <summary>When true (set from <see cref="ConfigureSkipGroundDiscForTerrain"/> before <see cref="Awake"/>), <see cref="CreateGround"/> skips beach/grass cylinders so a Unity Terrain can replace them.</summary>
        private bool _skipGroundDiscFromTerrain;

        /// <summary>Half-width of the island disc in world units.</summary>
        public float IslandRadius => groundDiameter * 0.5f;

        /// <summary>Island footprint X/Z (matches terrain size when using API heightmap).</summary>
        public float GroundDiameter => groundDiameter;

        /// <summary>World Y of the sea plane (local <see cref="groundY"/> under this transform). Match Suimono waterSurfaceY to this.</summary>
        public float SeaLevelWorldY => transform.position.y + groundY;

        /// <summary>Approximate world Y of the top of the grass/sand discs (standing height reference).</summary>
        public float LandTopWorldY => transform.position.y + groundY + Mathf.Max(0f, islandClearanceAboveSea);

        /// <summary>Local Y of grass/sand top (sea is at local groundY).</summary>
        private float LandTopLocalY => groundY + Mathf.Max(0f, islandClearanceAboveSea);

        public Vector3 RoadStartPosition => roadStartPosition;
        public Vector3 RoadEndPosition => roadStartPosition + Vector3.forward * roadLength;

        /// <summary>Road hub start in world space (teleport / spawn; <see cref="RoadStartPosition"/> is local).</summary>
        public Vector3 RoadStartWorldPosition => transform.TransformPoint(roadStartPosition);

        /// <summary>True when this builder will move the XR rig after a short delay (see <c>DeferredVrSpawn</c>).</summary>
        public bool RepositionsVrOriginOnStart => repositionVrOriginOnStart;

        /// <summary>
        /// Call from a component with earlier <c>DefaultExecutionOrder</c> (e.g. -120) so <see cref="Build"/> skips cylinder ground.
        /// </summary>
        public void ConfigureSkipGroundDiscForTerrain(bool skip) => _skipGroundDiscFromTerrain = skip;

        /// <summary>Hide or show the Island_Ground hierarchy (sand/grass discs) after terrain import or for debugging.</summary>
        public void SetGroundDiscsActive(bool active)
        {
            if (_groundRoot != null)
                _groundRoot.gameObject.SetActive(active);
        }

        void Awake()
        {
#if UNITY_EDITOR
            TryBindScaryMonsterIslandPrefabFromPath();
#endif
            Build();
        }

#if UNITY_EDITOR
        private bool _queuedEditorPrefabBind;

        private void OnValidate()
        {
            QueueScaryMonsterIslandPrefabBind();
            if (autoResolveAvatarFromImportedContent && vrPlayerAvatarPrefab == null)
                ResolveAvatarPrefabIfNeeded();
        }

        private void QueueScaryMonsterIslandPrefabBind()
        {
            if (_queuedEditorPrefabBind)
                return;
            _queuedEditorPrefabBind = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                _queuedEditorPrefabBind = false;
                if (this == null)
                    return;
                TryBindScaryMonsterIslandPrefabFromPath();
            };
        }

        private void TryBindScaryMonsterIslandPrefabFromPath()
        {
            if (scaryMonsterIslandPrefab != null || string.IsNullOrWhiteSpace(scaryMonsterIslandAssetPath))
                return;
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(scaryMonsterIslandAssetPath);
            if (p != null)
                scaryMonsterIslandPrefab = p;
        }
#endif

        void Start()
        {
            EnsureGameplayRuntimeSystems();
            ApplyVrCameraHorizonFallbackIfNeeded();
            EnsurePlayerAvatarPresent();
            StartCoroutine(DeferredApplyRuntimeAvatarVisibility());
            StartCoroutine(DeferredEnsureCosmeticBowOnRuntimeAvatar());
            if (!repositionVrOriginOnStart) return;
            StartCoroutine(DeferredVrSpawn());
        }

        private IEnumerator DeferredApplyRuntimeAvatarVisibility()
        {
            yield return null;
            Transform origin = xrOrigin;
            if (origin == null)
            {
                var xr = GameObject.Find("XR Origin (XR Rig)");
                if (xr != null)
                    origin = xr.transform;
            }
            if (origin == null)
                yield break;

            Transform avatarTf = origin.Find("RuntimePlayerAvatar");
            if (avatarTf == null)
            {
                foreach (var t in origin.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "RuntimePlayerAvatar")
                        continue;
                    avatarTf = t;
                    break;
                }
            }

            if (avatarTf != null)
                ApplyRuntimeAvatarVisibilitySetup(avatarTf.gameObject, origin);
        }

        private void ApplyRuntimeAvatarVisibilitySetup(GameObject avatar, Transform xrRig)
        {
            if (avatar == null || xrRig == null)
                return;

            if (putRuntimeAvatarOnDefaultLayer)
            {
                foreach (var t in avatar.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = 0;
            }

            if (!adjustXrCameraNearClipForBody)
                return;

            float clip = Mathf.Clamp(xrCameraNearClipBody, 0.03f, 0.2f);
            var cameras = xrRig.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null || !cam.isActiveAndEnabled)
                    continue;
                cam.nearClipPlane = Mathf.Min(cam.nearClipPlane, clip);
            }
        }

        /// <summary>
        /// Backup path when <see cref="RuntimePlayerAvatar"/> was not instantiated here (scene-placed avatar) or bow setup ran late.
        /// </summary>
        private IEnumerator DeferredEnsureCosmeticBowOnRuntimeAvatar()
        {
            yield return null;
            yield return null;
            Transform origin = xrOrigin;
            if (origin == null)
            {
                var xr = GameObject.Find("XR Origin (XR Rig)");
                if (xr != null)
                    origin = xr.transform;
            }
            if (origin == null)
                yield break;

            Transform avatarTf = origin.Find("RuntimePlayerAvatar");
            if (avatarTf == null)
            {
                foreach (var t in origin.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "RuntimePlayerAvatar")
                        continue;
                    avatarTf = t;
                    break;
                }
            }
            if (avatarTf == null)
                yield break;
            if (AvatarHasCosmeticBow(avatarTf.gameObject))
                yield break;

            AttachCosmeticBowToAvatarBody(avatarTf.gameObject);
        }

        /// <summary>
        /// Duplicate of the essential camera tuning from <see cref="SuimonoInfinityWaterBootstrap"/> when that object is disabled.
        /// </summary>
        private void ApplyVrCameraHorizonFallbackIfNeeded()
        {
            if (!applyVrCameraHorizonDefaultsWhenNoSuimono)
                return;
            if (FindObjectOfType<Suimono.Core.SuimonoModule>() != null)
                return;

            Transform xr = xrOrigin;
            if (xr == null)
            {
                var go = GameObject.Find("XR Origin (XR Rig)");
                if (go != null)
                    xr = go.transform;
            }

            Camera[] cameras;
            if (xr != null)
                cameras = xr.GetComponentsInChildren<Camera>(true);
            else
                cameras = FindObjectsOfType<Camera>();

            if (cameras == null || cameras.Length == 0)
                return;

            bool skyAssigned = RenderSettings.skybox != null;
            for (int i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null || !cam.enabled || !cam.gameObject.activeInHierarchy)
                    continue;
                if (cam.targetTexture != null)
                    continue;

                if (vrCameraFarClipForHorizon > 0f && cam.farClipPlane < vrCameraFarClipForHorizon)
                    cam.farClipPlane = vrCameraFarClipForHorizon;

                if (forceVrCamerasForwardRendering)
                    cam.renderingPath = RenderingPath.Forward;

                if (useSkyboxClearOnMainCameraWhenSkyboxAssigned && skyAssigned && cam.CompareTag("MainCamera"))
                    cam.clearFlags = CameraClearFlags.Skybox;
            }
        }

        private void EnsureGameplayRuntimeSystems()
        {
            var xr = GameObject.Find("XR Origin (XR Rig)");
            if (xr != null)
            {
                if (xr.GetComponent<PlayerCombatStats>() == null)
                    xr.AddComponent<PlayerCombatStats>();
                if (xr.GetComponent<WindManaBow>() == null)
                    xr.AddComponent<WindManaBow>();
                if (xr.GetComponent<PlayerEnemyStatusScreens>() == null)
                    xr.AddComponent<PlayerEnemyStatusScreens>();
                if (xr.GetComponent<DungeonRunObjectiveHud>() == null)
                    xr.AddComponent<DungeonRunObjectiveHud>();
                if (xr.GetComponent<VrHandVitalsMenu>() == null)
                    xr.AddComponent<VrHandVitalsMenu>();
            }

            var world = GameObject.Find("IslandWorldManager");
            if (world != null)
            {
                if (world.GetComponent<VoiceIntegrationSystem>() == null)
                    world.AddComponent<VoiceIntegrationSystem>();
                var dungeonCombat = world.GetComponent<DungeonFloorCombatController>();
                if (dungeonCombat == null)
                    dungeonCombat = world.AddComponent<DungeonFloorCombatController>();
                dungeonCombat.SetAutoStartRunOnPlay(autoStartDungeonCombatOnPlay);
                if (world.GetComponent<NavMeshSetupValidator>() == null)
                    world.AddComponent<NavMeshSetupValidator>();
                if (world.GetComponent<FinalVrSceneBootstrap>() == null)
                    world.AddComponent<FinalVrSceneBootstrap>();
            }

            TryAttachFullBodyIkToHumanoid();
        }

        private void EnsurePlayerAvatarPresent()
        {
            Transform origin = xrOrigin;
            if (origin == null)
            {
                var xr = GameObject.Find("XR Origin (XR Rig)");
                if (xr != null) origin = xr.transform;
            }
            if (origin == null)
                return;

            if (vrPlayerAvatarPrefab == null && autoResolveAvatarFromImportedContent)
                ResolveAvatarPrefabIfNeeded();
            if (vrPlayerAvatarPrefab == null && !string.IsNullOrWhiteSpace(runtimeAvatarResourcesPath))
                vrPlayerAvatarPrefab = Resources.Load<GameObject>(runtimeAvatarResourcesPath.Trim());
            if (vrPlayerAvatarPrefab == null)
            {
                Debug.LogWarning(
                    "[IslandWorldBuilder] VR avatar prefab is not assigned. In the editor: assign **VR Player Avatar Prefab** on IslandWorldManager, or keep **Auto Resolve Avatar From Imported Content** and save the scene after the GLB is imported. For device builds you can set **Runtime Avatar Resources Path** (Resources folder). Spawning fallback body if enabled.");
                if (spawnFallbackRuntimeAvatarIfMissing)
                {
                    BuildFallbackRuntimeAvatar(origin);
                }
                return;
            }

            if (replaceExistingRuntimeAvatar)
            {
                var existing = origin.Find("RuntimePlayerAvatar");
                if (existing != null)
                    Destroy(existing.gameObject);
            }

            var avatar = Instantiate(vrPlayerAvatarPrefab, origin);
            avatar.name = "RuntimePlayerAvatar";
            avatar.transform.localPosition = vrAvatarLocalPosition;
            avatar.transform.localRotation = Quaternion.Euler(vrAvatarLocalEuler);
            avatar.transform.localScale = vrAvatarLocalScale;
            EnsureAvatarMeshesAndRenderersActive(avatar);

            var anim = avatar.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                if (anim.GetComponent<VRFullBodyIkController>() == null)
                    anim.gameObject.AddComponent<VRFullBodyIkController>();
                Debug.Log("[IslandWorldBuilder] RuntimePlayerAvatar spawned with Humanoid IK.");
            }
            else
            {
                Debug.LogWarning("[IslandWorldBuilder] RuntimePlayerAvatar spawned as visual-only (non-humanoid). IK skipped.");
            }

            ApplyRuntimeAvatarVisibilitySetup(avatar, origin);
            StartCoroutine(DeferredAttachCosmeticBowToAvatarBody(avatar));
            Debug.Log("[IslandWorldBuilder] RuntimePlayerAvatar instantiated under XR Origin.");
        }

        /// <summary>Many GLB imports ship with mesh renderers disabled on chunks; force-visible for VR mirror body.</summary>
        static void EnsureAvatarMeshesAndRenderersActive(GameObject root)
        {
            if (root == null) return;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && !t.gameObject.activeSelf)
                    t.gameObject.SetActive(true);
            }

            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.enabled = true;
#if UNITY_2022_1_OR_NEWER
                r.forceRenderingOff = false;
#endif
            }

            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.enabled = true;
#if UNITY_2022_1_OR_NEWER
                r.forceRenderingOff = false;
#endif
            }
        }

        private IEnumerator DeferredAttachCosmeticBowToAvatarBody(GameObject avatar)
        {
            yield return null;
            if (avatar == null)
                yield break;
            AttachCosmeticBowToAvatarBody(avatar);
        }

        /// <summary>True if <paramref name="avatar"/> already has a descendant named CosmeticBow_InHand (any depth).</summary>
        public static bool AvatarHasCosmeticBow(GameObject avatar)
        {
            if (avatar == null) return false;
            foreach (var t in avatar.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "CosmeticBow_InHand")
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Third-person bow on the avatar mesh (WindManaBow still parents to XR hand anchor for gameplay).
        /// Safe to call from <see cref="WindManaBow"/> when the avatar was not spawned by this builder.
        /// Idempotent: skips if a cosmetic bow already exists anywhere under the avatar.
        /// </summary>
        public static void AttachCosmeticBowToAvatarBody(GameObject avatar)
        {
            if (avatar == null) return;
            if (AvatarHasCosmeticBow(avatar))
                return;

            Transform hand = ResolveAvatarLeftHandTransform(avatar);
            if (hand == null)
            {
                Debug.LogWarning("[IslandWorldBuilder] Could not find left hand bone for cosmetic bow; skipping body bow.");
                return;
            }

            var bow = WindManaBow.InstantiateBowVisualUnder(hand, false);
            if (bow == null)
                return;
            bow.name = "CosmeticBow_InHand";
            bow.SetActive(true);
            // Offset so mesh clears glove; scale readable on RPM / generic rigs (tune in inspector via future serialized hook if needed).
            bow.transform.localPosition = new Vector3(0.03f, 0.02f, 0.1f);
            bow.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            bow.transform.localScale = Vector3.one * 0.85f;
            Debug.Log($"[IslandWorldBuilder] Cosmetic bow attached under '{hand.name}' (expand RuntimePlayerAvatar → … → {hand.name} → CosmeticBow_InHand).");
        }

        private static Transform ResolveAvatarLeftHandTransform(GameObject avatar)
        {
            var anim = avatar.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                var h = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                if (h != null) return h;
                h = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                if (h != null) return h;
            }

            Transform hand = FindTransformByNameHints(avatar.transform,
                "lefthand", "left_hand", "hand_l", "l_hand", "hand.l",
                "mixamorig:lefthand", "mixamorig_lefthand", "leftwrist", "wrist_l", "l.wrist", "hand_l_");

            if (hand == null)
            {
                foreach (var t in avatar.GetComponentsInChildren<Transform>(true))
                {
                    var n = t.name.ToLowerInvariant().Replace(" ", string.Empty);
                    if (n.Contains("left") && (n.Contains("hand") || n.Contains("wrist")))
                        return t;
                }
            }

            if (hand == null)
            {
                foreach (var t in avatar.GetComponentsInChildren<Transform>(true))
                {
                    var n = t.name.ToLowerInvariant();
                    if (n == "lefthand" || n.EndsWith(".lefthand") || n.EndsWith("_lefthand"))
                        return t;
                }
            }

            return hand;
        }

        private static Transform FindTransformByNameHints(Transform root, params string[] nameHints)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name.ToLowerInvariant().Replace(" ", string.Empty).Replace("_", string.Empty);
                foreach (var h in nameHints)
                {
                    var hn = h.ToLowerInvariant().Replace(" ", string.Empty).Replace("_", string.Empty).Replace(".", string.Empty);
                    if (n.Contains(hn))
                        return t;
                }
            }

            return null;
        }

        private void BuildFallbackRuntimeAvatar(Transform origin)
        {
            var existing = origin.Find("RuntimePlayerAvatar");
            if (existing != null)
                Destroy(existing.gameObject);

            var root = new GameObject("RuntimePlayerAvatar");
            root.transform.SetParent(origin, false);
            root.transform.localPosition = new Vector3(0f, -1.25f, 0f);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            void AddPart(string n, PrimitiveType p, Vector3 lp, Vector3 ls, Color c)
            {
                var go = GameObject.CreatePrimitive(p);
                go.name = n;
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = lp;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = ls;
                var r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    var s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    if (s != null)
                    {
                        var m = new Material(s);
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
                        r.sharedMaterial = m;
                    }
                }
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            AddPart("Torso", PrimitiveType.Capsule, new Vector3(0f, 1.0f, 0f), new Vector3(0.34f, 0.58f, 0.24f), new Color(0.14f, 0.45f, 0.82f));
            AddPart("Head", PrimitiveType.Sphere, new Vector3(0f, 1.76f, 0f), new Vector3(0.34f, 0.34f, 0.34f), new Color(0.96f, 0.79f, 0.65f));
            AddPart("ArmL", PrimitiveType.Capsule, new Vector3(-0.34f, 1.16f, 0f), new Vector3(0.1f, 0.38f, 0.1f), new Color(0.96f, 0.79f, 0.65f));
            AddPart("ArmR", PrimitiveType.Capsule, new Vector3(0.34f, 1.16f, 0f), new Vector3(0.1f, 0.38f, 0.1f), new Color(0.96f, 0.79f, 0.65f));
            AddPart("LegL", PrimitiveType.Capsule, new Vector3(-0.15f, 0.42f, 0f), new Vector3(0.12f, 0.46f, 0.12f), new Color(0.13f, 0.2f, 0.45f));
            AddPart("LegR", PrimitiveType.Capsule, new Vector3(0.15f, 0.42f, 0f), new Vector3(0.12f, 0.46f, 0.12f), new Color(0.13f, 0.2f, 0.45f));

            EnsureAvatarMeshesAndRenderersActive(root);
            ApplyRuntimeAvatarVisibilitySetup(root, origin);

            var rightHandAnchor = new GameObject("RightHandBowAnchor");
            rightHandAnchor.transform.SetParent(root.transform, false);
            rightHandAnchor.transform.localPosition = new Vector3(0.43f, 1.08f, 0.08f);
            rightHandAnchor.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            if (WindManaBow.Instance != null && WindManaBow.Instance.ActiveBowVisualPrefab != null)
            {
                var bow = Object.Instantiate(WindManaBow.Instance.ActiveBowVisualPrefab, rightHandAnchor.transform, false);
                bow.name = "FallbackAvatarBow";
                bow.transform.localPosition = Vector3.zero;
                bow.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                bow.transform.localScale = Vector3.one * 0.42f;
                foreach (var c in bow.GetComponentsInChildren<Collider>(true))
                    Object.Destroy(c);
            }

            Debug.Log("[IslandWorldBuilder] Spawned fallback runtime avatar (bow only if WindManaBow + DownloadedBow prefab are ready).");
        }

        private void ResolveAvatarPrefabIfNeeded()
        {
#if UNITY_EDITOR
            TryImportExternalAvatarIfNeeded();

            if (!string.IsNullOrWhiteSpace(importedAvatarAssetPath))
            {
                foreach (var candidate in EnumerateAvatarAssetPathCandidates(importedAvatarAssetPath))
                {
                    var direct = AssetDatabase.LoadAssetAtPath<GameObject>(candidate);
                    if (direct == null) continue;
                    var anim = direct.GetComponentInChildren<Animator>();
                    var skin = direct.GetComponentInChildren<SkinnedMeshRenderer>();
                    if ((anim != null && anim.isHuman) || anim != null || skin != null)
                    {
                        vrPlayerAvatarPrefab = direct;
                        Debug.Log($"[IslandWorldBuilder] Linked VR avatar from path: {candidate}");
                        return;
                    }
                }
            }

            string[] searchRoots = { "Assets/ImportedContent", "Assets/Art", "Assets/Prefabs" };
            string[] keywords = { "readyplayerme_cyberpunk", "full-body-cyberpunk-male", "cyberpunk", "player avatar", "avatar", "humanoid", "character" };
            for (int k = 0; k < keywords.Length; k++)
            {
                var guids = AssetDatabase.FindAssets(keywords[k], searchRoots);
                for (int i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(path)) continue;
                    var lower = path.ToLowerInvariant();
                    if (!lower.EndsWith(".prefab") && !lower.EndsWith(".glb") && !lower.EndsWith(".fbx"))
                        continue;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    var anim = prefab.GetComponentInChildren<Animator>();
                    var skin = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (anim == null && skin == null) continue;
                    vrPlayerAvatarPrefab = prefab;
                    Debug.Log($"[IslandWorldBuilder] Auto-linked VR avatar asset: {path}");
                    return;
                }
            }
#endif
        }

#if UNITY_EDITOR
        static IEnumerable<string> EnumerateAvatarAssetPathCandidates(string primaryPath)
        {
            if (string.IsNullOrWhiteSpace(primaryPath))
                yield break;
            yield return primaryPath;
            var noExt = Path.ChangeExtension(primaryPath, null);
            yield return noExt + ".glb";
            yield return noExt + ".fbx";
            yield return noExt + ".prefab";
        }
#endif

#if UNITY_EDITOR
        private void TryImportExternalAvatarIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(externalAvatarSourcePath) || string.IsNullOrWhiteSpace(importedAvatarAssetPath))
                return;

            // Already imported and loadable.
            if (AssetDatabase.LoadAssetAtPath<Object>(importedAvatarAssetPath) != null)
                return;

            if (!File.Exists(externalAvatarSourcePath))
            {
                Debug.LogWarning($"[IslandWorldBuilder] External avatar file not found: {externalAvatarSourcePath}");
                return;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string targetAbsolute = Path.Combine(projectRoot, importedAvatarAssetPath.Replace('/', Path.DirectorySeparatorChar));
            string targetDir = Path.GetDirectoryName(targetAbsolute);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            File.Copy(externalAvatarSourcePath, targetAbsolute, true);
            AssetDatabase.Refresh();
            Debug.Log($"[IslandWorldBuilder] Imported external avatar GLB to: {importedAvatarAssetPath}");
        }
#endif

        private static void TryAttachFullBodyIkToHumanoid()
        {
            var animators = FindObjectsOfType<Animator>();
            for (int i = 0; i < animators.Length; i++)
            {
                var a = animators[i];
                if (a == null || !a.isHuman)
                    continue;
                if (a.GetComponent<VRFullBodyIkController>() != null)
                    return;
                a.gameObject.AddComponent<VRFullBodyIkController>();
                Debug.Log($"[IslandWorldBuilder] Added VRFullBodyIkController to humanoid avatar: {a.name}");
                return;
            }
        }

        private IEnumerator DeferredVrSpawn()
        {
            // After XR tracking / origin scripts initialize so we don't get overwritten.
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            ApplyVrSpawn();
        }

        private void ApplyVrSpawn()
        {
            if (!repositionVrOriginOnStart) return;

            Transform origin = xrOrigin;
            if (origin == null)
            {
                var go = GameObject.Find("XR Origin (XR Rig)");
                if (go != null) origin = go.transform;
            }
            if (origin == null) return;

            Transform rigRoot = ResolveXrRigRootTransform(origin);
            if (rigRoot == null)
                return;

            var o = transform.position;
            Vector3 spawn;
            Vector3 facingFlat;
            if (spawnVrAtIslandCenter)
            {
                spawn = new Vector3(o.x, LandTopWorldY, o.z);
                float yawRad = centerSpawnYawDegrees * Mathf.Deg2Rad;
                facingFlat = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
            }
            else
            {
                float margin = Mathf.Max(0f, beachSpawnRadiusMarginFromOuterEdge);
                float r = Mathf.Max(4f, IslandRadius - margin);
                float rad = beachSpawnAngleDegrees * Mathf.Deg2Rad;
                var flat = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                spawn = new Vector3(o.x + flat.x * r, LandTopWorldY, o.z + flat.z * r);
                facingFlat = flat;
            }

            if (snapVrSpawnYToGround)
            {
                float startY = spawn.y + Mathf.Max(25f, vrSpawnGroundProbeStartAbove);
                Vector3 rayStart = new Vector3(spawn.x, startY, spawn.z);
                float maxDist = Mathf.Max(250f, vrSpawnGroundRayMaxDistance);
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, maxDist, vrSpawnGroundLayers, QueryTriggerInteraction.Ignore))
                    spawn.y = hit.point.y + Mathf.Max(0f, vrSpawnHeightAboveGround);
            }

            if (Mathf.Abs(transform.position.y) > 350f)
            {
                Debug.LogWarning(
                    "[IslandWorldBuilder] IslandWorldManager transform Y is very large (" + transform.position.y +
                    " m). Place it near your terrain (often Y≈0) unless this is intentional, or BodyPose / NPCs will look wrong.");
            }

            rigRoot.position = spawn;

            if (orientVrOriginYawTowardOpenOcean)
                rigRoot.rotation = Quaternion.LookRotation(facingFlat, Vector3.up);

            Debug.Log($"[IslandWorldBuilder] VR rig spawn world pos={spawn} (rig={rigRoot.name})");
        }

        /// <summary>Always move the root named XR Origin, never a child like Main Camera (which would bury the headset).</summary>
        static Transform ResolveXrRigRootTransform(Transform origin)
        {
            if (origin == null) return null;
            Transform t = origin;
            while (t != null)
            {
                if (t.name.IndexOf("XR Origin", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
                t = t.parent;
            }

            return origin;
        }

        public void Build()
        {
            _islandSandRenderer = null;
            _islandGrassRenderer = null;

            _groundRoot = new GameObject("Island_Ground").transform;
            _groundRoot.SetParent(transform);
            _roadRoot = new GameObject("Island_Road").transform;
            _roadRoot.SetParent(transform);
            _treesRoot = new GameObject("Island_Trees").transform;
            _treesRoot.SetParent(transform);
            _mountainsRoot = new GameObject("Island_Mountains").transform;
            _mountainsRoot.SetParent(transform);

            float r = IslandRadius;
            if (mountainRingUseAbsoluteDistance && mountainDistance < r + 4f)
                mountainDistance = r + Mathf.Max(6f, coastalMountainGap);

            CreateGround();
            CreateBeachRocks();
            CreateRoad();
            CreateTrees();
            CreateMountains();
            CreateGrassPatches();
            CreateAnimals();
            EnsureIslandNavMeshBuilt();
            CreateEnemies();
        }

        private void EnsureIslandNavMeshBuilt()
        {
            if (!autoBakeIslandNavMesh)
                return;

            if (islandNavMeshSurface == null)
            {
                islandNavMeshSurface = GetComponent<NavMeshSurface>();
                if (islandNavMeshSurface == null)
                    islandNavMeshSurface = gameObject.AddComponent<NavMeshSurface>();
            }

            islandNavMeshSurface.collectObjects = CollectObjects.Children;
            islandNavMeshSurface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            islandNavMeshSurface.layerMask = ~0;
            islandNavMeshSurface.BuildNavMesh();
        }

        /// <summary>Half-width of the inner grass disc (matches CreateGround).</summary>
        private float GrassDiscRadiusLocal()
        {
            float grassD = Mathf.Max(groundDiameter - 2f * beachWidth, groundDiameter * 0.35f);
            return grassD * 0.5f;
        }

        /// <summary>Multiplier ~1.0 ± coastlineNoiseStrength so vegetation radius wobbles around the island.</summary>
        private float CoastRadiusMultiplier(float angleRadians)
        {
            if (coastlineNoiseStrength <= 0f) return 1f;
            float u = Mathf.Cos(angleRadians) * coastNoiseScale + coastNoiseSeed;
            float v = Mathf.Sin(angleRadians) * coastNoiseScale + coastNoiseSeed * 0.37f;
            float n = Mathf.PerlinNoise(u, v);
            return 1f + (n - 0.5f) * 2f * coastlineNoiseStrength;
        }

        private void CreateBeachRocks()
        {
            if (beachRockPrefabs == null || beachRockPrefabs.Length == 0) return;

            _beachDecorRoot = new GameObject("Island_BeachDecor").transform;
            _beachDecorRoot.SetParent(transform, false);

            float innerR = GrassDiscRadiusLocal() + 0.4f;
            float outerR = Mathf.Max(innerR + 1.5f, IslandRadius - 1.2f);
            if (outerR <= innerR + 0.5f) return;

            int spawned = 0;
            int attempts = beachRockCount * 4;
            for (int i = 0; i < attempts && spawned < beachRockCount; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float t = Random.Range(0f, 1f);
                float r = Mathf.Lerp(innerR, outerR, t);
                r *= CoastRadiusMultiplier(angle);
                r = Mathf.Clamp(r, innerR, outerR);

                GameObject tpl = beachRockPrefabs[Random.Range(0, beachRockPrefabs.Length)];
                if (tpl == null) continue;

                var inst = Object.Instantiate(tpl);
                inst.transform.SetParent(_beachDecorRoot, false);
                inst.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * r,
                    LandTopLocalY,
                    Mathf.Sin(angle) * r);
                inst.transform.localRotation = Quaternion.Euler(
                    Random.Range(-8f, 8f),
                    Random.Range(0f, 360f),
                    Random.Range(-8f, 8f));
                float sc = Random.Range(beachRockScaleMin, beachRockScaleMax);
                inst.transform.localScale = Vector3.one * sc;
                inst.name = $"BeachRock_{spawned}";
                spawned++;
            }
        }

        private void CreateGround()
        {
            if (_skipGroundDiscFromTerrain) return;

            // Cylinder mesh is 2 units tall in mesh space (-1..+1); scale.y is half-height from center to top.
            float topY = groundY + Mathf.Max(0f, islandClearanceAboveSea);
            float slab = Mathf.Max(0.05f, groundSlabThicknessY);
            float centerLocalY = topY - slab;
            // Separate grass from sand vertically to kill z-fighting / shader shimmer at the inner ring.
            const float zFightGrassLift = 0.05f;

            float grassD = Mathf.Max(groundDiameter - 2f * beachWidth, groundDiameter * 0.35f);
            bool hasBeach = beachWidth > 0.05f && grassD < groundDiameter - 0.1f;

            if (!hasBeach || sandMaterial == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ground.name = "IslandGround";
                ground.transform.SetParent(_groundRoot, false);
                ground.transform.localPosition = new Vector3(0f, centerLocalY, 0f);
                ground.transform.localRotation = Quaternion.identity;
                ground.transform.localScale = new Vector3(groundDiameter, slab, groundDiameter);
                if (ground.GetComponent<Renderer>() is Renderer ren && grassMaterial != null)
                {
                    ren.sharedMaterial = grassMaterial;
                    _islandGrassRenderer = ren;
                }
                FixIslandGroundCollider(ground);
                return;
            }

            // Full sand disc to the outer diameter — grass sits on top in the middle; sand shows as a beach ring to the water line.
            var sand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sand.name = "Island_Beach_Sand";
            sand.transform.SetParent(_groundRoot, false);
            sand.transform.localPosition = new Vector3(0f, centerLocalY, 0f);
            sand.transform.localRotation = Quaternion.identity;
            sand.transform.localScale = new Vector3(groundDiameter, slab, groundDiameter);
            if (sand.GetComponent<Renderer>() is Renderer sren && sandMaterial != null)
            {
                sren.sharedMaterial = sandMaterial;
                _islandSandRenderer = sren;
            }
            FixIslandGroundCollider(sand);

            var grass = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            grass.name = "Island_Grass";
            grass.transform.SetParent(_groundRoot, false);
            grass.transform.localPosition = new Vector3(0f, topY + zFightGrassLift - slab, 0f);
            grass.transform.localRotation = Quaternion.identity;
            grass.transform.localScale = new Vector3(grassD, slab, grassD);
            if (grass.GetComponent<Renderer>() is Renderer gren)
            {
                if (grassMaterial != null) gren.sharedMaterial = grassMaterial;
                else if (sandMaterial != null) gren.sharedMaterial = sandMaterial;
                _islandGrassRenderer = gren;
            }
            FixIslandGroundCollider(grass);
        }

        /// <summary>
        /// Apply the same AI-generated texture used for chunk tiles to the island sand/grass discs (URP Lit or Built-in).
        /// Called from <see cref="Aetheria.AI.TerrainTextureGenerator"/> when sync is enabled.
        /// </summary>
        public void ApplyGeneratedGroundTexture(TileType tileType, Texture2D texture)
        {
            if (texture == null) return;

            Renderer target = null;
            switch (tileType)
            {
                case TileType.Sand:
                    target = _islandSandRenderer;
                    break;
                case TileType.Grass:
                case TileType.Forest:
                    target = _islandGrassRenderer;
                    break;
            }

            if (target == null) return;

            var mat = target.material;
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", texture);
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", texture);
        }

        /// <summary>
        /// Cylinder primitives can get an ill-fitting collider; use mesh collider from the cylinder mesh only.
        /// </summary>
        private static void FixIslandGroundCollider(GameObject go)
        {
            foreach (var c in go.GetComponents<Collider>())
                Object.Destroy(c);

            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
        }

        private void CreateRoad()
        {
            Vector3 mainStart = roadStartPosition;
            Vector3 mainEnd = roadStartPosition + Vector3.forward * roadLength;
            BuildRoadAlong(mainStart, mainEnd, "MainRoad");

            if (!createStructuredRoadNetwork || roadBranchCount <= 0 || roadBranchLength <= 0.5f)
                return;

            // Deterministic branch connectors from the main road into the island.
            for (int i = 0; i < roadBranchCount; i++)
            {
                float t = (i + 1f) / (roadBranchCount + 1f);
                var anchor = Vector3.Lerp(mainStart, mainEnd, t);
                float side = i % 2 == 0 ? -1f : 1f;
                float yaw = side * (35f + (i % 3) * 8f);
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                var end = anchor + dir * roadBranchLength;
                BuildRoadAlong(anchor, end, $"BranchRoad_{i}");
            }
        }

        private void BuildRoadAlong(Vector3 localStart, Vector3 localEnd, string namePrefix)
        {
            var dir = (localEnd - localStart);
            float len = new Vector2(dir.x, dir.z).magnitude;
            if (len < 0.5f) return;
            Vector3 fwd = dir.normalized;
            Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);

            if (roadSegmentPrefab != null && roadSegmentLength > 0.25f)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / roadSegmentLength));
                float stepLen = len / steps;
                for (int i = 0; i < steps; i++)
                {
                    var seg = Object.Instantiate(roadSegmentPrefab, _roadRoot);
                    seg.name = $"{namePrefix}_Seg_{i}";
                    seg.transform.localPosition = localStart + fwd * (i * stepLen + stepLen * 0.5f) + Vector3.down * 0.5f;
                    seg.transform.localRotation = rot;
                    // Keep mesh authoring scale; only normalize length on Z.
                    var ls = seg.transform.localScale;
                    seg.transform.localScale = new Vector3(ls.x, ls.y, Mathf.Max(0.2f, stepLen / Mathf.Max(0.2f, roadSegmentLength)) * ls.z);
                    TryApplyRoadMaterial(seg);
                }
                return;
            }

            // Fallback if Meshy road prefab is not assigned.
            var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = namePrefix;
            road.transform.SetParent(_roadRoot);
            road.transform.localPosition = (localStart + localEnd) * 0.5f + Vector3.down * 0.5f;
            road.transform.localScale = new Vector3(roadWidth, 0.2f, len);
            road.transform.localRotation = rot;
            var r = road.GetComponent<Renderer>();
            if (r != null && roadMaterial != null) r.sharedMaterial = roadMaterial;
        }

        private void TryApplyRoadMaterial(GameObject go)
        {
            if (go == null || roadMaterial == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = roadMaterial;
        }

        private void CreateTrees()
        {
            var matTrunk = ResolveMaterial(treeTrunkMaterial, Color.gray);
            var matFoliage = ResolveMaterial(treeFoliageMaterial, new Color(0.2f, 0.6f, 0.2f));
            bool useArrayPrefabs = treePrefabs != null && treePrefabs.Length > 0;
            bool usePrefab = treePrefab != null || useArrayPrefabs;
            float grassR = GrassDiscRadiusLocal();
            float inner = Mathf.Max(2f, treeClearingRadiusAroundCenter);
            float outer = Mathf.Max(inner + 2f, grassR - Mathf.Max(0.5f, treeInlandEdgePadding));
            if (outer <= inner + 0.5f)
            {
                inner = Mathf.Min(inner, grassR * 0.25f);
                outer = Mathf.Max(inner + 2f, grassR - 1f);
            }

            int placed = 0;
            int maxAttempts = Mathf.Max(treeCount * 60, 800);
            float bias = Mathf.Clamp(treeRadialBiasExponent, 0.35f, 1.35f);

            for (int attempt = 0; attempt < maxAttempts && placed < treeCount; attempt++)
            {
                float u = StableHash01(treePlacementSeed, attempt * 2 + 11);
                float v = StableHash01(treePlacementSeed, attempt * 2 + 12);
                float angle = u * Mathf.PI * 2f;
                float radialT = Mathf.Pow(v, bias);
                float dist = Mathf.Lerp(inner, outer, radialT);
                dist *= CoastRadiusMultiplier(angle);
                dist = Mathf.Clamp(dist, inner, outer);
                Vector3 localPos = new Vector3(Mathf.Cos(angle) * dist, LandTopLocalY, Mathf.Sin(angle) * dist);

                float distFromRoad = DistanceToRoadSegmentXZ(localPos);
                if (distFromRoad < treeMinDistFromRoad) continue;

                Vector2 worldXZ = new Vector2(transform.position.x + localPos.x, transform.position.z + localPos.z);
                float dens = VegetationDensityGenerator.GetTreeDensity(worldXZ, treePlacementSeed);
                if (dens < treeDensityNoiseThreshold) continue;

                if (usePrefab)
                {
                    var prefabToUse = useArrayPrefabs
                        ? treePrefabs[Mathf.FloorToInt(StableHash01(treePlacementSeed, attempt + 10000) * treePrefabs.Length) % treePrefabs.Length]
                        : treePrefab;
                    if (prefabToUse == null) continue;
                    var instance = Object.Instantiate(prefabToUse, _treesRoot);
                    instance.transform.localPosition = localPos;
                    float yaw = StableHash01(treePlacementSeed, attempt + 5000) * 360f;
                    instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    float sc = treePrefabScale * (0.82f + StableHash01(treePlacementSeed, attempt + 7000) * 0.38f);
                    instance.transform.localScale = Vector3.one * sc;
                    instance.name = $"Tree_{placed}";
                    MarkIgnoredForNavMesh(instance);
                }
                else
                {
                    var root = CreateProceduralTree(localPos, matTrunk, matFoliage);
                    root.name = $"Tree_{placed}";
                }

                placed++;
            }

            if (placed < treeCount)
            {
                for (int attempt = 0; attempt < maxAttempts && placed < treeCount; attempt++)
                {
                    float u = StableHash01(treePlacementSeed + 777, attempt * 2 + 3);
                    float v = StableHash01(treePlacementSeed + 777, attempt * 2 + 4);
                    float angle = u * Mathf.PI * 2f;
                    float dist = Mathf.Lerp(inner, outer, Mathf.Pow(v, bias));
                    dist *= CoastRadiusMultiplier(angle);
                    dist = Mathf.Clamp(dist, inner, outer);
                    Vector3 localPos = new Vector3(Mathf.Cos(angle) * dist, LandTopLocalY, Mathf.Sin(angle) * dist);
                    if (DistanceToRoadSegmentXZ(localPos) < treeMinDistFromRoad) continue;

                    if (usePrefab)
                    {
                        var prefabToUse = useArrayPrefabs
                            ? treePrefabs[Mathf.FloorToInt(StableHash01(treePlacementSeed, attempt + 12000) * treePrefabs.Length) % treePrefabs.Length]
                            : treePrefab;
                        if (prefabToUse == null) continue;
                        var instance = Object.Instantiate(prefabToUse, _treesRoot);
                        instance.transform.localPosition = localPos;
                        instance.transform.localRotation = Quaternion.Euler(0f, StableHash01(treePlacementSeed, attempt + 888) * 360f, 0f);
                        instance.transform.localScale = Vector3.one * (treePrefabScale * (0.85f + StableHash01(treePlacementSeed, attempt + 999) * 0.3f));
                        instance.name = $"Tree_fill_{placed}";
                        MarkIgnoredForNavMesh(instance);
                    }
                    else
                    {
                        var root = CreateProceduralTree(localPos, matTrunk, matFoliage);
                        root.name = $"Tree_fill_{placed}";
                    }

                    placed++;
                }
            }
        }

        /// <summary>Distance from a point (local XZ on island) to the road segment in XZ.</summary>
        private float DistanceToRoadSegmentXZ(Vector3 localPos)
        {
            Vector3 a = roadStartPosition;
            Vector3 b = RoadEndPosition;
            Vector3 p = localPos;
            Vector3 ab = b - a;
            float abSqr = ab.x * ab.x + ab.z * ab.z;
            if (abSqr < 1e-6f)
                return Vector2.Distance(new Vector2(p.x, p.z), new Vector2(a.x, a.z));
            float t = Mathf.Clamp01(((p.x - a.x) * ab.x + (p.z - a.z) * ab.z) / abSqr);
            float qx = a.x + ab.x * t;
            float qz = a.z + ab.z * t;
            return Vector2.Distance(new Vector2(p.x, p.z), new Vector2(qx, qz));
        }

        private static float StableHash01(int seed, int salt)
        {
            float x = Mathf.Sin(seed * 12.9898f + salt * 78.233f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        private Transform CreateProceduralTree(Vector3 localPos, Material trunkMat, Material foliageMat)
        {
            var root = new GameObject("Tree").transform;
            root.SetParent(_treesRoot, false);
            root.localPosition = localPos;
            root.localRotation = Quaternion.identity;

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(root);
            trunk.transform.localPosition = Vector3.up * 2f;
            trunk.transform.localScale = new Vector3(0.4f, 2f, 0.4f);
            if (trunk.GetComponent<Renderer>() is Renderer rt) rt.sharedMaterial = trunkMat;

            var foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            foliage.name = "Foliage";
            foliage.transform.SetParent(root);
            foliage.transform.localPosition = Vector3.up * 5f;
            foliage.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
            if (foliage.GetComponent<Renderer>() is Renderer rf) rf.sharedMaterial = foliageMat;

            root.gameObject.AddComponent<TreeSway>();
            return root;
        }

        private void CreateMountains()
        {
            var mat = ResolveMaterial(mountainMaterial, new Color(0.5f, 0.45f, 0.4f));
            float seaWorldY = SeaLevelWorldY;
            float sink = Mathf.Max(0f, mountainPrefabGroundSink);

            float arcStartRad = 0f;
            float arcSpanRad = Mathf.PI * 2f;
            if (oceanViewMountainSparseHalfAngleDegrees > 0.5f && mountainCount > 0)
            {
                float gapHalf = oceanViewMountainSparseHalfAngleDegrees * Mathf.Deg2Rad;
                float oceanRad = beachSpawnAngleDegrees * Mathf.Deg2Rad;
                arcStartRad = oceanRad + gapHalf;
                arcSpanRad = Mathf.PI * 2f - 2f * gapHalf;
                if (arcSpanRad < 0.5f) { arcStartRad = 0f; arcSpanRad = Mathf.PI * 2f; }
            }

            for (int i = 0; i < mountainCount; i++)
            {
                float t = mountainCount <= 1 ? 0.5f : i / (float)(mountainCount - 1);
                float angle = arcStartRad + t * arcSpanRad;
                angle += (StableHash01(mountainPlacementSeed, i * 5 + 1) - 0.5f) * 2f * mountainAngleJitterRad;

                float distMul = 1f + (StableHash01(mountainPlacementSeed, i * 5 + 2) - 0.5f) * 2f * mountainDistanceJitter;
                float ringR;
                if (mountainRingUseAbsoluteDistance)
                    ringR = Mathf.Max(IslandRadius + 4f, mountainDistance) * distMul;
                else
                    ringR = (IslandRadius + Mathf.Max(0f, coastalMountainGap)) * distMul;

                var localFlat = new Vector3(Mathf.Cos(angle) * ringR, 0f, Mathf.Sin(angle) * ringR);

                if (mountainTerrainPrefab != null)
                {
                    float yaw = StableHash01(mountainPlacementSeed, i * 5 + 3) * 360f + mountainExtraYawOffsetDegrees;
                    var inst = Object.Instantiate(mountainTerrainPrefab, _mountainsRoot);
                    inst.transform.localPosition = localFlat;
                    inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    float s = mountainPrefabScale * (0.85f + StableHash01(mountainPlacementSeed, i * 5 + 4) * 0.35f);
                    inst.transform.localScale = Vector3.one * s;
                    inst.name = $"MountainTerrain_{i}";
                    float bottomTarget = snapMountainPrefabsToLandTop ? LandTopWorldY - sink : seaWorldY - sink;
                    AlignPrefabBottomToWorldY(inst, bottomTarget);
                    MarkIgnoredForNavMesh(inst);
                    continue;
                }

                float h = 15f + StableHash01(mountainPlacementSeed, i * 7) * 20f;
                float radius = 8f + StableHash01(mountainPlacementSeed, i * 7 + 1) * 12f;
                float baseWorldY = snapMountainPrefabsToLandTop ? LandTopWorldY - sink : seaWorldY - sink;
                float centerWorldY = baseWorldY + h;

                var m = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                m.name = $"Mountain_{i}";
                m.transform.SetParent(_mountainsRoot, false);
                var worldFlat = transform.TransformPoint(localFlat);
                m.transform.position = new Vector3(worldFlat.x, centerWorldY, worldFlat.z);
                m.transform.localScale = new Vector3(radius, h, radius);
                if (m.GetComponent<Renderer>() is Renderer rend) rend.sharedMaterial = mat;
            }
        }

        /// <summary>Moves instance so combined renderer bounds bottom matches <paramref name="targetWorldY"/>.</summary>
        private static void AlignPrefabBottomToWorldY(GameObject inst, float targetWorldY)
        {
            var rends = inst.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                b.Encapsulate(rends[i].bounds);
            float delta = targetWorldY - b.min.y;
            if (Mathf.Abs(delta) > 0.001f)
                inst.transform.position += new Vector3(0f, delta, 0f);
        }

        private void CreateGrassPatches()
        {
            Material mat = null;
            if (IsMaterialValid(grassPatchMaterial)) mat = grassPatchMaterial;
            else if (IsMaterialValid(grassMaterial)) mat = grassMaterial;
            else mat = CreateDefaultMaterial(new Color(0.25f, 0.65f, 0.2f));
            if (mat == null) return;
            float islandR = IslandRadius;
            for (int i = 0; i < grassPatchCount; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(2f, Mathf.Max(3f, islandR - 2f));
                dist *= CoastRadiusMultiplier(angle);
                dist = Mathf.Clamp(dist, 2f, islandR - 2f);
                Vector3 localPos = new Vector3(Mathf.Cos(angle) * dist, LandTopLocalY + 0.01f, Mathf.Sin(angle) * dist);
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "GrassPatch";
                quad.transform.SetParent(_groundRoot);
                quad.transform.localPosition = localPos;
                quad.transform.localRotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
                quad.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                if (quad.GetComponent<Renderer>() is Renderer r) r.sharedMaterial = mat;
                if (quad.GetComponent<Collider>() is Collider c) Object.Destroy(c);
            }
        }

        private void CreateAnimals()
        {
            var mat = ResolveMaterial(animalMaterial, new Color(0.6f, 0.4f, 0.2f));
            var animalsRoot = new GameObject("Island_Animals").transform;
            animalsRoot.SetParent(transform);
            float islandR = IslandRadius;
            for (int i = 0; i < animalCount; i++)
            {
                float angle = (i / (float)animalCount) * Mathf.PI * 2f + i;
                float dist = islandR * 0.3f + Random.Range(0f, islandR * 0.4f);
                dist *= CoastRadiusMultiplier(angle);
                dist = Mathf.Clamp(dist, 2f, islandR - 1f);
                Vector3 localPos = new Vector3(Mathf.Cos(angle) * dist, LandTopLocalY + 0.4f, Mathf.Sin(angle) * dist);
                var animal = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                animal.name = "Animal";
                animal.transform.SetParent(animalsRoot);
                animal.transform.localPosition = localPos;
                animal.transform.localScale = new Vector3(0.5f, 0.8f, 0.5f);
                if (animal.GetComponent<Renderer>() is Renderer r) r.sharedMaterial = mat;
                animal.AddComponent<AnimalWander>();
            }
        }

        private void CreateEnemies()
        {
#if UNITY_EDITOR
            TryBindScaryMonsterIslandPrefabFromPath();
#endif
            if (enemyCount <= 0) return;
            _enemiesRoot = new GameObject("Island_Enemies").transform;
            _enemiesRoot.SetParent(transform, false);

            GameObject prefabToSpawn = null;
            if (useImportedScaryMonsterOnIsland && scaryMonsterIslandPrefab != null)
                prefabToSpawn = scaryMonsterIslandPrefab;
            else if (enemyPrefab != null)
                prefabToSpawn = enemyPrefab;
            var usePrefab = prefabToSpawn != null;
            float grassR = GrassDiscRadiusLocal();
            float inner = Mathf.Max(3f, treeClearingRadiusAroundCenter * 0.8f);
            float outer = Mathf.Max(inner + 3f, grassR - 2f);

            for (int i = 0; i < enemyCount; i++)
            {
                bool nearRoad = StableHash01(17001, i * 13 + 1) < roadEnemyBias;
                Vector3 localPos;
                if (nearRoad)
                {
                    float t = StableHash01(17001, i * 13 + 2);
                    float side = StableHash01(17001, i * 13 + 3) < 0.5f ? -1f : 1f;
                    float lateral = Mathf.Lerp(minEnemyDistanceFromRoad, minEnemyDistanceFromRoad + 4f, StableHash01(17001, i * 13 + 4));
                    var anchor = Vector3.Lerp(roadStartPosition, RoadEndPosition, t);
                    localPos = anchor + Vector3.right * lateral * side;
                }
                else
                {
                    float u = StableHash01(17001, i * 13 + 5);
                    float v = StableHash01(17001, i * 13 + 6);
                    float angle = u * Mathf.PI * 2f;
                    float dist = Mathf.Lerp(inner, outer, v);
                    dist *= CoastRadiusMultiplier(angle);
                    localPos = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                }

                localPos.y = LandTopLocalY + enemySpawnHeightOffset;
                var go = usePrefab ? Object.Instantiate(prefabToSpawn, _enemiesRoot) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = $"Enemy_{i}";
                go.transform.localPosition = localPos;
                go.transform.localRotation = Quaternion.Euler(0f, StableHash01(17001, i * 13 + 7) * 360f, 0f);
                if (!usePrefab)
                    go.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
                else if (scaryMonsterIslandPrefab != null && prefabToSpawn == scaryMonsterIslandPrefab && scaryMonsterIslandScale > 0f)
                    go.transform.localScale = Vector3.one * scaryMonsterIslandScale;

                if (usePrefab && scaryMonsterIslandPrefab != null && prefabToSpawn == scaryMonsterIslandPrefab)
                    EnsureEnemyHitColliderForImported(go);

                var ec = go.GetComponent<EnemyController>();
                if (ec == null) ec = go.AddComponent<EnemyController>();

                if (go.GetComponent<EnemyBillboardVitals>() == null)
                    go.AddComponent<EnemyBillboardVitals>();

                var hasNavMesh = NavMesh.CalculateTriangulation().vertices.Length > 0;
                if (hasNavMesh)
                {
                    var agent = go.GetComponent<NavMeshAgent>();
                    if (agent == null) agent = go.AddComponent<NavMeshAgent>();
                    if (agent != null)
                    {
                        agent.radius = Mathf.Max(0.25f, agent.radius);
                        agent.height = Mathf.Max(1.2f, agent.height);
                        agent.speed = Mathf.Max(2.8f, agent.speed);
                        agent.angularSpeed = Mathf.Max(240f, agent.angularSpeed);
                    }

                    var ai = go.GetComponent<EnemyNavMeshAI>();
                    if (ai == null) ai = go.AddComponent<EnemyNavMeshAI>();
                    ai.ConfigureChase(80f, 1.8f);
                }
                else
                {
                    // Fallback behavior when NavMesh is unavailable.
                    var flee = go.GetComponent<SimpleFleeFromTarget>();
                    if (flee == null) flee = go.AddComponent<SimpleFleeFromTarget>();
                }

                TrySetEnemyTag(go);
            }
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
                Debug.LogWarning("[IslandWorldBuilder] Tag 'Enemy' is not defined in Tags & Layers. Using component checks only.");
            }
        }

        /// <summary>Imported meshes may ship without colliders; arrows need a physics body to hit.</summary>
        private static void EnsureEnemyHitColliderForImported(GameObject go)
        {
            if (go == null) return;
            if (go.GetComponentInChildren<Collider>(true) != null)
                return;
            var cap = go.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 1f, 0f);
            cap.height = 2.2f;
            cap.radius = 0.45f;
        }

        private static void MarkIgnoredForNavMesh(GameObject go)
        {
            if (go == null) return;
            var mod = go.GetComponent<NavMeshModifier>();
            if (mod == null)
                mod = go.AddComponent<NavMeshModifier>();
            mod.ignoreFromBuild = true;
        }

        private static bool IsMaterialValid(Material m)
        {
            if (m == null || m.shader == null) return false;
            var n = m.shader.name;
            return !n.Contains("InternalErrorShader") && !n.Contains("Hidden/InternalErrorShader");
        }

        private static Material ResolveMaterial(Material prefer, Color fallbackColor)
        {
            if (IsMaterialValid(prefer)) return prefer;
            return CreateDefaultMaterial(fallbackColor);
        }

        private static Material CreateDefaultMaterial(Color c)
        {
            Shader shader = null;
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            }
            else
            {
                shader = Shader.Find("Standard")
                    ?? Shader.Find("Legacy Shaders/Diffuse");
            }
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", c);
            else if (m.HasProperty("_Color"))
                m.color = c;
            return m;
        }
    }
}
