using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using Aetheria.World;

namespace Aetheria.Tests
{
    /// <summary>
    /// Tests for deterministic chunk generation.
    /// Verifies that the same seed always produces identical results.
    /// </summary>
    public class ChunkGenerationTests
    {
        private ChunkGenerator chunkGenerator;
        private GameObject testObject;
        
        [SetUp]
        public void Setup()
        {
            testObject = new GameObject("TestChunkGenerator");
            chunkGenerator = testObject.AddComponent<ChunkGenerator>();
        }
        
        [TearDown]
        public void TearDown()
        {
            if (testObject != null)
            {
                Object.DestroyImmediate(testObject);
            }
        }
        
        [Test]
        public void ChunkGenerator_SameSeed_ProducesIdenticalChunks()
        {
            // Arrange
            int seed = 12345;
            Vector2Int chunkCoord = new Vector2Int(0, 0);
            
            // Act
            chunkGenerator.SetSeed(seed);
            ChunkData chunk1 = chunkGenerator.GenerateChunk(chunkCoord);
            
            chunkGenerator.SetSeed(seed);
            ChunkData chunk2 = chunkGenerator.GenerateChunk(chunkCoord);
            
            // Assert
            Assert.AreEqual(chunk1.chunkCoord, chunk2.chunkCoord);
            Assert.AreEqual(chunk1.tiles.GetLength(0), chunk2.tiles.GetLength(0));
            Assert.AreEqual(chunk1.tiles.GetLength(1), chunk2.tiles.GetLength(1));
            
            // Check every tile is identical
            for (int x = 0; x < chunk1.tiles.GetLength(0); x++)
            {
                for (int z = 0; z < chunk1.tiles.GetLength(1); z++)
                {
                    Assert.AreEqual(chunk1.tiles[x, z], chunk2.tiles[x, z], 
                        $"Tiles at ({x}, {z}) should be identical");
                }
            }
        }
        
        [Test]
        public void ChunkGenerator_DifferentSeeds_ProducesDifferentChunks()
        {
            // Arrange
            Vector2Int chunkCoord = new Vector2Int(0, 0);
            
            // Act
            chunkGenerator.SetSeed(12345);
            ChunkData chunk1 = chunkGenerator.GenerateChunk(chunkCoord);
            
            chunkGenerator.SetSeed(54321);
            ChunkData chunk2 = chunkGenerator.GenerateChunk(chunkCoord);
            
            // Assert
            bool tilesAreDifferent = false;
            for (int x = 0; x < chunk1.tiles.GetLength(0); x++)
            {
                for (int z = 0; z < chunk1.tiles.GetLength(1); z++)
                {
                    if (chunk1.tiles[x, z] != chunk2.tiles[x, z])
                    {
                        tilesAreDifferent = true;
                        break;
                    }
                }
                if (tilesAreDifferent) break;
            }
            
            Assert.IsTrue(tilesAreDifferent, "Different seeds should produce different chunks");
        }
        
        [Test]
        public void ChunkGenerator_ChunkCaching_WorksCorrectly()
        {
            // Arrange
            int seed = 12345;
            Vector2Int chunkCoord = new Vector2Int(0, 0);
            
            // Act
            chunkGenerator.SetSeed(seed);
            ChunkData chunk1 = chunkGenerator.GenerateChunk(chunkCoord);
            ChunkData chunk2 = chunkGenerator.GenerateChunk(chunkCoord);
            
            // Assert
            Assert.AreSame(chunk1, chunk2, "Same chunk should be returned from cache");
        }
        
        [UnityTest]
        public IEnumerator ChunkManager_LoadsChunksAroundPlayer()
        {
            // Arrange
            GameObject playerObject = new GameObject("TestPlayer");
            playerObject.transform.position = Vector3.zero;
            
            GameObject chunkManagerObject = new GameObject("TestChunkManager");
            ChunkManager chunkManager = chunkManagerObject.AddComponent<ChunkManager>();
            ChunkGenerator chunkGenerator = chunkManagerObject.AddComponent<ChunkGenerator>();
            
            // Set references
            var playerField = typeof(ChunkManager).GetField("player", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            playerField?.SetValue(chunkManager, playerObject.transform);
            
            var chunkGeneratorField = typeof(ChunkManager).GetField("chunkGenerator", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            chunkGeneratorField?.SetValue(chunkManager, chunkGenerator);
            
            // Act
            yield return new WaitForSeconds(0.5f); // Let chunk manager update
            
            // Assert
            int loadedChunkCount = chunkManager.GetLoadedChunkCount();
            Assert.Greater(loadedChunkCount, 0, "Should have loaded at least one chunk");
            
            // Cleanup
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(chunkManagerObject);
        }
    }
}