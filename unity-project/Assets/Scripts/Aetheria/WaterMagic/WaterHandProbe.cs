using System;
using UnityEngine;

namespace Aetheria.WaterMagic
{
    /// <summary>
    /// Raycasts from this transform to detect the water surface (layer mask).
    /// Attach to each controller / hand anchor. Drives ripples and "touching water" state.
    /// </summary>
    public sealed class WaterHandProbe : MonoBehaviour
    {
        [SerializeField] LayerMask waterLayers = ~0;
        [SerializeField] float maxDistance = 2f;
        [SerializeField] Vector3 rayDirection = Vector3.down;
        [SerializeField] Transform rayOrigin;

        public bool IsTouchingWater { get; private set; }
        public Vector3 LastHitPoint { get; private set; }
        public Vector3 LastHitNormal { get; private set; }

        public event Action<Vector3, Vector3> WaterTouched;

        Vector3 Origin => rayOrigin != null ? rayOrigin.position : transform.position;
        Vector3 Direction => transform.TransformDirection(rayDirection.normalized);

        void Update()
        {
            var origin = Origin;
            var dir = Direction;
            IsTouchingWater = Physics.Raycast(origin, dir, out var hit, maxDistance, waterLayers,
                QueryTriggerInteraction.Collide);

            if (!IsTouchingWater)
                return;

            LastHitPoint = hit.point;
            LastHitNormal = hit.normal;
            WaterTouched?.Invoke(hit.point, hit.normal);
        }

        void OnDrawGizmosSelected()
        {
            if (!isActiveAndEnabled)
                return;
            var origin = Application.isPlaying ? Origin : (rayOrigin != null ? rayOrigin.position : transform.position);
            var dir = Application.isPlaying ? Direction : transform.TransformDirection(rayDirection.normalized);
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(origin, dir * maxDistance);
        }
    }
}
