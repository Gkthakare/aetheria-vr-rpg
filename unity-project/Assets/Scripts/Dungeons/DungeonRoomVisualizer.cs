using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Aetheria.Dungeons;
using Aetheria.World;
using Aetheria.Training;
using Aetheria.Gameplay;

namespace Aetheria.Dungeons
{
    /// <summary>
    /// Visualizes the DungeonGenerator graph by spawning room pads in a circle
    /// and placing portal pads between linked rooms. Stepping on a portal teleports
    /// the player to the linked room.
    /// </summary>
    public class DungeonRoomVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DungeonGenerator dungeonGenerator;
        [SerializeField] private Transform player;  // XR Origin or Camera Offset transform
        [SerializeField] private GameObject roomPadPrefab;
        [SerializeField] private GameObject portalPadPrefab;

        [Header("Layout")]
        [SerializeField] private float layoutRadius = 12f;
        [SerializeField] private float portalOffsetFromRoom = 2.2f;
        [Tooltip("Global sea / floor datum (World Y). Room pads spawn at this + groundHeight + yLift so they stay above Suimono when sea is Y=0.")]
        [SerializeField] private float seaLevelWorldY = 0f;
        [Tooltip("Height of playable ground above sea (chunk cube tops, etc.).")]
        [SerializeField] private float groundHeight = 1f;
        [SerializeField] private float yLift = 0.2f; // extra lift above ground to avoid z-fighting
        [SerializeField] private float portalDoorWidth = 0.9f;   // visual size for door-style portals
        [SerializeField] private float portalDoorHeight = 1.9f;  // visual size for door-style portals
        [SerializeField] private bool drawLinks = false;
        [Header("Floating Dungeon")]
        [Tooltip("If enabled, dungeon room pads are forced to this world Y height (floating in air).")]
        [SerializeField] private bool forceFloatingHeight = true;
        [SerializeField] private float floatingSurfaceWorldY = 22f;
        [Tooltip("World-space XZ offset for floating dungeon center so it appears away from the island play area.")]
        [SerializeField] private Vector2 floatingCenterXZ = new Vector2(46f, 46f);

        [Header("Room Materials")]
        [SerializeField] private Material matStart;
        [SerializeField] private Material matHub;
        [SerializeField] private Material matKey;
        [SerializeField] private Material matTrap;
        [SerializeField] private Material matMiniBoss;
        [SerializeField] private Material matBoss;
        [SerializeField] private Material matDefault;
        [SerializeField] private Material matPortal;
        [SerializeField] private Material matTraining;

        [Header("Sand & Grass Around Pads")]
        [SerializeField] private Material matSand;
        [SerializeField] private Material matGrass;
        [SerializeField] private float sandPatchSize = 5f;
        [SerializeField] private float grassRingRadius = 2.5f;
        [SerializeField] private int grassPatchCount = 16;
        [SerializeField] private int stripsPerPatch = 4;
        [SerializeField] private float stripLength = 0.4f;
        [SerializeField] private bool grassWobble = true;

        [Header("Floor 1 — Egypt chamber (ImportedContent)")]
        [Tooltip("If assigned, floor 1 uses this as the room pad mesh instead of auto-search.")]
        [SerializeField] private GameObject floor1EgyptChamberRoomPrefab;
        [Tooltip("Editor: search this folder for an asset whose name contains the hint (default picks Egypt.FBX).")]
        [SerializeField] private string floor1EgyptImportedRoot = "Assets/ImportedContent/Environments/egypt-chamber-v1/source/Egypt_level";
        [SerializeField] private string floor1EgyptImportedHint = "Egypt";
        [Tooltip("Uniform scale applied to each instantiated floor-1 room (set <1 if the FBX is huge).")]
        [SerializeField] private float floor1RoomUniformScale = 0.04f;

        [Header("SAO / Polish")]
        [Tooltip("If set, all room pads use this material (e.g. Mat_SAO_Toon) instead of per-room materials.")]
        [SerializeField] private Material overrideAllPadMaterials;
        [SerializeField] private bool addPortalVfxOnSpawn = true;
        [Tooltip("If true, auto-replaces unsupported/missing (magenta) materials with URP Lit fallbacks.")]
        [SerializeField] private bool autoFixMissingMaterials = true;

        [Header("AI Navigation")]
        [Tooltip("Builds NavMesh for floating dungeon room pads each time Build() runs.")]
        [SerializeField] private bool autoBakeDungeonNavMesh = true;
        [SerializeField] private NavMeshSurface dungeonNavMeshSurface;
        [Tooltip("If true, visualizer auto-builds once on Start. Disable when DungeonFloorCombatController drives Build().")]
        [SerializeField] private bool autoBuildOnStart = false;
        [Tooltip("Prefer generated Meshy assets from Assets/ImportedContent for room pads and portals.")]
#if UNITY_EDITOR
        [SerializeField] private bool preferImportedMeshyAssets = true;
#endif
        [Header("Visual polish")]
        [SerializeField] private bool enableRoomAuraLights = true;
        [SerializeField] private float auraIntensity = 1.35f;
        [SerializeField] private float auraRange = 4.2f;
        [SerializeField] private bool enableFloorPostProcessingPreset = true;
        [SerializeField] private Light sceneDirectionalLight;

        private int _themeFloor = 1;
        private bool _capturedLightingDefaults;
        private Color _defaultAmbient;
        private bool _defaultFogEnabled;
        private Color _defaultFogColor;
        private float _defaultFogDensity;
        private Color _defaultDirLightColor;
        private float _defaultDirLightIntensity;

        private readonly Dictionary<string, Transform> roomIdToTransform = new Dictionary<string, Transform>();
        private Material _fallbackPadMaterial;
        private Material _fallbackPortalMaterial;
        private Material _fallbackSandMaterial;
        private Material _fallbackGrassMaterial;

        void Start()
        {
            if (!autoBuildOnStart)
                return;

            // If combat controller is active, it handles floor setup + Build().
            if (FindObjectOfType<DungeonFloorCombatController>() != null)
                return;

            // Wait a frame for DungeonGenerator to finish, then build.
            StartCoroutine(BuildAfterDelay());
        }

        private IEnumerator BuildAfterDelay()
        {
            yield return new WaitForSeconds(0.1f); // Small delay to let DungeonGenerator finish
            var combat = FindObjectOfType<DungeonFloorCombatController>();
            if (combat != null && combat.isActiveAndEnabled)
                yield break; // Combat controller owns build timing.
            Build();
        }

        public void Build()
        {
            ResolveImportedMeshyPrefabsIfNeeded();
            ClearChildren();

            if (dungeonGenerator == null)
            {
                Debug.LogWarning("[Visualizer] No dungeon generator found!");
                return;
            }

            var graph = dungeonGenerator.CurrentGraph;
            if (graph == null || graph.Nodes.Count == 0)
            {
                Debug.LogWarning("[Visualizer] No dungeon graph found. Generating now...");
                dungeonGenerator.GenerateGraph();
                graph = dungeonGenerator.CurrentGraph;
                
                if (graph == null || graph.Nodes.Count == 0)
                {
                    Debug.LogWarning("[Visualizer] Still no dungeon graph after generation!");
                    return;
                }
            }

            Debug.Log($"[Visualizer] Building dungeon with {graph.Nodes.Count} rooms");

            // Lay out rooms around a circle deterministically by index
            roomIdToTransform.Clear();
            int count = graph.Nodes.Count;
            Debug.Log($"[Visualizer] Spawning {count} rooms in circle layout");
            
            float surfaceY = forceFloatingHeight ? floatingSurfaceWorldY : seaLevelWorldY + groundHeight + yLift;
            Vector3 center = forceFloatingHeight
                ? new Vector3(floatingCenterXZ.x, surfaceY, floatingCenterXZ.y)
                : new Vector3(0f, surfaceY, 0f);
            for (int i = 0; i < count; i++)
            {
                var node = graph.Nodes[i];
                float angle = (Mathf.PI * 2f) * (i / (float)count);
                Vector3 pos = center + new Vector3(
                    Mathf.Cos(angle) * layoutRadius,
                    0f,
                    Mathf.Sin(angle) * layoutRadius
                );

                Debug.Log($"[Visualizer] Spawning room {i}: {node.Type} at {pos}");
                
                var roomGO = Instantiate(roomPadPrefab, pos, Quaternion.identity, transform);
                roomGO.name = $"Room_{node.Type}_{node.Id.Substring(0, 6)}";
                if (_themeFloor == 1 && floor1RoomUniformScale > 0f && !Mathf.Approximately(floor1RoomUniformScale, 1f))
                    roomGO.transform.localScale = Vector3.one * floor1RoomUniformScale;
                SetRoomMaterial(roomGO, node.Type);
                if (enableRoomAuraLights)
                    AddRoomAura(roomGO, node.Type);

                if (matSand != null) CreateSandPatch(pos);
                if (matGrass != null) CreateGrassRing(pos);

                if (node.Type == RoomType.Training)
                    roomGO.AddComponent<TrainingRoomManager>();

                roomIdToTransform[node.Id] = roomGO.transform;
            }

            // Create portals for links
            foreach (var a in graph.Nodes)
            {
                foreach (var linkId in a.Links)
                {
                    // Only create one direction (avoid duplicates)
                    if (string.Compare(a.Id, linkId, System.StringComparison.Ordinal) >= 0) continue;

                    if (!roomIdToTransform.TryGetValue(a.Id, out var from)) continue;
                    if (!roomIdToTransform.TryGetValue(linkId, out var to)) continue;

                    Vector3 dir = (to.position - from.position).normalized;
                    Vector3 portalPosFrom = from.position + dir * portalOffsetFromRoom;
                    Vector3 portalPosTo = to.position - dir * portalOffsetFromRoom;

                    CreatePortal(portalPosFrom, to.position);
                    CreatePortal(portalPosTo, from.position);

                    if (drawLinks)
                    {
                        var lr = new GameObject($"Link_{a.Id.Substring(0, 4)}_{linkId.Substring(0, 4)}").AddComponent<LineRenderer>();
                        lr.transform.SetParent(transform);
                        lr.positionCount = 2;
                        lr.SetPositions(new[] { from.position + Vector3.up * 0.05f, to.position + Vector3.up * 0.05f });
                        lr.startWidth = lr.endWidth = 0.05f;
                        lr.material = matPortal != null ? matPortal : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    }
                }
            }

            EnsureDungeonNavMeshBuilt();
        }

        private void ResolveImportedMeshyPrefabsIfNeeded()
        {
#if UNITY_EDITOR
            if (preferImportedMeshyAssets)
            {
                var importedPortal = FindImportedPrefab("Assets/ImportedContent/Creatures/environment.portal.arcane.v1", "portal");
                if (importedPortal != null) portalPadPrefab = importedPortal;
            }
            else if (portalPadPrefab == null)
            {
                portalPadPrefab = FindImportedPrefab("Assets/ImportedContent/Creatures/environment.portal.arcane.v1", "portal");
            }

            if (_themeFloor == 1)
            {
                if (floor1EgyptChamberRoomPrefab != null)
                    roomPadPrefab = floor1EgyptChamberRoomPrefab;
                else if (!string.IsNullOrWhiteSpace(floor1EgyptImportedRoot) && !string.IsNullOrWhiteSpace(floor1EgyptImportedHint))
                {
                    var egypt = FindImportedPrefab(floor1EgyptImportedRoot, floor1EgyptImportedHint);
                    if (egypt != null)
                    {
                        roomPadPrefab = egypt;
                        Debug.Log($"[Visualizer] Floor 1 room pad → Egypt chamber ({floor1EgyptImportedRoot}, hint '{floor1EgyptImportedHint}').");
                    }
                }
            }

            if (preferImportedMeshyAssets)
            {
                if (_themeFloor != 1 || roomPadPrefab == null)
                {
                    var importedRoom = FindImportedPrefab("Assets/ImportedContent/Creatures/environment.dungeon.bricks.v1", "brick");
                    if (importedRoom != null) roomPadPrefab = importedRoom;
                }
            }
            else if (roomPadPrefab == null)
            {
                roomPadPrefab = FindImportedPrefab("Assets/ImportedContent/Creatures/environment.dungeon.bricks.v1", "brick");
            }
#endif
        }

#if UNITY_EDITOR
        private static GameObject FindImportedPrefab(string searchRoot, string hint)
        {
            var guids = AssetDatabase.FindAssets(hint, new[] { searchRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                {
                    Debug.Log($"[Visualizer] Auto-linked imported prefab: {path}");
                    return go;
                }
            }
            return null;
        }
#endif

        public void SetAutoBuildOnStart(bool enabled)
        {
            autoBuildOnStart = enabled;
        }

        private void EnsureDungeonNavMeshBuilt()
        {
            if (!autoBakeDungeonNavMesh)
                return;

            if (dungeonNavMeshSurface == null)
            {
                dungeonNavMeshSurface = GetComponent<NavMeshSurface>();
                if (dungeonNavMeshSurface == null)
                    dungeonNavMeshSurface = gameObject.AddComponent<NavMeshSurface>();
            }

            dungeonNavMeshSurface.collectObjects = CollectObjects.Children;
            dungeonNavMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            dungeonNavMeshSurface.layerMask = ~0;
            dungeonNavMeshSurface.BuildNavMesh();
        }

        private void CreatePortal(Vector3 position, Vector3 targetPosition)
        {
            var lookDir = (targetPosition - position).normalized;
            var portal = Instantiate(portalPadPrefab, position, Quaternion.LookRotation(lookDir, Vector3.up), transform);
            portal.name = "PortalPad";
            if (matPortal != null)
            {
                var r = portal.GetComponentInChildren<Renderer>();
                if (r != null) r.material = matPortal;
            }
            EnsureRenderableMaterials(portal, matPortal, new Color(0.2f, 0.55f, 0.95f), ref _fallbackPortalMaterial);

            // Try to give it a visible door-like footprint if it is a Quad/Plane
            // Users can still swap the prefab for a dedicated door mesh; this scales reasonably for common meshes
            portal.transform.localScale = new Vector3(portalDoorWidth, portalDoorHeight, 0.1f);

            var tp = portal.GetComponent<TeleportPortal>();
            if (tp == null) tp = portal.AddComponent<TeleportPortal>();
            tp.TargetPosition = targetPosition + Vector3.up * 0.1f;
            tp.Player = player;

            if (addPortalVfxOnSpawn && portal.GetComponent<PortalVFX>() == null)
                portal.AddComponent<PortalVFX>();
        }

        private float PadSurfaceWorldY => seaLevelWorldY + groundHeight + yLift;
        private float GroundPlaneWorldY => forceFloatingHeight ? floatingSurfaceWorldY - yLift : seaLevelWorldY + groundHeight;

        public void SetFloatingHeightWorldY(float worldY)
        {
            forceFloatingHeight = true;
            floatingSurfaceWorldY = worldY;
        }

        public void SetFloorVisualTheme(int floor)
        {
            _themeFloor = Mathf.Max(1, floor);
            layoutRadius = 12f + Mathf.Min(3f, (_themeFloor - 1) * 0.9f);
            portalDoorHeight = 1.9f + Mathf.Min(0.5f, (_themeFloor - 1) * 0.12f);
            auraIntensity = 1.2f + Mathf.Min(0.8f, (_themeFloor - 1) * 0.18f);
            ApplyFloorPostProcessPreset();
        }

        private void CreateSandPatch(Vector3 roomWorldPos)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "SandPatch";
            quad.transform.SetParent(transform);
            quad.transform.position = new Vector3(roomWorldPos.x, GroundPlaneWorldY, roomWorldPos.z);
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(sandPatchSize, sandPatchSize, 1f);
            var r = quad.GetComponent<Renderer>();
            if (r != null && matSand != null) r.sharedMaterial = matSand;
            EnsureRendererMaterial(r, matSand, new Color(0.78f, 0.67f, 0.45f), ref _fallbackSandMaterial);
            // Remove collider so it doesn't block movement
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        private void CreateGrassRing(Vector3 roomWorldPos)
        {
            float surfaceY = PadSurfaceWorldY + 0.005f;
            for (int p = 0; p < grassPatchCount; p++)
            {
                float angle = (Mathf.PI * 2f) * (p / (float)grassPatchCount);
                float baseX = roomWorldPos.x + Mathf.Cos(angle) * grassRingRadius;
                float baseZ = roomWorldPos.z + Mathf.Sin(angle) * grassRingRadius;
                for (int s = 0; s < stripsPerPatch; s++)
                {
                    float offsetX = (s - (stripsPerPatch - 1) * 0.5f) * 0.08f;
                    float offsetZ = (s - (stripsPerPatch - 1) * 0.5f) * 0.08f;
                    var strip = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    strip.name = "GrassStrip";
                    strip.transform.SetParent(transform);
                    strip.transform.position = new Vector3(baseX + offsetX, surfaceY, baseZ + offsetZ);
                    strip.transform.rotation = Quaternion.Euler(90f, -angle * Mathf.Rad2Deg, 0f);
                    strip.transform.localScale = new Vector3(stripLength, 0.02f, 0.06f);
                    var r = strip.GetComponent<Renderer>();
                    if (r != null && matGrass != null) r.sharedMaterial = matGrass;
                    EnsureRendererMaterial(r, matGrass, new Color(0.32f, 0.67f, 0.3f), ref _fallbackGrassMaterial);
                    var col = strip.GetComponent<Collider>();
                    if (col != null) Destroy(col);
                    if (grassWobble) strip.AddComponent<GrassWobble>();
                }
            }
        }

        private void SetRoomMaterial(GameObject roomGO, RoomType type)
        {
            if (overrideAllPadMaterials != null)
            {
                var r = roomGO.GetComponentInChildren<Renderer>();
                if (r != null) r.sharedMaterial = overrideAllPadMaterials;
                return;
            }

            Material mat = matDefault;
            switch (type)
            {
                case RoomType.Start: mat = matStart ?? matDefault; break;
                case RoomType.Hub: mat = matHub ?? matDefault; break;
                case RoomType.KeyRoom: mat = matKey ?? matDefault; break;
                case RoomType.TrapRoom: mat = matTrap ?? matDefault; break;
                case RoomType.MiniBoss: mat = matMiniBoss ?? matDefault; break;
                case RoomType.Boss: mat = matBoss ?? matDefault; break;
                case RoomType.Training: mat = matTraining ?? matDefault; break;
            }
            var ren = roomGO.GetComponentInChildren<Renderer>();
            if (ren != null && mat != null) ren.material = mat;
            EnsureRenderableMaterials(roomGO, mat, new Color(0.46f, 0.58f, 0.7f), ref _fallbackPadMaterial);
        }

        private void EnsureRenderableMaterials(GameObject root, Material preferred, Color fallbackColor, ref Material fallbackMat)
        {
            if (!autoFixMissingMaterials || root == null)
                return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                EnsureRendererMaterial(r, preferred, fallbackColor, ref fallbackMat);
        }

        private static void EnsureRendererMaterial(Renderer r, Material preferred, Color fallbackColor, ref Material fallbackMat)
        {
            if (r == null)
                return;

            var current = r.sharedMaterial;
            if (preferred != null && IsMaterialSupported(preferred))
            {
                if (!IsMaterialSupported(current))
                    r.sharedMaterial = preferred;
                return;
            }

            if (IsMaterialSupported(current))
                return;

            if (fallbackMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null)
                    return;
                fallbackMat = new Material(shader);
                if (fallbackMat.HasProperty("_BaseColor"))
                    fallbackMat.SetColor("_BaseColor", fallbackColor);
                if (fallbackMat.HasProperty("_Color"))
                    fallbackMat.SetColor("_Color", fallbackColor);
            }

            r.sharedMaterial = fallbackMat;
        }

        private static bool IsMaterialSupported(Material m)
        {
            if (m == null || m.shader == null)
                return false;
            var n = m.shader.name;
            if (string.IsNullOrEmpty(n))
                return false;
            if (n.IndexOf("Hidden/InternalErrorShader", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            return true;
        }

        private void AddRoomAura(GameObject roomGO, RoomType type)
        {
            if (roomGO == null) return;
            var aura = new GameObject("RoomAura");
            aura.transform.SetParent(roomGO.transform, false);
            aura.transform.localPosition = Vector3.up * 1.15f;
            var l = aura.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = auraRange;
            l.intensity = auraIntensity;
            l.shadows = LightShadows.None;
            l.color = type switch
            {
                RoomType.Boss => new Color(1f, 0.2f, 0.35f),
                RoomType.Training => new Color(0.2f, 0.95f, 1f),
                RoomType.KeyRoom => new Color(1f, 0.8f, 0.25f),
                RoomType.TrapRoom => new Color(0.8f, 0.4f, 1f),
                _ => new Color(0.5f, 0.75f, 1f)
            };
        }

        private void ApplyFloorPostProcessPreset()
        {
            if (!enableFloorPostProcessingPreset)
                return;

            CaptureLightingDefaultsIfNeeded();

            switch (_themeFloor)
            {
                case 1:
                    // Warm sandstone / tomb read for Egypt chamber floor 1.
                    RenderSettings.ambientLight = new Color(0.32f, 0.26f, 0.2f);
                    RenderSettings.fog = true;
                    RenderSettings.fogColor = new Color(0.26f, 0.2f, 0.14f);
                    RenderSettings.fogDensity = 0.0068f;
                    SetDirectionalLight(new Color(1f, 0.9f, 0.68f), 1.02f);
                    break;
                case 2:
                    RenderSettings.ambientLight = new Color(0.23f, 0.2f, 0.34f);
                    RenderSettings.fog = true;
                    RenderSettings.fogColor = new Color(0.2f, 0.14f, 0.33f);
                    RenderSettings.fogDensity = 0.0115f;
                    SetDirectionalLight(new Color(0.75f, 0.62f, 0.95f), 0.82f);
                    break;
                default:
                    RenderSettings.ambientLight = new Color(0.32f, 0.14f, 0.14f);
                    RenderSettings.fog = true;
                    RenderSettings.fogColor = new Color(0.25f, 0.08f, 0.08f);
                    RenderSettings.fogDensity = 0.014f;
                    SetDirectionalLight(new Color(1f, 0.52f, 0.4f), 1.0f);
                    break;
            }
        }

        private void CaptureLightingDefaultsIfNeeded()
        {
            if (_capturedLightingDefaults)
                return;
            _capturedLightingDefaults = true;
            _defaultAmbient = RenderSettings.ambientLight;
            _defaultFogEnabled = RenderSettings.fog;
            _defaultFogColor = RenderSettings.fogColor;
            _defaultFogDensity = RenderSettings.fogDensity;
            if (sceneDirectionalLight == null)
                sceneDirectionalLight = FindObjectOfType<Light>();
            if (sceneDirectionalLight != null)
            {
                _defaultDirLightColor = sceneDirectionalLight.color;
                _defaultDirLightIntensity = sceneDirectionalLight.intensity;
            }
        }

        private void SetDirectionalLight(Color c, float intensity)
        {
            if (sceneDirectionalLight == null)
                return;
            sceneDirectionalLight.color = c;
            sceneDirectionalLight.intensity = intensity;
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }
    }
}