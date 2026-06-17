using UnityEngine;

namespace Aetheria.WaterMagic
{
    /// <summary>
    /// Minimal desktop test input: hold Fire2 (right mouse) to gather.
    /// In VR, disable this and drive <see cref="WaterGatherController.SetGathering"/> from XRI.
    /// </summary>
    public sealed class WaterGatherInputBridge : MonoBehaviour
    {
        [SerializeField] WaterGatherController gather;

        void Update()
        {
            if (gather == null)
                return;
#if ENABLE_INPUT_SYSTEM
            var held = UnityEngine.InputSystem.Mouse.current != null &&
                       UnityEngine.InputSystem.Mouse.current.rightButton.isPressed;
#else
            var held = Input.GetMouseButton(1);
#endif
            gather.SetGathering(held);
        }
    }
}
