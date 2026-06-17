using UnityEngine;

namespace Aetheria.World
{
    /// <summary>
    /// Cheap "dragon circling" — moves transform in a horizontal ellipse around <see cref="center"/>.
    /// Replace mesh with Meshy/ai-3d-agent GLB; add wing flap animation later.
    /// </summary>
    public sealed class FlyingPatrolSimple : MonoBehaviour
    {
        [SerializeField] Transform center;

        [Tooltip("Prefab instances cannot reference scene objects. If Center is empty, we find this name at runtime (HelloVR: IslandWorldManager).")]
        [SerializeField] string autoResolveCenterByName = "IslandWorldManager";

        [SerializeField] Vector3 centerOffset = Vector3.zero;
        [SerializeField] float radiusX = 45f;
        [SerializeField] float radiusZ = 38f;
        [SerializeField] float heightAboveCenter = 22f;
        [SerializeField] float orbitSpeed = 0.15f;
        [SerializeField] bool faceTangent = true;

        float _angle;

        void Awake()
        {
            TryResolveCenter();
        }

        void Start()
        {
            TryResolveCenter();
        }

        /// <summary>Call after Instantiate if you need to re-bind before the first Update.</summary>
        public void TryResolveCenter()
        {
            if (center != null)
                return;
            if (string.IsNullOrWhiteSpace(autoResolveCenterByName))
                return;
            var go = GameObject.Find(autoResolveCenterByName);
            if (go != null)
                center = go.transform;
        }

        void Update()
        {
            var c = center != null ? center.position + centerOffset : transform.parent != null ? transform.parent.position : Vector3.zero;
            _angle += orbitSpeed * Time.deltaTime;
            var x = Mathf.Cos(_angle) * radiusX;
            var z = Mathf.Sin(_angle) * radiusZ;
            var p = c + new Vector3(x, heightAboveCenter, z);
            transform.position = p;

            if (faceTangent)
            {
                var tangent = new Vector3(-Mathf.Sin(_angle) * radiusX, 0f, Mathf.Cos(_angle) * radiusZ);
                if (tangent.sqrMagnitude > 1e-4f)
                    transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
            }
        }
    }
}
