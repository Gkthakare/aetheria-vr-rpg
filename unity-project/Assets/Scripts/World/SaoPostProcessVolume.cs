using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aetheria.World
{
    /// <summary>
    /// Adds a global URP Volume with SAO-style color grading + bloom (no asset required).
    /// Attach to any active GameObject (e.g. IslandWorldManager).
    /// </summary>
    [DisallowMultipleComponent]
    public class SaoPostProcessVolume : MonoBehaviour
    {
        [SerializeField] private bool enableOnAwake = true;
        [SerializeField] private float postExposure = 0.05f;
        [SerializeField] private float contrast = 5f;
        [SerializeField] private float saturation = 8f;
        [SerializeField] private float bloomThreshold = 0.95f;
        [SerializeField] private float bloomIntensity = 0.18f;
        [SerializeField] private float bloomScatter = 0.55f;

        private Volume _volume;

        void Awake()
        {
            if (!enableOnAwake) return;
            _volume = GetComponent<Volume>();
            if (_volume == null) _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 1f;
            _volume.weight = 1f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var ca = profile.Add<ColorAdjustments>(true);
            ca.active = true;
            ca.postExposure.Override(postExposure);
            ca.contrast.Override(contrast);
            ca.saturation.Override(saturation);

            var bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.Override(bloomThreshold);
            bloom.intensity.Override(bloomIntensity);
            bloom.scatter.Override(bloomScatter);

            _volume.profile = profile;

            var cam = Camera.main;
            if (cam != null && cam.TryGetComponent<UniversalAdditionalCameraData>(out var uacd))
                uacd.renderPostProcessing = true;
        }
    }
}
