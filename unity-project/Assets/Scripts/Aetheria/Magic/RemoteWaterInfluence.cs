using Aetheria.WaterMagic;
using UnityEngine;

namespace Aetheria.Magic
{
    /// <summary>
    /// Long-range water interaction: ray from hand/finger → if it hits <see cref="waterLayers"/>, spawns ripples and drives global shader push (see <see cref="WaterWorldInfluenceHub"/>).
    /// No per-frame AI — pure geometry + data you tune in the inspector.
    /// </summary>
    public sealed class RemoteWaterInfluence : MonoBehaviour
    {
        [SerializeField] MagicTargetingRay targeting;
        [SerializeField] LayerMask waterLayers;
        [SerializeField] WaterRippleSpawner rippleSpawner;
        [SerializeField] WaterWorldInfluenceHub worldInfluence;
        [SerializeField] float influenceStrength = 1f;

        /// <summary>Runtime wiring (VR bootstrap).</summary>
        public void SetRuntimeReferences(MagicTargetingRay ray, LayerMask water, WaterRippleSpawner ripples, WaterWorldInfluenceHub hub, float strength = 1f)
        {
            targeting = ray;
            waterLayers = water;
            rippleSpawner = ripples;
            worldInfluence = hub;
            influenceStrength = strength;
        }

        /// <summary>Bind to XR select, voice proxy, or AI intent → UnityEvent.</summary>
        public void CastAtAim()
        {
            if (targeting == null)
                return;
            if (!targeting.TryGetHitOnLayer(waterLayers, out var hit))
                return;

            if (rippleSpawner != null)
                rippleSpawner.TrySpawnRippleAt(hit.point, hit.normal);

            if (worldInfluence != null)
                worldInfluence.ApplyPulse(hit.point, influenceStrength);
        }
    }
}
