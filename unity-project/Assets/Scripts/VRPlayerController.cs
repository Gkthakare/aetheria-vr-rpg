using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;
using Aetheria.World;

namespace Aetheria.VR
{
    /// <summary>
    /// Basic VR player controller for Quest 3 with OpenXR support.
    /// Smooth move, snap turn, teleport, optional head-directed fly.
    /// 
    /// <b>Hands-first (default):</b> with <see cref="preferHandTrackingOnly"/>, thumbsticks and controller
    /// triggers are ignored when both hand anchors resolve — movement uses the <b>left hand</b> vs chest offset,
    /// snap turn uses <b>right hand</b> lateral offset, teleport uses <b>pinch</b>. Turn that off if you want
    /// Quest controllers to drive move/turn/trigger when they are tracked.
    /// 
    /// Setup: attach to the XR Origin; enable OpenXR + hand tracking in project settings.
    /// </summary>
    public class VRPlayerController : MonoBehaviour
    {
        [Header("VR References")]
        [SerializeField] private Transform xrOrigin;
        [SerializeField] private Camera xrCamera;
        
        [Header("Movement Settings")]
        [SerializeField] private float teleportDistance = 10f;
        [SerializeField] private LayerMask groundLayerMask = 1;
        [SerializeField] private float teleportCooldownSeconds = 0.35f;
        [SerializeField] private bool enableSmoothLocomotion = true;
        [SerializeField] private float moveSpeed = 2.2f;
        [SerializeField] private bool moveRelativeToHeadYaw = true;
        [SerializeField] private bool enableSnapTurn = true;
        [SerializeField] private float snapTurnDegrees = 30f;
        [SerializeField] private float snapTurnCooldownSeconds = 0.18f;

        [Header("Hand tracking — no controllers")]
        [Tooltip("When both hand anchors exist, ignore thumbsticks and controller triggers; use only hand locomotion + pinches. Disable to allow controllers when they are tracked.")]
        [SerializeField] private bool preferHandTrackingOnly = true;
        [Tooltip("Move from left hand vs chest, snap-turn from right hand lateral offset, pinch = teleport tap.")]
        [SerializeField] private bool enableHandTrackingLocomotion = true;
        [Tooltip("Chest reference point in camera-local space (meters). Tune so the hand at rest sits inside the move dead zone.")]
        [SerializeField] private Vector3 handMoveChestReferenceLocal = new Vector3(-0.12f, -0.38f, 0.22f);
        [SerializeField] private float handMoveDeadZoneMeters = 0.07f;
        [SerializeField] private float handMoveMaxOffsetMeters = 0.42f;
        [Tooltip("Right hand camera-local X beyond ±this value requests a snap turn (meters).")]
        [SerializeField] private float handSnapTurnLateralThresholdMeters = 0.22f;
        [SerializeField] private float handPinchTeleportDistanceMeters = 0.042f;
        [SerializeField] private bool snapToGroundOnStart = true;
        [SerializeField] private float spawnSafetyMinY = 1.2f;
        [SerializeField] private float groundProbeStartHeight = 20f;
        [SerializeField] private float groundOffsetY = 0.1f;

        [Header("Head-directed fly (gaze climb / look-down land)")]
        [Tooltip("When enabled, you fly along your look direction: tilt up to climb, level to coast, tilt down to descend and land. While feet are on the ground you must look up past Takeoff Pitch before you rise; then the softer Min Pitch gate applies in the air. Smooth locomotion stays snapped until you are actually airborne.")]
        [SerializeField] private bool enableHeadDirectedFly = true;
        [SerializeField] private float headFlyMaxRiseSpeed = 6.25f;
        [SerializeField] private float headFlyMaxDescentSpeed = 7.5f;
        [Tooltip("Look at least this far above the horizon (degrees) while feet are on/near the ground before vertical takeoff is allowed.")]
        [SerializeField] private float headFlyTakeoffPitchDegrees = 30f;
        [Tooltip("After takeoff, minimum look angle above horizontal (degrees) before rise strength ramps in.")]
        [SerializeField] private float headFlyMinPitchUpDegrees = 7f;
        [Tooltip("Look this far below horizontal (degrees) to apply descent along gaze (down + forward).")]
        [SerializeField] private float headFlyPitchDownDegrees = 12f;
        [Tooltip("Within this band of pitch around level, no head-driven vertical motion (reduces jitter).")]
        [SerializeField] private float headFlyNeutralDeadbandDegrees = 4f;
        [Tooltip("How strongly rise speed ramps between threshold and straight up (higher = gentler near horizon).")]
        [SerializeField] private float headFlyRiseEaseExponent = 1.35f;
        [SerializeField] private float headFlyGroundCheckDistance = 2.8f;
        [SerializeField] private float headFlyGroundedDistance = 0.42f;
        [Tooltip("While descending, snap to ground when ray hits within this height above the hit.")]
        [SerializeField] private float headFlyLandSnapHeight = 0.55f;
        [Tooltip("Low-pass on gaze pitch (Hz). Higher = snappier, lower = smoother flight. 0 = no smoothing.")]
        [SerializeField] private float headFlyPitchSmoothingHz = 12f;
        [Tooltip("While grounded: seconds you must keep looking above takeoff pitch before lift starts (reduces accidental hops).")]
        [SerializeField] private float headFlyTakeoffCommitSeconds = 0.15f;

        [Header("Hands + Body Debug")]
        [SerializeField] private bool showHandDebugProxies = true;
        [SerializeField] private float handProxySize = 0.04f;
        [SerializeField] private bool forceCameraRelativeHandProxyFallback = true;
        [Tooltip("Verbose: prints head/hand world pose on an interval. Off by default so Play Mode console stays readable.")]
        [SerializeField] private bool logBodyPose = false;
        [SerializeField] private float bodyPoseLogIntervalSeconds = 0.25f;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showRuntimeStatusHud = true;
        
        private InputDevice leftController;
        private InputDevice rightController;
        private bool isLeftControllerPressed = false;
        private bool isRightControllerPressed = false;
        private bool wasLeftControllerPressed = false;
        private bool wasRightControllerPressed = false;
        private float nextTeleportTime;
        private float nextSnapTurnTime;
        private float nextBodyPoseLogTime;
        private GameObject leftProxy;
        private GameObject rightProxy;
        private Transform leftHandAnchor;
        private Transform rightHandAnchor;
        private Transform leftThumbAnchor;
        private Transform rightThumbAnchor;
        private TextMesh statusHud;
        private float _headFlySmoothedUpY;
        private bool _headFlySmoothInitialized;
        private float _headFlyTakeoffCommitTimer;
        
        /// <summary>
        /// Initialize VR controller references
        /// </summary>
        void Start()
        {
            if (xrOrigin == null) xrOrigin = transform;
            if (xrCamera == null) xrCamera = Camera.main;
            InitializeVRControllers();
            ResolveHandAnchors();
            if (snapToGroundOnStart)
            {
                // IslandWorldBuilder moves the rig after a few frames and raycasts Y there; snapping here first
                // only hits scene origin and logs a misleading position.
                var island = FindObjectOfType<IslandWorldBuilder>();
                if (island != null && island.RepositionsVrOriginOnStart)
                    LogDebug("Startup ground snap skipped (IslandWorldBuilder will reposition rig).");
                else
                    SnapRigToGround("startup");
            }
            if (showHandDebugProxies)
                BuildHandProxies();
            if (showRuntimeStatusHud)
                BuildStatusHud();
            LogDebug("VRPlayerController initialized");
        }
        
        /// <summary>
        /// Update controller input and handle teleportation
        /// </summary>
        void Update()
        {
            TryReinitializeControllersIfNeeded();
            UpdateControllerInput();
            HandleHeadDirectedFly();
            HandleSmoothLocomotion();
            UpdateHandProxies();
            HandleTeleportation();
            HandleSnapTurn();
            LogBodyPoseIfNeeded();
            UpdateStatusHud();
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

        private void ResolveHandAnchors()
        {
            var leftRoot = XRHandAttachmentUtil.FindHandController(transform, false);
            var rightRoot = XRHandAttachmentUtil.FindHandController(transform, true);
            leftHandAnchor = XRHandAttachmentUtil.FindIndexTipOrController(leftRoot);
            rightHandAnchor = XRHandAttachmentUtil.FindIndexTipOrController(rightRoot);
            leftThumbAnchor = leftRoot != null ? XRHandAttachmentUtil.FindThumbTipOrController(leftRoot) : null;
            rightThumbAnchor = rightRoot != null ? XRHandAttachmentUtil.FindThumbTipOrController(rightRoot) : null;
        }
        
        /// <summary>
        /// Update controller input states
        /// </summary>
        private void UpdateControllerInput()
        {
            bool leftTrigger = false;
            if (enableHandTrackingLocomotion && TryHandPinch(leftHandAnchor, leftThumbAnchor))
                leftTrigger = true;
            else if (!preferHandTrackingOnly && leftController.isValid)
                leftController.TryGetFeatureValue(CommonUsages.triggerButton, out leftTrigger);

            bool rightTrigger = false;
            if (enableHandTrackingLocomotion && TryHandPinch(rightHandAnchor, rightThumbAnchor))
                rightTrigger = true;
            else if (!preferHandTrackingOnly && rightController.isValid)
                rightController.TryGetFeatureValue(CommonUsages.triggerButton, out rightTrigger);

            isLeftControllerPressed = leftTrigger;
            isRightControllerPressed = rightTrigger;
        }
        
        /// <summary>
        /// Handle teleportation based on controller input
        /// </summary>
        private void HandleTeleportation()
        {
            bool pressedThisFrame =
                (isLeftControllerPressed && !wasLeftControllerPressed) ||
                (isRightControllerPressed && !wasRightControllerPressed);
            if (pressedThisFrame && Time.time >= nextTeleportTime)
            {
                TeleportForward();
                nextTeleportTime = Time.time + Mathf.Max(0.05f, teleportCooldownSeconds);
            }

            wasLeftControllerPressed = isLeftControllerPressed;
            wasRightControllerPressed = isRightControllerPressed;
        }

        private void HandleSmoothLocomotion()
        {
            if (!enableSmoothLocomotion || xrOrigin == null)
                return;

            Vector2 moveAxis = Vector2.zero;
            bool allowThumbstick = !(preferHandTrackingOnly && HandsResolvedForHandsOnlyLocomotion());
            if (allowThumbstick && leftController.isValid &&
                leftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stickMove))
            {
                moveAxis = stickMove;
                if (moveAxis.sqrMagnitude < 0.0004f)
                    return;
            }
            else if (enableHandTrackingLocomotion && TryGetHandMoveAxisFromLeftHand(out Vector2 handMove))
            {
                moveAxis = handMove;
            }
            else
                return;

            Vector3 basisForward;
            if (moveRelativeToHeadYaw && xrCamera != null)
            {
                basisForward = xrCamera.transform.forward;
                basisForward.y = 0f;
                if (basisForward.sqrMagnitude < 0.0001f)
                    basisForward = xrOrigin.forward;
            }
            else
            {
                basisForward = xrOrigin.forward;
            }

            basisForward.Normalize();
            Vector3 basisRight = new Vector3(basisForward.z, 0f, -basisForward.x);
            Vector3 moveDir = (basisForward * moveAxis.y + basisRight * moveAxis.x);
            if (moveDir.sqrMagnitude > 1f)
                moveDir.Normalize();

            Vector3 desired = xrOrigin.position + moveDir * (moveSpeed * Time.deltaTime);
            if (SkipGroundSnapForHeadFly())
                xrOrigin.position = desired;
            else
                xrOrigin.position = ResolveGroundedPosition(desired);
        }

        private void HandleSnapTurn()
        {
            if (!enableSnapTurn || xrOrigin == null || Time.time < nextSnapTurnTime)
                return;

            bool allowTurnStick = !(preferHandTrackingOnly && HandsResolvedForHandsOnlyLocomotion());
            if (allowTurnStick && rightController.isValid &&
                rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 turnAxis))
            {
                const float gate = 0.75f;
                if (turnAxis.x > gate)
                {
                    ApplyYawTurn(snapTurnDegrees);
                    nextSnapTurnTime = Time.time + Mathf.Max(0.05f, snapTurnCooldownSeconds);
                }
                else if (turnAxis.x < -gate)
                {
                    ApplyYawTurn(-snapTurnDegrees);
                    nextSnapTurnTime = Time.time + Mathf.Max(0.05f, snapTurnCooldownSeconds);
                }
                return;
            }

            if (enableHandTrackingLocomotion && TryGetHandSnapTurnFromRightHand(out int directionSign) && directionSign != 0)
            {
                ApplyYawTurn(snapTurnDegrees * directionSign);
                nextSnapTurnTime = Time.time + Mathf.Max(0.05f, snapTurnCooldownSeconds);
            }
        }

        private void ApplyYawTurn(float degrees)
        {
            Vector3 pivot = xrCamera != null ? xrCamera.transform.position : xrOrigin.position;
            xrOrigin.RotateAround(pivot, Vector3.up, degrees);
            if (!SkipGroundSnapForHeadFly())
                xrOrigin.position = ResolveGroundedPosition(xrOrigin.position);
        }
        
        /// <summary>
        /// Teleport the player forward by teleportDistance
        /// </summary>
        private void TeleportForward()
        {
            if (xrOrigin == null || xrCamera == null) return;
            
            Vector3 forward = xrCamera.transform.forward;
            forward.y = 0; // Keep movement on horizontal plane
            if (forward.sqrMagnitude < 0.0001f)
                forward = xrOrigin.forward;
            forward.Normalize();
            
            Vector3 rawTarget = xrOrigin.position + (forward * teleportDistance);
            Vector3 newPosition = ResolveGroundedPosition(rawTarget);
            xrOrigin.position = newPosition;
            
            LogDebug($"Teleported to: {newPosition}");
        }

        private Vector3 ResolveGroundedPosition(Vector3 desired)
        {
            Vector3 origin = desired + Vector3.up * Mathf.Max(2f, groundProbeStartHeight);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 300f, groundLayerMask))
                return hit.point + Vector3.up * Mathf.Max(0f, groundOffsetY);

            // Fallback: never let spawn sit below safety Y (avoids being stuck in water plane).
            return new Vector3(desired.x, Mathf.Max(spawnSafetyMinY, desired.y), desired.z);
        }

        /// <summary>True when the rig is off the ground — smooth move / snap-turn skip ResolveGrounded so you can stay in the air (e.g. bow use).</summary>
        private bool SkipGroundSnapForHeadFly()
        {
            if (!enableHeadDirectedFly || xrOrigin == null)
                return false;
            return !IsFeetNearGround(headFlyGroundCheckDistance, headFlyGroundedDistance);
        }

        private bool IsFeetNearGround(float maxRayDistance, float maxHitDistanceFromFeet)
        {
            if (xrOrigin == null)
                return true;
            Vector3 p = xrOrigin.position;
            if (!Physics.Raycast(p + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, maxRayDistance, groundLayerMask))
                return false;
            return hit.distance <= maxHitDistanceFromFeet;
        }

        private void HandleHeadDirectedFly()
        {
            if (!enableHeadDirectedFly || xrOrigin == null || xrCamera == null)
                return;

            Vector3 forward = xrCamera.transform.forward;
            if (forward.sqrMagnitude < 1e-6f)
                return;
            forward.Normalize();

            float fyRaw = forward.y;
            if (!_headFlySmoothInitialized)
            {
                _headFlySmoothedUpY = fyRaw;
                _headFlySmoothInitialized = true;
            }

            float hz = Mathf.Max(0f, headFlyPitchSmoothingHz);
            if (hz > 0.01f)
            {
                float a = 1f - Mathf.Exp(-hz * Time.deltaTime);
                _headFlySmoothedUpY = Mathf.Lerp(_headFlySmoothedUpY, fyRaw, a);
            }
            else
            {
                _headFlySmoothedUpY = fyRaw;
            }

            float fy = _headFlySmoothedUpY;
            float deadSin = Mathf.Sin(Mathf.Clamp(headFlyNeutralDeadbandDegrees, 0f, 45f) * Mathf.Deg2Rad);
            float upSin = Mathf.Sin(Mathf.Clamp(headFlyMinPitchUpDegrees, 0.5f, 89f) * Mathf.Deg2Rad);
            float takeoffSin = Mathf.Sin(Mathf.Clamp(Mathf.Max(headFlyMinPitchUpDegrees + 0.5f, headFlyTakeoffPitchDegrees), 0.5f, 89f) * Mathf.Deg2Rad);
            float downSin = -Mathf.Sin(Mathf.Clamp(headFlyPitchDownDegrees, 0.5f, 89f) * Mathf.Deg2Rad);
            bool grounded = IsFeetNearGround(headFlyGroundCheckDistance, headFlyGroundedDistance);

            float commitNeed = Mathf.Max(0f, headFlyTakeoffCommitSeconds);
            if (grounded && commitNeed > 0f)
            {
                if (fyRaw > takeoffSin)
                    _headFlyTakeoffCommitTimer += Time.deltaTime;
                else
                    _headFlyTakeoffCommitTimer = 0f;
            }
            else if (!grounded)
            {
                _headFlyTakeoffCommitTimer = commitNeed;
            }

            bool takeoffCommitted = commitNeed <= 0f || _headFlyTakeoffCommitTimer >= commitNeed;

            float speed = 0f;
            if (fy > upSin)
            {
                if (grounded && fy <= takeoffSin)
                    return;
                if (grounded && !takeoffCommitted)
                    return;
                float span = Mathf.Max(0.01f, 1f - upSin);
                float t = Mathf.Clamp01((fy - upSin) / span);
                t = Mathf.Pow(t, Mathf.Max(1f, headFlyRiseEaseExponent));
                speed = headFlyMaxRiseSpeed * t;
            }
            else if (fy < downSin && Mathf.Abs(fy) > deadSin)
            {
                if (grounded)
                    return;
                float span = Mathf.Max(0.01f, 1f + downSin);
                float t = Mathf.Clamp01((downSin - fy) / span);
                speed = -headFlyMaxDescentSpeed * t;
            }

            if (Mathf.Abs(speed) < 1e-5f)
                return;

            Vector3 delta = forward * (speed * Time.deltaTime);
            Vector3 next = xrOrigin.position + delta;

            if (speed < 0f)
            {
                Vector3 from = next + Vector3.up * 0.35f;
                if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, headFlyGroundCheckDistance + 2f, groundLayerMask))
                {
                    float clearance = next.y - hit.point.y;
                    if (clearance <= headFlyLandSnapHeight)
                        next = hit.point + Vector3.up * Mathf.Max(groundOffsetY, 0.05f);
                }
            }

            xrOrigin.position = next;
        }

        private void SnapRigToGround(string reason)
        {
            if (xrOrigin == null)
                return;
            Vector3 snapped = ResolveGroundedPosition(xrOrigin.position);
            if (snapped.y < spawnSafetyMinY)
                snapped.y = spawnSafetyMinY;
            xrOrigin.position = snapped;
            LogDebug($"Rig snapped to ground ({reason}) at {snapped}");
        }

        private void BuildHandProxies()
        {
            leftProxy = CreateHandProxy("LeftHandProxy", new Color(0.2f, 0.7f, 1f, 0.8f));
            rightProxy = CreateHandProxy("RightHandProxy", new Color(0.4f, 1f, 0.45f, 0.8f));
        }

        private void BuildStatusHud()
        {
            if (statusHud != null)
                return;
            var cam = xrCamera != null ? xrCamera.transform : (Camera.main != null ? Camera.main.transform : transform);
            var go = new GameObject("VrRuntimeStatusHud");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(-0.52f, -0.18f, 1.1f);
            go.transform.localRotation = Quaternion.identity;
            statusHud = go.AddComponent<TextMesh>();
            statusHud.fontSize = 34;
            statusHud.characterSize = 0.01f;
            statusHud.anchor = TextAnchor.MiddleLeft;
            statusHud.alignment = TextAlignment.Left;
            statusHud.color = new Color(0.78f, 0.95f, 1f, 0.95f);
        }

        private void UpdateStatusHud()
        {
            if (!showRuntimeStatusHud || statusHud == null)
                return;

            bool leftOk = leftController.isValid;
            bool rightOk = rightController.isValid;
            bool leftAnchorOk = leftHandAnchor != null;
            bool rightAnchorOk = rightHandAnchor != null;

            bool xrRuntime = XRSettings.isDeviceActive;
            string inputMode;
            if (preferHandTrackingOnly && HandsResolvedForHandsOnlyLocomotion())
                inputMode = "Hands-first (no thumbstick/trigger)";
            else if (enableHandTrackingLocomotion && (!leftOk || !rightOk) && (leftAnchorOk || rightAnchorOk))
                inputMode = "Hands (move/turn/pinch)";
            else if (leftOk || rightOk)
                inputMode = "Controllers";
            else if (leftAnchorOk || rightAnchorOk)
                inputMode = "Hand anchors only";
            else
                inputMode = "No XR input";

            statusHud.text =
                "VR Runtime Status\n" +
                $"XR Runtime: {(xrRuntime ? "ON" : "OFF")}\n" +
                $"L Controller: {(leftOk ? "OK" : "MISS")}  R Controller: {(rightOk ? "OK" : "MISS")}\n" +
                $"L Anchor: {(leftAnchorOk ? "OK" : "MISS")}  R Anchor: {(rightAnchorOk ? "OK" : "MISS")}\n" +
                $"Input Mode: {inputMode}\n" +
                $"Move: {(enableSmoothLocomotion ? "Smooth ON" : "Smooth OFF")} · Turn: {(enableSnapTurn ? "Snap ON" : "Snap OFF")}\n" +
                $"Hand move: {(enableHandTrackingLocomotion ? "ON" : "OFF")} · Head fly: {(enableHeadDirectedFly ? "ON" : "OFF")}";
        }

        private GameObject CreateHandProxy(string name, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.localScale = Vector3.one * Mathf.Max(0.01f, handProxySize);
            if (go.TryGetComponent<Collider>(out Collider col))
                Destroy(col);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                var mat = new Material(shader);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                renderer.sharedMaterial = mat;
            }
            return go;
        }

        private void UpdateHandProxies()
        {
            if (!showHandDebugProxies)
                return;
            if (leftProxy == null || rightProxy == null)
                BuildHandProxies();

            if (TryGetLeftHandPosition(out Vector3 leftPos))
            {
                leftProxy.SetActive(true);
                leftProxy.transform.position = leftPos;
            }
            else
            {
                if (forceCameraRelativeHandProxyFallback && xrCamera != null)
                {
                    leftProxy.SetActive(true);
                    leftProxy.transform.position = xrCamera.transform.TransformPoint(new Vector3(-0.18f, -0.2f, 0.45f));
                }
                else
                {
                    leftProxy.SetActive(false);
                }
            }

            if (TryGetRightHandPosition(out Vector3 rightPos))
            {
                rightProxy.SetActive(true);
                rightProxy.transform.position = rightPos;
            }
            else
            {
                if (forceCameraRelativeHandProxyFallback && xrCamera != null)
                {
                    rightProxy.SetActive(true);
                    rightProxy.transform.position = xrCamera.transform.TransformPoint(new Vector3(0.18f, -0.2f, 0.45f));
                }
                else
                {
                    rightProxy.SetActive(false);
                }
            }
        }

        private void TryReinitializeControllersIfNeeded()
        {
            if (preferHandTrackingOnly && enableHandTrackingLocomotion && Time.frameCount % 30 == 0)
                ResolveHandAnchors();

            if (leftController.isValid && rightController.isValid)
            {
                if (!preferHandTrackingOnly && enableHandTrackingLocomotion && Time.frameCount % 60 == 0)
                    ResolveHandAnchors();
                return;
            }

            if (Time.frameCount % 45 != 0)
                return;
            InitializeVRControllers();
            ResolveHandAnchors();
        }

        /// <summary>Both sides have hand anchors — safe to ignore controller sticks for locomotion.</summary>
        private bool HandsResolvedForHandsOnlyLocomotion() =>
            leftHandAnchor != null && rightHandAnchor != null;

        private bool TryGetHandMoveAxisFromLeftHand(out Vector2 axis)
        {
            axis = Vector2.zero;
            if (leftHandAnchor == null || xrCamera == null)
                return false;

            Vector3 chest = xrCamera.transform.position + xrCamera.transform.rotation * handMoveChestReferenceLocal;
            Vector3 delta = leftHandAnchor.position - chest;
            Vector3 basisForward = xrCamera.transform.forward;
            basisForward.y = 0f;
            if (basisForward.sqrMagnitude < 0.0001f)
                basisForward = xrOrigin != null ? xrOrigin.forward : Vector3.forward;
            basisForward.Normalize();
            Vector3 basisRight = new Vector3(basisForward.z, 0f, -basisForward.x);
            float fwd = Vector3.Dot(delta, basisForward);
            float right = Vector3.Dot(delta, basisRight);
            axis = new Vector2(right, fwd);

            float mag = axis.magnitude;
            if (mag < handMoveDeadZoneMeters)
            {
                axis = Vector2.zero;
                return false;
            }

            float scaled = Mathf.Clamp01((mag - handMoveDeadZoneMeters) / Mathf.Max(0.01f, handMoveMaxOffsetMeters - handMoveDeadZoneMeters));
            axis *= scaled / Mathf.Max(1e-5f, mag);
            return true;
        }

        private bool TryGetHandSnapTurnFromRightHand(out int directionSign)
        {
            directionSign = 0;
            if (rightHandAnchor == null || xrCamera == null)
                return false;

            Vector3 local = xrCamera.transform.InverseTransformPoint(rightHandAnchor.position);
            if (local.x > handSnapTurnLateralThresholdMeters)
                directionSign = 1;
            else if (local.x < -handSnapTurnLateralThresholdMeters)
                directionSign = -1;
            return directionSign != 0;
        }

        private bool TryHandPinch(Transform indexTip, Transform thumbTip)
        {
            if (indexTip == null || thumbTip == null || indexTip == thumbTip)
                return false;
            return Vector3.Distance(indexTip.position, thumbTip.position) <= handPinchTeleportDistanceMeters;
        }

        private void LogBodyPoseIfNeeded()
        {
            if (!logBodyPose || Time.time < nextBodyPoseLogTime)
                return;
            nextBodyPoseLogTime = Time.time + Mathf.Max(0.05f, bodyPoseLogIntervalSeconds);

            Vector3 headPos = xrCamera != null ? xrCamera.transform.position : Vector3.zero;
            Quaternion headRot = xrCamera != null ? xrCamera.transform.rotation : Quaternion.identity;
            Vector3 lPos = Vector3.zero;
            Vector3 rPos = Vector3.zero;
            TryGetLeftHandPosition(out lPos);
            TryGetRightHandPosition(out rPos);

            Debug.Log(
                $"[BodyPose] t={Time.time:0.00} " +
                $"head=({headPos.x:0.###},{headPos.y:0.###},{headPos.z:0.###}) " +
                $"headRot=({headRot.eulerAngles.x:0.#},{headRot.eulerAngles.y:0.#},{headRot.eulerAngles.z:0.#}) " +
                $"left=({lPos.x:0.###},{lPos.y:0.###},{lPos.z:0.###}) " +
                $"right=({rPos.x:0.###},{rPos.y:0.###},{rPos.z:0.###})");
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

        private bool TryGetLeftHandPosition(out Vector3 pos)
        {
            if (leftController.isValid && leftController.TryGetFeatureValue(CommonUsages.devicePosition, out pos))
                return true;
            if (leftHandAnchor != null)
            {
                pos = leftHandAnchor.position;
                return true;
            }
            pos = Vector3.zero;
            return false;
        }

        private bool TryGetRightHandPosition(out Vector3 pos)
        {
            if (rightController.isValid && rightController.TryGetFeatureValue(CommonUsages.devicePosition, out pos))
                return true;
            if (rightHandAnchor != null)
            {
                pos = rightHandAnchor.position;
                return true;
            }
            pos = Vector3.zero;
            return false;
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
