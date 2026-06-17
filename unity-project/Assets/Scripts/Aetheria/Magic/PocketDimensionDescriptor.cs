using UnityEngine;

namespace Aetheria.Magic
{
    /// <summary>
    /// Authoring data for "space storage" / pocket dimensions: load additive scenes or Addressables by key later.
    /// </summary>
    [CreateAssetMenu(fileName = "PocketDimension", menuName = "Aetheria/Pocket Dimension", order = 2)]
    public sealed class PocketDimensionDescriptor : ScriptableObject
    {
        [Tooltip("Additive scene name in Build Settings, or Addressable address.")]
        public string sceneOrAddress;

        public Vector3 localSpawnOffset = Vector3.zero;
        public AudioClip enterSound;
    }
}
