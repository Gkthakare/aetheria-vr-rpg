using System.Collections.Generic;
using Aetheria.VR;
using Aetheria.WaterMagic;
using UnityEngine;
using UnityEngine.XR;

namespace Aetheria.Magic
{
    /// <summary>
    /// VR: aim the right-hand forward ray at the water surface; after a short dwell (or right trigger click while aimed),
    /// runs <see cref="RemoteWaterInfluence.CastAtAim"/> and <see cref="WaterOrbFormationRitual.BeginFormation"/> so an orb forms at the hand.
    /// On first <see cref="Start"/>, builds finger ray, orb visual, gather, ritual, and remote influence if missing.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class FingerPointWaterOrbMagic : MonoBehaviour
    {
        [SerializeField] bool autoBootstrap = true;
        [SerializeField] XRNode hand = XRNode.RightHand;
        [Tooltip("Seconds the aim ray must stay on water before the orb ritual fires (no trigger).")]
        [SerializeField] float aimDwellSeconds = 0.55f;
        [Tooltip("If true, only a right-hand trigger press while aimed at water starts the ritual (dwell ignored).")]
        [SerializeField] bool requireTriggerOnly;
        [Tooltip("When true (default), a short index–thumb pinch while aimed counts like a trigger — no physical controller trigger needed.")]
        [SerializeField] bool preferHandPinchOverTrigger = true;
        [SerializeField] float pinchConfirmDistance = 0.045f;
        [Header("Finger flick (hand velocity)")]
        [Tooltip("Snap a quick finger flick toward the horizon while aiming at water to cast (works with hand tracking).")]
        [SerializeField] bool enableFlickToCastWhileOnWater = true;
        [SerializeField] float flickMinSpeedMps = 1.05f;
        [Tooltip("Flick velocity vs flat head-forward (XZ). Lower = more directions count.")]
        [SerializeField] float flickMinHeadForwardDot = 0.12f;
        [SerializeField] float cooldownAfterForm = 1.75f;
        [SerializeField] Vector3 fingerRayLocalOffset = new Vector3(0f, 0.02f, 0.07f);
        [SerializeField] float fingerRayMaxDistance = 800f;
        [SerializeField] WaterWorldInfluenceHub influenceHubOverride;

        MagicTargetingRay _fingerRay;
        WaterOrbFormationRitual _ritual;
        RemoteWaterInfluence _remote;
        LayerMask _waterMask;
        float _dwell;
        float _nextAllowedTime = -999f;
        InputDevice _device;
        bool _wasTrigger;
        bool _wasPinch;
        Transform _pinchThumbAnchor;
        bool _didBootstrap;
        bool _bootstrapFailed;
        WaterRippleSpawner _rippleSpawner;
        bool _rippleLinkDone;
        int _rippleWaitFrames;
        Vector3 _flickLastPos;
        float _flickLastTime;
        bool _flickHasPos;

        void Start()
        {
            if (autoBootstrap)
                TryBootstrap();
            RefreshDevice();
        }

        void RefreshDevice()
        {
            var list = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(hand, list);
            _device = list.Count > 0 ? list[0] : default;
        }

        public void TryBootstrap()
        {
            if (_didBootstrap || _bootstrapFailed)
                return;

            var rightController = FindChildTransformByName(transform, "Right Controller");
            if (rightController == null)
                rightController = XRHandAttachmentUtil.FindHandController(transform, true);
            if (rightController == null)
            {
                _bootstrapFailed = true;
                Debug.LogWarning("[FingerPointWaterOrbMagic] No Right Controller / hand-tracking root found under this rig.");
                return;
            }

            var fingerAnchor = FindRightIndexTipOrFallback(rightController);
            _pinchThumbAnchor = XRHandAttachmentUtil.FindThumbTipOrController(rightController);

            _waterMask = BuildWaterSurfaceMask();
            if (_waterMask.value == 0)
            {
                Debug.LogWarning("[FingerPointWaterOrbMagic] No Water or Suimono_Water layer in Tags & Layers — using all layers for aim tests until you assign water meshes to Water or Suimono_Water.");
                _waterMask = ~0;
            }

            var hub = influenceHubOverride != null
                ? influenceHubOverride
                : FindObjectOfType<WaterWorldInfluenceHub>();

            var rayGo = new GameObject("WaterMagicFingerRay");
            rayGo.transform.SetParent(fingerAnchor, false);
            rayGo.transform.localPosition = fingerRayLocalOffset;
            rayGo.transform.localRotation = Quaternion.identity;
            _fingerRay = rayGo.AddComponent<MagicTargetingRay>();
            _fingerRay.SetMaxDistance(fingerRayMaxDistance);

            var orbRoot = new GameObject("HandWaterOrb");
            orbRoot.transform.SetParent(fingerAnchor, false);
            orbRoot.transform.localPosition = Vector3.zero;
            orbRoot.transform.localRotation = Quaternion.identity;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "OrbMesh";
            sphere.transform.SetParent(orbRoot.transform, false);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * 0.12f;
            Destroy(sphere.GetComponent<Collider>());
            var rend = sphere.GetComponent<Renderer>();
            ApplySimpleOrbMaterial(rend);

            var orbVisual = orbRoot.AddComponent<WaterOrbVisual>();
            orbVisual.SetRuntimeTargets(fingerAnchor, rend);

            var services = new GameObject("WaterMagicServices");
            services.transform.SetParent(transform, false);
            services.transform.localPosition = Vector3.zero;
            services.transform.localRotation = Quaternion.identity;

            var gather = services.AddComponent<WaterGatherController>();
            gather.SetRuntimeReferences(orbVisual, null);

            _ritual = services.AddComponent<WaterOrbFormationRitual>();
            _ritual.SetRuntimeReferences(_fingerRay, _waterMask, fingerAnchor, gather);

            _rippleSpawner = services.AddComponent<WaterRippleSpawner>();
            if (SuimonoWaterRuntimeLink.RippleNormalsPrefab != null)
                _rippleSpawner.SetRipplePrefab(SuimonoWaterRuntimeLink.RippleNormalsPrefab);

            _remote = services.AddComponent<RemoteWaterInfluence>();
            _remote.SetRuntimeReferences(_fingerRay, _waterMask, _rippleSpawner, hub, 1f);

            _didBootstrap = true;
        }

        /// <summary>
        /// External callers (e.g. <see cref="Aetheria.VR.HandVelocityFlickDetector"/> UnityEvent) can trigger the same cast.
        /// When <paramref name="requireFingerRayOnWater"/> is true, the aim ray must currently hit water (same as in-hand flick).
        /// </summary>
        public void CastWaterOrbRitualNow(bool requireFingerRayOnWater = true)
        {
            if (!_didBootstrap || _ritual == null)
                return;
            if (Time.time < _nextAllowedTime)
                return;
            if (requireFingerRayOnWater && (_fingerRay == null || _waterMask.value == 0 || !_fingerRay.TryGetHitOnLayer(_waterMask, out _)))
                return;
            FireOnce();
        }

        /// <summary>Inspector-friendly: wire <see cref="Aetheria.VR.HandVelocityFlickDetector.OnFlick"/> for “scene flick” without a water hit.</summary>
        public void CastWaterOrbFromFlickWithoutWaterCheck() => CastWaterOrbRitualNow(false);

        static Transform FindChildTransformByName(Transform root, string exactName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == exactName)
                    return t;
            }

            return null;
        }

        static Transform FindRightIndexTipOrFallback(Transform rightController)
        {
            foreach (var t in rightController.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name;
                if (n.IndexOf("index", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && n.IndexOf("tip", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            }

            foreach (var hint in new[]
                     {
                         "Index Tip", "Index Finger Tip", "IndexTip", "RightHand Index Tip",
                         "PointerTip", "index_tip"
                     })
            {
                foreach (var t in rightController.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return t;
                }
            }

            return rightController;
        }

        static LayerMask BuildWaterSurfaceMask()
        {
            var mask = 0;
            AddLayer(ref mask, "Water");
            AddLayer(ref mask, "Suimono_Water");
            return mask;
        }

        static void AddLayer(ref int mask, string layerName)
        {
            var i = LayerMask.NameToLayer(layerName);
            if (i >= 0)
                mask |= 1 << i;
        }

        static void ApplySimpleOrbMaterial(Renderer rend)
        {
            if (rend == null)
                return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                return;
            var m = new Material(shader);
            m.SetColor("_BaseColor", new Color(0.35f, 0.75f, 0.95f, 0.4f));
            if (m.HasProperty("_Color"))
                m.SetColor("_Color", m.GetColor("_BaseColor"));
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.82f);
            rend.sharedMaterial = m;
        }

        void Update()
        {
            if (!_didBootstrap || _fingerRay == null || _ritual == null)
                return;

            if (!_rippleLinkDone && _rippleSpawner != null)
            {
                if (SuimonoWaterRuntimeLink.RippleNormalsPrefab != null)
                {
                    _rippleSpawner.SetRipplePrefab(SuimonoWaterRuntimeLink.RippleNormalsPrefab);
                    _rippleLinkDone = true;
                }
                else if (++_rippleWaitFrames > 180)
                {
                    _rippleLinkDone = true;
                }
            }

            if (!_device.isValid)
                RefreshDevice();

            if (Time.time < _nextAllowedTime)
                return;

            _device.TryGetFeatureValue(CommonUsages.triggerButton, out var trigger);

            var onWater = _waterMask.value != 0 && _fingerRay.TryGetHitOnLayer(_waterMask, out _);

            if (enableFlickToCastWhileOnWater && onWater && TryConsumeFlickGesture())
            {
                FireOnce();
                _wasTrigger = trigger;
                return;
            }

            bool pinchingNow = preferHandPinchOverTrigger && _pinchThumbAnchor != null && _fingerRay != null
                && Vector3.Distance(_fingerRay.transform.position, _pinchThumbAnchor.position) <= pinchConfirmDistance;
            bool pinchEdge = pinchingNow && !_wasPinch;
            _wasPinch = pinchingNow;

            if (requireTriggerOnly)
            {
                if (onWater && ((!preferHandPinchOverTrigger && trigger && !_wasTrigger) || pinchEdge))
                    FireOnce();
            }
            else
            {
                if (onWater)
                    _dwell += Time.deltaTime;
                else
                    _dwell = 0f;

                var dwellDone = _dwell >= Mathf.Max(0.08f, aimDwellSeconds);
                var triggerFire = onWater && !preferHandPinchOverTrigger && trigger && !_wasTrigger;
                if (dwellDone || triggerFire || (onWater && pinchEdge))
                {
                    _dwell = 0f;
                    FireOnce();
                }
            }

            _wasTrigger = trigger;
        }

        void FireOnce()
        {
            if (_remote != null)
                _remote.CastAtAim();
            _ritual.BeginFormation();
            _nextAllowedTime = Time.time + cooldownAfterForm;
        }

        bool TryConsumeFlickGesture()
        {
            if (_fingerRay == null)
                return false;
            var tip = _fingerRay.transform;
            float t = Time.time;
            Vector3 p = tip.position;
            if (!_flickHasPos)
            {
                _flickLastPos = p;
                _flickLastTime = t;
                _flickHasPos = true;
                return false;
            }

            float dt = t - _flickLastTime;
            if (dt < 1e-4f)
                return false;
            Vector3 v = (p - _flickLastPos) / dt;
            _flickLastPos = p;
            _flickLastTime = t;

            if (v.sqrMagnitude < flickMinSpeedMps * flickMinSpeedMps)
                return false;

            var cam = Camera.main;
            if (cam != null && flickMinHeadForwardDot > -0.99f)
            {
                Vector3 f = cam.transform.forward;
                f.y = 0f;
                if (f.sqrMagnitude > 1e-4f)
                {
                    f.Normalize();
                    Vector3 vh = v;
                    vh.y = 0f;
                    if (vh.sqrMagnitude < 1e-4f)
                        return false;
                    vh.Normalize();
                    if (Vector3.Dot(vh, f) < flickMinHeadForwardDot)
                        return false;
                }
            }

            return true;
        }
    }
}
