using UnityEngine;

namespace Aetheria.WaterMagic
{
    /// <summary>
    /// Spawns a one-shot particle prefab when water is touched. Cheap "reaction" on Quest.
    /// </summary>
    public sealed class WaterRippleSpawner : MonoBehaviour
    {
        [SerializeField] WaterHandProbe probe;
        [SerializeField] GameObject ripplePrefab;
        [SerializeField] float minSecondsBetweenRipples = 0.12f;

        float _lastRippleTime = -999f;

        /// <summary>Runtime hook (e.g. VR bootstrap) to use the same Suimono ripple prefab as the infinite ocean.</summary>
        public void SetRipplePrefab(GameObject prefab)
        {
            ripplePrefab = prefab;
        }

        void OnEnable()
        {
            if (probe != null)
                probe.WaterTouched += OnWaterTouched;
        }

        void OnDisable()
        {
            if (probe != null)
                probe.WaterTouched -= OnWaterTouched;
        }

        void OnWaterTouched(Vector3 point, Vector3 normal)
        {
            TrySpawnRippleAt(point, normal);
        }

        /// <summary>Remote magic / finger ray hits water — same throttle as probe ripples.</summary>
        public bool TrySpawnRippleAt(Vector3 point, Vector3 normal)
        {
            if (ripplePrefab == null)
                return false;
            if (Time.time - _lastRippleTime < minSecondsBetweenRipples)
                return false;
            _lastRippleTime = Time.time;

            var rot = Quaternion.LookRotation(normal.sqrMagnitude > 1e-6f ? normal : Vector3.up);
            Instantiate(ripplePrefab, point, rot);
            return true;
        }
    }
}
