using System;
using UnityEngine;
using Aetheria.AI;
using Aetheria.World;

namespace Aetheria.AI
{
    /// <summary>
    /// Extracts intent from text/voice input and converts to chunk generation parameters
    /// </summary>
    public class LLMIntentExtractor : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool useAIBackend = true;

        private VoiceProcessor voiceProcessor;
        private AIClient aiClient;

        public event System.Action<ChunkParameters> OnParametersExtracted;

        private void Start()
        {
            voiceProcessor = FindObjectOfType<VoiceProcessor>();
            aiClient = AIClient.Instance;

            if (voiceProcessor != null)
            {
                voiceProcessor.OnTranscriptionComplete += HandleTranscription;
            }
        }

        /// <summary>
        /// Extract intent from text input
        /// </summary>
        public void ExtractIntentFromText(string text)
        {
            if (useAIBackend)
            {
                // Use AI backend to extract intent and get parameters
                aiClient.GetChunkParameters(
                    text,
                    (parameters) =>
                    {
                        Debug.Log($"Extracted parameters: {parameters.biome_type}, difficulty: {parameters.difficulty}");
                        
                        // Apply parameters to ChunkGenerator
                        var chunkGen = FindObjectOfType<ChunkGenerator>();
                        if (chunkGen != null)
                        {
                            parameters.ApplyToChunkGenerator(chunkGen);
                            
                            // Clear and regenerate chunks
                            var chunkMgr = FindObjectOfType<ChunkManager>();
                            if (chunkMgr != null)
                            {
                                chunkMgr.ClearAllChunks();
                            }
                        }
                        
                        OnParametersExtracted?.Invoke(parameters);
                    },
                    (error) =>
                    {
                        Debug.LogError($"Failed to extract intent: {error}");
                        // Fallback to simple keyword matching
                        ExtractIntentSimple(text);
                    }
                );
            }
            else
            {
                ExtractIntentSimple(text);
            }
        }

        /// <summary>
        /// Handle voice transcription
        /// </summary>
        private void HandleTranscription(string transcription)
        {
            ExtractIntentFromText(transcription);
        }

        /// <summary>
        /// Simple keyword-based intent extraction (fallback)
        /// </summary>
        private void ExtractIntentSimple(string text)
        {
            text = text.ToLower();
            ChunkParameters parameters = new ChunkParameters();

            // Simple keyword matching
            if (text.Contains("forest") || text.Contains("wood"))
            {
                parameters.biome_type = "forest";
            }
            else if (text.Contains("desert") || text.Contains("sand"))
            {
                parameters.biome_type = "desert";
            }
            else if (text.Contains("mountain") || text.Contains("peak"))
            {
                parameters.biome_type = "mountain";
            }
            else if (text.Contains("cave") || text.Contains("dungeon"))
            {
                parameters.biome_type = "cave";
            }
            else
            {
                parameters.biome_type = "plains"; // default
            }

            // Extract difficulty
            if (text.Contains("easy") || text.Contains("simple"))
            {
                parameters.difficulty = 0.3f;
            }
            else if (text.Contains("hard") || text.Contains("difficult") || text.Contains("challenge"))
            {
                parameters.difficulty = 0.8f;
            }
            else
            {
                parameters.difficulty = 0.5f; // default
            }

            // Extract size
            if (text.Contains("small") || text.Contains("tiny"))
            {
                parameters.size = 50;
            }
            else if (text.Contains("large") || text.Contains("big") || text.Contains("huge"))
            {
                parameters.size = 200;
            }
            else
            {
                parameters.size = 100; // default
            }

            parameters.seed = UnityEngine.Random.Range(0, int.MaxValue).ToString();
            parameters.features = new string[] { "trees", "rocks" }; // default

            OnParametersExtracted?.Invoke(parameters);
        }
    }
}

