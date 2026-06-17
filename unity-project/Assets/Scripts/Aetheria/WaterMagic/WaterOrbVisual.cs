using UnityEngine;

namespace Aetheria.WaterMagic
{
    /// <summary>
    /// Visual for water held in hand: scales with fill, follows an attach transform.
    /// </summary>
    public sealed class WaterOrbVisual : MonoBehaviour
    {
        [SerializeField] Transform attachPoint;
        [SerializeField] Vector3 localOffset;
        [SerializeField] float minScale = 0f;
        [SerializeField] float maxScale = 0.18f;
        [SerializeField] Renderer[] tintTargets;
        [SerializeField] Color emptyColor = new Color(0.4f, 0.75f, 1f, 0.15f);
        [SerializeField] Color fullColor = new Color(0.4f, 0.9f, 1f, 0.65f);

        /// <summary>Used when the orb hierarchy is created at runtime (e.g. VR bootstrap).</summary>
        public void SetRuntimeTargets(Transform attach, Renderer primaryTint)
        {
            attachPoint = attach;
            tintTargets = primaryTint != null ? new[] { primaryTint } : null;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock _mpb;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
        }

        void LateUpdate()
        {
            if (attachPoint == null)
                return;
            transform.position = attachPoint.TransformPoint(localOffset);
            transform.rotation = attachPoint.rotation;
        }

        public void SetFill(float t)
        {
            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();

            t = Mathf.Clamp01(t);
            var s = Mathf.Lerp(minScale, maxScale, t);
            transform.localScale = Vector3.one * s;

            var c = Color.Lerp(emptyColor, fullColor, t);
            if (tintTargets == null)
                return;
            foreach (var r in tintTargets)
            {
                if (r == null)
                    continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, c);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
