using UnityEngine;

namespace Aetheria.WaterMagic
{
    /// <summary>
    /// Desktop test: left click casts. In VR, call <see cref="WaterMagicCaster.TryCastPrimary"/> from XRI.
    /// </summary>
    public sealed class WaterCastInputBridge : MonoBehaviour
    {
        [SerializeField] WaterMagicCaster caster;

        void Update()
        {
            if (caster == null)
                return;
#if ENABLE_INPUT_SYSTEM
            var pressed = UnityEngine.InputSystem.Mouse.current != null &&
                          UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
#else
            var pressed = Input.GetMouseButtonDown(0);
#endif
            if (pressed)
                caster.TryCastPrimary();
        }
    }
}
