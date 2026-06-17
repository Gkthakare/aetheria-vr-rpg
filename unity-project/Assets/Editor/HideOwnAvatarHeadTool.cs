using Aetheria.VR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Aetheria.EditorTools
{
    /// <summary>
    /// Aetheria -> Avatar -> Hide Own Avatar Head (Force All)
    ///
    /// Walks every loaded scene, finds anything that looks like a Ready Player Me
    /// avatar (has a 'Hips' descendant plus 'Wolf3D_*' or 'EyeLeft' / 'EyeRight'
    /// mesh nodes) and attaches RpmAvatarFirstPersonHider to its root, so the
    /// head, hair, eyes, teeth, glasses, headwear and beard renderers are
    /// turned off every frame at runtime. The XR camera will then never see
    /// the inside of the player's own head.
    ///
    /// Safe to run multiple times. Marks the scene dirty so you can save.
    /// </summary>
    public static class HideOwnAvatarHeadTool
    {
        private const string MenuPath = "Aetheria/Avatar/Hide Own Avatar Head (Force All)";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            int attached = 0;
            int alreadyPresent = 0;

#if UNITY_2023_1_OR_NEWER
            var smrs = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var smrs = Object.FindObjectsOfType<SkinnedMeshRenderer>(true);
#endif

            var visited = new System.Collections.Generic.HashSet<Transform>();
            foreach (var smr in smrs)
            {
                if (smr == null) continue;
                var root = FindRpmAvatarRoot(smr.transform);
                if (root == null) continue;
                if (!visited.Add(root)) continue;

                var existing = root.GetComponent<RpmAvatarFirstPersonHider>();
                if (existing != null)
                {
                    alreadyPresent++;
                    continue;
                }

                Undo.AddComponent<RpmAvatarFirstPersonHider>(root.gameObject);
                attached++;
                Debug.Log($"[HideOwnAvatarHeadTool] Attached RpmAvatarFirstPersonHider to '{root.name}'.");
            }

            EditorSceneManager.MarkAllScenesDirty();

            EditorUtility.DisplayDialog(
                "Hide Own Avatar Head",
                $"Done.\n\n" +
                $"Attached:  {attached} avatar root(s)\n" +
                $"Already present:  {alreadyPresent}\n\n" +
                $"Now press Play (or Build) — the head/hair/eyes/teeth/glasses meshes will\n" +
                $"be turned off at runtime so they cannot block the HMD view.\n\n" +
                $"If chest/shoulders still poke into your view, open the avatar root in the\n" +
                $"Inspector, expand RpmAvatarFirstPersonHider, and tick 'Hide Torso And Outfit'.",
                "OK");
        }

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
    }
}
