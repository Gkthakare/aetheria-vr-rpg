using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Aetheria.World;
using Aetheria.Dungeons;
using Aetheria.Combat;
using Aetheria.AI;
using Aetheria.ElementalMagic;

namespace Aetheria.Voice
{
    /// <summary>
    /// Voice integration system with keyboard simulation and voice recording.
    /// Maps voice commands to world generation parameters.
    /// Ready for future integration with Whisper STT and OpenAI API.
    /// </summary>
    public class VoiceIntegrationSystem : MonoBehaviour
    {
        [Header("API Configuration")]
        [SerializeField] private bool enableAIIntegration = false; // Set to true when you have API keys
        
        [Header("Voice Settings")]
        [SerializeField] private float voiceTimeout = 5f; // Timeout for voice processing
        [SerializeField] private float commandCooldown = 2f;
        [SerializeField] private bool enableVoiceCommands = true;
        
        [Header("References")]
        [SerializeField] private ChunkGenerator chunkGenerator;
        [SerializeField] private DungeonGenerator dungeonGenerator;
        [SerializeField] private AbilitySystem abilitySystem;
        [SerializeField] private GenerationInputController generationInput;
        
        private AudioSource audioSource;
        private bool isRecording = false;
        private float lastCommandTime;
        
        // Voice command patterns
        private readonly Dictionary<string, string> commandPatterns = new Dictionary<string, string>
        {
            // World generation
            {"forest", "generate forest world"},
            {"desert", "generate desert world"},
            {"mountain", "generate mountain world"},
            {"water", "generate water world"},
            {"ocean", "generate water world"},
            
            // Dungeon commands
            {"easy dungeon", "create easy dungeon"},
            {"hard dungeon", "create hard dungeon"},
            {"dungeon", "create random dungeon"},
            {"labyrinth", "create random dungeon"},
            
            // Abilities
            {"light attack", "use light attack"},
            {"heavy attack", "use heavy attack"},
            {"finisher", "use finisher attack"},
            {"ultimate", "use finisher attack"},
            
            // General
            {"new world", "generate new world"},
            {"change world", "generate new world"},
            {"reset", "generate new world"}
        };
        
        void Start()
        {
            // Initialize components
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            
            // Initialize voice system
            if (enableAIIntegration)
            {
                Debug.Log("[Voice] AI Integration enabled - will use AI backend when available");
            }
            
            // Auto-find references
            if (chunkGenerator == null) chunkGenerator = FindObjectOfType<ChunkGenerator>();
            if (dungeonGenerator == null) dungeonGenerator = FindObjectOfType<DungeonGenerator>();
            if (abilitySystem == null) abilitySystem = FindObjectOfType<AbilitySystem>();
            if (generationInput == null) generationInput = FindObjectOfType<GenerationInputController>();
            
            Debug.Log($"[Voice] Voice Integration System initialized (Timeout: {voiceTimeout}s)");
        }
        
        void Update()
        {
            // Voice recording controls
            if (enableVoiceCommands && Input.GetKeyDown(KeyCode.V))
            {
                StartVoiceRecording();
            }
            
            if (isRecording && Input.GetKeyUp(KeyCode.V))
            {
                StopVoiceRecording();
            }
            
            // Fallback keyboard commands for testing
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ProcessVoiceCommand("generate forest world");
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                ProcessVoiceCommand("generate desert world");
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                ProcessVoiceCommand("create easy dungeon");
            }

            // Elemental magic — F5–F9 (Alpha5–8 are used by VoiceCommandProcessor for dungeon/tests)
            if (Input.GetKeyDown(KeyCode.F5))
                ProcessVoiceCommand("cast water");
            if (Input.GetKeyDown(KeyCode.F6))
                ProcessVoiceCommand("cast fire");
            if (Input.GetKeyDown(KeyCode.F7))
                ProcessVoiceCommand("cast shadow");
            if (Input.GetKeyDown(KeyCode.F8))
                ProcessVoiceCommand("cast earth");
            if (Input.GetKeyDown(KeyCode.F9))
                ProcessVoiceCommand("throw");
        }
        
        /// <summary>
        /// Start recording voice input
        /// </summary>
        public void StartVoiceRecording()
        {
            if (isRecording) return;
            
            isRecording = true;
            Debug.Log("[Voice] Recording started... (Hold V key)");
            
            // Start recording from microphone
            string deviceName = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
            if (deviceName != null)
            {
                audioSource.clip = Microphone.Start(deviceName, false, 10, 44100);
            }
        }
        
        /// <summary>
        /// Stop recording and process voice input
        /// </summary>
        public void StopVoiceRecording()
        {
            if (!isRecording) return;
            
            isRecording = false;
            Debug.Log("[Voice] Recording stopped, processing...");
            
            // Stop microphone
            Microphone.End(null);
            
            // Process the recorded audio
            StartCoroutine(ProcessVoiceInput());
        }
        
        /// <summary>
        /// Process recorded voice input
        /// </summary>
        private IEnumerator ProcessVoiceInput()
        {
            // For now, simulate voice processing
            // In a real implementation, this would send audio to Whisper API
            // Use voiceTimeout to limit processing time
            yield return new WaitForSeconds(Mathf.Min(1f, voiceTimeout));
            
            // Simulate voice recognition result
            string[] simulatedCommands = {
                "generate forest world",
                "create easy dungeon", 
                "use light attack",
                "generate new world"
            };
            
            string simulatedCommand = simulatedCommands[Random.Range(0, simulatedCommands.Length)];
            Debug.Log($"[Voice] Simulated voice recognition: '{simulatedCommand}'");
            
            ProcessVoiceCommand(simulatedCommand);
        }
        
        /// <summary>
        /// Process voice command and map to system parameters
        /// </summary>
        public void ProcessVoiceCommand(string command)
        {
            if (Time.time - lastCommandTime < commandCooldown) return;

            command = command.Trim();
            if (string.IsNullOrEmpty(command))
                return;

            if (WindManaBow.TryConsumeVoiceCommand(command))
            {
                lastCommandTime = Time.time;
                return;
            }

            if (ElementalManaAndMagicHub.TryConsumeVoiceCommand(command))
            {
                lastCommandTime = Time.time;
                return;
            }

            lastCommandTime = Time.time;

            Debug.Log($"[Voice] Processing command: '{command}'");

            // Try generation commands first (texture, 3D, etc.)
            if (generationInput != null && generationInput.ProcessInput(command))
                return;
            
            // Check for exact matches first
            if (commandPatterns.ContainsKey(command))
            {
                ProcessVoiceCommand(commandPatterns[command]);
                return;
            }
            
            // Check for partial matches
            foreach (var pattern in commandPatterns)
            {
                if (command.Contains(pattern.Key))
                {
                    ProcessVoiceCommand(pattern.Value);
                    return;
                }
            }
            
            // Try simple interpretation
            ProcessSimpleCommand(command);
        }
        
        
        /// <summary>
        /// Process simple voice commands without AI
        /// </summary>
        private void ProcessSimpleCommand(string command)
        {
            // World generation commands
            if (command.Contains("forest") || command.Contains("green"))
            {
                GenerateForestWorld();
            }
            else if (command.Contains("desert") || command.Contains("sand"))
            {
                GenerateDesertWorld();
            }
            else if (command.Contains("mountain") || command.Contains("rock"))
            {
                GenerateMountainWorld();
            }
            else if (command.Contains("water") || command.Contains("ocean"))
            {
                GenerateWaterWorld();
            }
            
            // Dungeon commands
            else if (command.Contains("easy") || command.Contains("simple"))
            {
                CreateEasyDungeon();
            }
            else if (command.Contains("hard") || command.Contains("difficult"))
            {
                CreateHardDungeon();
            }
            else if (command.Contains("dungeon") || command.Contains("labyrinth"))
            {
                CreateRandomDungeon();
            }
            
            // Ability commands
            else if (command.Contains("light") || command.Contains("quick"))
            {
                UseLightAttack();
            }
            else if (command.Contains("heavy") || command.Contains("strong"))
            {
                UseHeavyAttack();
            }
            else if (command.Contains("finisher") || command.Contains("ultimate"))
            {
                UseFinisherAttack();
            }
            
            // General commands
            else if (command.Contains("new") || command.Contains("change") || command.Contains("reset"))
            {
                GenerateNewWorld();
            }
            
            else
            {
                Debug.Log($"[Voice] Unknown command: '{command}'");
            }
        }
        
        // World generation methods
        private void GenerateForestWorld()
        {
            Debug.Log("[Voice] Generating forest world...");
            SetNewSeed(12345);
        }
        
        private void GenerateDesertWorld()
        {
            Debug.Log("[Voice] Generating desert world...");
            SetNewSeed(54321);
        }
        
        private void GenerateMountainWorld()
        {
            Debug.Log("[Voice] Generating mountain world...");
            SetNewSeed(99999);
        }
        
        private void GenerateWaterWorld()
        {
            Debug.Log("[Voice] Generating water world...");
            SetNewSeed(11111);
        }
        
        private void GenerateNewWorld()
        {
            Debug.Log("[Voice] Generating new world...");
            int newSeed = Random.Range(1000, 9999);
            SetNewSeed(newSeed);
        }
        
        // Dungeon generation methods
        private void CreateEasyDungeon()
        {
            Debug.Log("[Voice] Creating easy dungeon...");
            dungeonGenerator.SetSeedAndFloor(12345, 1);
        }
        
        private void CreateHardDungeon()
        {
            Debug.Log("[Voice] Creating hard dungeon...");
            dungeonGenerator.SetSeedAndFloor(12345, 10);
        }
        
        private void CreateRandomDungeon()
        {
            Debug.Log("[Voice] Creating random dungeon...");
            int randomFloor = Random.Range(1, 20);
            dungeonGenerator.SetSeedAndFloor(Random.Range(1000, 9999), randomFloor);
        }
        
        // Ability methods
        private void UseLightAttack()
        {
            Debug.Log("[Voice] Using light attack...");
            abilitySystem.TryUseSkill(WeaponSkills.LightCut);
        }
        
        private void UseHeavyAttack()
        {
            Debug.Log("[Voice] Using heavy attack...");
            abilitySystem.TryUseSkill(WeaponSkills.HeavyCleave);
        }
        
        private void UseFinisherAttack()
        {
            Debug.Log("[Voice] Using finisher attack...");
            abilitySystem.TryUseSkill(WeaponSkills.FinisherThrust);
        }
        
        // Utility methods
        private void SetNewSeed(int seed)
        {
            chunkGenerator.SetSeed(seed);
            Debug.Log($"[Voice] World seed changed to: {seed}");
        }
        
        void OnDestroy()
        {
            // Cleanup if needed
        }
    }
}