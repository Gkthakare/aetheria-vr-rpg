using UnityEngine;

namespace Aetheria.WaterMagic
{
    /// <summary>
    /// Simple water bolt: flies forward, spawns splash on impact. Attach to spell prefab root.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class WaterProjectile : MonoBehaviour
    {
        [SerializeField] float speed = 18f;
        [SerializeField] float lifetimeSeconds = 4f;
        [SerializeField] GameObject impactFxPrefab;
        [SerializeField] LayerMask hitMask = ~0;

        Rigidbody _body;

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.useGravity = false;
        }

        public void Launch(Vector3 direction)
        {
            PhysicsCompat.SetLinearVelocity(_body, direction.normalized * speed);
            Destroy(gameObject, lifetimeSeconds);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (((1 << collision.gameObject.layer) & hitMask) == 0)
                return;
            if (impactFxPrefab != null)
                Instantiate(impactFxPrefab, collision.GetContact(0).point, Quaternion.LookRotation(collision.GetContact(0).normal));
            Destroy(gameObject);
        }
    }
}
