using UnityEngine;
using Aetheria.World;
using Aetheria.Dungeons;
using Aetheria.Combat;
using Aetheria.ElementalMagic;

namespace Aetheria.Voice
{
    /// <summary>
    /// Voice command processor that maps speech to world generation parameters.
    /// Integrates with ChunkGenerator, DungeonGenerator, and AbilitySystem.
    /// </summary>
    public class VoiceCommandProcessor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ChunkGenerator chunkGenerator;
        [SerializeField] private DungeonGenerator dungeonGenerator;
        [SerializeField] private AbilitySystem abilitySystem;
        
        [Header("Voice Settings")]
        [SerializeField] private bool enableVoiceCommands = true;
        [SerializeField] private float commandCooldown = 1f;
        
        private float lastCommandTime;
        
        void Start()
        {
            // Auto-find components if not assigned
            if (chunkGenerator == null) chunkGenerator = FindObjectOfType<ChunkGenerator>();
            if (dungeonGenerator == null) dungeonGenerator = FindObjectOfType<DungeonGenerator>();
            if (abilitySystem == null) abilitySystem = FindObjectOfType<AbilitySystem>();
        }
        
        void Update()
        {
            // Simulate voice commands with keyboard for testing
            if (enableVoiceCommands && Time.time - lastCommandTime > commandCooldown)
            {
                ProcessKeyboardCommands();
            }
        }
        
        /// <summary>
        /// Process keyboard commands (simulating voice input)
        /// </summary>
        private void ProcessKeyboardCommands()
        {
            // World generation commands
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
                ProcessVoiceCommand("generate mountain world");
            }
            
            // Dungeon commands
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                ProcessVoiceCommand("create easy dungeon");
            }
            if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                ProcessVoiceCommand("create hard dungeon");
            }
            
            // Ability commands
            if (Input.GetKeyDown(KeyCode.Alpha6))
            {
                ProcessVoiceCommand("use light attack");
            }
            if (Input.GetKeyDown(KeyCode.Alpha7))
            {
                ProcessVoiceCommand("use heavy attack");
            }
        }
        
        /// <summary>
        /// Process voice command and map to system parameters
        /// </summary>
        public void ProcessVoiceCommand(string command)
        {
            if (Time.time - lastCommandTime < commandCooldown) return;

            command = command.ToLower().Trim();
            if (string.IsNullOrEmpty(command))
                return;

            if (ElementalManaAndMagicHub.TryConsumeVoiceCommand(command))
            {
                lastCommandTime = Time.time;
                Debug.Log($"[Voice] Elemental magic handled: '{command}'");
                return;
            }

            lastCommandTime = Time.time;

            Debug.Log($"[Voice] Processing command: '{command}'");

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
            
            // Seed commands
            else if (command.Contains("seed") || command.Contains("world"))
            {
                int newSeed = Random.Range(1000, 9999);
                SetNewSeed(newSeed);
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
            // Adjust terrain types for forest
            // This would modify the ChunkGenerator's terrain types
            SetNewSeed(12345); // Forest seed
        }
        
        private void GenerateDesertWorld()
        {
            Debug.Log("[Voice] Generating desert world...");
            SetNewSeed(54321); // Desert seed
        }
        
        private void GenerateMountainWorld()
        {
            Debug.Log("[Voice] Generating mountain world...");
            SetNewSeed(99999); // Mountain seed
        }
        
        private void GenerateWaterWorld()
        {
            Debug.Log("[Voice] Generating water world...");
            SetNewSeed(11111); // Water seed
        }
        
        // Dungeon generation methods
        private void CreateEasyDungeon()
        {
            Debug.Log("[Voice] Creating easy dungeon...");
            dungeonGenerator.SetSeedAndFloor(12345, 1); // Easy floor
        }
        
        private void CreateHardDungeon()
        {
            Debug.Log("[Voice] Creating hard dungeon...");
            dungeonGenerator.SetSeedAndFloor(12345, 10); // Hard floor
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
    }
}