using UnityEngine;

namespace Aetheria.Combat
{
    /// <summary>
    /// Runs away on XZ when the target (player camera) is within <see cref="detectRadius"/>.
    /// </summary>
    public class SimpleFleeFromTarget : MonoBehaviour
    {
        [Tooltip("Player view to flee from. For VR, drag the HMD camera under XR Origin (tag Main Camera). Leave empty to use Camera.main when it exists.")]
        [SerializeField] private Transform target;

        [Tooltip("Start fleeing when horizontal distance to target is less than this.")]
        [SerializeField] private float detectRadius = 14f;

        [SerializeField] private float moveSpeed = 4f;

        private void Awake() => TryBindCameraMain();

        private void Update()
        {
            if (target == null) TryBindCameraMain();
            if (target == null) return;

            Vector3 delta = transform.position - target.position;
            delta.y = 0f;

            float sqr = delta.sqrMagnitude;
            if (sqr > detectRadius * detectRadius || sqr < 0.0001f) return;

            transform.position += delta.normalized * (moveSpeed * Time.deltaTime);
        }

        private void TryBindCameraMain()
        {
            if (target != null) return;
            if (Camera.main != null) target = Camera.main.transform;
        }
    }
}
