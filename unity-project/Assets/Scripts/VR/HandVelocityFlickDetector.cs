using UnityEngine;
using UnityEngine.Events;

namespace Aetheria.VR
{
    /// <summary>
    /// Detects a quick finger/wrist flick from hand-tracked tip velocity (common in VR gesture tutorials).
    /// Wire <see cref="OnFlick"/> to spells, UI, or scene events — no controller buttons required.
    /// </summary>
    public sealed class HandVelocityFlickDetector : MonoBehaviour
    {
        [Header("What to track")]
        [Tooltip("If null, resolves from XR rig: index tip on left or right hand.")]
        [SerializeField] Transform trackTransformOverride;
        [SerializeField] bool useRightHand = true;
        [SerializeField] bool autoResolveEachFrame = true;

        [Header("Flick thresholds")]
        [Tooltip("Peak hand speed (m/s) to count as a flick.")]
        [SerializeField] float minPeakSpeedMps = 1.15f;
        [Tooltip("Minimum alignment of velocity with head forward (0 = any direction, 1 = straight ahead).")]
        [SerializeField] float minVelocityHeadForwardDot = 0.2f;
        [SerializeField] float cooldownSeconds = 0.55f;

        [Header("Scene hooks (Inspector)")]
        [SerializeField] UnityEvent onFlick;

        Transform _resolvedTip;
        Vector3 _lastPos;
        float _lastTime;
        bool _hasSample;
        float _nextAllowed;

        /// <summary>Fired when a flick passes thresholds (after cooldown).</summary>
        public event System.Action<Vector3> Flicked;

        public void SetTrackTransform(Transform tip) => trackTransformOverride = tip;

        void LateUpdate()
        {
            if (autoResolveEachFrame || _resolvedTip == null)
                ResolveTip();

            var tip = trackTransformOverride != null ? trackTransformOverride : _resolvedTip;
            if (tip == null)
                return;

            float t = Time.time;
            Vector3 p = tip.position;
            if (!_hasSample)
            {
                _lastPos = p;
                _lastTime = t;
                _hasSample = true;
                return;
            }

            float dt = t - _lastTime;
            if (dt < 1e-4f)
                return;

            Vector3 v = (p - _lastPos) / dt;
            _lastPos = p;
            _lastTime = t;

            if (t < _nextAllowed)
                return;

            if (v.sqrMagnitude < minPeakSpeedMps * minPeakSpeedMps)
                return;

            if (minVelocityHeadForwardDot > -0.99f)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 f = cam.transform.forward;
                    f.y = 0f;
                    if (f.sqrMagnitude > 1e-4f)
                    {
                        f.Normalize();
                        Vector3 vh = v;
                        vh.y = 0f;
                        if (vh.sqrMagnitude > 1e-4f)
                        {
                            vh.Normalize();
                            if (Vector3.Dot(vh, f) < minVelocityHeadForwardDot)
                                return;
                        }
                    }
                }
            }

            _nextAllowed = t + Mathf.Max(0.05f, cooldownSeconds);
            onFlick?.Invoke();
            Flicked?.Invoke(v);
        }

        void ResolveTip()
        {
            var handRoot = XRHandAttachmentUtil.FindHandController(transform, useRightHand);
            _resolvedTip = XRHandAttachmentUtil.FindIndexTipOrController(handRoot);
        }
    }
}
