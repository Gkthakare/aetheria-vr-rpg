using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aetheria.Magic
{
    /// <summary>
    /// Minimal dimension hop: load/unload additive scene. Expand later with portals, staging areas, or AI-chosen targets.
    /// </summary>
    public sealed class DimensionGateway : MonoBehaviour
    {
        [SerializeField] PocketDimensionDescriptor dimension;
        [SerializeField] Transform exitSpawn;
        [Tooltip("XR Origin root or player capsule — assign explicitly to avoid assembly deps.")]
        [SerializeField] Transform playerRigRoot;

        public void EnterDimension()
        {
            if (dimension == null || string.IsNullOrEmpty(dimension.sceneOrAddress))
                return;
            SceneManager.LoadSceneAsync(dimension.sceneOrAddress, LoadSceneMode.Additive);
        }

        public void ExitToSpawn()
        {
            if (exitSpawn == null || playerRigRoot == null)
                return;
            playerRigRoot.SetPositionAndRotation(exitSpawn.position, exitSpawn.rotation);
        }
    }
}
