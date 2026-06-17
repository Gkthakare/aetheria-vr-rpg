#if HAS_XR_HANDS
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using Unity.XR.CoreUtils;
using System.Collections.Generic;

namespace Aetheria.VR
{
    /// <summary>
    /// Bridges the Unity XR Hands subsystem (com.unity.xr.hands) into a simple,
    /// always-visible representation that:
    ///
    /// 1) Draws a small colored sphere at every tracked hand joint, so the
    ///    player can SEE their own hands in VR — independent of any IK rig.
    ///
    /// 2) Creates GameObjects with the exact names that
    ///    <see cref="XRHandAttachmentUtil"/> searches for ("Hand Tracking Left",
    ///    "Hand Tracking Right", "LeftHand Index Tip", "RightHand Index Tip",
    ///    "LeftHand Thumb Tip", "RightHand Thumb Tip"), so the existing
    ///    <see cref="VRPlayerController"/> pinch-to-teleport, hand-move and
    ///    snap-turn logic activates automatically with no further wiring.
    ///
    /// Attach once to a child of the XR Origin (or any persistent scene root);
    /// it is fully self-bootstrapping.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class XRHandsRuntimeBridge : MonoBehaviour
    {
        [Header("Visualization")]
        [Tooltip("Sphere radius in meters for each joint marker.")]
        [SerializeField] private float jointRadius = 0.008f;

        [Tooltip("Sphere radius in meters for the larger fingertip markers.")]
        [SerializeField] private float tipRadius = 0.014f;

        [Tooltip("Color used for left-hand joint spheres.")]
        [SerializeField] private Color leftColor = new Color(0.35f, 0.75f, 1f, 1f);

        [Tooltip("Color used for right-hand joint spheres.")]
        [SerializeField] private Color rightColor = new Color(0.45f, 1f, 0.55f, 1f);

        [Header("Diagnostics")]
        [SerializeField] private bool verboseLogs = true;

        private XRHandSubsystem _subsystem;
        private Transform _leftRoot;
        private Transform _rightRoot;
        private Transform _leftIndexTip;
        private Transform _rightIndexTip;
        private Transform _leftThumbTip;
        private Transform _rightThumbTip;

        private readonly Dictionary<XRHandJointID, Transform> _leftJointTfs = new();
        private readonly Dictionary<XRHandJointID, Transform> _rightJointTfs = new();

        private bool _ready;
        private float _nextSubsystemRetry;

        private void Awake()
        {
            EnsureUnderXrOrigin();
        }

        /// <summary>
        /// <see cref="VRPlayerController.ResolveHandAnchors"/> only searches under the XR
        /// Origin transform. If this bridge lives at scene root, anchors are never found
        /// (invisible pinch, stuck movement) even when joint spheres render correctly.
        /// </summary>
        private void EnsureUnderXrOrigin()
        {
            var origin = FindObjectOfType<XROrigin>();
            if (origin == null)
            {
                if (verboseLogs)
                    Debug.LogWarning(
                        "[XRHandsRuntimeBridge] No XROrigin in scene. Parent this GameObject under your XR Rig so VRPlayerController can find 'Hand Tracking Left/Right'.");
                return;
            }

            if (transform.parent == origin.transform)
                return;

            transform.SetParent(origin.transform, worldPositionStays: true);
            if (verboseLogs)
                Debug.Log(
                    $"[XRHandsRuntimeBridge] Reparented under XR Origin '{origin.name}' so hand anchors are discoverable.");
        }

        private void OnEnable()
        {
            TryAcquireSubsystem(forceLog: true);
        }

        private void Update()
        {
            if (_subsystem == null)
            {
                if (Time.unscaledTime >= _nextSubsystemRetry)
                {
                    _nextSubsystemRetry = Time.unscaledTime + 1.0f;
                    TryAcquireSubsystem(forceLog: false);
                }
                return;
            }

            if (!_ready) return;

            UpdateHand(_subsystem.leftHand,  _leftRoot,  _leftJointTfs,  isRight: false);
            UpdateHand(_subsystem.rightHand, _rightRoot, _rightJointTfs, isRight: true);
        }

        private void TryAcquireSubsystem(bool forceLog)
        {
            var loader = XRGeneralSettings.Instance != null
                ? XRGeneralSettings.Instance.Manager?.activeLoader
                : null;
            if (loader == null)
            {
                if (forceLog && verboseLogs)
                    Debug.Log("[XRHandsRuntimeBridge] No active XR loader yet — will retry.");
                return;
            }

            _subsystem = loader.GetLoadedSubsystem<XRHandSubsystem>();
            if (_subsystem == null)
            {
                if (forceLog && verboseLogs)
                    Debug.Log("[XRHandsRuntimeBridge] XRHandSubsystem not loaded yet. Make sure 'Hand Tracking Subsystem' OpenXR feature is enabled in Project Settings -> XR Plug-in Management -> OpenXR (Android tab).");
                return;
            }

            BuildVisuals();
            _ready = true;
            if (verboseLogs)
                Debug.Log("[XRHandsRuntimeBridge] XRHandSubsystem acquired. Hand markers active.");
        }

        private void BuildVisuals()
        {
            _leftRoot  = new GameObject("Hand Tracking Left").transform;
            _rightRoot = new GameObject("Hand Tracking Right").transform;
            _leftRoot.SetParent(transform, worldPositionStays: false);
            _rightRoot.SetParent(transform, worldPositionStays: false);

            CreateAllJoints(_leftRoot,  _leftJointTfs,  leftColor,  side: "LeftHand");
            CreateAllJoints(_rightRoot, _rightJointTfs, rightColor, side: "RightHand");

            _leftIndexTip  = _leftJointTfs[XRHandJointID.IndexTip];
            _rightIndexTip = _rightJointTfs[XRHandJointID.IndexTip];
            _leftThumbTip  = _leftJointTfs[XRHandJointID.ThumbTip];
            _rightThumbTip = _rightJointTfs[XRHandJointID.ThumbTip];

            // Add discoverable aliases so XRHandAttachmentUtil's name-based
            // search ("LeftHand Index Tip" / "RightHand Thumb Tip" / etc.)
            // resolves to the right joint transforms.
            AddAliasUnder(_leftRoot,  "LeftHand Index Tip", _leftIndexTip);
            AddAliasUnder(_rightRoot, "RightHand Index Tip", _rightIndexTip);
            AddAliasUnder(_leftRoot,  "LeftHand Thumb Tip",  _leftThumbTip);
            AddAliasUnder(_rightRoot, "RightHand Thumb Tip", _rightThumbTip);

            if (_leftJointTfs.TryGetValue(XRHandJointID.Wrist, out var leftWrist))
                AddAliasUnder(_leftRoot, "LeftHand Wrist", leftWrist);
            if (_rightJointTfs.TryGetValue(XRHandJointID.Wrist, out var rightWrist))
                AddAliasUnder(_rightRoot, "RightHand Wrist", rightWrist);
            if (_leftJointTfs.TryGetValue(XRHandJointID.Palm, out var leftPalm))
                AddAliasUnder(_leftRoot, "LeftHand Palm", leftPalm);
            if (_rightJointTfs.TryGetValue(XRHandJointID.Palm, out var rightPalm))
                AddAliasUnder(_rightRoot, "RightHand Palm", rightPalm);
        }

        private void CreateAllJoints(Transform parent, Dictionary<XRHandJointID, Transform> tfs, Color color, string side)
        {
            for (int i = (int)XRHandJointID.BeginMarker; i < (int)XRHandJointID.EndMarker; i++)
            {
                var id = (XRHandJointID)i;
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = side + "_" + id.ToString();
                Object.Destroy(go.GetComponent<Collider>());

                bool isTip = id == XRHandJointID.IndexTip || id == XRHandJointID.ThumbTip ||
                             id == XRHandJointID.MiddleTip || id == XRHandJointID.RingTip ||
                             id == XRHandJointID.LittleTip;
                float r = (isTip ? tipRadius : jointRadius) * 2f;
                go.transform.localScale = new Vector3(r, r, r);
                go.transform.SetParent(parent, worldPositionStays: false);

                var mr = go.GetComponent<MeshRenderer>();
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                if (mat.shader == null)
                    mat = new Material(Shader.Find("Unlit/Color"));
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;

                tfs[id] = go.transform;
            }
        }

        private static void AddAliasUnder(Transform parent, string aliasName, Transform target)
        {
            var alias = new GameObject(aliasName).transform;
            alias.SetParent(target, worldPositionStays: false);
        }

        private void UpdateHand(XRHand hand, Transform root, Dictionary<XRHandJointID, Transform> tfs, bool isRight)
        {
            bool tracked = hand.isTracked;
            for (int i = (int)XRHandJointID.BeginMarker; i < (int)XRHandJointID.EndMarker; i++)
            {
                var id = (XRHandJointID)i;
                if (!tfs.TryGetValue(id, out var jointTf) || jointTf == null) continue;
                var mr = jointTf.GetComponent<MeshRenderer>();

                if (!tracked)
                {
                    if (mr != null && mr.enabled) mr.enabled = false;
                    continue;
                }

                var joint = hand.GetJoint(id);
                if (!joint.TryGetPose(out Pose pose))
                {
                    if (mr != null && mr.enabled) mr.enabled = false;
                    continue;
                }

                jointTf.SetPositionAndRotation(pose.position, pose.rotation);
                if (mr != null && !mr.enabled) mr.enabled = true;
            }
        }
    }
}
#endif
