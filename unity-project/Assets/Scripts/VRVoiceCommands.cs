using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Aetheria.World;
using Aetheria.Dungeons;
using Aetheria.Combat;

namespace Aetheria.VR
{
    /// <summary>
    /// VR-specific voice/gesture command bridge for Quest controllers.
    /// Lives in Assembly-CSharp (not Aetheria.VR asmdef) so it can reference world/dungeon/combat types.
    /// </summary>
    public class VRVoiceCommands : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ChunkGenerator chunkGenerator;
        [SerializeField] private DungeonGenerator dungeonGenerator;
        [SerializeField] private AbilitySystem abilitySystem;
        [SerializeField] private VRPlayerController vrController;

        [Header("Settings")]
        [SerializeField] private bool enableCommands = true;
        [SerializeField] private float commandCooldown = 1.0f;

        private InputDevice leftController;
        private InputDevice rightController;
        private bool leftGrip;
        private bool rightGrip;
        private float lastCommandAt;

        private void Awake()
        {
            if (chunkGenerator == null) chunkGenerator = FindObjectOfType<ChunkGenerator>();
            if (dungeonGenerator == null) dungeonGenerator = FindObjectOfType<DungeonGenerator>();
            if (abilitySystem == null) abilitySystem = FindObjectOfType<AbilitySystem>();
            if (vrController == null) vrController = FindObjectOfType<VRPlayerController>();
        }

        private void Update()
        {
            if (!enableCommands) return;
            UpdateXRDevices();
            ReadGrips();
            HandleMappings();

            if (Input.GetKeyDown(KeyCode.Alpha1)) SetNewSeed(12345);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetNewSeed(54321);
            if (Input.GetKeyDown(KeyCode.Alpha3)) dungeonGenerator?.SetSeedAndFloor(12345, 1);
            if (Input.GetKeyDown(KeyCode.Alpha4)) dungeonGenerator?.SetSeedAndFloor(12345, 10);
            if (Input.GetKeyDown(KeyCode.J)) abilitySystem?.TryUseSkill(WeaponSkills.LightCut);
            if (Input.GetKeyDown(KeyCode.K)) abilitySystem?.TryUseSkill(WeaponSkills.HeavyCleave);
            if (Input.GetKeyDown(KeyCode.L)) abilitySystem?.TryUseSkill(WeaponSkills.FinisherThrust);
        }

        private void UpdateXRDevices()
        {
            var left = new List<InputDevice>();
            var right = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, left);
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, right);
            if (left.Count > 0) leftController = left[0];
            if (right.Count > 0) rightController = right[0];
        }

        private void ReadGrips()
        {
            if (leftController.isValid)
                leftController.TryGetFeatureValue(CommonUsages.gripButton, out leftGrip);
            if (rightController.isValid)
                rightController.TryGetFeatureValue(CommonUsages.gripButton, out rightGrip);
        }

        private void HandleMappings()
        {
            if (Time.time - lastCommandAt < commandCooldown) return;

            if (leftGrip && rightGrip)
            {
                lastCommandAt = Time.time;
                int newSeed = Random.Range(1000, 9999);
                SetNewSeed(newSeed);
                Debug.Log($"[VRVoiceCommands] New world via both grips. Seed {newSeed}");
                return;
            }

            if (leftGrip)
            {
                lastCommandAt = Time.time;
                Debug.Log("[VRVoiceCommands] Left grip detected (placeholder for teleport)");
                return;
            }

            if (rightGrip)
            {
                lastCommandAt = Time.time;
                abilitySystem?.TryUseSkill(WeaponSkills.LightCut);
                return;
            }
        }

        private void SetNewSeed(int seed)
        {
            if (chunkGenerator != null)
                chunkGenerator.SetSeed(seed);
        }
    }
}
