using UnityEngine;

namespace Aetheria.WaterMagic
{
    [CreateAssetMenu(fileName = "WaterSpell", menuName = "Aetheria/Water Spell", order = 0)]
    public sealed class WaterSpellDefinition : ScriptableObject
    {
        [Tooltip("Cost in 0..1 orb charge units.")]
        [Range(0.05f, 1f)]
        public float chargeCost = 0.35f;

        public GameObject spawnPrefab;
        public float spawnForwardOffset = 0.35f;
        public AudioClip castSound;
    }
}
