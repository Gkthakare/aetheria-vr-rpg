using UnityEngine;

namespace Aetheria.VR
{
    /// <summary>
    /// Runtime full-body VR IK driver:
    /// - maps humanoid head/hands to XR targets
    /// - keeps body root under the HMD
    /// - adjusts feet to terrain with per-foot raycasts
    /// Inspired by practical VR body + foot-placement workflows.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class VRFullBodyIkController : MonoBehaviour
    {
        [Header("XR targets")]
        [SerializeField] private Transform xrHead;
        [SerializeField] private Transform xrLeftHand;
        [SerializeField] private Transform xrRightHand;
        [SerializeField] private bool autoResolveTargets = true;

        [Header("Body follow")]
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private float bodyHeightOffset = -0.52f;
        [SerializeField] private float bodyFollowSpeed = 10f;
        [SerializeField] private float yawFollowSpeed = 11f;
        [SerializeField] private bool rotateBodyWithHeadYaw = true;

        [Header("Hand IK")]
        [SerializeField] private Vector3 leftHandPosOffset = new Vector3(-0.045f, -0.03f, -0.11f);
        [SerializeField] private Vector3 rightHandPosOffset = new Vector3(0.045f, -0.03f, -0.11f);
        [SerializeField] private Vector3 leftHandEulerOffset = new Vector3(0f, 90f, -98f);
        [SerializeField] private Vector3 rightHandEulerOffset = new Vector3(0f, 270f, 98f);
        [SerializeField, Range(0f, 1f)] private float handPositionWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float handRotationWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float headRotationWeight = 1f;

        [Header("Local VR visibility")]
        [Tooltip("Shrinks the humanoid Head bone so the facial mesh does not fill the HMD and clip; you still see torso, arms, and legs when looking down.")]
        [SerializeField] private bool shrinkHeadBoneForLocalVr = true;
        [SerializeField] private float headBoneUniformScale = 0.01f;

        [Header("Foot placement")]
        [SerializeField] private bool enableFootPlacement = true;
        [SerializeField] private LayerMask walkableMask = ~0;
        [SerializeField] private float footRaycastStartHeight = 0.35f;
        [SerializeField] private float footRaycastDistance = 1.15f;
        [SerializeField] private float footSoleOffset = 0.02f;
        [SerializeField] private float footPositionLerp = 18f;
        [SerializeField] private float footRotationLerp = 20f;
        [SerializeField] private float footPlantMoveThreshold = 0.035f;
        [SerializeField] private float idleFeetFreezeSpeed = 0.08f;

        private Animator _animator;
        private Vector3 _leftIkPos;
        private Vector3 _rightIkPos;
        private Quaternion _leftIkRot = Quaternion.identity;
        private Quaternion _rightIkRot = Quaternion.identity;
        private Vector3 _lastBodyPos;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (bodyRoot == null)
                bodyRoot = transform;
            _lastBodyPos = bodyRoot.position;
        }

        private void Start()
        {
            if (autoResolveTargets)
                AutoResolveTargets();
        }

        private void LateUpdate()
        {
            if (xrHead == null || bodyRoot == null)
                return;

            var targetPos = xrHead.position + Vector3.up * bodyHeightOffset;
            bodyRoot.position = Vector3.Lerp(bodyRoot.position, targetPos, Time.deltaTime * Mathf.Max(0.01f, bodyFollowSpeed));

            if (rotateBodyWithHeadYaw)
            {
                var flatForward = xrHead.forward;
                flatForward.y = 0f;
                if (flatForward.sqrMagnitude > 0.0001f)
                {
                    var targetRot = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
                    bodyRoot.rotation = Quaternion.Slerp(bodyRoot.rotation, targetRot, Time.deltaTime * Mathf.Max(0.01f, yawFollowSpeed));
                }
            }

            if (shrinkHeadBoneForLocalVr && _animator != null && _animator.isHuman)
            {
                var head = _animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    float s = Mathf.Max(1e-4f, headBoneUniformScale);
                    head.localScale = new Vector3(s, s, s);
                }
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || !_animator.isHuman)
                return;

            ApplyHeadIk();
            ApplyHandIk();
            ApplyFeetIk();
        }

        private void ApplyHeadIk()
        {
            if (xrHead == null)
            {
                _animator.SetLookAtWeight(0f);
                return;
            }

            _animator.SetLookAtWeight(headRotationWeight, 0.2f, 1f, 1f, 0.5f);
            var lookPos = xrHead.position + xrHead.forward * 2f;
            _animator.SetLookAtPosition(lookPos);
        }

        private void ApplyHandIk()
        {
            ApplyHand(AvatarIKGoal.LeftHand, xrLeftHand, leftHandPosOffset, leftHandEulerOffset);
            ApplyHand(AvatarIKGoal.RightHand, xrRightHand, rightHandPosOffset, rightHandEulerOffset);
        }

        private void ApplyHand(AvatarIKGoal goal, Transform target, Vector3 posOffset, Vector3 eulerOffset)
        {
            if (target == null)
            {
                _animator.SetIKPositionWeight(goal, 0f);
                _animator.SetIKRotationWeight(goal, 0f);
                return;
            }

            var pos = target.TransformPoint(posOffset);
            var rot = target.rotation * Quaternion.Euler(eulerOffset);
            _animator.SetIKPositionWeight(goal, handPositionWeight);
            _animator.SetIKRotationWeight(goal, handRotationWeight);
            _animator.SetIKPosition(goal, pos);
            _animator.SetIKRotation(goal, rot);
        }

        private void ApplyFeetIk()
        {
            if (!enableFootPlacement)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
                _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
                _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
                return;
            }

            bool isIdle = IsBodyNearlyIdle();
            ApplyFoot(AvatarIKGoal.LeftFoot, ref _leftIkPos, ref _leftIkRot, isIdle);
            ApplyFoot(AvatarIKGoal.RightFoot, ref _rightIkPos, ref _rightIkRot, isIdle);
            _lastBodyPos = bodyRoot != null ? bodyRoot.position : transform.position;
        }

        private bool IsBodyNearlyIdle()
        {
            if (bodyRoot == null)
                return false;
            float speed = (bodyRoot.position - _lastBodyPos).magnitude / Mathf.Max(0.0001f, Time.deltaTime);
            return speed < idleFeetFreezeSpeed;
        }

        private void ApplyFoot(AvatarIKGoal goal, ref Vector3 ikPos, ref Quaternion ikRot, bool isIdle)
        {
            Vector3 footAnimPos = _animator.GetIKPosition(goal);
            var rayStart = footAnimPos + Vector3.up * Mathf.Max(0.05f, footRaycastStartHeight);
            if (!Physics.Raycast(rayStart, Vector3.down, out var hit, Mathf.Max(0.2f, footRaycastDistance), walkableMask))
            {
                _animator.SetIKPositionWeight(goal, 0f);
                _animator.SetIKRotationWeight(goal, 0f);
                return;
            }

            var targetPos = hit.point + hit.normal * Mathf.Max(0f, footSoleOffset);
            var footForward = Vector3.ProjectOnPlane(bodyRoot != null ? bodyRoot.forward : transform.forward, hit.normal).normalized;
            if (footForward.sqrMagnitude < 0.0001f)
                footForward = Vector3.forward;
            var targetRot = Quaternion.LookRotation(footForward, hit.normal);

            if (ikPos == Vector3.zero)
                ikPos = targetPos;
            if (ikRot == Quaternion.identity)
                ikRot = targetRot;

            float posLerp = Mathf.Max(1f, footPositionLerp) * Time.deltaTime;
            float rotLerp = Mathf.Max(1f, footRotationLerp) * Time.deltaTime;
            float moveDist = Vector3.Distance(ikPos, targetPos);

            if (isIdle && moveDist < footPlantMoveThreshold)
            {
                posLerp *= 0.15f;
                rotLerp *= 0.2f;
            }

            ikPos = Vector3.Lerp(ikPos, targetPos, posLerp);
            ikRot = Quaternion.Slerp(ikRot, targetRot, rotLerp);

            _animator.SetIKPositionWeight(goal, 1f);
            _animator.SetIKRotationWeight(goal, 1f);
            _animator.SetIKPosition(goal, ikPos);
            _animator.SetIKRotation(goal, ikRot);
        }

        [ContextMenu("Auto Resolve XR Targets")]
        public void AutoResolveTargets()
        {
            if (xrHead == null && Camera.main != null)
                xrHead = Camera.main.transform;

            Transform rigRoot = ResolveXrRigRootTransform();
            var leftHandRoot = XRHandAttachmentUtil.FindHandController(rigRoot, false);
            var rightHandRoot = XRHandAttachmentUtil.FindHandController(rigRoot, true);

            if (xrLeftHand == null)
                xrLeftHand = XRHandAttachmentUtil.FindIndexTipOrController(leftHandRoot);
            if (xrRightHand == null)
                xrRightHand = XRHandAttachmentUtil.FindIndexTipOrController(rightHandRoot);
        }

        /// <summary>Controllers live under XR Origin, not under the skinned avatar — search from rig root.</summary>
        Transform ResolveXrRigRootTransform()
        {
            Transform t = transform;
            while (t != null)
            {
                if (t.name.IndexOf("XR Origin", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
                t = t.parent;
            }

            if (Camera.main != null)
            {
                t = Camera.main.transform;
                while (t != null)
                {
                    if (t.name.IndexOf("XR Origin", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return t;
                    t = t.parent;
                }
            }

            return transform.root;
        }
    }
}
