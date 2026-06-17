using UnityEngine;

namespace Aetheria.Environment
{
    /// <summary>
    /// Authoring asset for wind, time-of-day, darkness, sea level. Grass/water shaders can sample globals set by <see cref="EnvironmentShaderHub"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "EnvironmentState", menuName = "Aetheria/Environment State", order = 0)]
    public sealed class EnvironmentStateAsset : ScriptableObject
    {
        [Tooltip("Base wind direction and strength in world space (XZ primary).")]
        public Vector3 windVector = new Vector3(1f, 0f, 0.3f);

        [Tooltip("0..1 extra sway multiplier (gusts).")]
        [Range(0f, 1f)]
        public float windGust;

        [Tooltip("0 = midnight, 0.5 = noon, 1 = midnight.")]
        [Range(0f, 1f)]
        public float timeOfDay;

        [Tooltip("0 = normal, 1 = heavy dark-magic / eclipse tint.")]
        [Range(0f, 1f)]
        public float globalDarkness;

        [Tooltip("World Y of the water surface (for depth shaders).")]
        public float seaLevel;
    }
}
