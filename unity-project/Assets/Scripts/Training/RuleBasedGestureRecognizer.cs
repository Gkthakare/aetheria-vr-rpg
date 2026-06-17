using UnityEngine;

namespace Aetheria.Training
{
    /// <summary>
    /// Rule-based gesture recognizer. Replace with ML-based implementation for production.
    /// Tune thresholds in Inspector; later feed same HandPoseData to an ML model.
    /// </summary>
    public class RuleBasedGestureRecognizer : MonoBehaviour, IGestureRecognizer
    {
        [Header("Thresholds (tune for your hand tracking)")]
        [SerializeField] private float pinchThreshold = 0.7f;
        [SerializeField] private float gripThreshold = 0.7f;
        [SerializeField] private float openPalmGripMax = 0.2f;

        public GestureType Recognize(HandPoseData pose)
        {
            if (!pose.IsTracked) return GestureType.None;

            if (pose.PinchStrength >= pinchThreshold)
                return GestureType.Pinch;
            if (pose.GripStrength >= gripThreshold)
                return GestureType.Fist;
            if (pose.GripStrength <= openPalmGripMax)
                return GestureType.OpenPalm;

            return GestureType.None;
        }
    }
}
