using UnityEngine;

namespace Aetheria.World
{
    /// <summary>
    /// Optional: sets RenderSettings skybox at startup (use if not using WeaponRoomSkyboxSwitcher for "outside").
    /// Prefer assigning Fantasy Skybox on WeaponRoomSkyboxSwitcher.skyboxOutside instead for hub flow.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldEnvironmentBootstrap : MonoBehaviour
    {
        [SerializeField] private Material skyboxMaterial;
        [SerializeField] private bool applyOnAwake = true;

        void Awake()
        {
            if (!applyOnAwake || skyboxMaterial == null) return;
            RenderSettings.skybox = skyboxMaterial;
        }
    }
}
