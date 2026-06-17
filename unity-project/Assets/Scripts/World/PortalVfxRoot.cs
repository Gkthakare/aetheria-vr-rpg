using UnityEngine;

namespace Aetheria.World
{
    /// <summary>
    /// Drop on portal root: slow spin + optional light pulse. Swap mesh for your VFX Graph / Hovl prefab.
    /// </summary>
    public sealed class PortalVfxRoot : MonoBehaviour
    {
        [SerializeField] float spinDegreesPerSecond = 12f;
        [SerializeField] Light portalLight;
        [SerializeField] float lightPulseMin = 0.8f;
        [SerializeField] float lightPulseMax = 2.2f;
        [SerializeField] float pulseSpeed = 2f;
        [Tooltip("Uncheck for steady brightness (no automatic bright/dim cycle).")]
        [SerializeField] bool animateLightIntensity = false;

        void Update()
        {
            transform.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);

            if (portalLight == null)
                return;
            if (!animateLightIntensity)
            {
                portalLight.intensity = (lightPulseMin + lightPulseMax) * 0.5f;
                return;
            }

            var w = (Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f);
            portalLight.intensity = Mathf.Lerp(lightPulseMin, lightPulseMax, w);
        }
    }
}
