#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Aetheria.EditorTools
{
    /// <summary>
    /// HelloVR references prefabs by fixed GUIDs. If Unity logs "Missing Prefab Asset" while files exist on disk,
    /// force a reimport or restore <c>.meta</c> files from version control.
    /// </summary>
    public static class AetheriaSceneDependencyRepair
    {
        const string WorldManagerGuid = "d5fe4947b3942f1458d226baabe0dd4b";
        const string DragonGuid = "3e4e54e8861e5994b8e03bb82dafeecd";

        const string WorldManagerPath = "Assets/Scenes/WorldManager.prefab";
        const string DragonPath = "Assets/ImportedContent/Creatures/creature.dragon.v1/Dragon.prefab";

        [MenuItem("Aetheria/Scenes/Reimport HelloVR dependencies (WorldManager + Dragon)")]
        public static void ReimportHelloVrPrefabDependencies()
        {
            ReimportAndVerify(WorldManagerPath, WorldManagerGuid, "WorldManager");
            ReimportAndVerify(DragonPath, DragonGuid, "Dragon");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Aetheria] Finished reimport. Re-open HelloVR.unity if warnings persist; otherwise delete the Library folder and reopen the project.");
        }

        static void ReimportAndVerify(string assetPath, string expectedGuid, string label)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var absolute = projectRoot != null ? Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)) : null;

            if (absolute == null || !File.Exists(absolute))
            {
                Debug.LogError(
                    $"[Aetheria] Missing file for {label}: {assetPath}. Restore it (and its .meta) from git, or run a full clone of the repo.");
                return;
            }

            var resolved = AssetDatabase.GUIDToAssetPath(expectedGuid);
            if (string.IsNullOrEmpty(resolved))
            {
                Debug.LogWarning(
                    $"[Aetheria] Unity has no asset registered for GUID {expectedGuid} ({label}). " +
                    $"Check that {assetPath}.meta exists and contains that guid, then run this menu again.");
            }
            else if (resolved != assetPath)
            {
                Debug.LogWarning(
                    $"[Aetheria] GUID {expectedGuid} maps to '{resolved}', expected '{assetPath}'. " +
                    "Fix duplicate or wrong .meta files so only one asset owns this guid.");
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[Aetheria] Forced reimport: {assetPath}");
        }
    }
}
#endif
