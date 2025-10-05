using UnityEngine;
using System.Collections.Generic;

namespace Aetheria.World
{
    /// <summary>
    /// Generates deterministic world chunks using seeded noise.
    /// Each chunk is a 16x16 grid of tiles that can be regenerated
    /// with the same seed to produce identical results.
    /// </summary>
    public class ChunkGenerator : MonoBehaviour
    {
        [Header("Chunk Settings")]
        [SerializeField] private int chunkSize = 16;
        [SerializeField] private float tileSize = 1f;
        [SerializeField] private int seed = 12345;
        
        [Header("Noise Settings")]
        [SerializeField] private float noiseScale = 0.1f;
        [SerializeField] private int octaves = 4;
        [SerializeField] private float persistence = 0.5f;
        [SerializeField] private float lacunarity = 2f;
        
        [Header("Terrain Types")]
        [SerializeField] private TerrainType[] terrainTypes;
        
        private Dictionary<Vector2Int, ChunkData> generatedChunks = new Dictionary<Vector2Int, ChunkData>();
        
        /// <summary>
        /// Generate a chunk at the specified world coordinates
        /// </summary>
        public ChunkData GenerateChunk(Vector2Int chunkCoord)
        {
            if (generatedChunks.ContainsKey(chunkCoord))
            {
                return generatedChunks[chunkCoord];
            }
            
            ChunkData chunk = new ChunkData();
            chunk.chunkCoord = chunkCoord;
            chunk.tiles = new TileType[chunkSize, chunkSize];
            
            // Generate terrain using seeded noise
            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    Vector2 worldPos = new Vector2(
                        chunkCoord.x * chunkSize + x,
                        chunkCoord.z * chunkSize + z
                    );
                    
                    float noiseValue = GenerateNoise(worldPos);
                    chunk.tiles[x, z] = GetTerrainType(noiseValue);
                }
            }
            
            generatedChunks[chunkCoord] = chunk;
            return chunk;
        }
        
        /// <summary>
        /// Generate deterministic noise for a world position
        /// </summary>
        private float GenerateNoise(Vector2 worldPos)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float noiseValue = 0f;
            float maxValue = 0f;
            
            for (int i = 0; i < octaves; i++)
            {
                float sampleX = (worldPos.x + seed) * noiseScale * frequency;
                float sampleY = (worldPos.y + seed) * noiseScale * frequency;
                
                float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                noiseValue += perlinValue * amplitude;
                
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }
            
            return noiseValue / maxValue;
        }
        
        /// <summary>
        /// Get terrain type based on noise value
        /// </summary>
        private TileType GetTerrainType(float noiseValue)
        {
            for (int i = 0; i < terrainTypes.Length; i++)
            {
                if (noiseValue <= terrainTypes[i].height)
                {
                    return terrainTypes[i].tileType;
                }
            }
            return TileType.Grass; // Default fallback
        }
        
        /// <summary>
        /// Set the seed for deterministic generation
        /// </summary>
        public void SetSeed(int newSeed)
        {
            seed = newSeed;
            generatedChunks.Clear(); // Clear cache to regenerate with new seed
        }
        
        /// <summary>
        /// Get current seed
        /// </summary>
        public int GetSeed()
        {
            return seed;
        }
    }
    
    /// <summary>
    /// Data structure for a single chunk
    /// </summary>
    [System.Serializable]
    public class ChunkData
    {
        public Vector2Int chunkCoord;
        public TileType[,] tiles;
        public bool isLoaded = false;
    }
    
    /// <summary>
    /// Types of terrain tiles
    /// </summary>
    public enum TileType
    {
        Water,
        Sand,
        Grass,
        Forest,
        Mountain,
        Snow
    }
    
    /// <summary>
    /// Terrain type configuration
    /// </summary>
    [System.Serializable]
    public class TerrainType
    {
        public string name;
        public TileType tileType;
        public float height;
        public Color color;
    }
}