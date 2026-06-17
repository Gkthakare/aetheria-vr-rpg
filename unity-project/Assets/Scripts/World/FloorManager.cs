using UnityEngine;
using Aetheria.Dungeons;
using Aetheria.AI;

namespace Aetheria.World
{
    /// <summary>
    /// SAO-style floor manager. Each floor = one dungeon + themed field.
    /// Coordinates DungeonGenerator, ChunkGenerator, and TerrainTextureGenerator.
    /// </summary>
    public class FloorManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DungeonGenerator dungeonGenerator;
        [SerializeField] private ChunkGenerator chunkGenerator;
        [SerializeField] private ChunkManager chunkManager;
        [SerializeField] private TerrainTextureGenerator terrainTextureGenerator;
        [SerializeField] private DungeonRoomVisualizer dungeonVisualizer;

        [Header("Floor Settings")]
        [SerializeField] private int seed = 12345;
        [SerializeField] private int currentFloorIndex = 1;
        [SerializeField] private FloorThemeConfig[] floorThemes;
        [SerializeField] private bool generateTerrainOnFloorLoad = true;

        public int CurrentFloor => currentFloorIndex;
        public int Seed => seed;

        private void Start()
        {
            if (dungeonGenerator == null) dungeonGenerator = FindObjectOfType<DungeonGenerator>();
            if (chunkGenerator == null) chunkGenerator = FindObjectOfType<ChunkGenerator>();
            if (chunkManager == null) chunkManager = FindObjectOfType<ChunkManager>();
            if (terrainTextureGenerator == null) terrainTextureGenerator = FindObjectOfType<TerrainTextureGenerator>();
            if (dungeonVisualizer == null) dungeonVisualizer = FindObjectOfType<DungeonRoomVisualizer>();

            LoadFloor(currentFloorIndex);
        }

        /// <summary>Load a floor. Regenerates dungeon and field with floor-specific seed.</summary>
        public void LoadFloor(int floorIndex)
        {
            currentFloorIndex = Mathf.Clamp(floorIndex, 1, 999);
            int floorSeed = seed + currentFloorIndex * 1000;

            // Dungeon
            if (dungeonGenerator != null)
            {
                dungeonGenerator.SetSeedAndFloor(seed, currentFloorIndex);
                if (dungeonVisualizer != null)
                    dungeonVisualizer.Build();
            }

            // Field (infinite terrain)
            if (chunkGenerator != null)
            {
                chunkGenerator.SetSeed(floorSeed);
                if (chunkManager != null)
                    chunkManager.ClearAllChunks();
            }

            // AI terrain textures for this floor's theme
            if (generateTerrainOnFloorLoad && terrainTextureGenerator != null)
            {
                var theme = GetThemeForFloor(currentFloorIndex);
                if (theme != null)
                {
                    terrainTextureGenerator.GenerateWithPrompt(TileType.Mountain, theme.mountainPrompt + " " + theme.promptSuffix);
                    terrainTextureGenerator.GenerateWithPrompt(TileType.Water, theme.waterPrompt + " " + theme.promptSuffix);
                    terrainTextureGenerator.GenerateWithPrompt(TileType.Grass, theme.grassPrompt + " " + theme.promptSuffix);
                    terrainTextureGenerator.GenerateWithPrompt(TileType.Forest, theme.forestPrompt + " " + theme.promptSuffix);
                    terrainTextureGenerator.GenerateWithPrompt(TileType.Sand, theme.sandPrompt + " " + theme.promptSuffix);
                    terrainTextureGenerator.GenerateWithPrompt(TileType.Snow, theme.snowPrompt + " " + theme.promptSuffix);
                }
                else
                {
                    terrainTextureGenerator.GenerateAllTerrain();
                }
            }

            Debug.Log($"[FloorManager] Loaded floor {currentFloorIndex} (theme: {GetThemeForFloor(currentFloorIndex)?.themeName ?? "default"})");
        }

        /// <summary>Get theme config for floor. Cycles through themes if more floors than themes.</summary>
        public FloorThemeConfig GetThemeForFloor(int floor)
        {
            if (floorThemes == null || floorThemes.Length == 0) return null;
            int idx = (floor - 1) % floorThemes.Length;
            return floorThemes[idx];
        }

        /// <summary>Set seed and reload current floor.</summary>
        public void SetSeed(int newSeed)
        {
            seed = newSeed;
            LoadFloor(currentFloorIndex);
        }
    }
}
