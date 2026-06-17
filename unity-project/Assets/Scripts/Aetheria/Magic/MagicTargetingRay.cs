using UnityEngine;

namespace Aetheria.Magic
{
    /// <summary>
    /// World-space ray from an XR hand, finger bone, or staff tip. Infinite "reach" is a <see cref="maxDistance"/> choice — keep Quest-friendly (50–200 m typical).
    /// </summary>
    public sealed class MagicTargetingRay : MonoBehaviour
    {
        [SerializeField] Transform rayOrigin;
        [SerializeField] Vector3 localDirection = Vector3.forward;
        [SerializeField] float maxDistance = 120f;
        [SerializeField] LayerMask hitLayers = ~0;
        [SerializeField] bool drawDebug;

        public void SetMaxDistance(float distance)
        {
            maxDistance = Mathf.Max(1f, distance);
        }

        public Ray WorldRay
        {
            get
            {
                var t = rayOrigin != null ? rayOrigin : transform;
                var o = t.position;
                var d = t.TransformDirection(localDirection.normalized);
                return new Ray(o, d);
            }
        }

        public bool TryGetHit(out RaycastHit hit)
        {
            var r = WorldRay;
            return Physics.Raycast(r.origin, r.direction, out hit, maxDistance, hitLayers, QueryTriggerInteraction.Collide);
        }

        public bool TryGetHitOnLayer(LayerMask layerMask, out RaycastHit hit)
        {
            var r = WorldRay;
            return Physics.Raycast(r.origin, r.direction, out hit, maxDistance, layerMask, QueryTriggerInteraction.Collide);
        }

        void OnDrawGizmosSelected()
        {
            if (!drawDebug)
                return;
            var r = WorldRay;
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(r.origin, r.direction * maxDistance);
        }
    }
}
