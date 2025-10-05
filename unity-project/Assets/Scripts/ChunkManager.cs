using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace Aetheria.World
{
    /// <summary>
    /// Manages chunk loading/unloading as the player moves through the world.
    /// Implements efficient streaming for infinite world generation.
    /// </summary>
    public class ChunkManager : MonoBehaviour
    {
        [Header("Chunk Settings")]
        [SerializeField] private int renderDistance = 3;
        [SerializeField] private int chunkSize = 16;
        [SerializeField] private float tileSize = 1f;
        
        [Header("References")]
        [SerializeField] private ChunkGenerator chunkGenerator;
        [SerializeField] private Transform player;
        
        private Dictionary<Vector2Int, GameObject> loadedChunks = new Dictionary<Vector2Int, GameObject>();
        private Vector2Int lastPlayerChunk = Vector2Int.zero;
        
        void Start()
        {
            if (chunkGenerator == null)
                chunkGenerator = GetComponent<ChunkGenerator>();
            
            if (player == null)
                player = Camera.main.transform;
            
            StartCoroutine(UpdateChunks());
        }
        
        /// <summary>
        /// Continuously update chunks based on player position
        /// </summary>
        private IEnumerator UpdateChunks()
        {
            while (true)
            {
                Vector2Int currentChunk = GetChunkCoord(player.position);
                
                if (currentChunk != lastPlayerChunk)
                {
                    UpdateLoadedChunks(currentChunk);
                    lastPlayerChunk = currentChunk;
                }
                
                yield return new WaitForSeconds(0.1f); // Update every 100ms
            }
        }
        
        /// <summary>
        /// Get chunk coordinate for a world position
        /// </summary>
        private Vector2Int GetChunkCoord(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / chunkSize),
                Mathf.FloorToInt(worldPos.z / chunkSize)
            );
        }
        
        /// <summary>
        /// Update which chunks are loaded based on player position
        /// </summary>
        private void UpdateLoadedChunks(Vector2Int playerChunk)
        {
            // Unload chunks outside render distance
            List<Vector2Int> chunksToUnload = new List<Vector2Int>();
            foreach (var chunk in loadedChunks.Keys)
            {
                if (Vector2Int.Distance(chunk, playerChunk) > renderDistance)
                {
                    chunksToUnload.Add(chunk);
                }
            }
            
            foreach (var chunk in chunksToUnload)
            {
                UnloadChunk(chunk);
            }
            
            // Load chunks within render distance
            for (int x = -renderDistance; x <= renderDistance; x++)
            {
                for (int z = -renderDistance; z <= renderDistance; z++)
                {
                    Vector2Int chunkCoord = playerChunk + new Vector2Int(x, z);
                    
                    if (!loadedChunks.ContainsKey(chunkCoord))
                    {
                        LoadChunk(chunkCoord);
                    }
                }
            }
        }
        
        /// <summary>
        /// Load a chunk and create its visual representation
        /// </summary>
        private void LoadChunk(Vector2Int chunkCoord)
        {
            ChunkData chunkData = chunkGenerator.GenerateChunk(chunkCoord);
            GameObject chunkObject = CreateChunkVisual(chunkData);
            loadedChunks[chunkCoord] = chunkObject;
        }
        
        /// <summary>
        /// Unload a chunk and destroy its visual representation
        /// </summary>
        private void UnloadChunk(Vector2Int chunkCoord)
        {
            if (loadedChunks.ContainsKey(chunkCoord))
            {
                Destroy(loadedChunks[chunkCoord]);
                loadedChunks.Remove(chunkCoord);
            }
        }
        
        /// <summary>
        /// Create visual representation of a chunk
        /// </summary>
        private GameObject CreateChunkVisual(ChunkData chunkData)
        {
            GameObject chunkObject = new GameObject($"Chunk_{chunkData.chunkCoord.x}_{chunkData.chunkCoord.z}");
            chunkObject.transform.position = new Vector3(
                chunkData.chunkCoord.x * chunkSize,
                0,
                chunkData.chunkCoord.z * chunkSize
            );
            
            // Create tiles for this chunk
            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    CreateTile(chunkData.tiles[x, z], x, z, chunkObject.transform);
                }
            }
            
            return chunkObject;
        }
        
        /// <summary>
        /// Create a single tile
        /// </summary>
        private void CreateTile(TileType tileType, int x, int z, Transform parent)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = $"{tileType}_{x}_{z}";
            tile.transform.parent = parent;
            tile.transform.localPosition = new Vector3(x, 0, z);
            tile.transform.localScale = Vector3.one * tileSize;
            
            // Set color based on tile type
            Renderer renderer = tile.GetComponent<Renderer>();
            renderer.material.color = GetTileColor(tileType);
        }
        
        /// <summary>
        /// Get color for a tile type
        /// </summary>
        private Color GetTileColor(TileType tileType)
        {
            switch (tileType)
            {
                case TileType.Water: return Color.blue;
                case TileType.Sand: return Color.yellow;
                case TileType.Grass: return Color.green;
                case TileType.Forest: return new Color(0.2f, 0.6f, 0.2f);
                case TileType.Mountain: return Color.gray;
                case TileType.Snow: return Color.white;
                default: return Color.green;
            }
        }
        
        /// <summary>
        /// Get number of currently loaded chunks
        /// </summary>
        public int GetLoadedChunkCount()
        {
            return loadedChunks.Count;
        }
        
        /// <summary>
        /// Clear all loaded chunks
        /// </summary>
        public void ClearAllChunks()
        {
            foreach (var chunk in loadedChunks.Values)
            {
                Destroy(chunk);
            }
            loadedChunks.Clear();
        }
    }
}