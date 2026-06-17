using UnityEngine;

namespace Aetheria.Environment
{
    /// <summary>
    /// Pushes <see cref="EnvironmentStateAsset"/> fields to global shader properties each frame.
    /// Use these names in Shader Graph: Global Wind Vector, Global Time Of Day, Global Darkness, Global Sea Level.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class EnvironmentShaderHub : MonoBehaviour
    {
        public static readonly int GlobalWind = Shader.PropertyToID("_GlobalWind");
        public static readonly int GlobalTimeOfDay = Shader.PropertyToID("_GlobalTimeOfDay");
        public static readonly int GlobalDarkness = Shader.PropertyToID("_GlobalDarkness");
        public static readonly int GlobalSeaLevel = Shader.PropertyToID("_GlobalSeaLevel");

        [SerializeField] EnvironmentStateAsset state;

        void LateUpdate()
        {
            if (state == null)
                return;

            var w = state.windVector;
            Shader.SetGlobalVector(GlobalWind, new Vector4(w.x, w.y, w.z, state.windGust));
            Shader.SetGlobalFloat(GlobalTimeOfDay, state.timeOfDay);
            Shader.SetGlobalFloat(GlobalDarkness, state.globalDarkness);
            Shader.SetGlobalFloat(GlobalSeaLevel, state.seaLevel);
        }
    }
}
