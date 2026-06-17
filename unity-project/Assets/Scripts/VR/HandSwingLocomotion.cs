using UnityEngine;

namespace Aetheria.VR
{
    /// <summary>
    /// Walk when hands swing naturally; run when swing is faster.
    /// Works alongside thumbstick and teleport. Add to XR Origin.
    /// </summary>
    public class HandSwingLocomotion : MonoBehaviour
    {
        [Header("Hand References")]
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private Transform forwardSource;

        [Header("Speeds")]
        [SerializeField] private float walkSpeed = 1.5f;
        [SerializeField] private float runSpeed = 3.5f;
        [SerializeField] private float runThreshold = 2.5f;

        [Header("Sensitivity")]
        [SerializeField] private float swingSensitivity = 1f;
        [SerializeField] private float minSwingVelocity = 0.3f;

        [Header("Optional")]
        [SerializeField] private CharacterController characterController;

        private Vector3 _lastLeftPos;
        private Vector3 _lastRightPos;
        private float _leftVelZ;
        private float _rightVelZ;
        private bool _initialized;

        private void Awake()
        {
            if (forwardSource == null) forwardSource = Camera.main?.transform;
            if (characterController == null) characterController = GetComponent<CharacterController>();
            FindHands();
        }

        private void FindHands()
        {
            if (leftHand != null && rightHand != null) return;
            var offset = transform.Find("Camera Offset");
            if (offset == null) return;
            foreach (Transform t in offset)
            {
                if (t.name.Contains("Left")) leftHand = t;
                if (t.name.Contains("Right")) rightHand = t;
            }
        }

        private void Start()
        {
            if (leftHand != null) _lastLeftPos = leftHand.position;
            if (rightHand != null) _lastRightPos = rightHand.position;
            _initialized = leftHand != null && rightHand != null;
        }

        private void Update()
        {
            if (!_initialized || forwardSource == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0) return;

            Vector3 fwd = forwardSource.forward;
            fwd.y = 0f;
            fwd.Normalize();

            Vector3 leftPos = leftHand.position;
            Vector3 rightPos = rightHand.position;

            Vector3 leftLocal = forwardSource.InverseTransformPoint(leftPos);
            Vector3 rightLocal = forwardSource.InverseTransformPoint(rightPos);

            _leftVelZ = (leftLocal.z - forwardSource.InverseTransformPoint(_lastLeftPos).z) / dt;
            _rightVelZ = (rightLocal.z - forwardSource.InverseTransformPoint(_lastRightPos).z) / dt;

            _lastLeftPos = leftPos;
            _lastRightPos = rightPos;

            float swing = ComputeSwing();
            if (Mathf.Abs(swing) < minSwingVelocity) return;

            float speed = Mathf.Abs(swing) >= runThreshold ? runSpeed : walkSpeed;
            Vector3 move = fwd * (swing * swingSensitivity * speed * dt);

            if (characterController != null && characterController.enabled)
                characterController.Move(move);
            else
                transform.position += move;
        }

        private float ComputeSwing()
        {
            bool leftForward = _leftVelZ > minSwingVelocity;
            bool leftBack = _leftVelZ < -minSwingVelocity;
            bool rightForward = _rightVelZ > minSwingVelocity;
            bool rightBack = _rightVelZ < -minSwingVelocity;

            if (leftForward && rightBack) return (_leftVelZ - _rightVelZ) * 0.5f;
            if (leftBack && rightForward) return (_rightVelZ - _leftVelZ) * 0.5f;
            return 0f;
        }
    }
}
