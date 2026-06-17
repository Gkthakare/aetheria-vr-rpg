using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Aetheria.AI;

namespace Aetheria.World
{
    /// <summary>
    /// Manages chunk loading/unloading as the player moves through the world.
    /// Implements efficient streaming for infinite world generation.
    /// </summary>
    public class ChunkManager : MonoBehaviour
    {
        [Header("Chunk Settings")]
        [SerializeField] private int renderDistance = 4;
        [SerializeField] private int chunkSize = 16;
        [SerializeField] private float tileSize = 1f; // Fallback if ChunkGenerator not available

        [Header("Tile Materials")]
        [SerializeField] private Material waterMaterial;
        [SerializeField] private Material sandMaterial;
        [SerializeField] private Material grassMaterial;
        [SerializeField] private Material forestMaterial;
        [SerializeField] private Material mountainMaterial;
        [SerializeField] private Material snowMaterial;

        [Header("Decor Prefabs")]
        [SerializeField] private GameObject grassPrefab;

        [Header("Pack Art (infinite world)")]
        [Tooltip("When non-empty, grass/forest tiles prefer these prefabs over the ModelPool API.")]
        [SerializeField] private GameObject[] packTreePrefabs;
        [Tooltip("Spawned on mountain tiles for readable silhouettes (Mountain Terrain pack rocks).")]
        [SerializeField] private GameObject[] packMountainRockPrefabs;
        [SerializeField] [Range(0f, 1f)] private float mountainRockSpawnChance = 0.35f;
        [SerializeField] private float mountainRockScaleMin = 0.35f;
        [SerializeField] private float mountainRockScaleMax = 0.65f;

        [Header("AI-Generated Decor (infinite dungeon)")]
        [Tooltip("Optional: spawn AI 3D models (trees, rocks) from ModelPool on grass/forest tiles")]
        [SerializeField] private ModelPool modelPool;
        [SerializeField] [Range(0f, 1f)] private float aiDecorChance = 0.15f;
        [Tooltip("Use vegetation density for deterministic placement (same seed = same layout)")]
        [SerializeField] private bool useVegetationDensity = true;

        [Header("SUIMONO infinite ocean")]
        [Tooltip("When true, water tiles are not meshed (use Suimono InfinityWaterBootstrap instead).")]
        [SerializeField] private bool suppressWaterTilesForSuimono;

        [Tooltip("When false, no procedural chunk tiles are spawned (only Suimono + your hand-placed island/hub). Use for island-in-ocean scenes to avoid duplicate sand/grass fragments floating in the water.")]
        [SerializeField] private bool enableProceduralTerrainChunks = true;
        
        [Header("References")]
        [SerializeField] private ChunkGenerator chunkGenerator;
        [SerializeField] private Transform player;
        
        /// <summary>
        /// Get tile size from ChunkGenerator if available, otherwise use local value
        /// </summary>
        private float GetTileSize()
        {
            if (chunkGenerator != null)
            {
                return chunkGenerator.GetTileSize();
            }
            return tileSize;
        }
        
        private Dictionary<Vector2Int, GameObject> loadedChunks = new Dictionary<Vector2Int, GameObject>();
        private Vector2Int lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);
        private static Material _defaultWaterMaterial;
        private bool _chunksInitialized;

        /// <summary>Called from SuimonoInfinityWaterBootstrap before chunks load.</summary>
        public void SetSuppressWaterTilesForSuimono(bool suppress) => suppressWaterTilesForSuimono = suppress;

        /// <summary>World follow target (XR rig / camera root). Used by ambient spawners.</summary>
        public Transform PlayerTransform => player;
        
        void Start()
        {
            if (chunkGenerator == null)
                chunkGenerator = GetComponent<ChunkGenerator>();
            if (player == null && Camera.main != null)
                player = Camera.main.transform;

            if (!enableProceduralTerrainChunks)
            {
                _chunksInitialized = true;
                return;
            }

            // Force an initial load around the current player position
            var currentChunk = GetChunkCoord(player.position);
            UpdateLoadedChunks(currentChunk);
            lastPlayerChunk = currentChunk;
            _chunksInitialized = true;

            StartCoroutine(UpdateChunks());
        }

        /// <summary>Call after toggling suppressWaterTilesForSuimono so existing chunk meshes match the new mode.</summary>
        public void ReloadChunksAfterWaterModeChange()
        {
            if (!enableProceduralTerrainChunks) return;
            if (!_chunksInitialized) return;
            if (player == null && Camera.main != null)
                player = Camera.main.transform;
            if (player == null) return;
            ClearAllChunks();
            var currentChunk = GetChunkCoord(player.position);
            UpdateLoadedChunks(currentChunk);
            lastPlayerChunk = currentChunk;
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
                Mathf.FloorToInt(worldPos.z / chunkSize)  // This is correct - worldPos.z is fine
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
            GameObject chunkObject = new GameObject($"Chunk_{chunkData.chunkCoord.x}_{chunkData.chunkCoord.y}");
            chunkObject.transform.position = new Vector3(
                chunkData.chunkCoord.x * chunkSize,
                0,
                chunkData.chunkCoord.y * chunkSize  // Fixed: use .y instead of .z
            );
            
            // Create tiles for this chunk
            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    CreateTile(chunkData, chunkData.tiles[x, z], x, z, chunkObject.transform);
                }
            }
            
            return chunkObject;
        }
        
        /// <summary>
        /// Create a single tile
        /// </summary>
        private void CreateTile(ChunkData chunkData, TileType tileType, int x, int z, Transform parent)
        {
            float tileSizeVal = GetTileSize();
            GameObject tile;

            if (tileType == TileType.Water)
            {
                if (suppressWaterTilesForSuimono)
                    return;

                // Water: flat Quad for realistic surface (no cube walls)
                tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tile.name = $"{tileType}_{x}_{z}";
                tile.transform.parent = parent;
                tile.transform.localPosition = new Vector3(x + 0.5f, 0.002f, z + 0.5f);
                tile.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                tile.transform.localScale = new Vector3(tileSizeVal, tileSizeVal, 1f);
                var col = tile.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
            else
            {
                tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"{tileType}_{x}_{z}";
                tile.transform.parent = parent;
                tile.transform.localPosition = new Vector3(x, 0, z);
                tile.transform.localScale = Vector3.one * tileSizeVal;
            }

            // Set material based on tile type
            Renderer renderer = tile.GetComponent<Renderer>();
            var mat = GetTileMaterial(tileType);
            if (mat != null)
            {
                renderer.sharedMaterial = mat;
            }
            else
            {
                renderer.material.color = GetTileColor(tileType);
            }

            // Optional: add grass decor on grass/forest tiles
            if (grassPrefab != null && (tileType == TileType.Grass || tileType == TileType.Forest))
            {
                var grass = Instantiate(grassPrefab, tile.transform);
                grass.transform.localPosition = new Vector3(0f, tile.transform.localScale.y * 0.5f, 0f);
            }

            int tileSeed = chunkGenerator != null
                ? (chunkData.chunkCoord.x * 1000 + chunkData.chunkCoord.y) * 256 + x * 16 + z
                : (x * 16 + z);
            if (useVegetationDensity && chunkGenerator != null && (tileType == TileType.Grass || tileType == TileType.Forest))
            {
                var worldPos = new Vector2(
                    chunkData.chunkCoord.x * chunkSize + x,
                    chunkData.chunkCoord.y * chunkSize + z
                );
                float density = VegetationDensityGenerator.GetTreeDensity(worldPos, chunkGenerator.GetSeed());
                if (density < 0.3f) tileSeed = -1;
            }

            bool packTrees = packTreePrefabs != null && packTreePrefabs.Length > 0;
            bool apiTrees = modelPool != null && modelPool.IsReady;
            bool shouldSpawnTree = tileSeed >= 0 && (packTrees || apiTrees)
                && (tileType == TileType.Grass || tileType == TileType.Forest);
            if (shouldSpawnTree)
            {
                float roll = useVegetationDensity
                    ? VegetationDensityGenerator.SeededRandom(tileSeed)
                    : Random.value;
                if (roll < aiDecorChance)
                {
                    float rx = useVegetationDensity ? VegetationDensityGenerator.SeededRandom(tileSeed + 1) - 0.5f : Random.value - 0.5f;
                    float rz = useVegetationDensity ? VegetationDensityGenerator.SeededRandom(tileSeed + 2) - 0.5f : Random.value - 0.5f;
                    float ry = useVegetationDensity ? VegetationDensityGenerator.SeededRandom(tileSeed + 3) * 360f : Random.Range(0f, 360f);
                    var localPos = new Vector3(x + rx * 0.8f, tileSizeVal * 0.5f, z + rz * 0.8f);
                    var rot = Quaternion.Euler(0, ry, 0);
                    if (packTrees)
                    {
                        var prefab = packTreePrefabs[Mathf.Abs(tileSeed) % packTreePrefabs.Length];
                        if (prefab != null)
                        {
                            var inst = Instantiate(prefab, parent);
                            inst.transform.localPosition = localPos;
                            inst.transform.localRotation = rot;
                            inst.transform.localScale = Vector3.one * (0.35f + Mathf.Abs(rx) * 0.25f);
                        }
                    }
                    else
                        modelPool.SpawnAt("tree", localPos, rot, parent);
                }
            }

            if (tileType == TileType.Mountain && packMountainRockPrefabs != null && packMountainRockPrefabs.Length > 0)
            {
                float rRock = VegetationDensityGenerator.SeededRandom(tileSeed + 17);
                if (rRock < mountainRockSpawnChance)
                {
                    var prefab = packMountainRockPrefabs[Mathf.Abs(tileSeed) % packMountainRockPrefabs.Length];
                    if (prefab != null)
                    {
                        float ry = VegetationDensityGenerator.SeededRandom(tileSeed + 3) * 360f;
                        float sc = Mathf.Lerp(mountainRockScaleMin, mountainRockScaleMax, VegetationDensityGenerator.SeededRandom(tileSeed + 5));
                        var inst = Instantiate(prefab, parent);
                        inst.transform.localPosition = new Vector3(x + 0.5f, tileSizeVal * 0.5f, z + 0.5f);
                        inst.transform.localRotation = Quaternion.Euler(0, ry, 0);
                        inst.transform.localScale = Vector3.one * sc;
                    }
                }
            }
        }

        /// <summary>
        /// Get material for a tile type (runtime override first, then serialized, else default)
        /// </summary>
        private Material GetTileMaterial(TileType tileType)
        {
            if (runtimeMaterials != null && runtimeMaterials.TryGetValue(tileType, out var rt) && rt != null)
                return rt;
            if (tileType == TileType.Water && waterMaterial == null)
                return GetOrCreateDefaultWaterMaterial();
            switch (tileType)
            {
                case TileType.Water: return waterMaterial;
                case TileType.Sand: return sandMaterial;
                case TileType.Grass: return grassMaterial;
                case TileType.Forest: return forestMaterial;
                case TileType.Mountain: return mountainMaterial;
                case TileType.Snow: return snowMaterial;
                default: return null;
            }
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

        /// <summary>
        /// Set AI-generated texture for a terrain type. Updates existing and future tiles.
        /// Called by TerrainTextureGenerator.
        /// </summary>
        public void SetTerrainTexture(TileType tileType, Texture2D texture)
        {
            if (texture == null) return;

            var baseMat = GetTileMaterial(tileType);
            if (baseMat == null)
                baseMat = grassMaterial ?? new Material(Aetheria.AI.URPMaterialHelper.GetLitShader());

            var runtimeMat = new Material(baseMat);
            runtimeMat.mainTexture = texture;
            SetRuntimeMaterial(tileType, runtimeMat);

            foreach (var chunk in loadedChunks.Values)
            {
                for (int i = 0; i < chunk.transform.childCount; i++)
                {
                    var tile = chunk.transform.GetChild(i);
                    if (ParseTileTypeFromName(tile.name, out var t) && t == tileType)
                    {
                        var r = tile.GetComponent<Renderer>();
                        if (r != null) r.sharedMaterial = runtimeMat;
                    }
                }
            }
        }

        private readonly Dictionary<TileType, Material> runtimeMaterials = new Dictionary<TileType, Material>();

        private void SetRuntimeMaterial(TileType t, Material m)
        {
            if (runtimeMaterials.ContainsKey(t) && runtimeMaterials[t] != null)
                Destroy(runtimeMaterials[t]);
            runtimeMaterials[t] = m;
        }

        private static bool ParseTileTypeFromName(string name, out TileType type)
        {
            type = TileType.Grass;
            var parts = name.Split('_');
            if (parts.Length < 1) return false;
            return System.Enum.TryParse(parts[0], true, out type);
        }

        private static Material GetOrCreateDefaultWaterMaterial()
        {
            if (_defaultWaterMaterial != null) return _defaultWaterMaterial;
            var shader = Shader.Find("Aetheria/Water Simple URP") ?? Shader.Find("Aetheria/Water Simple");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            _defaultWaterMaterial = new Material(shader != null ? shader : Aetheria.AI.URPMaterialHelper.GetLitShader());
            _defaultWaterMaterial.color = new Color(0.1f, 0.35f, 0.55f, 0.88f);
            if (_defaultWaterMaterial.HasProperty("_Glossiness"))
                _defaultWaterMaterial.SetFloat("_Glossiness", 0.95f);
            if (_defaultWaterMaterial.HasProperty("_Smoothness"))
                _defaultWaterMaterial.SetFloat("_Smoothness", 0.95f);
            return _defaultWaterMaterial;
        }
    }
}