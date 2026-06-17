using UnityEngine;

namespace Aetheria.World
{
    /// <summary>
    /// Deterministic vegetation density for procedural placement.
    /// Use for grass patches, trees, rocks — ensures same seed = same layout.
    /// </summary>
    public static class VegetationDensityGenerator
    {
        /// <summary>Grass density 0-1. Higher = more grass patches.</summary>
        public static float GetGrassDensity(Vector2 worldPos, int seed)
        {
            float n = Mathf.PerlinNoise((worldPos.x + seed) * 0.2f, (worldPos.y + seed) * 0.2f);
            return Mathf.Clamp01(n * 1.2f - 0.2f);
        }

        /// <summary>Tree density 0-1. Use threshold (e.g. 0.5) to spawn trees.</summary>
        public static float GetTreeDensity(Vector2 worldPos, int seed)
        {
            float n = Mathf.PerlinNoise((worldPos.x + seed * 3) * 0.15f, (worldPos.y + seed * 3) * 0.15f);
            return n > 0.5f ? (n - 0.5f) * 2f : 0f;
        }

        /// <summary>Rock density 0-1.</summary>
        public static float GetRockDensity(Vector2 worldPos, int seed)
        {
            float n = Mathf.PerlinNoise((worldPos.x + seed * 5) * 0.25f, (worldPos.y + seed * 5) * 0.25f);
            return n > 0.6f ? (n - 0.6f) * 2.5f : 0f;
        }

        /// <summary>Seeded random 0-1 for deterministic placement.</summary>
        public static float SeededRandom(int seed)
        {
            return (float)((seed * 9301 + 49297) % 233280) / 233280f;
        }
    }
}
