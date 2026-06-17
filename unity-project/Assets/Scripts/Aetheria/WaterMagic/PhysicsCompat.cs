using UnityEngine;

namespace Aetheria.WaterMagic
{
    /// <summary>
    /// Central place for physics API differences across Unity versions (e.g. Unity 6+ linear velocity).
    /// When upgrading editors, fix deprecations here instead of across every gameplay script.
    /// </summary>
    internal static class PhysicsCompat
    {
        public static void SetLinearVelocity(Rigidbody body, Vector3 velocity)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = velocity;
#else
            body.velocity = velocity;
#endif
        }
    }
}
