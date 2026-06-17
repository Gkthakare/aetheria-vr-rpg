using UnityEngine;
using System.Text.RegularExpressions;
using Aetheria.World;

namespace Aetheria.AI
{
    /// <summary>
    /// Handles voice and message input for AI generation.
    /// Parse commands like "generate mountain", "generate water with blue ocean" and trigger generation.
    /// </summary>
    public class GenerationInputController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TerrainTextureGenerator terrainTextureGenerator;
        [SerializeField] private VoiceProcessor voiceProcessor;

        private void Start()
        {
            if (terrainTextureGenerator == null) terrainTextureGenerator = FindObjectOfType<TerrainTextureGenerator>();
            if (voiceProcessor == null) voiceProcessor = FindObjectOfType<VoiceProcessor>();

            if (voiceProcessor != null)
                voiceProcessor.OnTranscriptionComplete += OnVoiceInput;
        }

        private void OnDestroy()
        {
            if (voiceProcessor != null)
                voiceProcessor.OnTranscriptionComplete -= OnVoiceInput;
        }

        private void OnVoiceInput(string text)
        {
            ProcessInput(text);
        }

        /// <summary>Process text from voice or message. Returns true if handled as generation.</summary>
        public bool ProcessInput(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim().ToLowerInvariant();

            // "generate mountain", "generate water", "generate sand with golden desert"
            var genMatch = Regex.Match(text, @"generate\s+(\w+)(?:\s+with\s+(.+))?");
            if (genMatch.Success)
            {
                var component = genMatch.Groups[1].Value;
                var customPrompt = genMatch.Groups[2].Success ? genMatch.Groups[2].Value.Trim() : null;

                if (terrainTextureGenerator != null && TryParseTerrainType(component, out var tileType))
                {
                    if (!string.IsNullOrEmpty(customPrompt))
                        terrainTextureGenerator.GenerateWithPrompt(tileType, customPrompt);
                    else
                        terrainTextureGenerator.GenerateForTerrain(tileType);
                    Debug.Log($"[GenerationInput] Triggered: {tileType}" + (customPrompt != null ? $" with '{customPrompt}'" : ""));
                    return true;
                }
            }

            // "generate all terrain" / "generate all"
            if (terrainTextureGenerator != null && (text.Contains("generate all") || text.Contains("generate terrain")))
            {
                terrainTextureGenerator.GenerateAllTerrain();
                Debug.Log("[GenerationInput] Triggered: all terrain");
                return true;
            }

            // AI-match island disc to chunk terrain (sand + grass; uses TerrainTextureGenerator island sync)
            if (terrainTextureGenerator != null &&
                (text.Contains("style island") || text.Contains("island texture") || text.Contains("match island")))
            {
                terrainTextureGenerator.GenerateForTerrain(TileType.Sand);
                terrainTextureGenerator.GenerateForTerrain(TileType.Grass);
                Debug.Log("[GenerationInput] Triggered: island sand + grass textures");
                return true;
            }

            // "go to floor 5" / "floor 5" / "load floor 3"
            var floorMatch = Regex.Match(text, @"(?:go to |load )?floor\s+(\d+)");
            if (floorMatch.Success)
            {
                var floorMgr = FindObjectOfType<Aetheria.World.FloorManager>();
                if (floorMgr != null && int.TryParse(floorMatch.Groups[1].Value, out int floorNum))
                {
                    floorMgr.LoadFloor(floorNum);
                    Debug.Log($"[GenerationInput] Loaded floor {floorNum}");
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseTerrainType(string word, out TileType type)
        {
            type = TileType.Grass;
            switch (word)
            {
                case "mountain": case "mountains": case "rock": type = TileType.Mountain; return true;
                case "water": case "ocean": case "sea": type = TileType.Water; return true;
                case "sand": case "desert": type = TileType.Sand; return true;
                case "grass": type = TileType.Grass; return true;
                case "forest": case "woods": type = TileType.Forest; return true;
                case "snow": type = TileType.Snow; return true;
                default: return false;
            }
        }
    }
}
