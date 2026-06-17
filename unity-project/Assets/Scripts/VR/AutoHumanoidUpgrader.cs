using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Aetheria.VR
{
    /// <summary>
    /// Detects Ready Player Me / Wolf3D-style avatars in the scene at runtime,
    /// builds a Unity Humanoid <see cref="Avatar"/> by mapping the predictable
    /// RPM bone names to Unity humanoid bones, and assigns it to the Animator.
    /// Once <see cref="Animator.isHuman"/> becomes true, attaches
    /// <see cref="VRFullBodyIkController"/> so first-person body presence
    /// (head, hands, feet) is driven from XR targets.
    ///
    /// Required because glTFast (com.unity.cloud.gltfast) imports GLB files as
    /// Generic Mecanim only and does not expose a Humanoid rig option in its
    /// Inspector. This component closes that gap without modifying the GLB.
    ///
    /// VERBOSE: This component logs every step so it is easy to see what is or
    /// is not happening. Drop verboseLogs to false later to silence.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AutoHumanoidUpgrader : MonoBehaviour
    {
        [Header("Upgrade behaviour")]
        [SerializeField] private float watchSeconds = 8f;
        [SerializeField] private float watchInterval = 0.2f;
        [SerializeField] private bool addIkController = true;
        [Tooltip("If true, also auto-attach RpmAvatarFirstPersonHider to every detected RPM avatar so head/hair never blocks the HMD view. Strongly recommended for first-person VR.")]
        [SerializeField] private bool addFirstPersonHider = true;
        [SerializeField] private bool verboseLogs = true;
        [SerializeField] private bool runOnceManually = false;

        private static readonly Dictionary<string, string> RpmToUnity = new()
        {
            { "Hips", "Hips" },
            { "Spine", "Spine" },
            { "Spine1", "Chest" },
            { "Spine2", "UpperChest" },
            { "Neck", "Neck" },
            { "Head", "Head" },

            { "LeftShoulder", "LeftShoulder" },
            { "LeftArm", "LeftUpperArm" },
            { "LeftForeArm", "LeftLowerArm" },
            { "LeftHand", "LeftHand" },

            { "RightShoulder", "RightShoulder" },
            { "RightArm", "RightUpperArm" },
            { "RightForeArm", "RightLowerArm" },
            { "RightHand", "RightHand" },

            { "LeftUpLeg", "LeftUpperLeg" },
            { "LeftLeg", "LeftLowerLeg" },
            { "LeftFoot", "LeftFoot" },
            { "LeftToeBase", "LeftToes" },

            { "RightUpLeg", "RightUpperLeg" },
            { "RightLeg", "RightLowerLeg" },
            { "RightFoot", "RightFoot" },
            { "RightToeBase", "RightToes" },

            { "LeftEye", "LeftEye" },
            { "RightEye", "RightEye" },

            { "LeftHandThumb1", "Left Thumb Proximal" },
            { "LeftHandThumb2", "Left Thumb Intermediate" },
            { "LeftHandThumb3", "Left Thumb Distal" },
            { "LeftHandIndex1", "Left Index Proximal" },
            { "LeftHandIndex2", "Left Index Intermediate" },
            { "LeftHandIndex3", "Left Index Distal" },
            { "LeftHandMiddle1", "Left Middle Proximal" },
            { "LeftHandMiddle2", "Left Middle Intermediate" },
            { "LeftHandMiddle3", "Left Middle Distal" },
            { "LeftHandRing1", "Left Ring Proximal" },
            { "LeftHandRing2", "Left Ring Intermediate" },
            { "LeftHandRing3", "Left Ring Distal" },
            { "LeftHandPinky1", "Left Little Proximal" },
            { "LeftHandPinky2", "Left Little Intermediate" },
            { "LeftHandPinky3", "Left Little Distal" },

            { "RightHandThumb1", "Right Thumb Proximal" },
            { "RightHandThumb2", "Right Thumb Intermediate" },
            { "RightHandThumb3", "Right Thumb Distal" },
            { "RightHandIndex1", "Right Index Proximal" },
            { "RightHandIndex2", "Right Index Intermediate" },
            { "RightHandIndex3", "Right Index Distal" },
            { "RightHandMiddle1", "Right Middle Proximal" },
            { "RightHandMiddle2", "Right Middle Intermediate" },
            { "RightHandMiddle3", "Right Middle Distal" },
            { "RightHandRing1", "Right Ring Proximal" },
            { "RightHandRing2", "Right Ring Intermediate" },
            { "RightHandRing3", "Right Ring Distal" },
            { "RightHandPinky1", "Right Little Proximal" },
            { "RightHandPinky2", "Right Little Intermediate" },
            { "RightHandPinky3", "Right Little Distal" },
        };

        private static readonly string[] CoreRequired =
        {
            "Hips", "Spine", "Head",
            "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
            "RightUpperLeg", "RightLowerLeg", "RightFoot",
        };

        private readonly HashSet<int> _processed = new();
        private float _watchUntil;
        private float _nextScanAt;
        private bool _everReportedAvatarCount;

        private void Awake()
        {
            Debug.Log($"[AutoHumanoidUpgrader] Awake on '{gameObject.name}'. watchSeconds={watchSeconds}, addIkController={addIkController}.");
        }

        private void OnEnable()
        {
            _watchUntil = Time.realtimeSinceStartup + Mathf.Max(0.5f, watchSeconds);
            _nextScanAt = 0f;
            _everReportedAvatarCount = false;
            Debug.Log($"[AutoHumanoidUpgrader] OnEnable: watching for {watchSeconds:F1}s.");
        }

        private void Update()
        {
            if (runOnceManually)
            {
                runOnceManually = false;
                Debug.Log("[AutoHumanoidUpgrader] Manual trigger fired.");
                ScanAndUpgradeOnce(true);
                return;
            }

            if (Time.realtimeSinceStartup > _watchUntil) return;
            if (Time.realtimeSinceStartup < _nextScanAt) return;
            _nextScanAt = Time.realtimeSinceStartup + Mathf.Max(0.05f, watchInterval);

            ScanAndUpgradeOnce(false);
        }

        private void ScanAndUpgradeOnce(bool forceVerbose)
        {
            EnsureAnimatorsOnRpmRoots(forceVerbose);

#if UNITY_2023_1_OR_NEWER
            var animators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var animators = Object.FindObjectsOfType<Animator>(true);
#endif
            if ((forceVerbose || verboseLogs) && !_everReportedAvatarCount)
            {
                Debug.Log($"[AutoHumanoidUpgrader] First scan: found {animators.Length} Animator(s) in scene.");
                _everReportedAvatarCount = true;
            }

            foreach (var a in animators)
            {
                if (a == null) continue;
                int id = a.GetInstanceID();
                if (_processed.Contains(id)) continue;

                if (a.isHuman)
                {
                    if (forceVerbose || verboseLogs)
                        Debug.Log($"[AutoHumanoidUpgrader] Animator '{a.name}' is already humanoid. Skipping.");
                    if (addIkController && a.GetComponent<VRFullBodyIkController>() == null)
                    {
                        a.gameObject.AddComponent<VRFullBodyIkController>();
                        Debug.Log($"[AutoHumanoidUpgrader] Added VRFullBodyIkController on '{a.name}' (already humanoid).");
                    }
                    _processed.Add(id);
                    continue;
                }

                bool hasBones = HasRpmBones(a.transform, out int boneHits, out string sample);
                if (forceVerbose || verboseLogs)
                    Debug.Log($"[AutoHumanoidUpgrader] Animator '{a.name}' bone hits={boneHits} sample={sample}.");

                if (!hasBones)
                {
                    _processed.Add(id);
                    continue;
                }

                if (TryUpgrade(a, forceVerbose))
                {
                    _processed.Add(id);
                    if (addIkController && a.GetComponent<VRFullBodyIkController>() == null)
                    {
                        a.gameObject.AddComponent<VRFullBodyIkController>();
                        Debug.Log($"[AutoHumanoidUpgrader] Added VRFullBodyIkController on '{a.name}'.");
                    }
                }
                else
                {
                    _processed.Add(id);
                }
            }
        }

        // Walk every root transform in every loaded scene; if a hierarchy looks
        // like an RPM avatar (has 'Hips' descendant + at least one SkinnedMeshRenderer)
        // and lacks an Animator on the root, add one. Required because glTFast
        // omits Animator on GLBs that have no animation clips.
        // Also attaches RpmAvatarFirstPersonHider unconditionally so the
        // player's head is hidden even if humanoid conversion later fails.
        private void EnsureAnimatorsOnRpmRoots(bool forceVerbose)
        {
#if UNITY_2023_1_OR_NEWER
            var smrs = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var smrs = Object.FindObjectsOfType<SkinnedMeshRenderer>(true);
#endif
            var visitedRoots = new HashSet<Transform>();
            foreach (var smr in smrs)
            {
                if (smr == null) continue;
                var root = FindRpmAvatarRoot(smr.transform);
                if (root == null) continue;
                if (!visitedRoots.Add(root)) continue;

                if (root.GetComponent<Animator>() == null)
                {
                    root.gameObject.AddComponent<Animator>();
                    if (forceVerbose || verboseLogs)
                        Debug.Log($"[AutoHumanoidUpgrader] Added missing Animator on RPM avatar root '{root.name}'.");
                }

                if (addFirstPersonHider && root.GetComponent<RpmAvatarFirstPersonHider>() == null)
                {
                    root.gameObject.AddComponent<RpmAvatarFirstPersonHider>();
                    if (forceVerbose || verboseLogs)
                        Debug.Log($"[AutoHumanoidUpgrader] Attached RpmAvatarFirstPersonHider to '{root.name}' (head/face will be hidden from HMD).");
                }
            }
        }

        // Walks up from a SkinnedMeshRenderer until it finds an ancestor whose
        // descendants contain a transform named 'Hips' AND a 'Wolf3D_*' or
        // 'EyeLeft' / 'EyeRight' mesh node, indicating the RPM root.
        private static Transform FindRpmAvatarRoot(Transform start)
        {
            Transform cur = start;
            while (cur != null)
            {
                bool hasHips = false;
                bool hasWolf3D = false;
                foreach (var child in cur.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "Hips") hasHips = true;
                    if (child.name.StartsWith("Wolf3D_") || child.name == "EyeLeft" || child.name == "EyeRight")
                        hasWolf3D = true;
                    if (hasHips && hasWolf3D) return cur;
                }
                cur = cur.parent;
            }
            return null;
        }

        private static bool HasRpmBones(Transform root, out int hits, out string sample)
        {
            hits = 0;
            var sb = new StringBuilder();
            int sampleCount = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Hips" || t.name == "Spine" || t.name == "Head" ||
                    t.name == "LeftHand" || t.name == "RightHand" ||
                    t.name == "LeftArm" || t.name == "RightArm")
                {
                    hits++;
                    if (sampleCount < 5)
                    {
                        if (sampleCount > 0) sb.Append(", ");
                        sb.Append(t.name);
                        sampleCount++;
                    }
                }
            }
            sample = sb.Length == 0 ? "(none)" : sb.ToString();
            return hits >= 3;
        }

        private bool TryUpgrade(Animator animator, bool forceVerbose)
        {
            var avatarRoot = animator.transform;
            var nameToTransform = new Dictionary<string, Transform>();
            foreach (var t in avatarRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!nameToTransform.ContainsKey(t.name))
                    nameToTransform[t.name] = t;
            }

            var humanBones = new List<HumanBone>();
            var skeletonBones = new List<SkeletonBone>
            {
                new SkeletonBone
                {
                    name = avatarRoot.name,
                    position = avatarRoot.localPosition,
                    rotation = avatarRoot.localRotation,
                    scale = avatarRoot.localScale
                }
            };

            foreach (var t in avatarRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t == avatarRoot) continue;
                skeletonBones.Add(new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale
                });
            }

            foreach (var kvp in RpmToUnity)
            {
                if (!nameToTransform.TryGetValue(kvp.Key, out _)) continue;
                humanBones.Add(new HumanBone
                {
                    boneName = kvp.Key,
                    humanName = kvp.Value,
                    limit = new HumanLimit { useDefaultValues = true }
                });
            }

            var present = new HashSet<string>();
            foreach (var hb in humanBones) present.Add(hb.humanName);

            var missing = new List<string>();
            foreach (var req in CoreRequired)
                if (!present.Contains(req)) missing.Add(req);

            if (missing.Count > 0)
            {
                Debug.LogWarning(
                    $"[AutoHumanoidUpgrader] '{avatarRoot.name}' is missing core bones: {string.Join(", ", missing)}.\n" +
                    $"  Mapped bones: {humanBones.Count}. Need RPM names like 'Hips', 'Spine', 'LeftArm', etc. Skipping humanoid build.");
                return false;
            }

            if (forceVerbose || verboseLogs)
                Debug.Log($"[AutoHumanoidUpgrader] '{avatarRoot.name}' will be promoted: {humanBones.Count} bones mapped, {skeletonBones.Count} skeleton entries.");

            var humanDescription = new HumanDescription
            {
                human = humanBones.ToArray(),
                skeleton = skeletonBones.ToArray(),
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false
            };

            Avatar built = AvatarBuilder.BuildHumanAvatar(avatarRoot.gameObject, humanDescription);
            if (built == null || !built.isValid)
            {
                Debug.LogWarning(
                    $"[AutoHumanoidUpgrader] AvatarBuilder.BuildHumanAvatar failed on '{avatarRoot.name}'. " +
                    "Probably an invalid bone parent chain. Avatar will stay Generic.");
                return false;
            }

            built.name = avatarRoot.name + "_Humanoid";
            animator.avatar = built;

            Debug.Log(
                $"[AutoHumanoidUpgrader] SUCCESS: built humanoid Avatar with {humanBones.Count} bones for '{avatarRoot.name}'. isHuman={animator.isHuman}.");
            return animator.isHuman;
        }

        [ContextMenu("Upgrade Now (run from Inspector context menu)")]
        public void ContextMenuUpgrade()
        {
            ScanAndUpgradeOnce(true);
        }
    }
}
