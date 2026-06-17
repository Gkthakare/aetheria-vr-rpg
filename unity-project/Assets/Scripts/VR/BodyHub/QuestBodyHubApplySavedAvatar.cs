using UnityEngine;

namespace Aetheria.VR.BodyHub
{
    /// <summary>
    /// Non-hub scenes: spawn the same avatar slot the player chose in the body hub
    /// (<see cref="QuestBodyHubDirector.PlayerPrefsAvatarIndexKey"/>).
    /// Assign the same prefab list in the same order as on the hub director (e.g. drag
    /// <c>readyplayerme_cyberpunk.glb</c> root from <see cref="QuestBodyHubDirector.DefaultCyberpunkMaleGlbAssetPath"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestBodyHubApplySavedAvatar : MonoBehaviour
    {
        [SerializeField] private Transform avatarMount;
        [SerializeField] private GameObject[] avatarPrefabs = new GameObject[0];
        [SerializeField] private Vector3 avatarLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 avatarLocalEuler = Vector3.zero;

        private GameObject _spawned;

        private void Start()
        {
            if (avatarMount == null)
                avatarMount = transform;

            if (avatarPrefabs == null || avatarPrefabs.Length == 0)
                return;

            int ix = PlayerPrefs.GetInt(QuestBodyHubDirector.PlayerPrefsAvatarIndexKey, 0);
            ix = Mathf.Clamp(ix, 0, avatarPrefabs.Length - 1);
            var prefab = avatarPrefabs[ix];
            if (prefab == null)
                return;

            if (_spawned != null)
                Destroy(_spawned);

            _spawned = Instantiate(prefab, avatarMount);
            _spawned.name = prefab.name + "_SessionInstance";
            _spawned.transform.localPosition = avatarLocalPosition;
            _spawned.transform.localRotation = Quaternion.Euler(avatarLocalEuler);
        }
    }
}
