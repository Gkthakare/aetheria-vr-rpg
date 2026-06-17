using UnityEngine;

namespace Aetheria.Magic
{
    /// <summary>
    /// Drives cheap global shader parameters for "telekinesis" water / magic at a distance.
    /// Add matching properties to your water Shader Graph: _MagicWaterPoint (Vector4 xyz + w=radius), _MagicWaterStrength (Float, decays).
    /// Works anywhere in the world without AI — infinite ocean reads the same globals.
    /// </summary>
    public sealed class WaterWorldInfluenceHub : MonoBehaviour
    {
        public static readonly int MagicWaterPointId = Shader.PropertyToID("_MagicWaterPoint");
        public static readonly int MagicWaterStrengthId = Shader.PropertyToID("_MagicWaterStrength");

        [SerializeField] float decayPerSecond = 3f;
        [SerializeField] float defaultRadius = 4f;

        float _strength;

        void Update()
        {
            if (_strength <= 0f)
                return;
            _strength = Mathf.Max(0f, _strength - decayPerSecond * Time.deltaTime);
            Shader.SetGlobalFloat(MagicWaterStrengthId, _strength);
        }

        public void ApplyPulse(Vector3 worldPoint, float strength)
        {
            _strength = Mathf.Max(_strength, strength);
            Shader.SetGlobalVector(MagicWaterPointId, new Vector4(worldPoint.x, worldPoint.y, worldPoint.z, defaultRadius));
            Shader.SetGlobalFloat(MagicWaterStrengthId, _strength);
        }

        public void ClearInfluence()
        {
            _strength = 0f;
            Shader.SetGlobalFloat(MagicWaterStrengthId, 0f);
        }
    }
}
