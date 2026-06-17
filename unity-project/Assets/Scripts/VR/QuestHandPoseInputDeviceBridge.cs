using UnityEngine;
using UnityEngine.XR;

namespace Aetheria.VR
{
    /// <summary>
    /// Fallback hand target bridge for the body lab.
    /// XR Hands gives full joints, but on some Quest/OpenXR setups the basic XRNode hand pose is available first.
    /// This creates stable hand target transforms that avatar IK and locomotion can resolve without binding to avatar bones.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestHandPoseInputDeviceBridge : MonoBehaviour
    {
        [Header("Visible diagnostics")]
        [SerializeField] private bool showVisibleFallbackMarkers = true;
        [SerializeField] private float wristMarkerRadius = 0.035f;
        [SerializeField] private float tipMarkerRadius = 0.022f;
        [SerializeField] private Color leftColor = new Color(0.25f, 0.75f, 1f, 0.95f);
        [SerializeField] private Color rightColor = new Color(0.35f, 1f, 0.45f, 0.95f);

        [Header("Diagnostics")]
        [SerializeField] private bool verboseLogs = true;

        private Transform _leftRoot;
        private Transform _rightRoot;
        private Transform _leftWrist;
        private Transform _rightWrist;
        private Transform _leftIndexTip;
        private Transform _rightIndexTip;
        private Transform _leftThumbTip;
        private Transform _rightThumbTip;
        private InputDevice _leftDevice;
        private InputDevice _rightDevice;
        private bool _loggedLeftTracked;
        private bool _loggedRightTracked;

        private void Awake()
        {
            EnsureUnderXrOrigin();
            BuildTargets();
        }

        private void Update()
        {
            UpdateSide(XRNode.LeftHand, ref _leftDevice, _leftRoot, _leftWrist, _leftIndexTip, _leftThumbTip, false);
            UpdateSide(XRNode.RightHand, ref _rightDevice, _rightRoot, _rightWrist, _rightIndexTip, _rightThumbTip, true);
        }

        private void EnsureUnderXrOrigin()
        {
            var origin = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin");
            if (origin != null && transform.parent != origin.transform)
                transform.SetParent(origin.transform, worldPositionStays: false);
        }

        private void BuildTargets()
        {
            _leftRoot = CreateRoot("Hand Tracking Left InputDevice");
            _rightRoot = CreateRoot("Hand Tracking Right InputDevice");

            _leftWrist = CreateChild(_leftRoot, "LeftHand Wrist", Vector3.zero, wristMarkerRadius, leftColor);
            _rightWrist = CreateChild(_rightRoot, "RightHand Wrist", Vector3.zero, wristMarkerRadius, rightColor);

            // Approximate fingertips from the hand pose; full XR Hands will override with real joints when available.
            _leftIndexTip = CreateChild(_leftRoot, "LeftHand Index Tip", new Vector3(0.025f, 0.015f, 0.105f), tipMarkerRadius, leftColor);
            _rightIndexTip = CreateChild(_rightRoot, "RightHand Index Tip", new Vector3(-0.025f, 0.015f, 0.105f), tipMarkerRadius, rightColor);
            _leftThumbTip = CreateChild(_leftRoot, "LeftHand Thumb Tip", new Vector3(0.055f, -0.025f, 0.055f), tipMarkerRadius, leftColor);
            _rightThumbTip = CreateChild(_rightRoot, "RightHand Thumb Tip", new Vector3(-0.055f, -0.025f, 0.055f), tipMarkerRadius, rightColor);
        }

        private Transform CreateRoot(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            return go.transform;
        }

        private Transform CreateChild(Transform parent, string name, Vector3 localPosition, float radius, Color color)
        {
            var go = showVisibleFallbackMarkers
                ? GameObject.CreatePrimitive(PrimitiveType.Sphere)
                : new GameObject(name);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * Mathf.Max(0.005f, radius * 2f);

            if (go.TryGetComponent<Collider>(out var col))
                Destroy(col);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                var mat = new Material(shader);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return go.transform;
        }

        private void UpdateSide(
            XRNode node,
            ref InputDevice device,
            Transform root,
            Transform wrist,
            Transform indexTip,
            Transform thumbTip,
            bool right)
        {
            if (!device.isValid)
                device = InputDevices.GetDeviceAtXRNode(node);

            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            bool hasPos = device.isValid && device.TryGetFeatureValue(CommonUsages.devicePosition, out pos);
            bool hasRot = device.isValid && device.TryGetFeatureValue(CommonUsages.deviceRotation, out rot);
            bool tracked = hasPos && hasRot;
            root.gameObject.SetActive(tracked);
            if (!tracked)
                return;

            root.SetPositionAndRotation(pos, rot);
            wrist.SetPositionAndRotation(pos, rot);
            indexTip.rotation = rot;
            thumbTip.rotation = rot;

            if (verboseLogs)
            {
                if (!right && !_loggedLeftTracked)
                {
                    _loggedLeftTracked = true;
                    Debug.Log("[QuestHandPoseInputDeviceBridge] Left hand/device pose acquired.");
                }
                if (right && !_loggedRightTracked)
                {
                    _loggedRightTracked = true;
                    Debug.Log("[QuestHandPoseInputDeviceBridge] Right hand/device pose acquired.");
                }
            }
        }
    }
}
