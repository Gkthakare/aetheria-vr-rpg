using System.Text;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Aetheria.Gameplay
{
    /// <summary>
    /// Lightweight runtime/editor validator for combat prerequisites:
    /// - NavMesh baked and non-empty
    /// - Enemy tag exists
    /// - Optional objects present in scene
    /// </summary>
    public sealed class NavMeshSetupValidator : MonoBehaviour
    {
        [SerializeField] private bool runValidationOnStart = true;
        [SerializeField] private bool logSuccessAsInfo = true;
        [SerializeField] private bool checkExpectedObjects = true;

        [Header("Optional expected objects")]
        [SerializeField] private string expectedXrRigName = "XR Origin (XR Rig)";
        [SerializeField] private string expectedWorldManagerName = "IslandWorldManager";

        public bool LastNavMeshOk { get; private set; }
        public bool LastEnemyTagOk { get; private set; }
        public bool LastExpectedObjectsOk { get; private set; }
        public bool LastImportedAssetsOk { get; private set; }
        public string LastReport { get; private set; } = "Not validated yet.";

        private void Start()
        {
            if (runValidationOnStart)
                ValidateNow();
        }

        [ContextMenu("Validate NavMesh + Tags + Scene Objects")]
        public void ValidateNow()
        {
            var sb = new StringBuilder();
            LastNavMeshOk = CheckNavMesh(sb);
            LastEnemyTagOk = CheckEnemyTag(sb);
            LastExpectedObjectsOk = checkExpectedObjects ? CheckExpectedObjects(sb) : true;
            LastImportedAssetsOk = CheckImportedAssets(sb);

            bool ok = LastNavMeshOk && LastEnemyTagOk && LastExpectedObjectsOk && LastImportedAssetsOk;
            LastReport = sb.ToString().Trim();

            if (ok)
            {
                if (logSuccessAsInfo)
                    Debug.Log($"[NavMeshSetupValidator] OK\n{LastReport}");
            }
            else
            {
                Debug.LogWarning($"[NavMeshSetupValidator] Setup issues found:\n{LastReport}");
            }
        }

        private static bool CheckNavMesh(StringBuilder sb)
        {
            var tri = NavMesh.CalculateTriangulation();
            bool ok = tri.vertices != null && tri.vertices.Length > 0;
            sb.AppendLine(ok
                ? $"NavMesh: OK ({tri.vertices.Length} vertices)"
                : "NavMesh: MISSING (add NavMeshSurface and Bake/BuildNavMesh for island + dungeon)");
            return ok;
        }

        private static bool CheckEnemyTag(StringBuilder sb)
        {
            try
            {
                // Throws UnityException when tag is undefined.
                GameObject.FindWithTag("Enemy");
                sb.AppendLine("Tag 'Enemy': OK");
                return true;
            }
            catch (UnityException)
            {
                sb.AppendLine("Tag 'Enemy': MISSING (add in Project Settings > Tags and Layers)");
                return false;
            }
        }

        private bool CheckExpectedObjects(StringBuilder sb)
        {
            bool ok = true;
            if (!string.IsNullOrWhiteSpace(expectedXrRigName))
            {
                var xr = GameObject.Find(expectedXrRigName);
                bool xok = xr != null;
                ok &= xok;
                sb.AppendLine(xok ? $"Scene object: {expectedXrRigName} OK" : $"Scene object: {expectedXrRigName} MISSING");
            }

            if (!string.IsNullOrWhiteSpace(expectedWorldManagerName))
            {
                var wm = GameObject.Find(expectedWorldManagerName);
                bool wok = wm != null;
                ok &= wok;
                sb.AppendLine(wok ? $"Scene object: {expectedWorldManagerName} OK" : $"Scene object: {expectedWorldManagerName} MISSING");
            }

            return ok;
        }

        private static bool CheckImportedAssets(StringBuilder sb)
        {
#if UNITY_EDITOR
            int bowCount = CountImportedGameObjectAssets("Assets/ImportedContent/Creatures/weapon.bow.mythic.v1");
            int brickCount = CountImportedGameObjectAssets("Assets/ImportedContent/Creatures/environment.dungeon.bricks.v1");
            int portalCount = CountImportedGameObjectAssets("Assets/ImportedContent/Creatures/environment.portal.arcane.v1");
            int scaryCount = CountImportedGameObjectAssets("Assets/ImportedContent/Creatures/scary-monster");
            bool bow = bowCount > 0;
            bool brick = brickCount > 0;
            bool portal = portalCount > 0;
            bool scary = scaryCount > 0;
            sb.AppendLine(bow ? "Imported Bow: OK" : "Imported Bow: MISSING");
            sb.AppendLine(brick ? "Imported Bricks: OK" : "Imported Bricks: MISSING");
            sb.AppendLine(portal ? "Imported Portal: OK" : "Imported Portal: MISSING");
            sb.AppendLine(scary ? "Imported Scary Monster: OK" : "Imported Scary Monster: MISSING");
            sb.AppendLine($"Imported Counts: bow={bowCount}, bricks={brickCount}, portal={portalCount}, scary={scaryCount}");
            return bow && brick && portal;
#else
            sb.AppendLine("Imported Assets: runtime check skipped (editor-only).");
            return true;
#endif
        }

#if UNITY_EDITOR
        private static int CountImportedGameObjectAssets(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
                return 0;

            // Path-scoped check: if any FBX/prefab model GameObject exists under the folder, treat as present.
            var guids = AssetDatabase.FindAssets("t:GameObject", new[] { root });
            int count = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                    count++;
            }
            return count;
        }
#endif
    }
}
