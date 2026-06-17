using System.Collections;
using Aetheria.WaterMagic;
using UnityEngine;
using Suimono.Core;

namespace Aetheria.World
{
    /// <summary>
    /// Spawns SUIMONO 2 Module + Surface in Infinite 3D Ocean mode and disables per-tile water quads on ChunkManager
    /// so the ocean is seamless. Assign prefabs from Assets/SUIMONO - WATER SYSTEM 2/PREFABS/.
    /// </summary>
    /// <remarks>
    /// Infinite ocean is one horizontal plane at <see cref="waterSurfaceY"/> (or island sea level): it extends forever in X/Z.
    /// It is not a “lake” around an island — dry land is anything above that Y (terrain, pads, island mesh). To limit water visually you would need a different approach (finite mesh, stencil, etc.).
    /// </remarks>
    [DefaultExecutionOrder(-80)]
    public class SuimonoInfinityWaterBootstrap : MonoBehaviour
    {
        [Header("Enable")]
        [Tooltip("Infinite ocean fills the whole world on X/Z at one sea height. Land must be above that height; there is no donut-only ocean.")]
        [SerializeField] private bool enableSuimonoInfiniteOcean = true;

        [Header("Prefabs (SUIMONO - WATER SYSTEM 2 / PREFABS)")]
        [SerializeField] private GameObject suimonoModulePrefab;
        [SerializeField] private GameObject suimonoSurfacePrefab;

        [Header("Placement")]
        [SerializeField] private float waterSurfaceY;
        [Tooltip("When set, sea level = island transform.y + groundY. Use only if the playable shore is on that island; do not use when the hub/chunks are at world y=0 and the island is only offset (floods the hub).")]
        [SerializeField] private IslandWorldBuilder islandWorldForSeaLevel;
        [SerializeField] private Transform parentOverride;

        [Header("Camera (VR)")]
        [Tooltip("XR center eye / main camera. Strongly recommended for VR. If null, resolved at runtime from Camera.main / MainCamera tag.")]
        [SerializeField] private Transform manualCameraTransform;

        [Header("Sun (Suimono Module)")]
        [Tooltip("Scene directional light — assigned to SuimonoModule.setLight (caustics, spec, colors). If null and Auto Find is on, first Directional light in scene is used.")]
        [SerializeField] private Light sceneDirectionalLight;

        [SerializeField] private bool autoFindDirectionalLight = true;

        [Header("Water motion & magic (surface defaults)")]
        [Tooltip("Apply calm defaults on spawned SUIMONO_Surface for visible motion + interaction without opening the prefab.")]
        [SerializeField] private bool applyGameplayWaterDefaults = true;

        [Tooltip("SuimonoObject.flowSpeed — small = calm sea (0.05–0.15 typical).")]
        [SerializeField] private float defaultFlowSpeed = 0.05f;

        [SerializeField] private float defaultWaveScale = 0.45f;

        [Tooltip("Keeps Suimono ripple/physics interaction on (module + surface).")]
        [SerializeField] private bool enableSuimonoInteraction = true;

        [Tooltip("Optional: drag Assets/SUIMONO - WATER SYSTEM 2/EFFECTS/fx_rippleNormals.prefab — spawned under module so setLight/waves can drive ripples without demo scene.")]
        [SerializeField] private GameObject suimonoRippleFxPrefab;

        [Tooltip("Suimono surface shaders are Built-in Forward-oriented; forcing Forward avoids grab/refraction glitches on Deferred.")]
        [SerializeField] private bool forceMainCameraForwardRendering = true;

        [Tooltip("Increases main camera far clip so skybox and distant land stay visible past the water plane (default 1000 can clip the horizon).")]
        [SerializeField] private bool extendMainCameraFarClipForHorizon = true;
        [SerializeField] private float mainCameraFarClipPlane = 5000f;

        [Tooltip("When RenderSettings.skybox is set, force Main Camera clear flags to Skybox (not Solid Color). Fixes grey void / harsh horizon split with Suimono.")]
        [SerializeField] private bool ensureSkyboxClearFlagsOnMainCamera = true;

        [Tooltip("If assigned, sets RenderSettings.skybox at startup so Suimono water & skybox sampling stay in sync (optional if scene already sets skybox).")]
        [SerializeField] private Material skyboxMaterialEnsure;

        [Header("VR / Performance")]
        [Tooltip("Disables planar + dynamic reflections (common source of horizontal streaks / rainbow artifacts in VR and multi-camera setups).")]
        [SerializeField] private bool reduceReflectionWorkForVr = true;

        [Tooltip("Slightly lowers refraction strength when reflection path is reduced (less smear).")]
        [SerializeField] private float refractStrengthWhenStableVr = 0.55f;

        [Header("ChunkManager")]
        [SerializeField] private ChunkManager chunkManager;

        private SuimonoModule _module;
        private SuimonoObject _surface;
        private GameObject _surfaceGo;
        private float _lockedSeaLevelY;
        private int _horizonReapplyFrames;

        private void Awake()
        {
            if (chunkManager == null)
                chunkManager = GetComponent<ChunkManager>() ?? FindFirstObjectByType<ChunkManager>();

            if (IsAliveUnityObject(suimonoRippleFxPrefab))
                SuimonoWaterRuntimeLink.RippleNormalsPrefab = suimonoRippleFxPrefab;

            if (!enableSuimonoInfiniteOcean)
            {
                if (chunkManager != null)
                    chunkManager.SetSuppressWaterTilesForSuimono(false);
                return;
            }

            // Parent at scene root (not under WorldManager prefab instance) so Play-mode Hierarchy always shows
            // Aetheria_SuimonoOcean at the top level next to WorldManager — nested prefab instances often hide runtime children.
            var oceanRoot = new GameObject("Aetheria_SuimonoOcean");
            if (parentOverride != null)
            {
                oceanRoot.transform.SetParent(parentOverride, false);
                oceanRoot.transform.localPosition = Vector3.zero;
                oceanRoot.transform.localRotation = Quaternion.identity;
                oceanRoot.transform.localScale = Vector3.one;
            }
            else
            {
                oceanRoot.transform.SetParent(null);
                oceanRoot.transform.position = Vector3.zero;
                oceanRoot.transform.rotation = Quaternion.identity;
                oceanRoot.transform.localScale = Vector3.one;
            }

            var oceanParent = oceanRoot.transform;

            // ChunkManager "Water Material" is unrelated (chunk quads are off when Suimono is on). If inspector refs break, we fall back to Resources.
            var modGo = InstantiateModuleOrFallback(oceanParent);
            if (modGo == null)
            {
                Debug.LogError("[Suimono] Could not instantiate SUIMONO_Module. Assign suimonoModulePrefab to the project prefab (blue cube), or keep Assets/Resources/Suimono/SUIMONO_Module.prefab (runtime fallback).");
                FailSuimonoAndRestoreChunkWater();
                return;
            }
            modGo.transform.localPosition = Vector3.zero;
            modGo.transform.localRotation = Quaternion.identity;
            modGo.transform.localScale = Vector3.one;

            _surfaceGo = InstantiateSurfaceOrFallback(oceanParent);
            if (_surfaceGo == null)
            {
                Debug.LogError("[Suimono] Could not instantiate SUIMONO_Surface. Assign suimonoSurfacePrefab or keep Assets/Resources/Suimono/SUIMONO_Surface.prefab.");
                Destroy(modGo);
                FailSuimonoAndRestoreChunkWater();
                return;
            }

            // Only suppress chunk water quads after module + surface exist; otherwise the world has no ocean at all.
            if (chunkManager != null)
                chunkManager.SetSuppressWaterTilesForSuimono(true);
            _surfaceGo.SetActive(false);
            if (islandWorldForSeaLevel == null)
                islandWorldForSeaLevel = FindFirstObjectByType<IslandWorldBuilder>();
            _lockedSeaLevelY = ResolveWaterSurfaceY();
            _surfaceGo.transform.SetPositionAndRotation(new Vector3(0f, _lockedSeaLevelY, 0f), Quaternion.identity);
            _surfaceGo.transform.localScale = Vector3.one;

            _module = modGo.GetComponent<SuimonoModule>();
            _surface = _surfaceGo.GetComponent<SuimonoObject>();

            if (_surface != null)
            {
                _surface.moduleObject = _module;
                _surface.typeIndex = 0;
            }

            if (_module != null)
            {
                _module.suimonoObject = _surface;
                _module.cameraTypeIndex = 1;
                TryAssignCameraImmediate();
                ApplySkyboxRenderSettingsIfNeeded();
                ApplyHorizonCameraSettings(_module.manualCamera);
            }

            Debug.Log(
                "[Suimono] Ocean objects live under top-level GameObject 'Aetheria_SuimonoOcean' (scene root, not inside WorldManager). " +
                "Expand it in Hierarchy during Play to see SUIMONO_Module and SUIMONO_Surface. Surface activates after a short delay.",
                oceanRoot);

            StartCoroutine(DeferredWaterActivation());
        }

        private void LateUpdate()
        {
            if (!enableSuimonoInfiniteOcean || _module == null) return;
            if (_horizonReapplyFrames >= 8) return;
            if (_module.manualCamera == null) return;
            ApplyHorizonCameraSettings(_module.manualCamera);
            _horizonReapplyFrames++;
        }

        private void ApplySkyboxRenderSettingsIfNeeded()
        {
            if (skyboxMaterialEnsure != null)
                RenderSettings.skybox = skyboxMaterialEnsure;
        }

        private float ResolveWaterSurfaceY()
        {
            if (islandWorldForSeaLevel == null)
                islandWorldForSeaLevel = FindFirstObjectByType<IslandWorldBuilder>();

            float y = islandWorldForSeaLevel != null
                ? islandWorldForSeaLevel.SeaLevelWorldY
                : waterSurfaceY;

            if (islandWorldForSeaLevel != null)
            {
                float landTop = islandWorldForSeaLevel.LandTopWorldY;
                const float minClearanceBelowLand = 0.25f;
                float maxSea = landTop - minClearanceBelowLand;
                if (y > maxSea)
                {
                    Debug.LogWarning(
                        $"[Suimono] Resolved sea Y={y:F2} is at or above usable land top {landTop:F2} (max sea {maxSea:F2}). " +
                        "Clamping so the shore stays dry. Fix: assign IslandWorldBuilder on this component, tune groundY / islandClearanceAboveSea, or lower waterSurfaceY.",
                        islandWorldForSeaLevel);
                    y = maxSea;
                }
            }

            return y;
        }

        private void FailSuimonoAndRestoreChunkWater()
        {
            if (chunkManager == null)
                chunkManager = GetComponent<ChunkManager>() ?? FindFirstObjectByType<ChunkManager>();
            if (chunkManager != null)
            {
                chunkManager.SetSuppressWaterTilesForSuimono(false);
                chunkManager.ReloadChunksAfterWaterModeChange();
            }
        }

        private GameObject InstantiateModuleOrFallback(Transform parent)
        {
            var tpl = ResolvePrefabOrResources(suimonoModulePrefab, "Suimono/SUIMONO_Module");
            var go = InstantiatePrefabRoot(tpl, parent);
            if (go == null)
            {
                tpl = Resources.Load<GameObject>("Suimono/SUIMONO_Module");
                go = InstantiatePrefabRoot(tpl, parent);
            }
            return go;
        }

        private GameObject InstantiateSurfaceOrFallback(Transform parent)
        {
            var tpl = ResolvePrefabOrResources(suimonoSurfacePrefab, "Suimono/SUIMONO_Surface");
            var go = InstantiatePrefabRoot(tpl, parent);
            if (go == null)
            {
                tpl = Resources.Load<GameObject>("Suimono/SUIMONO_Surface");
                go = InstantiatePrefabRoot(tpl, parent);
            }
            return go;
        }

        /// <summary>
        /// Inspector refs can be "missing" (moved/deleted asset): they may not compare as C# null but throw on access.
        /// </summary>
        private static GameObject ResolvePrefabOrResources(GameObject inspectorPrefab, string resourcesPath)
        {
            if (!IsAliveUnityObject(inspectorPrefab))
                return Resources.Load<GameObject>(resourcesPath);
            return inspectorPrefab;
        }

        private static bool IsAliveUnityObject(GameObject o)
        {
            if (o == null) return false;
            try
            {
                return o.transform != null;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }

        /// <summary>
        /// Non-generic Instantiate(Object, Transform, bool) — avoids Instantiate&lt;T&gt; which can throw on some Unity versions.
        /// </summary>
        private static UnityEngine.Object InstantiateNonGeneric(UnityEngine.Object original, Transform parent, bool worldPositionStays)
        {
            return UnityEngine.Object.Instantiate(original, parent, worldPositionStays);
        }

        private static GameObject InstantiatePrefabRoot(GameObject prefab, Transform parent)
        {
            if (prefab == null) return null;

            Transform rootTransform;
            try
            {
                rootTransform = prefab.transform;
            }
            catch (MissingReferenceException)
            {
                return null;
            }

            if (rootTransform == null) return null;

            // Clone the root Transform (cast to Object so we bind non-generic Instantiate, not Instantiate<Transform>).
            UnityEngine.Object copy = InstantiateNonGeneric((UnityEngine.Object)rootTransform, parent, false);
            if (copy == null) return null;

            if (copy is Transform tr)
                return tr.gameObject;
            if (copy is GameObject go)
                return go;
            if (copy is Component comp)
                return comp.gameObject;

            Debug.LogError(
                $"[Suimono] Instantiate clone type {copy.GetType().FullName} is not a GameObject root. Prefab={prefab.name} ({prefab.GetType().Name}). Re-link prefab fields.");
            return null;
        }

        private void TryAssignCameraImmediate()
        {
            if (_module == null) return;
            if (manualCameraTransform != null)
                _module.manualCamera = manualCameraTransform;
            else if (Camera.main != null)
                _module.manualCamera = Camera.main.transform;
        }

        private static Transform ResolveMainCameraTransform()
        {
            var go = GameObject.FindGameObjectWithTag("MainCamera");
            if (go != null) return go.transform;
            if (Camera.main != null) return Camera.main.transform;
            var cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var c in cams)
            {
                if (c != null && c.enabled && c.gameObject.activeInHierarchy)
                    return c.transform;
            }
            return null;
        }

        /// <summary>
        /// Applies far clip + forward path to all likely scene/VR cameras under the rig root, and Skybox clear on depth-0 cameras.
        /// Suimono must see the same skybox the main pass draws; a clipped far plane or Solid Color clear reads as grey vs blue split.
        /// </summary>
        private void ApplyHorizonCameraSettings(Transform camRoot)
        {
            if (camRoot == null) return;

            var cameras = camRoot.root.GetComponentsInChildren<Camera>(true);
            if (cameras == null || cameras.Length == 0)
            {
                var one = camRoot.GetComponent<Camera>();
                if (one != null) cameras = new[] { one };
                else return;
            }

            bool skyAssigned = RenderSettings.skybox != null;

            foreach (var cam in cameras)
            {
                if (cam == null || !cam.enabled || !cam.gameObject.activeInHierarchy) continue;
                if (cam.targetTexture != null) continue;

                if (extendMainCameraFarClipForHorizon && cam.farClipPlane < mainCameraFarClipPlane)
                    cam.farClipPlane = mainCameraFarClipPlane;

                if (forceMainCameraForwardRendering)
                    cam.renderingPath = RenderingPath.Forward;

                if (ensureSkyboxClearFlagsOnMainCamera && skyAssigned &&
                    (cam.CompareTag("MainCamera") || cam.transform == camRoot))
                    cam.clearFlags = CameraClearFlags.Skybox;
            }
        }

        private void ApplyVrReflectionAndRefractionSettings()
        {
            if (_module == null) return;

            if (reduceReflectionWorkForVr)
            {
                _module.enableDynamicReflections = false;
                _module.enableReflections = false;
                _module.transRenderDistance = 60f;
                if (_surface != null)
                {
                    _surface.enableDynamicReflections = false;
                    _surface.enableReflections = false;
                    _surface.refractStrength = Mathf.Min(_surface.refractStrength, refractStrengthWhenStableVr);
                }
            }
            else
            {
                _module.enableDynamicReflections = false;
                _module.enableReflections = true;
                _module.transRenderDistance = 60f;
            }
        }

        private IEnumerator DeferredWaterActivation()
        {
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();

            if (_module != null)
            {
                if (manualCameraTransform != null)
                    _module.manualCamera = manualCameraTransform;
                else
                {
                    var camT = ResolveMainCameraTransform();
                    if (camT != null)
                        _module.manualCamera = camT;
                }
                ApplySkyboxRenderSettingsIfNeeded();
                ApplyHorizonCameraSettings(_module.manualCamera);
            }

            ApplyVrReflectionAndRefractionSettings();

            if (_surfaceGo != null)
                _surfaceGo.SetActive(true);

            TryAssignDirectionalLight();
            TrySpawnSuimonoRippleFx();

            if (_module != null && _surface != null)
                _module.RegisterSurface(_surface);

            // SuimonoObject.Start runs when the surface becomes active; one frame later re-apply Y in case vendor scripts touched the transform.
            ApplyLockedSeaLevelY();
            yield return null;
            ApplyLockedSeaLevelY();
            if (_module != null)
                ApplyHorizonCameraSettings(_module.manualCamera);

            ApplyGameplayWaterDefaults();

            if (_surfaceGo != null)
                SuimonoOceanRuntimeSetup.ConfigureSurfaceRoot(_surfaceGo);

            // ChunkManager may have spawned water quads before Awake order completed; strip them once Suimono owns the sea.
            if (chunkManager != null)
                chunkManager.ReloadChunksAfterWaterModeChange();

            if (_surfaceGo != null)
            {
                string landHint;
                if (islandWorldForSeaLevel != null)
                {
                    float top = islandWorldForSeaLevel.LandTopWorldY;
                    landHint =
                        $"Linked IslandWorldBuilder on '{islandWorldForSeaLevel.gameObject.name}': land top ≈ Y={top:F2} " +
                        $"(sea Y={islandWorldForSeaLevel.SeaLevelWorldY:F2}). Feet should be on sand/grass above sea. ";
                }
                else
                {
                    landHint =
                        "No IslandWorldBuilder assigned or found — sea height is manual waterSurfaceY only. " +
                        "Assign Island World For Sea Level on this component so the shore matches your island. ";
                }

                Debug.Log(
                    $"[Suimono] Ocean surface active at sea level Y={_lockedSeaLevelY:F3} (infinite plane on X/Z). {landHint}" +
                    "If land still looks flooded, raise IslandWorldBuilder islandClearanceAboveSea or lower waterSurfaceY. " +
                    "Water magic: Suimono_Water/Water + URP driver; ripples for VR (see Docs/LONG_RANGE_MAGIC.md).",
                    _surfaceGo);
            }
        }

        private void TryAssignDirectionalLight()
        {
            if (_module == null)
                return;

            if (sceneDirectionalLight != null)
            {
                _module.setLight = sceneDirectionalLight;
                return;
            }

            if (!autoFindDirectionalLight)
                return;

            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l == null || l.type != LightType.Directional)
                    continue;
                _module.setLight = l;
                return;
            }
        }

        private void TrySpawnSuimonoRippleFx()
        {
            if (_module == null || suimonoRippleFxPrefab == null)
                return;
            if (_module.fxRippleObject != null)
                return;

            var fx = Instantiate(suimonoRippleFxPrefab, _module.transform);
            fx.name = suimonoRippleFxPrefab.name + "(Clone)";
            _module.fxRippleObject = fx;
        }

        private void ApplyGameplayWaterDefaults()
        {
            if (!applyGameplayWaterDefaults || _surface == null)
                return;

            _surface.flowSpeed = defaultFlowSpeed;
            _surface.waveScale = defaultWaveScale;
            _surface.enableInteraction = enableSuimonoInteraction;

            if (_module != null)
                _module.enableInteraction = enableSuimonoInteraction;
        }

        private void ApplyLockedSeaLevelY()
        {
            if (_surfaceGo == null) return;
            var p = _surfaceGo.transform.position;
            _surfaceGo.transform.position = new Vector3(p.x, _lockedSeaLevelY, p.z);
        }
    }
}
