using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

namespace Aetheria.VR
{
    /// <summary>
    /// Teleport via gestures on the XR rig:
    /// - One pinch (single hand edge) = weapon room
    /// - Both pinches held together = outside world
    /// Default uses real index–thumb distance (hand tracking). Optional controller grip fallback.
    /// </summary>
    public class GestureTeleport : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private Transform weaponRoomTarget;
        [SerializeField] private Vector3 weaponRoomPosition = new Vector3(0f, 1.2f, 6f);
        [SerializeField] private Vector3 outsideWorldPosition = new Vector3(8f, 1.2f, 8f);

        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private float cooldownSeconds = 1f;

        [Header("Hands-first")]
        [Tooltip("Use index–thumb pinch distance (hand tracking). When off, uses XR grip button / grip axis only.")]
        [SerializeField] private bool preferFingerPinch = true;
        [SerializeField] private float pinchHoldDistance = 0.042f;

        private InputDevice _leftHand;
        private InputDevice _rightHand;
        private bool _leftPinching;
        private bool _rightPinching;
        private float _lastTeleportTime;
        private Transform _leftIndex;
        private Transform _leftThumb;
        private Transform _rightIndex;
        private Transform _rightThumb;

        /// <summary>Set outside world teleport position at runtime (e.g. from IslandWorldBuilder).</summary>
        public void SetOutsideWorldPosition(Vector3 pos) => outsideWorldPosition = pos;

        private void Start()
        {
            RefreshDevices();
            if (player == null) player = transform;
            ResolveFingerTips();
        }

        private void Update()
        {
            if (player == null) return;
            if (Time.time - _lastTeleportTime < cooldownSeconds) return;

            if (Time.frameCount % 20 == 0)
            {
                RefreshDevices();
                if (preferFingerPinch)
                    ResolveFingerTips();
            }

            bool wasLeft = _leftPinching;
            bool wasRight = _rightPinching;

            if (preferFingerPinch && FingerTipsReady())
            {
                _leftPinching = IsPinched(_leftIndex, _leftThumb);
                _rightPinching = IsPinched(_rightIndex, _rightThumb);
            }
            else
            {
                _leftPinching = _leftHand.isValid && _leftHand.TryGetFeatureValue(CommonUsages.gripButton, out bool l) && l;
                _rightPinching = _rightHand.isValid && _rightHand.TryGetFeatureValue(CommonUsages.gripButton, out bool r) && r;
            }

            bool leftJustPressed = _leftPinching && !wasLeft;
            bool rightJustPressed = _rightPinching && !wasRight;

            if (leftJustPressed || rightJustPressed)
            {
                bool twoPinches = _leftPinching && _rightPinching;
                Vector3 target = twoPinches
                    ? outsideWorldPosition
                    : (weaponRoomTarget != null ? weaponRoomTarget.position : weaponRoomPosition);
                target = new Vector3(target.x, target.y + 0.1f, target.z);
                player.position = target;
                _lastTeleportTime = Time.time;
            }
        }

        void RefreshDevices()
        {
            var left = new List<InputDevice>();
            var right = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, left);
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, right);
            if (left.Count > 0) _leftHand = left[0];
            if (right.Count > 0) _rightHand = right[0];
        }

        void ResolveFingerTips()
        {
            var root = player != null ? player : transform;
            var lHand = XRHandAttachmentUtil.FindHandController(root, false);
            var rHand = XRHandAttachmentUtil.FindHandController(root, true);
            _leftIndex = XRHandAttachmentUtil.FindIndexTipOrController(lHand);
            _leftThumb = lHand != null ? XRHandAttachmentUtil.FindThumbTipOrController(lHand) : null;
            _rightIndex = XRHandAttachmentUtil.FindIndexTipOrController(rHand);
            _rightThumb = rHand != null ? XRHandAttachmentUtil.FindThumbTipOrController(rHand) : null;
        }

        bool IsPinched(Transform index, Transform thumb)
        {
            if (index == null || thumb == null || index == thumb)
                return false;
            return Vector3.Distance(index.position, thumb.position) <= pinchHoldDistance;
        }

        bool FingerTipsReady() =>
            _leftIndex != null && _leftThumb != null && _leftIndex != _leftThumb
            && _rightIndex != null && _rightThumb != null && _rightIndex != _rightThumb;
    }
}
