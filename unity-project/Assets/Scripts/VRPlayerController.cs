using UnityEngine;
using UnityEngine.XR;

namespace Aetheria.VR
{
    /// <summary>
    /// Basic VR player controller for Quest 3 with OpenXR support.
    /// Handles teleportation movement and basic interactions.
    /// 
    /// Setup Instructions:
    /// 1. Attach this script to the XR Origin GameObject
    /// 2. Ensure XR Origin has XR Origin component
    /// 3. Add XR Interaction Manager to the scene
    /// 4. Configure Input Actions for teleportation
    /// </summary>
    public class VRPlayerController : MonoBehaviour
    {
        [Header("VR References")]
        [SerializeField] private Transform xrOrigin;
        [SerializeField] private Camera xrCamera;
        
        [Header("Movement Settings")]
        [SerializeField] private float teleportDistance = 10f;
        [SerializeField] private LayerMask groundLayerMask = 1;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        
        private InputDevice leftController;
        private InputDevice rightController;
        private bool isLeftControllerPressed = false;
        private bool isRightControllerPressed = false;
        
        /// <summary>
        /// Initialize VR controller references
        /// </summary>
        void Start()
        {
            InitializeVRControllers();
            LogDebug("VRPlayerController initialized");
        }
        
        /// <summary>
        /// Update controller input and handle teleportation
        /// </summary>
        void Update()
        {
            UpdateControllerInput();
            HandleTeleportation();
        }
        
        /// <summary>
        /// Initialize VR controller devices
        /// </summary>
        private void InitializeVRControllers()
        {
            var leftHandDevices = new List<InputDevice>();
            var rightHandDevices = new List<InputDevice>();
            
            InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftHandDevices);
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightHandDevices);
            
            if (leftHandDevices.Count > 0)
                leftController = leftHandDevices[0];
            
            if (rightHandDevices.Count > 0)
                rightController = rightHandDevices[0];
            
            LogDebug($"Left controller found: {leftController.isValid}");
            LogDebug($"Right controller found: {rightController.isValid}");
        }
        
        /// <summary>
        /// Update controller input states
        /// </summary>
        private void UpdateControllerInput()
        {
            // Check left controller trigger
            if (leftController.isValid)
            {
                leftController.TryGetFeatureValue(CommonUsages.triggerButton, out bool leftTriggerPressed);
                isLeftControllerPressed = leftTriggerPressed;
            }
            
            // Check right controller trigger
            if (rightController.isValid)
            {
                rightController.TryGetFeatureValue(CommonUsages.triggerButton, out bool rightTriggerPressed);
                isRightControllerPressed = rightTriggerPressed;
            }
        }
        
        /// <summary>
        /// Handle teleportation based on controller input
        /// </summary>
        private void HandleTeleportation()
        {
            // Simple teleportation: press trigger to move forward
            if (isLeftControllerPressed || isRightControllerPressed)
            {
                TeleportForward();
            }
        }
        
        /// <summary>
        /// Teleport the player forward by teleportDistance
        /// </summary>
        private void TeleportForward()
        {
            if (xrOrigin == null) return;
            
            Vector3 forward = xrCamera.transform.forward;
            forward.y = 0; // Keep movement on horizontal plane
            forward.Normalize();
            
            Vector3 newPosition = xrOrigin.position + (forward * teleportDistance);
            
            // Simple ground check - in a real implementation, you'd use raycast
            xrOrigin.position = newPosition;
            
            LogDebug($"Teleported to: {newPosition}");
        }
        
        /// <summary>
        /// Debug logging helper
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[VRPlayerController] {message}");
            }
        }
        
        /// <summary>
        /// Public method to get current player position
        /// </summary>
        public Vector3 GetPlayerPosition()
        {
            return xrOrigin != null ? xrOrigin.position : Vector3.zero;
        }
        
        /// <summary>
        /// Public method to get current player rotation
        /// </summary>
        public Quaternion GetPlayerRotation()
        {
            return xrOrigin != null ? xrOrigin.rotation : Quaternion.identity;
        }
    }
}
