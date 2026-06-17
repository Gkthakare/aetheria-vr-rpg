using UnityEngine;

namespace Aetheria.Training
{
    /// <summary>
    /// Attach to the Training room pad. Detects player in zone, activates stations.
    /// Use optional content prefab for stations + VFX, or add stations as children in editor.
    /// </summary>
    public class TrainingRoomManager : MonoBehaviour
    {
        [Header("Zone")]
        [SerializeField] private float zoneRadius = 4f;
        [SerializeField] private bool useTrigger = true;

        [Header("Optional: full content prefab (stations + VFX)")]
        [SerializeField] private GameObject trainingContentPrefab;

        [Header("Player (auto if null)")]
        [SerializeField] private Transform player;

        private bool _playerInZone;
        private GameObject _spawnedContent;

        private void Awake()
        {
            if (player == null)
            {
                var cam = Camera.main;
                if (cam != null) player = cam.transform;
                if (player == null)
                {
                    foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (t.name.Contains("XR Origin")) { player = t; break; }
                }
            }

            if (useTrigger)
            {
                var col = gameObject.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = zoneRadius;
                col.center = Vector3.zero;
            }

            if (trainingContentPrefab != null)
            {
                _spawnedContent = Instantiate(trainingContentPrefab, transform);
                _spawnedContent.transform.localPosition = Vector3.zero;
                _spawnedContent.transform.localRotation = Quaternion.identity;
                _spawnedContent.name = "TrainingContent";
            }
            else
            {
                CreateDefaultStations();
            }
        }

        private void CreateDefaultStations()
        {
            _spawnedContent = new GameObject("TrainingContent");
            _spawnedContent.transform.SetParent(transform);
            _spawnedContent.transform.localPosition = Vector3.zero;
            _spawnedContent.transform.localRotation = Quaternion.identity;

            var gestureGo = new GameObject("GestureStation");
            gestureGo.transform.SetParent(_spawnedContent.transform);
            gestureGo.transform.localPosition = new Vector3(-1.5f, 0f, 0f);
            gestureGo.AddComponent<HandPoseStub>();
            gestureGo.AddComponent<RuleBasedGestureRecognizer>();
            gestureGo.AddComponent<TrainingVFX>();
            gestureGo.AddComponent<GestureTrainingStation>();

            var weaponGo = new GameObject("WeaponStation");
            weaponGo.transform.SetParent(_spawnedContent.transform);
            weaponGo.transform.localPosition = new Vector3(1.5f, 0f, 0f);
            weaponGo.AddComponent<TrainingVFX>();
            weaponGo.AddComponent<WeaponTrainingStation>();

        }

        private void Update()
        {
            if (!useTrigger && player != null)
            {
                float d = Vector3.Distance(transform.position, player.position);
                _playerInZone = d <= zoneRadius;
            }

            SetStationsActive(_playerInZone);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!useTrigger) return;
            if (player != null && other.transform.IsChildOf(player) || other.transform == player)
                _playerInZone = true;
            else if (other.CompareTag("Player") || other.GetComponentInChildren<Camera>() != null)
                _playerInZone = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!useTrigger) return;
            if (player != null && other.transform.IsChildOf(player) || other.transform == player)
                _playerInZone = false;
            else if (other.CompareTag("Player") || other.GetComponentInChildren<Camera>() != null)
                _playerInZone = false;
        }

        private void SetStationsActive(bool active)
        {
            foreach (var station in GetComponentsInChildren<GestureTrainingStation>(true))
                station.enabled = active;
            foreach (var station in GetComponentsInChildren<WeaponTrainingStation>(true))
                station.enabled = active;
        }
    }
}
