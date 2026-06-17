using UnityEngine;
using Aetheria.World;

namespace Aetheria.AI
{
    /// <summary>
    /// Generates realistic terrain textures (mountain, water, sand, grass, etc.)
    /// via Stable Diffusion and applies them to ChunkManager materials.
    /// Can be triggered by voice, message, or direct call.
    /// </summary>
    public class TerrainTextureGenerator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ChunkManager chunkManager;
        [SerializeField] private VFXTextureRequest textureRequest;

        [Header("Island (HelloVR disc)")]
        [Tooltip("When set, AI sand/grass/forest textures also apply to IslandWorldBuilder ground so the island matches chunk world style.")]
        [SerializeField] private IslandWorldBuilder islandWorld;
        [SerializeField] private bool syncGeneratedTexturesToIsland = true;

        [Header("Presets (SAO anime-style terrain)")]
        [SerializeField] private string mountainPrompt = "anime style mountain rock texture, cel shaded, soft colors, fantasy game";
        [SerializeField] private string waterPrompt = "anime style water surface, soft blue, cel shaded, fantasy JRPG";
        [SerializeField] private string sandPrompt = "anime style sand texture, golden, soft, fantasy game";
        [SerializeField] private string grassPrompt = "anime style grass texture, soft green, cel shaded, JRPG";
        [SerializeField] private string forestPrompt = "anime style forest floor, leaves, soft lighting, anime game";
        [SerializeField] private string snowPrompt = "anime style snow texture, soft white, cel shaded, fantasy";

        [Tooltip("When true, grass tiles keep ChunkManager's grass material (e.g. ALP GrassFlowers) and are not overwritten by AI.")]
        [SerializeField] private bool skipAiGrassTexture;

        private void Awake()
        {
            if (chunkManager == null) chunkManager = FindObjectOfType<ChunkManager>();
            if (textureRequest == null) textureRequest = GetComponent<VFXTextureRequest>();
            if (textureRequest == null) textureRequest = FindObjectOfType<VFXTextureRequest>();
            if (islandWorld == null) islandWorld = FindObjectOfType<IslandWorldBuilder>();
        }

        /// <summary>Generate and apply texture for a terrain type. Call from voice or UI.</summary>
        public void GenerateForTerrain(TileType terrainType)
        {
            if (skipAiGrassTexture && terrainType == TileType.Grass) return;
            string prompt = GetPromptForTerrain(terrainType);
            GenerateWithPrompt(terrainType, prompt);
        }

        /// <summary>Generate with custom prompt. E.g. "generate mountain with volcanic rock"</summary>
        public void GenerateWithPrompt(TileType terrainType, string customPrompt)
        {
            if (skipAiGrassTexture && terrainType == TileType.Grass) return;
            if (textureRequest == null || chunkManager == null)
            {
                Debug.LogWarning("[TerrainTexture] Missing ChunkManager or VFXTextureRequest. Assign in Inspector.");
                return;
            }

            Debug.Log($"[TerrainTexture] Generating {terrainType} with: {customPrompt}");
            textureRequest.RequestTexture(
                customPrompt,
                tex =>
                {
                    chunkManager.SetTerrainTexture(terrainType, tex);
                    if (syncGeneratedTexturesToIsland && islandWorld != null)
                        islandWorld.ApplyGeneratedGroundTexture(terrainType, tex);
                    Debug.Log($"[TerrainTexture] Applied {terrainType} texture" +
                              (syncGeneratedTexturesToIsland && islandWorld != null ? " (+ island ground)" : ""));
                },
                err => Debug.LogError($"[TerrainTexture] Failed: {err}")
            );
        }

        /// <summary>Generate all terrain types with presets (realistic look).</summary>
        public void GenerateAllTerrain()
        {
            GenerateForTerrain(TileType.Mountain);
            GenerateForTerrain(TileType.Water);
            GenerateForTerrain(TileType.Sand);
            if (!skipAiGrassTexture)
                GenerateForTerrain(TileType.Grass);
            GenerateForTerrain(TileType.Forest);
            GenerateForTerrain(TileType.Snow);
        }

        private string GetPromptForTerrain(TileType t)
        {
            switch (t)
            {
                case TileType.Mountain: return mountainPrompt;
                case TileType.Water: return waterPrompt;
                case TileType.Sand: return sandPrompt;
                case TileType.Grass: return grassPrompt;
                case TileType.Forest: return forestPrompt;
                case TileType.Snow: return snowPrompt;
                default: return grassPrompt;
            }
        }
    }
}
