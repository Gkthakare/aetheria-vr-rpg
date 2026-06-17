using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

namespace Aetheria.Training
{
    /// <summary>
    /// Hand pose for testing: keyboard (1=Pinch, 2=Fist, 3=OpenPalm) in Editor,
    /// or XR controller grip/trigger on Quest. Feeds GestureTrainingStation and APIGestureRecognizer.
    /// </summary>
    public class HandPoseStub : MonoBehaviour
    {
        [Header("Editor: keyboard")]
        [SerializeField] private KeyCode pinchKey = KeyCode.Alpha1;
        [SerializeField] private KeyCode fistKey = KeyCode.Alpha2;
        [SerializeField] private KeyCode openKey = KeyCode.Alpha3;

        [Header("VR: enable in Inspector to send controller input to API")]
        [SerializeField] private bool useControllerInput = false;

        private GestureType _current;
        private InputDevice _leftHand;
        private InputDevice _rightHand;
        private bool _devicesCached;

        private void Update()
        {
            _current = GestureType.None;

            // 1) Keyboard (Editor)
            if (Input.GetKey(pinchKey)) _current = GestureType.Pinch;
            else if (Input.GetKey(fistKey)) _current = GestureType.Fist;
            else if (Input.GetKey(openKey)) _current = GestureType.OpenPalm;

            // 2) XR controller (Quest) – trigger=pinch, grip=fist
            if (_current == GestureType.None && useControllerInput)
            {
                CacheDevices();
                float pinch = 0f, grip = 0f;
                if (_leftHand.isValid)
                {
                    _leftHand.TryGetFeatureValue(CommonUsages.trigger, out float lt);
                    _leftHand.TryGetFeatureValue(CommonUsages.grip, out float lg);
                    pinch = Mathf.Max(pinch, lt);
                    grip = Mathf.Max(grip, lg);
                }
                if (_rightHand.isValid)
                {
                    _rightHand.TryGetFeatureValue(CommonUsages.trigger, out float rt);
                    _rightHand.TryGetFeatureValue(CommonUsages.grip, out float rg);
                    pinch = Mathf.Max(pinch, rt);
                    grip = Mathf.Max(grip, rg);
                }
                if (pinch > 0.5f) _current = GestureType.Pinch;
                else if (grip > 0.5f) _current = GestureType.Fist;
                else if (grip < 0.2f && pinch < 0.2f) _current = GestureType.OpenPalm;
                // else: partial press = None (avoids spamming API when idle)
            }
        }

        private void CacheDevices()
        {
            if (_devicesCached) return;
            var left = new List<InputDevice>();
            var right = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, left);
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, right);
            if (left.Count > 0) _leftHand = left[0];
            if (right.Count > 0) _rightHand = right[0];
            _devicesCached = true;
        }

        public HandPoseData GetPose()
        {
            float pinch = _current == GestureType.Pinch ? 1f : 0f;
            float grip = _current == GestureType.Fist ? 1f : (_current == GestureType.OpenPalm ? 0f : 0.5f);
            return new HandPoseData
            {
                IsLeft = false,
                PalmPosition = transform.position + Vector3.forward,
                PalmNormal = Vector3.forward,
                PinchStrength = pinch,
                GripStrength = grip,
                IsTracked = _current != GestureType.None
            };
        }
    }
}
