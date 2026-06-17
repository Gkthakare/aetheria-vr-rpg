using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Aetheria.World;
using Aetheria.Dungeons;

namespace Aetheria.AI
{
    /// <summary>
    /// HTTP client for communicating with the AI backend API
    /// Uses Unity's built-in JsonUtility (no external dependencies)
    /// </summary>
    public class AIClient : MonoBehaviour
    {
        [Header("API Configuration")]
        [Tooltip("3d-monster-gen FastAPI default: http://localhost:8001 — set to your LAN/tunnel URL for Quest builds.")]
        [SerializeField] private string apiBaseUrl = "http://localhost:8001";
        [SerializeField] private float requestTimeout = 30f;

        private static AIClient instance;
        public static AIClient Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("AIClient");
                    instance = go.AddComponent<AIClient>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Parse player text into spawn/catalog intent (vertical slice: POST /api/v1/intent/parse).
        /// </summary>
        public void ParseIntent(string text, System.Action<IntentParseResponse> onSuccess, System.Action<string> onError = null)
        {
            StartCoroutine(ParseIntentCoroutine(text, onSuccess, onError));
        }

        private IEnumerator ParseIntentCoroutine(string text, System.Action<IntentParseResponse> onSuccess, System.Action<string> onError)
        {
            var request = new IntentParseHttpRequest { text = text, locale = "en" };
            string json = JsonUtility.ToJson(request);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest www = new UnityWebRequest($"{apiBaseUrl}/api/v1/intent/parse", "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = (int)requestTimeout;

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<IntentParseResponse>(www.downloadHandler.text);
                        onSuccess?.Invoke(response);
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Failed to parse intent response: {e.Message}");
                    }
                }
                else
                {
                    onError?.Invoke($"API Error: {www.error}");
                }
            }
        }

        /// <summary>
        /// Generate chunk generation parameters from player intent
        /// </summary>
        public void GetChunkParameters(string intent, System.Action<ChunkParameters> onSuccess, System.Action<string> onError = null)
        {
            StartCoroutine(GetChunkParametersCoroutine(intent, onSuccess, onError));
        }

        private IEnumerator GetChunkParametersCoroutine(string intent, System.Action<ChunkParameters> onSuccess, System.Action<string> onError)
        {
            // Get player level and chunk info from existing systems
            int playerLevel = 1; // Default, can be extended with actual player stats
            string[] previousChunks = new string[0];
            
            var chunkMgr = FindObjectOfType<ChunkManager>();
            if (chunkMgr != null)
            {
                // ChunkManager doesn't expose chunk IDs directly, so we'll use empty array
                // This can be extended if ChunkManager is updated to track chunk IDs
            }
            
            var request = new ChunkParameterRequest
            {
                intent = intent,
                player_level = playerLevel,
                previous_chunks = previousChunks
            };

            string json = JsonUtility.ToJson(request);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest www = new UnityWebRequest($"{apiBaseUrl}/api/v1/chunk/parameters", "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = (int)requestTimeout;

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<ChunkParametersResponse>(www.downloadHandler.text);
                        onSuccess?.Invoke(response.parameters);
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Failed to parse response: {e.Message}");
                    }
                }
                else
                {
                    onError?.Invoke($"API Error: {www.error}");
                }
            }
        }

        /// <summary>
        /// Process player message and get NPC response
        /// </summary>
        public void ProcessMessage(string message, string npcId, System.Action<string> onSuccess, System.Action<string> onError = null)
        {
            StartCoroutine(ProcessMessageCoroutine(message, npcId, onSuccess, onError));
        }

        private IEnumerator ProcessMessageCoroutine(string message, string npcId, System.Action<string> onSuccess, System.Action<string> onError)
        {
            var request = new MessageRequest
            {
                message = message,
                npc_id = npcId,
                context = new GameContext
                {
                    player_level = 1, // Default, can be extended
                    current_location = "unknown" // Can be extended to get from ChunkManager
                }
            };

            string json = JsonUtility.ToJson(request);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest www = new UnityWebRequest($"{apiBaseUrl}/api/v1/message/process", "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = (int)requestTimeout;

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<MessageResponse>(www.downloadHandler.text);
                        onSuccess?.Invoke(response.text);
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Failed to parse response: {e.Message}");
                    }
                }
                else
                {
                    onError?.Invoke($"API Error: {www.error}");
                }
            }
        }

        /// <summary>
        /// Transcribe voice recording
        /// </summary>
        public void TranscribeVoice(byte[] audioData, string filename, System.Action<string> onSuccess, System.Action<string> onError = null)
        {
            StartCoroutine(TranscribeVoiceCoroutine(audioData, filename, onSuccess, onError));
        }

        private IEnumerator TranscribeVoiceCoroutine(byte[] audioData, string filename, System.Action<string> onSuccess, System.Action<string> onError)
        {
            WWWForm form = new WWWForm();
            form.AddBinaryData("file", audioData, filename, "audio/wav");

            using (UnityWebRequest www = UnityWebRequest.Post($"{apiBaseUrl}/api/v1/voice/transcribe", form))
            {
                www.timeout = (int)requestTimeout;
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<TranscriptionResponse>(www.downloadHandler.text);
                        onSuccess?.Invoke(response.text);
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Failed to parse response: {e.Message}");
                    }
                }
                else
                {
                    onError?.Invoke($"API Error: {www.error}");
                }
            }
        }

        /// <summary>
        /// Generate dungeon expansion
        /// </summary>
        public void GenerateDungeonGrowth(string dungeonId, System.Action<DungeonGrowthData> onSuccess, System.Action<string> onError = null)
        {
            StartCoroutine(GenerateDungeonGrowthCoroutine(dungeonId, onSuccess, onError));
        }

        private IEnumerator GenerateDungeonGrowthCoroutine(string dungeonId, System.Action<DungeonGrowthData> onSuccess, System.Action<string> onError)
        {
            // Get dungeon size from DungeonGenerator if available
            int currentSize = 0;
            var dungeonGen = FindObjectOfType<DungeonGenerator>();
            if (dungeonGen != null && dungeonGen.CurrentGraph != null)
            {
                currentSize = dungeonGen.CurrentGraph.Nodes.Count;
            }
            
            var request = new DungeonGrowthRequest
            {
                dungeon_id = dungeonId,
                player_level = 1, // Default, can be extended
                current_size = currentSize
            };

            string json = JsonUtility.ToJson(request);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest www = new UnityWebRequest($"{apiBaseUrl}/api/v1/dungeon/grow", "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = (int)requestTimeout;

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<DungeonGrowthResponse>(www.downloadHandler.text);
                        onSuccess?.Invoke(response.growth_data);
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Failed to parse response: {e.Message}");
                    }
                }
                else
                {
                    onError?.Invoke($"API Error: {www.error}");
                }
            }
        }

        /// <summary>
        /// Generate AI opponent based on player stats
        /// </summary>
        public void GenerateOpponent(System.Action<OpponentData> onSuccess, System.Action<string> onError = null)
        {
            StartCoroutine(GenerateOpponentCoroutine(onSuccess, onError));
        }

        private IEnumerator GenerateOpponentCoroutine(System.Action<OpponentData> onSuccess, System.Action<string> onError)
        {
            // Get player stats - can be extended when player stats system is added
            var request = new OpponentRequest
            {
                player_level = 1, // Default, can be extended
                player_rank = 1, // Default, can be extended
                player_skills = new string[0], // Can be extended
                has_secret_items = false // Can be extended
            };

            string json = JsonUtility.ToJson(request);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest www = new UnityWebRequest($"{apiBaseUrl}/api/v1/opponent/generate", "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = (int)requestTimeout;

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<OpponentResponse>(www.downloadHandler.text);
                        onSuccess?.Invoke(response.opponent);
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Failed to parse response: {e.Message}");
                    }
                }
                else
                {
                    onError?.Invoke($"API Error: {www.error}");
                }
            }
        }

        /// <summary>
        /// Check API health
        /// </summary>
        public void CheckHealth(System.Action<bool> onComplete)
        {
            StartCoroutine(CheckHealthCoroutine(onComplete));
        }

        private IEnumerator CheckHealthCoroutine(System.Action<bool> onComplete)
        {
            using (UnityWebRequest www = UnityWebRequest.Get($"{apiBaseUrl}/health"))
            {
                www.timeout = 5;
                yield return www.SendWebRequest();
                onComplete?.Invoke(www.result == UnityWebRequest.Result.Success);
            }
        }
    }

    // Request/Response Models
    [Serializable]
    public class ChunkParameterRequest
    {
        public string intent;
        public int player_level;
        public string[] previous_chunks;
    }

    [Serializable]
    public class ChunkParametersResponse
    {
        public ChunkParameters parameters;
    }

    [Serializable]
    public class ChunkParameters
    {
        public string biome_type;
        public float difficulty;
        public string[] features;
        public int size;
        public string seed;
        
        // Convert to ChunkGenerator compatible format
        public int GetSeedAsInt()
        {
            if (int.TryParse(seed, out int seedInt))
                return seedInt;
            return seed.GetHashCode();
        }
        
        // Map biome_type to noise parameters for ChunkGenerator
        public void ApplyToChunkGenerator(ChunkGenerator generator)
        {
            if (generator == null) return;
            
            generator.SetSeed(GetSeedAsInt());
            // Additional biome-specific parameters can be set here
        }
    }

    [Serializable]
    public class MessageRequest
    {
        public string message;
        public string npc_id;
        public GameContext context;
    }

    [Serializable]
    public class GameContext
    {
        public int player_level;
        public string current_location;
    }

    [Serializable]
    public class MessageResponse
    {
        public string text;
        public float confidence;
    }

    [Serializable]
    public class TranscriptionResponse
    {
        public string text;
        public float confidence;
    }

    [Serializable]
    public class DungeonGrowthRequest
    {
        public string dungeon_id;
        public int player_level;
        public int current_size;
    }

    [Serializable]
    public class DungeonGrowthResponse
    {
        public DungeonGrowthData growth_data;
    }

    [Serializable]
    public class DungeonGrowthData
    {
        public string[] new_rooms;
        public string[] new_enemies;
        public string[] new_rewards;
    }

    [Serializable]
    public class OpponentRequest
    {
        public int player_level;
        public int player_rank;
        public string[] player_skills;
        public bool has_secret_items;
    }

    [Serializable]
    public class OpponentResponse
    {
        public OpponentData opponent;
    }

    [Serializable]
    public class OpponentData
    {
        public string name;
        public string type;
        public int level;
        public int health;
        public int attack;
        public string[] abilities;
    }
}
