using UnityEngine;

namespace Aetheria.WaterMagic
{
    /// <summary>
    /// While the gather button is held and a probe touches water, fills a held water orb (0..1).
    /// Wires to VR input in the inspector (Unity Events) or call from your input script.
    /// </summary>
    public sealed class WaterGatherController : MonoBehaviour
    {
        [SerializeField] WaterHandProbe dominantHandProbe;
        [SerializeField] WaterOrbVisual orbVisual;
        [SerializeField] float secondsToFullCharge = 1.25f;
        [SerializeField] float drainPerSecondWhenNotGathering = 0.35f;

        [Range(0f, 1f)] [SerializeField] float charge;

        /// <summary>Optional probe for dip-to-gather; can be null if the orb is filled only via ForceFullOrb (e.g. finger ritual).</summary>
        public void SetRuntimeReferences(WaterOrbVisual visual, WaterHandProbe probe = null)
        {
            orbVisual = visual;
            dominantHandProbe = probe;
        }

        public float Charge => charge;
        public bool IsGathering { get; private set; }

        void Update()
        {
            if (IsGathering)
                TickGather();
            else if (charge > 0f)
            {
                charge = Mathf.Max(0f, charge - drainPerSecondWhenNotGathering * Time.deltaTime);
                if (orbVisual != null)
                    orbVisual.SetFill(charge);
            }
        }

        public void SetGathering(bool gathering)
        {
            IsGathering = gathering;
        }

        public void TickGather()
        {
            if (dominantHandProbe == null || orbVisual == null)
                return;
            if (!IsGathering || !dominantHandProbe.IsTouchingWater)
                return;

            charge = Mathf.Clamp01(charge + Time.deltaTime / Mathf.Max(0.05f, secondsToFullCharge));
            orbVisual.SetFill(charge);
        }

        /// <summary>Consume charge for a spell; returns false if not enough.</summary>
        public bool TryConsume(float amount)
        {
            if (charge < amount)
                return false;
            charge -= amount;
            orbVisual.SetFill(charge);
            return true;
        }

        public void ClearCharge()
        {
            charge = 0f;
            if (orbVisual != null)
                orbVisual.SetFill(0f);
        }

        /// <summary>Fills orb after a formation ritual (voice + finger on water) without holding gather.</summary>
        public void ForceFullOrb()
        {
            IsGathering = false;
            charge = 1f;
            if (orbVisual != null)
                orbVisual.SetFill(1f);
        }
    }
}
