using UnityEngine;

namespace Aetheria.Gameplay
{
    /// <summary>
    /// ScriptableObject: maps backend catalogId (e.g. creature.wolf.v1) to a spawnable prefab.
    /// Create via Assets → Create → Aetheria → Creature Catalog Entry
    /// Prefer SpawnDirector's inline **Catalog (easiest)** if you only want to drag prefabs without creating .asset files.
    /// </summary>
    [CreateAssetMenu(fileName = "CreatureCatalogEntry", menuName = "Aetheria/Creature Catalog Entry", order = 0)]
    public class CreatureCatalogEntry : ScriptableObject
    {
        [Tooltip("Must match IntentParseResponse.spawn.catalogId from the backend.")]
        public string catalogId = "creature.wolf.v1";

        public GameObject prefab;

        [Tooltip("Fallback if spawn payload omits tint.")]
        public Material materialOverride;
    }
}
