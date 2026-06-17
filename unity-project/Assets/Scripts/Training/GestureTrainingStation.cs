using UnityEngine;
using UnityEngine.Events;

namespace Aetheria.Training
{
    /// <summary>
    /// One station that asks for a target gesture and gives VFX feedback.
    /// Uses IGestureRecognizer (rule-based or ML). Perfect one element at a time.
    /// </summary>
    public class GestureTrainingStation : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private GestureType targetGesture = GestureType.Pinch;
        [SerializeField] private float holdDuration = 0.5f;

        [Header("Dependencies")]
        [SerializeField] private MonoBehaviour recognizerComponent;
        [SerializeField] private TrainingVFX vfx;

        private IGestureRecognizer recognizer;

        [Header("Events (for UI / AI feedback)")]
        public UnityEvent OnCorrect;
        public UnityEvent OnIncorrect;
        public UnityEvent OnCorrectionHint;

        private float _holdTimer;
        private bool _recentlyCorrect;

        [Header("Debug")]
        [SerializeField] private bool logPose;

        private void Awake()
        {
            if (recognizerComponent != null) recognizer = recognizerComponent as IGestureRecognizer;
            if (recognizer == null) recognizer = GetComponentInChildren<IGestureRecognizer>();
            if (vfx == null) vfx = GetComponentInChildren<TrainingVFX>();
            if (recognizer == null) Debug.LogWarning("[GestureStation] No recognizer found! Assign Recognizer Component.");
        }

        private void Update()
        {
            HandPoseData pose = GetCurrentHandPose();
            if (logPose && pose.IsTracked && Time.frameCount % 30 == 0)
                Debug.Log($"[GestureStation] Pose: pinch={pose.PinchStrength} grip={pose.GripStrength}");
            GestureType current = recognizer != null ? recognizer.Recognize(pose) : GestureType.None;

            if (current == targetGesture)
            {
                _holdTimer += Time.deltaTime;
                if (_holdTimer >= holdDuration && !_recentlyCorrect)
                {
                    _recentlyCorrect = true;
                    if (vfx != null) vfx.PlaySuccess();
                    OnCorrect?.Invoke();
                }
            }
            else
            {
                _holdTimer = 0f;
                if (current != GestureType.None && current != targetGesture)
                {
                    if (vfx != null) vfx.PlayCorrection();
                    OnCorrectionHint?.Invoke();
                }
            }

            if (current == GestureType.None) _recentlyCorrect = false;
        }

        /// <summary>Override or plug in XR Hands here. Stub uses keyboard for testing.</summary>
        private HandPoseData GetCurrentHandPose()
        {
            var stub = GetComponent<HandPoseStub>();
            if (stub != null) return stub.GetPose();
            return new HandPoseData { IsTracked = false };
        }
    }
}
