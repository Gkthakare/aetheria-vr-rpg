using Aetheria.VR;
using Aetheria.World;
using UnityEngine;
using UnityEngine.XR;
using System.Collections;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Aetheria.Combat
{
    /// <summary>
    /// VR wind mana bow using <c>Assets/ImportedContent/Weapons/DownloadedBow</c> (assign FBX or rely on Imported Bow Model Asset Path in editor).
    /// Pull the string (two-hand distance / trigger / RMB) to form the mana arrow; release to fire.
    /// </summary>
    public sealed class WindManaBow : MonoBehaviour
    {
        public static WindManaBow Instance { get; private set; }

        public const string PackagedBowResourcePath = "WindManaBow/WindManaBow_Visual";

        [Header("Optional visual prefab")]
        [SerializeField] private GameObject meshyBowPrefab;
        [Tooltip("When Meshy Bow Prefab is empty, the editor resolves this asset path (FBX/GLB under Assets). External Bow Source Folder is synced into the project when this file is missing.")]
        [SerializeField] private string importedBowModelAssetPath = "Assets/ImportedContent/Weapons/DownloadedBow/source/Bow.fbx.fbx";
        [Tooltip("Folder to copy from (e.g. Downloads/bow with source/ and textures/). Synced to Assets/ImportedContent/Weapons/DownloadedBow.")]
#pragma warning disable 0414 // Editor-only; used by TryImportExternalBowFolderIfNeeded.
        [SerializeField] private string externalBowSourceDirectory = @"C:\Users\intru\Downloads\bow";
#pragma warning restore 0414
        [Tooltip("If true, skips other ImportedContent/Resources bow assets; only Meshy Bow Prefab + Imported Bow Model Asset Path.")]
        [SerializeField] private bool useOnlyDownloadedBowFolder = true;
        [Tooltip("If true, rejects FBX materials that use InternalErrorShader (pink). Fix materials or turn off to force-assign.")]
        [SerializeField] private bool rejectShaderBrokenBowPrefabs = true;
        [SerializeField] private Vector3 bowLocalPosition = new Vector3(0.02f, -0.02f, 0.04f);
        [SerializeField] private Vector3 bowLocalEuler = new Vector3(0f, 90f, 0f);
        [SerializeField] private float bowScale = 0.48f;

        [Header("Mesh bow (arrow + string)")]
        [Tooltip("Bow-local position for charging mana arrow VFX and shot spawn. Tune when using an imported FBX.")]
        [SerializeField] private Vector3 arrowSpawnLocalPosition = new Vector3(0f, 0.07f, 0.35f);
        [SerializeField] private Vector3 arrowSpawnLocalEulerAngles = Vector3.zero;
        [Tooltip("Procedural string moves with pull so forming the mana arrow reads as drawing the bowstring.")]
        [SerializeField] private bool drawProceduralBowString = true;
        [Tooltip("Removes colliders from instantiated mesh bows for comfortable VR holding.")]
        [SerializeField] private bool stripCollidersFromImportedBow = true;

        [Header("Clean test mode")]
        [Tooltip("Spawns a ground bow prop at start. Off for VR deploy: bow is already in hand.")]
        [SerializeField] private bool spawnGroundBowAtStart = false;
        [SerializeField] private Vector3 groundBowOffsetFromPlayer = new Vector3(0.8f, 0f, 1.8f);
        [Tooltip("When true, only the ground test prop exists (no hand bow). Leave off for normal VR play.")]
        [SerializeField] private bool testModeGroundBowOnly = false;
        [Tooltip("Desktop/editor controls when VR controllers are absent: B equip/unequip, hold Right Mouse to pull/release fire.")]
        [SerializeField] private bool enableDesktopMouseKeyboardTest = true;
        [SerializeField] private float desktopChargeFillSeconds = 1.15f;
        [SerializeField] private bool enableBowInspectToggle = true;
        [SerializeField] private KeyCode inspectToggleKey = KeyCode.I;

        [Header("VR hand-tracking")]
        [Tooltip("On headset: skip controller trigger + grip for draw/swap when pinch tips exist — use pinches only (hands-free of controllers). Disable to use triggers/grips when tracked.")]
        [SerializeField] private bool preferHandTrackingOnly = true;
        [SerializeField] private bool enableHandTrackingPinchPull = true;
        [SerializeField] private float pinchHoldDistance = 0.035f;
        [SerializeField] private float pinchReleaseDistance = 0.055f;
        [SerializeField] private bool showControlHintHud = true;
        [SerializeField] private KeyCode pinchPresetRelaxedKey = KeyCode.F1;
        [SerializeField] private KeyCode pinchPresetPreciseKey = KeyCode.F2;
        [SerializeField] private bool autoConfigureForStandaloneHandTracking = true;
        [Tooltip("Force XR in-hand mode even when display runtime detection is unreliable in editor-link workflows.")]
        [SerializeField] private bool forceXrHandMode = false;
        [Tooltip("When XR hands/controllers are missing, create camera-relative anchors so bow is still visible/testable in headset.")]
        [SerializeField] private bool createCameraFallbackAnchorsInXr = true;
        [Tooltip("If true, when no XR headset is running (editor / desktop), equip the bow on camera anchors immediately instead of ground-only + B to equip.")]
        [SerializeField] private bool equipHandBowWhenNoHeadset = true;
        [Tooltip("Without a headset: use camera hand stubs so the bow stays in the Game view (recommended). Off = follow rig controllers (often sit below the camera in editor and disappear from the frame).")]
        [SerializeField] private bool preferCameraAnchorsWhenNoHeadset = true;
        [Tooltip("If Prefer Camera Anchors is off, still switch to camera stubs when controllers are much lower than the HMD (editor default poses).")]
        [SerializeField] private bool autoCameraStubsWhenControllersBelowCamera = true;
        [Tooltip("Meters: if main camera is this much above the left grip, use camera stubs (no headset only).")]
        [SerializeField] private float noHeadsetStubIfCameraAboveGripMeters = 0.85f;

        [Header("VR two-hand bow")]
        [Tooltip("Bow grip follows this hand; the other hand draws the string (trigger or pinch).")]
        [SerializeField] private bool bowHeldInLeftHand = true;
        [Tooltip("Rotate the bow so the arrow line points from the grip hand toward the drawing hand (natural aiming).")]
        [SerializeField] private bool aimBowTowardOtherHand = true;
        [Tooltip("On device: hold both grips this long while not drawing to pass the bow to the other hand.")]
        [SerializeField] private float swapHandsGripHoldSeconds = 0.45f;

        [Header("Pull + Mana")]
        [SerializeField] private float pullDeadZoneMeters = 0.18f;
        [SerializeField] private float pullForMaxPowerMeters = 0.45f;
        [SerializeField] private float minReleasePull = 0.15f;
        [Tooltip("Sandbox / comfort: shots cost no MP; you can play without PlayerCombatStats. Turn off for RPG progression.")]
        [SerializeField] private bool ignoreManaCost = true;
        [SerializeField] private float baseMpCost = 6f;
        [SerializeField] private float extraMpCostAtMaxPull = 12f;

        [Header("Arrow")]
        [SerializeField] private Color manaArrowCore = new Color(0.15f, 1f, 0.42f, 1f);
        [SerializeField] private Color manaArrowTip = new Color(0.55f, 1f, 0.75f, 1f);
        [SerializeField] private float minArrowSpeed = 14f;
        [SerializeField] private float maxArrowSpeed = 42f;
        [SerializeField] private float minArrowDamage = 14f;
        [SerializeField] private float maxArrowDamage = 58f;
        [SerializeField] private float voiceFireMpCost = 14f;
        [SerializeField] private float voiceArrowSpeed = 32f;
        [SerializeField] private float voiceArrowDamage = 35f;
        [Header("HUD")]
        [SerializeField] private bool showManaReadyHud = false;
        [SerializeField] private bool showManaChargeBar = false;

        private Transform _left;
        private Transform _right;
        private PlayerCombatStats _playerStats;
        private GameObject _bowInstance;
        private Transform _arrowSpawn;
        private Transform _stringVisual;
        private Transform _chargeArrowRoot;
        private Transform _chargeShaft;
        private Transform _chargeHead;
        private Transform _chargeKnockGlow;
        private readonly Transform[] _chargeFletch = new Transform[3];
        private Light _chargeLight;
        private Renderer _chargeShaftRend;
        private Renderer _chargeHeadRend;
        private float _pull01;
        private bool _wasHolding;
        private bool _isHoldingString;
        private GameObject _groundBow;
        private TextMesh _manaHud;
        private TextMesh _manaBarHud;
        private bool _desktopEquipped;
        private Transform _desktopLeftAnchor;
        private Transform _desktopRightAnchor;
        /// <summary>Stable parent under XR rig; follows <see cref="_left"/> each frame so XR does not strip the bow from the controller.</summary>
        private Transform _bowFollowMount;
        private Transform _rightThumbTip;
        private Transform _leftThumbTip;
        private float _bothGripsSwapTimer;
        private GameObject _inspectBow;
        private TextMesh _hintHud;
        private bool _xrRuntimeActive;
        public float CurrentPull01 => _pull01;
        public bool IsHoldingString => _isHoldingString;

        /// <summary>Resolved bow mesh (FBX/prefab or null). Used for third-person cosmetic copy on avatar.</summary>
        public GameObject ActiveBowVisualPrefab => meshyBowPrefab;

        /// <summary>Forces in-hand bow for device builds / finalized scenes (disables ground-only test defaults).</summary>
        public void EnsureHandBowForDeployment()
        {
            testModeGroundBowOnly = false;
            spawnGroundBowAtStart = false;
        }

        private bool _xrDisplaySubsystemRunning;

        private Transform BowGripTransform => bowHeldInLeftHand ? _left : _right;
        private Transform StringHandIndexTransform => bowHeldInLeftHand ? _right : _left;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
#if UNITY_EDITOR
            TryImportExternalBowFolderIfNeeded();
            TryBindBowPrefabFromImportPath();
#endif
            ResolveBowPrefabIfNeeded();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            TryImportExternalBowFolderIfNeeded();
            TryBindBowPrefabFromImportPath();
        }

        /// <summary>Copies <see cref="externalBowSourceDirectory"/> into Assets/ImportedContent/Weapons/DownloadedBow when the FBX is not in the project yet.</summary>
        private void TryImportExternalBowFolderIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(importedBowModelAssetPath) || string.IsNullOrWhiteSpace(externalBowSourceDirectory))
                return;
            if (!Directory.Exists(externalBowSourceDirectory))
                return;

            string modelUnderData = importedBowModelAssetPath.StartsWith("Assets/", System.StringComparison.Ordinal)
                ? importedBowModelAssetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar)
                : importedBowModelAssetPath.Replace('/', Path.DirectorySeparatorChar);
            string targetModelAbs = Path.Combine(Application.dataPath, modelUnderData);
            if (File.Exists(targetModelAbs))
                return;

            string targetRoot = Path.Combine(Application.dataPath, "ImportedContent", "Weapons", "DownloadedBow");
            Directory.CreateDirectory(targetRoot);

            string extRoot = Path.GetFullPath(externalBowSourceDirectory.TrimEnd(Path.DirectorySeparatorChar, '/'));
            foreach (string filePath in Directory.GetFiles(externalBowSourceDirectory, "*", SearchOption.AllDirectories))
            {
                if (filePath.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                string full = Path.GetFullPath(filePath);
                if (full.Length < extRoot.Length)
                    continue;
                string rel = full.Substring(extRoot.Length).TrimStart(Path.DirectorySeparatorChar);
                string dest = Path.Combine(targetRoot, rel);
                string destDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);
                File.Copy(filePath, dest, true);
            }

            AssetDatabase.Refresh();
            Debug.Log("[WindManaBow] Synced external bow folder into Assets/ImportedContent/Weapons/DownloadedBow.");
        }

        private void TryBindBowPrefabFromImportPath()
        {
            if (meshyBowPrefab != null || string.IsNullOrWhiteSpace(importedBowModelAssetPath))
                return;
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(importedBowModelAssetPath);
            if (p == null)
                return;
            if (rejectShaderBrokenBowPrefabs && !IsPrefabRenderPipelineCompatible(p))
            {
                Debug.LogWarning($"[WindManaBow] Bow at {importedBowModelAssetPath} failed material/shader check; fix materials or disable Reject Shader Broken Bow Prefabs.");
                return;
            }

            meshyBowPrefab = p;
        }
#endif

        private void Start()
        {
            DestroyStrayFollowMountsUnderRuntimeAvatar();
            _playerStats = GetComponent<PlayerCombatStats>() ?? GetComponentInParent<PlayerCombatStats>() ?? FindObjectOfType<PlayerCombatStats>();
            ResolveBowPrefabIfNeeded();
            _xrDisplaySubsystemRunning = IsXrRuntimeActive();
            _xrRuntimeActive = forceXrHandMode || _xrDisplaySubsystemRunning;

            Transform rigForHands = ResolveXrRigRootTransform();
            var leftHand = XRHandAttachmentUtil.FindHandController(rigForHands, false);
            var rightHand = XRHandAttachmentUtil.FindHandController(rigForHands, true);
            _left = XRHandAttachmentUtil.FindIndexTipOrController(leftHand);
            _right = XRHandAttachmentUtil.FindIndexTipOrController(rightHand);
            _rightThumbTip = XRHandAttachmentUtil.FindThumbTipOrController(rightHand);
            _leftThumbTip = XRHandAttachmentUtil.FindThumbTipOrController(leftHand);
            AutoConfigureRuntimeMode();

            // Without a headset: rig controllers are often at waist height while the Game camera is at the head — bow ends up below the frame. Prefer camera stubs unless user opts out and poses look reasonable.
            if (!_xrDisplaySubsystemRunning && equipHandBowWhenNoHeadset && !testModeGroundBowOnly)
            {
                bool haveRigHands = _left != null && _right != null;
                bool useCameraStubs = preferCameraAnchorsWhenNoHeadset || !haveRigHands;
                if (!useCameraStubs && haveRigHands && autoCameraStubsWhenControllersBelowCamera && Camera.main != null && _left != null)
                {
                    float dy = Camera.main.transform.position.y - _left.position.y;
                    if (dy >= noHeadsetStubIfCameraAboveGripMeters)
                    {
                        useCameraStubs = true;
                        Debug.Log($"[WindManaBow] No headset: camera is {dy:0.##}m above left grip — switching to camera hand stubs so the bow stays visible in Game view (disable Auto Camera Stubs When Controllers Below Camera to force rig hands).");
                    }
                }

                if (useCameraStubs)
                {
                    CreateDesktopAnchors();
                    Debug.Log(!haveRigHands
                        ? "[WindManaBow] No XR display: camera anchors (no Left/Right Controller on rig)."
                        : "[WindManaBow] No XR display: camera hand anchors — bow in lower-left of view; on device with headset, real hands are used.");
                }
                else
                    Debug.Log("[WindManaBow] No XR display: bow follows Left/Right Controller transforms.");
                _desktopEquipped = true;
            }

            if (_xrRuntimeActive && createCameraFallbackAnchorsInXr && (_left == null || _right == null))
            {
                CreateDesktopAnchors();
                _desktopEquipped = true;
                Debug.LogWarning("[WindManaBow] XR runtime active but anchors missing. Using camera fallback anchors so bow remains visible.");
            }

            if ((_left == null || _right == null) && (enableDesktopMouseKeyboardTest || (_xrRuntimeActive && createCameraFallbackAnchorsInXr)))
                CreateDesktopAnchors();

            if (_left == null && !testModeGroundBowOnly && equipHandBowWhenNoHeadset)
            {
                CreateDesktopAnchors();
                _desktopEquipped = true;
            }

            StartCoroutine(CoEnsureCosmeticBowOnBodyAvatar());

            if (_left == null)
            {
                Debug.LogWarning("[WindManaBow] Left grip still missing — using camera/desktop bow anchors so the bow stays usable (never disabling this component).");
                if (!testModeGroundBowOnly)
                {
                    CreateDesktopAnchors();
                    _desktopEquipped = true;
                }
            }

            if (spawnGroundBowAtStart)
                SpawnGroundBowProp();

            if (!testModeGroundBowOnly && BowGripTransform != null)
                BuildBowVisual();

            if (showManaReadyHud)
                BuildManaHud();
            if (showControlHintHud)
                BuildHintHud();

            // Auto-equip in-hand when not in ground-only test mode (XR + editor fallback paths).
            if (BowGripTransform != null && !testModeGroundBowOnly)
            {
                _desktopEquipped = true;
                if (_groundBow != null)
                    _groundBow.SetActive(false);
                if (_bowInstance == null)
                    BuildBowVisual();
            }
        }

        private IEnumerator CoEnsureCosmeticBowOnBodyAvatar()
        {
            yield return null;
            yield return null;
            Transform root = transform.root;
            Transform avatarTf = root.Find("RuntimePlayerAvatar");
            if (avatarTf == null)
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "RuntimePlayerAvatar") continue;
                    avatarTf = t;
                    break;
                }
            }

            if (avatarTf == null)
                yield break;
            if (IslandWorldBuilder.AvatarHasCosmeticBow(avatarTf.gameObject))
                yield break;

            IslandWorldBuilder.AttachCosmeticBowToAvatarBody(avatarTf.gameObject);
        }

        private void Update()
        {
            if (testModeGroundBowOnly && !_desktopEquipped)
            {
                UpdateManaHud();
                if (enableDesktopMouseKeyboardTest)
                    UpdateDesktopControls();
                TryRefreshHandTrackingAnchors();
                if (_xrRuntimeActive && BowGripTransform != null && _bowInstance == null && !testModeGroundBowOnly)
                    BuildBowVisual();
                return;
            }

            // Controllers / hand bones often appear after the first frame; refresh before bailing out.
            TryRefreshHandTrackingAnchors();
            if (BowGripTransform == null)
            {
                UpdateManaHud();
                if (enableDesktopMouseKeyboardTest)
                    UpdateDesktopControls();
                return;
            }
            if (_bowInstance == null && BowGripTransform != null)
                BuildBowVisual();
            bool holding = TryGetStringPullHeld();
            _isHoldingString = holding;

            TryProcessSwapHandsInput();

            if (_desktopEquipped && enableDesktopMouseKeyboardTest)
            {
                if (holding)
                    _pull01 = Mathf.MoveTowards(_pull01, 1f, Time.deltaTime / Mathf.Max(0.15f, desktopChargeFillSeconds));
                else
                    _pull01 = 0f;
            }
            else if (BowGripTransform != null && StringHandIndexTransform != null && holding)
            {
                float d = Vector3.Distance(StringHandIndexTransform.position, BowGripTransform.position);
                _pull01 = Mathf.Clamp01((d - pullDeadZoneMeters) / Mathf.Max(0.05f, pullForMaxPowerMeters));
            }
            else
                _pull01 = 0f;

            UpdateVisuals();

            if (_wasHolding && !holding)
                FireIfCharged();
            _wasHolding = holding;
            UpdateManaHud();
            if (enableDesktopMouseKeyboardTest)
                UpdateDesktopControls();
            if (enableBowInspectToggle && Input.GetKeyDown(inspectToggleKey))
                ToggleInspectBow();
            HandlePinchPresetHotkeys();
            UpdateHintHud();
        }

        private void LateUpdate()
        {
            if (_bowFollowMount != null && _bowInstance != null)
                EnsureFollowMountParentedToXrRig();
            SyncBowFollowMountToHand();
        }

        /// <summary>XR rig root — never use transform.root (avatar subtree can confuse parenting).</summary>
        private Transform ResolveXrRigRootTransform()
        {
            Transform t = transform;
            while (t != null)
            {
                if (t.name.IndexOf("XR Origin", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
                t = t.parent;
            }

            var go = GameObject.Find("XR Origin (XR Rig)");
            if (go != null)
                return go.transform;
            return transform.root;
        }

        private void EnsureFollowMountParentedToXrRig()
        {
            Transform rig = ResolveXrRigRootTransform();
            if (rig == null || _bowFollowMount == null) return;
            if (_bowFollowMount.parent != rig)
                _bowFollowMount.SetParent(rig, true);
            _bowFollowMount.gameObject.SetActive(true);
        }

        /// <summary>Removes inactive/junk mounts saved under the body avatar (not the gameplay rig).</summary>
        private static void DestroyStrayFollowMountsUnderRuntimeAvatar()
        {
            var avatar = GameObject.Find("RuntimePlayerAvatar");
            if (avatar == null) return;
            var stray = avatar.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < stray.Length; i++)
            {
                if (stray[i] == null) continue;
                if (stray[i].name != "WindManaBow_FollowMount") continue;
                UnityEngine.Object.Destroy(stray[i].gameObject);
            }
        }

        private void EnsureBowFollowMount()
        {
            Transform rig = ResolveXrRigRootTransform();
            DestroyStrayFollowMountsUnderRuntimeAvatar();

            if (_bowFollowMount != null)
            {
                EnsureFollowMountParentedToXrRig();
                return;
            }

            var go = new GameObject("WindManaBow_FollowMount");
            go.transform.SetParent(rig, false);
            go.SetActive(true);
            _bowFollowMount = go.transform;
        }

        private void SyncBowFollowMountToHand()
        {
            if (_bowFollowMount == null || _bowInstance == null)
                return;
            var grip = BowGripTransform;
            if (grip == null)
                return;

            _bowFollowMount.position = grip.position;

            if (aimBowTowardOtherHand)
            {
                var other = StringHandIndexTransform;
                if (other != null)
                {
                    Vector3 aim = other.position - grip.position;
                    if (aim.sqrMagnitude > 1e-6f)
                    {
                        Vector3 up = Camera.main != null ? Camera.main.transform.up : Vector3.up;
                        _bowFollowMount.rotation = Quaternion.LookRotation(aim.normalized, up);
                        return;
                    }
                }
            }

            // Controllers often report the same pose in editor / before tracking — aim with HMD.
            if (Camera.main != null)
            {
                Transform cam = Camera.main.transform;
                _bowFollowMount.rotation = Quaternion.LookRotation(cam.forward, cam.up);
                return;
            }

            _bowFollowMount.rotation = grip.rotation;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (_bowFollowMount != null)
            {
                Destroy(_bowFollowMount.gameObject);
                _bowFollowMount = null;
            }
        }

        public static bool TryConsumeVoiceCommand(string raw)
        {
            if (Instance == null || string.IsNullOrWhiteSpace(raw))
                return false;

            var c = raw.Trim().ToLowerInvariant();
            if (!ContainsAny(c, "fire", "shoot", "release arrow"))
                return false;

            return Instance.TryFireByVoice();
        }

        private bool TryFireByVoice()
        {
            if (!ignoreManaCost)
            {
                if (_playerStats == null)
                {
                    Debug.Log("[WindManaBow] No PlayerCombatStats; enable Ignore Mana Cost or add stats to the rig.");
                    return true;
                }

                if (!_playerStats.TrySpendMp(voiceFireMpCost))
                {
                    Debug.Log($"[WindManaBow] Not enough mana/MP to fire. Need {voiceFireMpCost:0.0}.");
                    return true;
                }
            }

            var cam = Camera.main != null ? Camera.main.transform : transform;
            var spawnPos = cam.position + cam.forward * 0.7f + Vector3.up * -0.05f;
            var dir = FindAimDirection(cam);
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);

            var arrow = BuildArrow(spawnPos, rot);
            var proj = arrow.AddComponent<WindManaArrow>();
            proj.Initialize(voiceArrowSpeed, voiceArrowDamage);
            Debug.Log("[WindManaBow] Voice fire: green wind arrow released.");
            return true;
        }

        private Vector3 FindAimDirection(Transform origin)
        {
            var enemies = FindObjectsOfType<EnemyController>();
            EnemyController nearest = null;
            float nearestSqr = float.MaxValue;
            var p = origin.position;
            foreach (var e in enemies)
            {
                if (e == null) continue;
                float d = (e.transform.position - p).sqrMagnitude;
                if (d < nearestSqr)
                {
                    nearestSqr = d;
                    nearest = e;
                }
            }

            if (nearest != null)
                return (nearest.transform.position + Vector3.up * 0.8f - origin.position).normalized;

            return origin.forward;
        }

        private void SpawnGroundBowProp()
        {
            if (_groundBow != null)
                return;
            if (meshyBowPrefab == null)
                return;

            var cam = Camera.main != null ? Camera.main.transform : transform;
            var basePos = cam.position + cam.forward * groundBowOffsetFromPlayer.z + cam.right * groundBowOffsetFromPlayer.x;
            float y = cam.position.y - 0.9f;
            var pos = new Vector3(basePos.x, y, basePos.z);

            _groundBow = Instantiate(meshyBowPrefab, pos, Quaternion.identity);
            _groundBow.name = "GroundTest_WindManaBow";
            _groundBow.transform.position = pos;
            _groundBow.transform.rotation = Quaternion.Euler(0f, cam.eulerAngles.y + 90f, 0f);
            _groundBow.transform.localScale = Vector3.one * Mathf.Max(0.2f, bowScale);
            Debug.Log($"[WindManaBow] Ground bow spawned at {pos}. Press B to equip in desktop mode.");
        }

        private void ResolveBowPrefabIfNeeded()
        {
#if UNITY_EDITOR
            TryImportExternalBowFolderIfNeeded();
            TryBindBowPrefabFromImportPath();
#endif
            if (meshyBowPrefab != null)
                return;

            if (!useOnlyDownloadedBowFolder)
            {
#if UNITY_EDITOR
                var editorPackaged = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/WindManaBow/WindManaBow_Visual.prefab");
                if (editorPackaged != null && (!rejectShaderBrokenBowPrefabs || IsPrefabRenderPipelineCompatible(editorPackaged)))
                {
                    meshyBowPrefab = editorPackaged;
                    Debug.LogWarning("[WindManaBow] Legacy: using Resources WindManaBow_Visual (set Use Only Downloaded Bow Folder = true to disable).");
                    return;
                }
#endif
                var packaged = Resources.Load<GameObject>(PackagedBowResourcePath);
                if (packaged != null && (!rejectShaderBrokenBowPrefabs || IsPrefabRenderPipelineCompatible(packaged)))
                {
                    meshyBowPrefab = packaged;
                    Debug.LogWarning("[WindManaBow] Legacy: using Resources packaged bow.");
                    return;
                }
            }

            Debug.LogError(
                "[WindManaBow] No bow assigned. Use only DownloadedBow: import " + importedBowModelAssetPath +
                " (or drag the FBX onto Meshy Bow Prefab on this component). Builds require Meshy Bow Prefab set in the scene.");
        }

        /// <summary>Third-person / mirror bow on avatar hand — uses the same mesh as gameplay when available.</summary>
        public static GameObject InstantiateBowVisualUnder(Transform parent, bool worldPositionStays = false)
        {
            var template = Instance != null ? Instance.meshyBowPrefab : null;
            if (template == null)
            {
                Debug.LogWarning("[WindManaBow] Cosmetic bow skipped: Meshy Bow Prefab / DownloadedBow not assigned on WindManaBow.");
                return null;
            }

            var inst = Object.Instantiate(template, parent, worldPositionStays);
            foreach (var c in inst.GetComponentsInChildren<Collider>(true))
                Object.Destroy(c);
            EnsureImportedBowMeshesActive(inst);
            return inst;
        }

        /// <summary>
        /// Many FBX/GLB imports leave mesh chunks (e.g. arco02) inactive; they stay invisible in editor, Game view, and device builds.
        /// </summary>
        private static void EnsureImportedBowMeshesActive(GameObject bowRoot)
        {
            if (bowRoot == null) return;
            bowRoot.SetActive(true);
            foreach (var t in bowRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && !t.gameObject.activeSelf)
                    t.gameObject.SetActive(true);
            }

            foreach (var r in bowRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null)
                    r.enabled = true;
            }
        }

        /// <summary>Procedural recurve-style bow for packaged prefab / fallback (wood, accents, string line).</summary>
        public static GameObject CreatePackagedBowVisualRoot()
        {
            var root = new GameObject("WindManaBow_Visual");

            var riser = GameObject.CreatePrimitive(PrimitiveType.Cube);
            riser.name = "RiserGrip";
            riser.transform.SetParent(root.transform, false);
            riser.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            riser.transform.localScale = new Vector3(0.038f, 0.24f, 0.055f);
            if (riser.TryGetComponent<Collider>(out var cr)) DestroyObjectSafe(cr);
            ApplyLitWood(riser.GetComponent<Renderer>(), new Color(0.34f, 0.24f, 0.14f), 0.42f);

            var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelf.name = "ArrowShelf";
            shelf.transform.SetParent(root.transform, false);
            shelf.transform.localPosition = new Vector3(0.022f, 0.06f, 0.08f);
            shelf.transform.localScale = new Vector3(0.015f, 0.028f, 0.04f);
            shelf.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            if (shelf.TryGetComponent<Collider>(out var sh)) DestroyObjectSafe(sh);
            ApplyLitMetal(shelf.GetComponent<Renderer>(), new Color(0.72f, 0.74f, 0.78f), 0.65f);

            var limbUpper = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            limbUpper.name = "LimbUpper";
            limbUpper.transform.SetParent(root.transform, false);
            limbUpper.transform.localPosition = new Vector3(0f, 0.12f, -0.02f);
            limbUpper.transform.localScale = new Vector3(0.038f, 0.2f, 0.038f);
            limbUpper.transform.localRotation = Quaternion.Euler(0f, 0f, 38f);
            if (limbUpper.TryGetComponent<Collider>(out var lu)) DestroyObjectSafe(lu);
            ApplyLitWood(limbUpper.GetComponent<Renderer>(), new Color(0.48f, 0.34f, 0.2f), 0.38f);

            var limbLower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            limbLower.name = "LimbLower";
            limbLower.transform.SetParent(root.transform, false);
            limbLower.transform.localPosition = new Vector3(0f, -0.12f, -0.02f);
            limbLower.transform.localScale = new Vector3(0.038f, 0.2f, 0.038f);
            limbLower.transform.localRotation = Quaternion.Euler(0f, 0f, -38f);
            if (limbLower.TryGetComponent<Collider>(out var ll)) DestroyObjectSafe(ll);
            ApplyLitWood(limbLower.GetComponent<Renderer>(), new Color(0.48f, 0.34f, 0.2f), 0.38f);

            var tipUpper = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tipUpper.name = "TipUpper";
            tipUpper.transform.SetParent(limbUpper.transform, false);
            tipUpper.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            tipUpper.transform.localScale = Vector3.one * 0.35f;
            if (tipUpper.TryGetComponent<Collider>(out var tu)) DestroyObjectSafe(tu);
            ApplyLitWood(tipUpper.GetComponent<Renderer>(), new Color(0.62f, 0.5f, 0.32f), 0.55f);

            var tipLower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tipLower.name = "TipLower";
            tipLower.transform.SetParent(limbLower.transform, false);
            tipLower.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            tipLower.transform.localScale = Vector3.one * 0.35f;
            if (tipLower.TryGetComponent<Collider>(out var tl)) DestroyObjectSafe(tl);
            ApplyLitWood(tipLower.GetComponent<Renderer>(), new Color(0.62f, 0.5f, 0.32f), 0.55f);

            var wrapUpper = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wrapUpper.name = "LimbWrapUpper";
            wrapUpper.transform.SetParent(limbUpper.transform, false);
            wrapUpper.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            wrapUpper.transform.localScale = new Vector3(1.15f, 0.08f, 1.15f);
            wrapUpper.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            if (wrapUpper.TryGetComponent<Collider>(out var wu)) DestroyObjectSafe(wu);
            ApplyUnlitColor(wrapUpper.GetComponent<Renderer>(), new Color(0.18f, 0.16f, 0.14f, 1f));

            var wrapLower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wrapLower.name = "LimbWrapLower";
            wrapLower.transform.SetParent(limbLower.transform, false);
            wrapLower.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            wrapLower.transform.localScale = new Vector3(1.15f, 0.08f, 1.15f);
            wrapLower.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            if (wrapLower.TryGetComponent<Collider>(out var wl)) DestroyObjectSafe(wl);
            ApplyUnlitColor(wrapLower.GetComponent<Renderer>(), new Color(0.18f, 0.16f, 0.14f, 1f));

            var stringLine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stringLine.name = "BowStringRest";
            stringLine.transform.SetParent(root.transform, false);
            stringLine.transform.localScale = new Vector3(0.0035f, 0.19f, 0.0035f);
            stringLine.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            stringLine.transform.localPosition = new Vector3(0f, 0f, -0.12f);
            if (stringLine.TryGetComponent<Collider>(out var sl)) DestroyObjectSafe(sl);
            ApplyUnlitColor(stringLine.GetComponent<Renderer>(), new Color(0.95f, 0.93f, 0.85f, 1f));

            return root;
        }

        private static void DestroyObjectSafe(Object o)
        {
            if (o == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(o);
                return;
            }
#endif
            Object.Destroy(o);
        }

        private static void ApplyLitWood(Renderer rend, Color albedo, float smoothness)
        {
            if (rend == null) return;
            var s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null)
            {
                ApplyUnlitColor(rend, albedo);
                return;
            }

            var m = new Material(s);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", albedo);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.06f);
            rend.sharedMaterial = m;
        }

        private static void ApplyLitMetal(Renderer rend, Color albedo, float smoothness)
        {
            if (rend == null) return;
            var s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null)
            {
                ApplyUnlitColor(rend, albedo);
                return;
            }

            var m = new Material(s);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", albedo);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.72f);
            rend.sharedMaterial = m;
        }

        private void CreateDesktopAnchors()
        {
            var cam = Camera.main != null ? Camera.main.transform : transform;
            if (_desktopLeftAnchor == null)
            {
                _desktopLeftAnchor = new GameObject("DesktopLeftBowAnchor").transform;
                _desktopLeftAnchor.SetParent(cam, false);
                _desktopLeftAnchor.localPosition = new Vector3(-0.42f, -0.38f, 0.55f);
                _desktopLeftAnchor.localRotation = Quaternion.identity;
            }

            if (_desktopRightAnchor == null)
            {
                _desktopRightAnchor = new GameObject("DesktopRightPullAnchor").transform;
                _desktopRightAnchor.SetParent(cam, false);
                _desktopRightAnchor.localPosition = new Vector3(0.38f, -0.36f, 0.48f);
                _desktopRightAnchor.localRotation = Quaternion.identity;
            }

            _left = _desktopLeftAnchor;
            _right = _desktopRightAnchor;
        }

        private void UpdateDesktopControls()
        {
            if (!enableDesktopMouseKeyboardTest)
                return;

            if (Input.GetKeyDown(KeyCode.B))
            {
                if (_desktopEquipped)
                    UnequipDesktopBow();
                else
                    EquipDesktopBow();
            }
        }

        private void EquipDesktopBow()
        {
            if (_left == null)
                CreateDesktopAnchors();
            if (_left == null)
                return;

            _desktopEquipped = true;
            if (_groundBow != null)
                _groundBow.SetActive(false);
            if (_bowInstance == null)
                BuildBowVisual();
            Debug.Log("[WindManaBow] Desktop bow equipped. Hold Right Mouse to pull, release to fire.");
        }

        private void UnequipDesktopBow()
        {
            _desktopEquipped = false;
            if (_bowInstance != null)
            {
                Destroy(_bowInstance);
                _bowInstance = null;
            }
            if (_bowFollowMount != null)
            {
                Destroy(_bowFollowMount.gameObject);
                _bowFollowMount = null;
            }
            if (_groundBow != null)
                _groundBow.SetActive(true);
            Debug.Log("[WindManaBow] Desktop bow unequipped.");
        }

        private void BuildManaHud()
        {
            if (_manaHud != null)
                return;

            var cam = Camera.main != null ? Camera.main.transform : transform;
            var go = new GameObject("ManaReadyHud");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0.52f, -0.04f, 1.1f);
            go.transform.localRotation = Quaternion.identity;
            _manaHud = go.AddComponent<TextMesh>();
            _manaHud.fontSize = 48;
            _manaHud.characterSize = 0.01f;
            _manaHud.anchor = TextAnchor.MiddleLeft;
            _manaHud.alignment = TextAlignment.Left;

            if (showManaChargeBar)
            {
                var bar = new GameObject("ManaChargeBarHud");
                bar.transform.SetParent(cam, false);
                bar.transform.localPosition = new Vector3(0.52f, -0.09f, 1.1f);
                bar.transform.localRotation = Quaternion.identity;
                _manaBarHud = bar.AddComponent<TextMesh>();
                _manaBarHud.fontSize = 48;
                _manaBarHud.characterSize = 0.01f;
                _manaBarHud.anchor = TextAnchor.MiddleLeft;
                _manaBarHud.alignment = TextAlignment.Left;
            }
        }

        private void UpdateManaHud()
        {
            if (_manaHud == null || _playerStats == null)
                return;

            float ratio = Mathf.Clamp01(_playerStats.CurrentMp / Mathf.Max(0.01f, voiceFireMpCost));
            bool ready = ratio >= 0.999f;
            _manaHud.text = ready ? "Ready to Fire" : "Need Mana";
            _manaHud.color = ready ? new Color(0.35f, 1f, 0.45f, 1f) : new Color(1f, 0.45f, 0.35f, 1f);

            if (_manaBarHud != null)
            {
                float barRatio = _isHoldingString ? _pull01 : ratio;
                barRatio = Mathf.Clamp01(barRatio);
                const int slots = 12;
                int filled = Mathf.RoundToInt(barRatio * slots);
                if (filled < 0) filled = 0;
                if (filled > slots) filled = slots;
                string bar = "[" + new string('|', filled) + new string('.', slots - filled) + "]";
                _manaBarHud.text = $"{(_isHoldingString ? "Charging" : "Mana Arrow")}: {bar} {Mathf.RoundToInt(barRatio * 100f)}%";
                _manaBarHud.color = ready
                    ? new Color(0.35f, 1f, 0.45f, 1f)
                    : (_isHoldingString ? new Color(0.78f, 1f, 0.55f, 1f) : new Color(0.65f, 0.95f, 1f, 1f));
            }
        }

        /// <summary>Drawing hand: trigger or pinch on the hand that is <b>not</b> holding the bow.</summary>
        private bool TryGetStringPullHeld()
        {
            if (enableHandTrackingPinchPull)
            {
                Transform stringIndex = StringHandIndexTransform;
                Transform stringThumb = bowHeldInLeftHand ? _rightThumbTip : _leftThumbTip;
                if (stringIndex != null && stringThumb != null && stringIndex != stringThumb)
                {
                    float d = Vector3.Distance(stringIndex.position, stringThumb.position);
                    if (_wasHolding)
                        return d <= pinchReleaseDistance;
                    return d <= pinchHoldDistance;
                }

                if (preferHandTrackingOnly && _xrDisplaySubsystemRunning)
                    return _desktopEquipped && enableDesktopMouseKeyboardTest && Input.GetMouseButton(1);
            }

            var list = new List<InputDevice>();
            var node = bowHeldInLeftHand ? XRNode.RightHand : XRNode.LeftHand;
            InputDevices.GetDevicesAtXRNode(node, list);
            if (list.Count > 0 && list[0].TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
                return triggerValue > 0.2f;

            return _desktopEquipped && enableDesktopMouseKeyboardTest && Input.GetMouseButton(1);
        }

        private void TryProcessSwapHandsInput()
        {
            if (!_xrDisplaySubsystemRunning || swapHandsGripHoldSeconds <= 0f)
            {
                _bothGripsSwapTimer = 0f;
                return;
            }

            if (_isHoldingString || _pull01 > 0.08f)
            {
                _bothGripsSwapTimer = 0f;
                return;
            }

            if (!TryBothGripsOrDualPinchHeld())
            {
                _bothGripsSwapTimer = 0f;
                return;
            }

            _bothGripsSwapTimer += Time.deltaTime;
            if (_bothGripsSwapTimer < swapHandsGripHoldSeconds)
                return;

            _bothGripsSwapTimer = 0f;
            bowHeldInLeftHand = !bowHeldInLeftHand;
            Debug.Log($"[WindManaBow] Bow moved to {(bowHeldInLeftHand ? "left" : "right")} hand — hold both grips (or both pinches) ~{swapHandsGripHoldSeconds:0.2}s again to swap back.");
        }

        /// <summary>Controller grips, or both hands pinching (hand-tracking-only sessions).</summary>
        private bool TryBothGripsOrDualPinchHeld()
        {
            if (enableHandTrackingPinchPull && preferHandTrackingOnly
                && _left != null && _right != null && _leftThumbTip != null && _rightThumbTip != null
                && _left != _leftThumbTip && _right != _rightThumbTip)
            {
                bool leftPinch = Vector3.Distance(_left.position, _leftThumbTip.position) <= pinchHoldDistance;
                bool rightPinch = Vector3.Distance(_right.position, _rightThumbTip.position) <= pinchHoldDistance;
                return leftPinch && rightPinch;
            }

            var leftDevices = new List<InputDevice>();
            var rightDevices = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftDevices);
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightDevices);
            bool leftGrip = leftDevices.Count > 0 && TryGripPressed(leftDevices[0]);
            bool rightGrip = rightDevices.Count > 0 && TryGripPressed(rightDevices[0]);
            if (leftGrip && rightGrip)
                return true;

            if (!enableHandTrackingPinchPull)
                return false;

            bool leftPinchLo = _left != null && _leftThumbTip != null && _left != _leftThumbTip
                && Vector3.Distance(_left.position, _leftThumbTip.position) <= pinchHoldDistance;
            bool rightPinchLo = _right != null && _rightThumbTip != null && _right != _rightThumbTip
                && Vector3.Distance(_right.position, _rightThumbTip.position) <= pinchHoldDistance;
            return leftPinchLo && rightPinchLo;
        }

        private static bool TryGripPressed(InputDevice device)
        {
            if (!device.isValid)
                return false;
            if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool pressed) && pressed)
                return true;
            return device.TryGetFeatureValue(CommonUsages.grip, out float g) && g >= 0.55f;
        }

        private void ToggleInspectBow()
        {
            if (_inspectBow != null)
            {
                Destroy(_inspectBow);
                _inspectBow = null;
                return;
            }

            if (meshyBowPrefab == null)
            {
                Debug.LogWarning("[WindManaBow] Inspect: assign DownloadedBow FBX to Meshy Bow Prefab first.");
                return;
            }

            var cam = Camera.main != null ? Camera.main.transform : transform;
            _inspectBow = Instantiate(meshyBowPrefab);
            _inspectBow.name = "BowInspectPreview";
            _inspectBow.transform.position = cam.position + cam.forward * 1.1f + Vector3.down * 0.05f;
            _inspectBow.transform.rotation = Quaternion.LookRotation(-cam.forward, Vector3.up);
            _inspectBow.transform.localScale = Vector3.one * Mathf.Max(0.25f, bowScale * 1.4f);
        }

        private void HandlePinchPresetHotkeys()
        {
            if (Input.GetKeyDown(pinchPresetRelaxedKey))
                ApplyPinchPresetRelaxed();
            if (Input.GetKeyDown(pinchPresetPreciseKey))
                ApplyPinchPresetPrecise();
        }

        private void ApplyPinchPresetRelaxed()
        {
            pinchHoldDistance = 0.045f;
            pinchReleaseDistance = 0.07f;
            Debug.Log("[WindManaBow] Pinch preset: RELAXED (easier hold/release).");
        }

        private void ApplyPinchPresetPrecise()
        {
            pinchHoldDistance = 0.03f;
            pinchReleaseDistance = 0.048f;
            Debug.Log("[WindManaBow] Pinch preset: PRECISE (tighter control).");
        }

        private void BuildHintHud()
        {
            if (_hintHud != null)
                return;
            var cam = Camera.main != null ? Camera.main.transform : transform;
            var go = new GameObject("BowControlHintHud");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0.52f, -0.17f, 1.1f);
            go.transform.localRotation = Quaternion.identity;
            _hintHud = go.AddComponent<TextMesh>();
            _hintHud.fontSize = 36;
            _hintHud.characterSize = 0.01f;
            _hintHud.anchor = TextAnchor.MiddleLeft;
            _hintHud.alignment = TextAlignment.Left;
            _hintHud.color = new Color(0.8f, 0.95f, 1f, 0.95f);
        }

        private void UpdateHintHud()
        {
            if (_hintHud == null)
                return;
            string anchors =
                $"L[{(_left != null ? "OK" : "MISS")}] R[{(_right != null ? "OK" : "MISS")}] " +
                $"Thumbs L[{(_leftThumbTip != null ? "OK" : "—")}] R[{(_rightThumbTip != null ? "OK" : "—")}] · Bow grip: {(bowHeldInLeftHand ? "LEFT" : "RIGHT")}";
            string free = ignoreManaCost ? "Free bow (no MP) · " : "";
            string pullVerb = preferHandTrackingOnly ? "pinch" : "trigger or pinch";
            string swapVerb = preferHandTrackingOnly ? "both pinches" : "both grips or both pinches";
            _hintHud.text =
                $"{free}Draw: other-hand {pullVerb} · Release to shoot (aim along hands)\n" +
                $"VR swap hands: {swapVerb} ~{swapHandsGripHoldSeconds:0.2}s (not while drawing)\n" +
                "Desktop: B equip, RMB draw/release · Voice: 'fire' · I inspect\n" +
                $"Pinch {pinchHoldDistance:0.000}m / release {pinchReleaseDistance:0.000}m (F1/F2) · {anchors}";
        }

        private void FireIfCharged()
        {
            if (_pull01 < minReleasePull || _arrowSpawn == null)
                return;

            if (!ignoreManaCost)
            {
                if (_playerStats == null)
                    return;

                float mpCost = baseMpCost + extraMpCostAtMaxPull * _pull01;
                if (!_playerStats.TrySpendMp(mpCost))
                {
                    Debug.Log("[WindManaBow] Not enough MP to form wind arrow.");
                    return;
                }
            }

            var arrow = BuildArrow(_arrowSpawn.position, _arrowSpawn.rotation);
            var proj = arrow.AddComponent<WindManaArrow>();
            float speed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, _pull01);
            float dmg = Mathf.Lerp(minArrowDamage, maxArrowDamage, _pull01);
            proj.Initialize(speed, dmg);
            _pull01 = 0f;
        }

        private void BuildBowVisual()
        {
            if (BowGripTransform == null)
            {
                Debug.LogWarning("[WindManaBow] BuildBowVisual skipped: bow grip anchor is null (left/right controller).");
                return;
            }

            if (meshyBowPrefab == null)
            {
                ResolveBowPrefabIfNeeded();
                if (meshyBowPrefab == null)
                {
                    Debug.LogError("[WindManaBow] BuildBowVisual skipped: assign DownloadedBow (Meshy Bow Prefab or Imported Bow Model Asset Path).");
                    return;
                }
            }

            if (_bowInstance != null) Destroy(_bowInstance);
            _chargeArrowRoot = null;
            _chargeShaft = null;
            _chargeHead = null;
            _chargeKnockGlow = null;
            _chargeLight = null;
            _chargeShaftRend = null;
            _chargeHeadRend = null;
            for (int i = 0; i < _chargeFletch.Length; i++) _chargeFletch[i] = null;

            _bowInstance = Instantiate(meshyBowPrefab);

            if (stripCollidersFromImportedBow)
            {
                foreach (var c in _bowInstance.GetComponentsInChildren<Collider>(true))
                    Destroy(c);
            }

            EnsureBowFollowMount();
            SyncBowFollowMountToHand();

            _bowInstance.name = "WindManaBow";
            _bowInstance.transform.SetParent(_bowFollowMount, false);
            _bowInstance.transform.localPosition = bowLocalPosition;
            _bowInstance.transform.localRotation = Quaternion.Euler(bowLocalEuler);
            _bowInstance.transform.localScale = Vector3.one * bowScale;
            EnsureImportedBowMeshesActive(_bowInstance);

            _arrowSpawn = new GameObject("ArrowSpawn").transform;
            _arrowSpawn.SetParent(_bowInstance.transform, false);
            _arrowSpawn.localPosition = arrowSpawnLocalPosition;
            _arrowSpawn.localRotation = Quaternion.Euler(arrowSpawnLocalEulerAngles);

            _stringVisual = null;
            if (drawProceduralBowString)
            {
                _stringVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
                _stringVisual.name = "BowString";
                _stringVisual.SetParent(_bowInstance.transform, false);
                _stringVisual.localScale = new Vector3(0.004f, 0.2f, 0.004f);
                _stringVisual.localRotation = Quaternion.Euler(90f, 0f, 0f);
                if (_stringVisual.TryGetComponent<Collider>(out var c0)) Destroy(c0);
                ApplyUnlitColor(_stringVisual.GetComponent<Renderer>(), new Color(0.92f, 0.9f, 0.82f, 1f));
            }

            BuildChargingArrowVisual();
            string which = bowHeldInLeftHand ? "left" : "right";
            Debug.Log($"[WindManaBow] Bow in {which} hand under '{_bowFollowMount.name}' (grip '{BowGripTransform.name}', aim toward other hand). Swap: both grips {swapHandsGripHoldSeconds:0.2}s. Cosmetic on avatar: CosmeticBow_InHand.");
        }

        private void BuildChargingArrowVisual()
        {
            _chargeArrowRoot = new GameObject("ChargingWindArrow").transform;
            _chargeArrowRoot.SetParent(_arrowSpawn, false);
            _chargeArrowRoot.localPosition = Vector3.zero;
            _chargeArrowRoot.localRotation = Quaternion.identity;

            _chargeShaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            _chargeShaft.name = "ChargeShaft";
            _chargeShaft.SetParent(_chargeArrowRoot, false);
            _chargeShaft.localRotation = Quaternion.Euler(90f, 0f, 0f);
            if (_chargeShaft.TryGetComponent<Collider>(out var cs)) Destroy(cs);
            _chargeShaftRend = _chargeShaft.GetComponent<Renderer>();
            ApplyManaShaftMaterial(_chargeShaftRend, 0f);

            _chargeHead = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            _chargeHead.name = "ChargeHead";
            _chargeHead.SetParent(_chargeArrowRoot, false);
            if (_chargeHead.TryGetComponent<Collider>(out var ch)) Destroy(ch);
            _chargeHeadRend = _chargeHead.GetComponent<Renderer>();
            ApplyManaHeadMaterial(_chargeHeadRend, 0f);

            for (int i = 0; i < 3; i++)
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                f.name = $"ChargeFletch_{i}";
                f.SetParent(_chargeArrowRoot, false);
                if (f.TryGetComponent<Collider>(out var cf)) Destroy(cf);
                float ang = i * 120f * Mathf.Deg2Rad;
                f.localRotation = Quaternion.Euler(0f, 0f, i * 120f);
                f.localPosition = new Vector3(Mathf.Cos(ang) * 0.012f, Mathf.Sin(ang) * 0.012f, -0.02f);
                f.localScale = new Vector3(0.008f, 0.022f, 0.004f);
                ApplyUnlitColor(f.GetComponent<Renderer>(), new Color(0.32f, 0.95f, 0.52f, 0.35f));
                _chargeFletch[i] = f;
            }

            _chargeKnockGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            _chargeKnockGlow.name = "KnockManaGlow";
            _chargeKnockGlow.SetParent(_chargeArrowRoot, false);
            _chargeKnockGlow.localPosition = Vector3.zero;
            if (_chargeKnockGlow.TryGetComponent<Collider>(out var ck)) Destroy(ck);
            ApplyUnlitColor(_chargeKnockGlow.GetComponent<Renderer>(), new Color(manaArrowCore.r, manaArrowCore.g * 1.1f, manaArrowCore.b, 0.25f));

            var lightGo = new GameObject("ChargeManaLight");
            lightGo.transform.SetParent(_chargeArrowRoot, false);
            _chargeLight = lightGo.AddComponent<Light>();
            _chargeLight.type = LightType.Point;
            _chargeLight.color = manaArrowCore;
            _chargeLight.range = 0.85f;
            _chargeLight.intensity = 0f;
            _chargeLight.shadows = LightShadows.None;
        }

        private void UpdateVisuals()
        {
            if (_stringVisual != null)
                _stringVisual.localPosition = new Vector3(0f, 0f, Mathf.Lerp(-0.02f, -0.14f, _pull01));

            if (_chargeArrowRoot == null)
                return;

            // Mana arrow only appears while drawing the string (holding pull).
            bool showCharge = _isHoldingString;
            if (_chargeArrowRoot.gameObject.activeSelf != showCharge)
                _chargeArrowRoot.gameObject.SetActive(showCharge);
            if (!showCharge)
                return;

            float eased = Mathf.SmoothStep(0f, 1f, _pull01);
            float shaftLen = Mathf.Lerp(0.06f, 0.42f, eased);
            const float cylHalf = 0.5f;
            if (_chargeShaft != null)
            {
                _chargeShaft.localScale = new Vector3(0.012f, shaftLen * cylHalf, 0.012f);
                _chargeShaft.localPosition = new Vector3(0f, 0f, shaftLen * cylHalf);
            }

            if (_chargeHead != null)
            {
                float headT = Mathf.Pow(eased, 0.65f);
                _chargeHead.localPosition = new Vector3(0f, 0f, shaftLen + Mathf.Lerp(0.02f, 0.06f, headT));
                _chargeHead.localScale = Vector3.one * Mathf.Lerp(0.02f, 0.055f, headT);
            }

            if (_chargeKnockGlow != null)
            {
                float g = Mathf.Lerp(0.018f, 0.07f, eased);
                _chargeKnockGlow.localScale = Vector3.one * g;
                var r = _chargeKnockGlow.GetComponent<Renderer>();
                if (r != null)
                    ApplyUnlitColor(r, new Color(manaArrowCore.r, manaArrowCore.g, manaArrowCore.b, Mathf.Lerp(0.12f, 0.75f, eased)));
            }

            for (int i = 0; i < _chargeFletch.Length; i++)
            {
                if (_chargeFletch[i] == null) continue;
                float ang = i * 120f * Mathf.Deg2Rad;
                float r = Mathf.Lerp(0.006f, 0.014f, eased);
                _chargeFletch[i].localPosition = new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, Mathf.Lerp(-0.01f, -0.05f, eased));
                _chargeFletch[i].localScale = new Vector3(0.006f, Mathf.Lerp(0.01f, 0.028f, eased), 0.003f);
                var fr = _chargeFletch[i].GetComponent<Renderer>();
                if (fr != null)
                    ApplyUnlitColor(fr, new Color(0.4f, 1f, 0.55f, Mathf.Lerp(0.08f, 0.9f, eased)));
            }

            if (_chargeLight != null)
                _chargeLight.intensity = Mathf.Lerp(0f, 1.85f, eased);

            if (_chargeShaftRend != null)
                ApplyManaShaftMaterial(_chargeShaftRend, eased);
            if (_chargeHeadRend != null)
                ApplyManaHeadMaterial(_chargeHeadRend, eased);
        }

        private GameObject BuildArrow(Vector3 pos, Quaternion rot)
        {
            return BuildFlyingWindArrow(pos, rot, manaArrowCore, manaArrowTip);
        }

        /// <summary>Shared flying projectile mesh + physics for reliable trigger hits.</summary>
        public static GameObject BuildFlyingWindArrow(Vector3 pos, Quaternion rot, Color core, Color tip)
        {
            var go = new GameObject("WindManaArrow");
            go.transform.SetPositionAndRotation(pos, rot);
            go.layer = LayerMask.NameToLayer("Default");

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "ArrowShaft";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(0.014f, 0.11f, 0.014f);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localPosition = new Vector3(0f, 0f, 0.11f);
            if (body.TryGetComponent<Collider>(out var bc)) Destroy(bc);
            ApplyUnlitColor(body.GetComponent<Renderer>(), new Color(core.r, core.g, core.b, 0.95f));

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "ArrowHead";
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0f, 0f, 0.26f);
            head.transform.localScale = new Vector3(0.045f, 0.045f, 0.08f);
            if (head.TryGetComponent<Collider>(out var hc)) Destroy(hc);
            ApplyUnlitColor(head.GetComponent<Renderer>(), new Color(tip.r, tip.g, tip.b, 1f));

            for (int i = 0; i < 3; i++)
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
                f.name = $"Fletch_{i}";
                f.transform.SetParent(go.transform, false);
                if (f.TryGetComponent<Collider>(out var fc)) Destroy(fc);
                float ang = i * 120f * Mathf.Deg2Rad;
                f.transform.localRotation = Quaternion.Euler(0f, 0f, i * 120f);
                f.transform.localPosition = new Vector3(Mathf.Cos(ang) * 0.012f, Mathf.Sin(ang) * 0.012f, 0.02f);
                f.transform.localScale = new Vector3(0.006f, 0.024f, 0.003f);
                ApplyUnlitColor(f.GetComponent<Renderer>(), new Color(0.35f, 0.95f, 0.5f, 0.9f));
            }

            var col = go.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.direction = 2;
            col.radius = 0.045f;
            col.height = 0.38f;
            col.center = new Vector3(0f, 0f, 0.16f);

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = core;
            light.range = 2.2f;
            light.intensity = 1.4f;
            light.shadows = LightShadows.None;

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.startWidth = 0.06f;
            trail.endWidth = 0.01f;
            trail.minVertexDistance = 0.02f;
            trail.material = CreateTrailMaterial(core);
            trail.startColor = new Color(core.r, core.g, core.b, 0.55f);
            trail.endColor = new Color(core.r, core.g, core.b, 0f);

            return go;
        }

        private void ApplyManaShaftMaterial(Renderer rend, float charge01)
        {
            if (rend == null) return;
            charge01 = Mathf.Clamp01(charge01);
            var c = Color.Lerp(new Color(manaArrowCore.r, manaArrowCore.g, manaArrowCore.b, 0.12f), manaArrowCore, charge01);
            ApplyUnlitColor(rend, c);
        }

        private void ApplyManaHeadMaterial(Renderer rend, float charge01)
        {
            if (rend == null) return;
            charge01 = Mathf.Clamp01(charge01);
            var c = Color.Lerp(new Color(manaArrowTip.r, manaArrowTip.g, manaArrowTip.b, 0.05f), manaArrowTip, charge01);
            ApplyUnlitColor(rend, c);
        }

        private static Material CreateTrailMaterial(Color tint)
        {
            var s = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color");
            var m = new Material(s);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, 0.4f));
            if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(tint.r, tint.g, tint.b, 0.4f));
            return m;
        }

        private static void ApplyUnlitColor(Renderer rend, Color color)
        {
            if (rend == null) return;
            var s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (s == null) return;
            var m = new Material(s);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            rend.sharedMaterial = m;
        }

        private static bool ContainsAny(string hay, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (hay.Contains(needles[i]))
                    return true;
            }
            return false;
        }

        private static bool IsPrefabRenderPipelineCompatible(GameObject prefab)
        {
            if (prefab == null)
                return false;

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return true;

            for (int i = 0; i < renderers.Length; i++)
            {
                var mats = renderers[i].sharedMaterials;
                if (mats == null) continue;
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null) continue;
                    var shader = mat.shader;
                    if (shader == null) return false;
                    string shaderName = shader.name ?? string.Empty;
                    if (shaderName.Contains("Hidden/InternalErrorShader"))
                        return false;
                }
            }
            return true;
        }

        private void AutoConfigureRuntimeMode()
        {
            if (!autoConfigureForStandaloneHandTracking)
                return;

            if (_xrRuntimeActive)
            {
                // In-hand bow; keep mouse/RMB when no real headset (force-XR / editor) so pull still works.
                testModeGroundBowOnly = false;
                spawnGroundBowAtStart = false;
                enableDesktopMouseKeyboardTest = !_xrDisplaySubsystemRunning;
                enableHandTrackingPinchPull = _xrDisplaySubsystemRunning;
                if (!equipHandBowWhenNoHeadset && !_xrDisplaySubsystemRunning)
                {
                    testModeGroundBowOnly = true;
                    spawnGroundBowAtStart = true;
                    enableDesktopMouseKeyboardTest = true;
                }
                Debug.Log(_xrDisplaySubsystemRunning
                    ? "[WindManaBow] Auto-configured for XR headset + hand tracking."
                    : "[WindManaBow] Auto-configured for editor / no headset (camera anchors + mouse when controllers missing).");
            }
            else
            {
                if (equipHandBowWhenNoHeadset)
                {
                    testModeGroundBowOnly = false;
                    spawnGroundBowAtStart = false;
                }
                else
                {
                    testModeGroundBowOnly = true;
                    spawnGroundBowAtStart = true;
                }
                enableDesktopMouseKeyboardTest = true;
                enableHandTrackingPinchPull = false;
                Debug.Log("[WindManaBow] Auto-configured for desktop test mode.");
            }
        }

        private static bool IsXrRuntimeActive()
        {
            if (XRSettings.isDeviceActive)
                return true;
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetInstances(displays);
            for (int i = 0; i < displays.Count; i++)
            {
                if (displays[i] != null && displays[i].running)
                    return true;
            }
            return false;
        }

        private void TryRefreshHandTrackingAnchors()
        {
            if (!_xrRuntimeActive)
                return;

            Transform rigForHands = ResolveXrRigRootTransform();
            var leftHand = XRHandAttachmentUtil.FindHandController(rigForHands, false);
            var rightHand = XRHandAttachmentUtil.FindHandController(rigForHands, true);
            var newLeft = leftHand != null ? XRHandAttachmentUtil.FindIndexTipOrController(leftHand) : null;
            var newRight = rightHand != null ? XRHandAttachmentUtil.FindIndexTipOrController(rightHand) : null;
            if (newLeft != null) _left = newLeft;
            if (newRight != null) _right = newRight;

            var newLThumb = leftHand != null ? XRHandAttachmentUtil.FindThumbTipOrController(leftHand) : null;
            var newRThumb = rightHand != null ? XRHandAttachmentUtil.FindThumbTipOrController(rightHand) : null;
            if (newLThumb != null) _leftThumbTip = newLThumb;
            if (newRThumb != null) _rightThumbTip = newRThumb;

            if (createCameraFallbackAnchorsInXr && (_left == null || _right == null))
                CreateDesktopAnchors();
        }
    }
}
