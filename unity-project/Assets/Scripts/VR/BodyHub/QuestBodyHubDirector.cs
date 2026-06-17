using UnityEngine;

namespace Aetheria.VR.BodyHub
{
    /// <summary>
    /// Quest-only body lab: spawn a catalog avatar under the XR rig, persist choice for other scenes.
    /// Assign <see cref="avatarPrefabs"/> in the Inspector; default project body path is
    /// <see cref="DefaultCyberpunkMaleGlbAssetPath"/> (wired by the Quest Body Hub scene wizard).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestBodyHubDirector : MonoBehaviour
    {
        public const string PlayerPrefsAvatarIndexKey = "aetheria_quest_bodyhub_avatar_ix";

        /// <summary>Project-relative path to the default Ready Player Me cyberpunk male GLB (glTFast import).</summary>
        public const string DefaultCyberpunkMaleGlbAssetPath =
            "Assets/ImportedContent/Characters/full-body-cyberpunk-male/readyplayerme_cyberpunk.glb";

        [Header("Spawn")]
        [Tooltip("Parent for instantiated avatar (typically empty child under XR Origin at floor).")]
        [SerializeField] private Transform avatarMount;
        [Tooltip("Optional offset after spawn (meters, avatar local space).")]
        [SerializeField] private Vector3 avatarLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 avatarLocalEuler = Vector3.zero;

        [Header("Catalog")]
        [SerializeField] private GameObject[] avatarPrefabs = new GameObject[0];

        [Tooltip("Clamp when PlayerPrefs or UI requests an out-of-range index.")]
        [SerializeField] private int startingAvatarIndex;

        private int _currentIndex = -1;
        private GameObject _spawned;

        public int AvatarCount => avatarPrefabs != null ? avatarPrefabs.Length : 0;
        public int CurrentAvatarIndex => _currentIndex;

        private void Awake()
        {
            EnsureAvatarMountResolved();
        }

        private void Start()
        {
            EnsureAvatarMountResolved();

            int saved = PlayerPrefs.GetInt(PlayerPrefsAvatarIndexKey, startingAvatarIndex);
            SelectAvatar(saved, savePreference: false);
        }

        /// <summary>
        /// If the Inspector reference is missing, use <c>AvatarMount</c> under the XR rig (not this hub root —
        /// parenting the body to the hub floor made the avatar sit at world origin while the HMD stayed on the rig).
        /// </summary>
        public void EnsureAvatarMountResolved()
        {
            if (avatarMount != null)
                return;

            var found = TryFindAvatarMountInScene();
            if (found != null)
            {
                avatarMount = found;
                return;
            }

            var xr = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin");
            if (xr != null)
            {
                avatarMount = xr.transform;
                Debug.LogWarning(
                    "[QuestBodyHubDirector] No child named 'AvatarMount' under XR rig — assign Avatar Mount in the Inspector. " +
                    "Using XR root as temporary parent.");
            }
        }

        private static Transform TryFindAvatarMountInScene()
        {
            var xr = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin");
            if (xr == null)
                return null;

            var direct = xr.transform.Find("AvatarMount");
            if (direct != null)
                return direct;

            foreach (var t in xr.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "AvatarMount")
                    return t;
            }

            return null;
        }

        /// <summary>Select avatar by slot index; persists to PlayerPrefs when <paramref name="savePreference"/> is true.</summary>
        public void SelectAvatar(int index, bool savePreference = true)
        {
            EnsureAvatarMountResolved();
            if (avatarMount == null)
            {
                Debug.LogError("[QuestBodyHubDirector] Avatar Mount could not be resolved — add AvatarMount under XR Origin or assign it in the Inspector.");
                return;
            }

            if (avatarPrefabs == null || avatarPrefabs.Length == 0)
            {
                Debug.LogWarning("[QuestBodyHubDirector] No avatarPrefabs assigned — assign RPM / body prefabs on this component.");
                return;
            }

            int clamped = Mathf.Clamp(index, 0, avatarPrefabs.Length - 1);
            var prefab = avatarPrefabs[clamped];
            if (prefab == null)
            {
                Debug.LogWarning($"[QuestBodyHubDirector] avatarPrefabs[{clamped}] is null.");
                return;
            }

            if (_spawned != null)
                Destroy(_spawned);

            _spawned = Instantiate(prefab, avatarMount);
            _spawned.name = prefab.name + "_HubInstance";
            _spawned.transform.localPosition = avatarLocalPosition;
            _spawned.transform.localRotation = Quaternion.Euler(avatarLocalEuler);
            _currentIndex = clamped;

            if (savePreference)
            {
                PlayerPrefs.SetInt(PlayerPrefsAvatarIndexKey, clamped);
                PlayerPrefs.Save();
            }

            Debug.Log($"[QuestBodyHubDirector] Avatar slot {clamped} spawned ({prefab.name}).");
        }

        public void SelectNextAvatar()
        {
            if (AvatarCount <= 0) return;
            int next = (_currentIndex < 0 ? 0 : _currentIndex + 1) % AvatarCount;
            SelectAvatar(next, true);
        }

        public void SelectPreviousAvatar()
        {
            if (AvatarCount <= 0) return;
            int cur = _currentIndex < 0 ? 0 : _currentIndex;
            int prev = (cur - 1 + AvatarCount) % AvatarCount;
            SelectAvatar(prev, true);
        }
    }
}
