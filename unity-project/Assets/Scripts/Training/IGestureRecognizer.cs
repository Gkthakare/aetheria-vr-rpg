using UnityEngine;

namespace Aetheria.Training
{
    /// <summary>
    /// ML-ready interface for gesture recognition.
    /// Implement with rule-based logic first; swap for ML model (ONNX / Unity ML Agents) later.
    /// </summary>
    public interface IGestureRecognizer
    {
        /// <summary>Recognized gesture types for training and combat.</summary>
        GestureType Recognize(HandPoseData pose);
    }

    public enum GestureType
    {
        None,
        Pinch,
        OpenPalm,
        Fist,
        Point,
        Thrust,
        Shield
    }

    /// <summary>Hand pose input: can be filled from XR Hands or synthetic data for ML training.</summary>
    public struct HandPoseData
    {
        public bool IsLeft;
        public Vector3 PalmPosition;
        public Vector3 PalmNormal;
        public float PinchStrength;   // 0..1
        public float GripStrength;    // 0..1
        public bool IsTracked;
    }
}
