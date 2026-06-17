#if UNITY_EDITOR
using Aetheria.Combat;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates <c>Assets/Resources/WindManaBow/WindManaBow_Visual.prefab</c> for <see cref="WindManaBow"/>.
/// Run once after clone, or when you want to refresh the default bow mesh.
/// </summary>
public static class WindManaBowVisualPrefabCreator
{
    public const string PrefabAssetPath = "Assets/Resources/WindManaBow/WindManaBow_Visual.prefab";

    [MenuItem("Aetheria/Combat/Create Wind Mana Bow Visual Prefab")]
    public static void CreatePrefabAsset()
    {
        var root = WindManaBow.CreatePackagedBowVisualRoot();
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        var dir = "Assets/Resources/WindManaBow";
        if (!AssetDatabase.IsValidFolder(dir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder("Assets/Resources", "WindManaBow");
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabAssetPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (prefab != null)
            EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[WindManaBow] Saved bow visual prefab: {PrefabAssetPath} (loaded at runtime via Resources/{WindManaBow.PackagedBowResourcePath}).");
    }

    [MenuItem("Aetheria/Combat/Assign Bow Prefab To WindManaBow (selected)")]
    public static void AssignToSelectedWindManaBow()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogWarning("[WindManaBow] Select a GameObject with WindManaBow.");
            return;
        }

        var bow = go.GetComponent<WindManaBow>();
        if (bow == null)
        {
            Debug.LogWarning("[WindManaBow] Selected object has no WindManaBow component.");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[WindManaBow] Prefab missing. Run **Aetheria → Combat → Create Wind Mana Bow Visual Prefab** first. Expected: {PrefabAssetPath}");
            return;
        }

        var so = new SerializedObject(bow);
        var prop = so.FindProperty("meshyBowPrefab");
        if (prop == null)
        {
            Debug.LogError("[WindManaBow] Serialized field meshyBowPrefab not found (rename?).");
            return;
        }

        prop.objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bow);
        Debug.Log($"[WindManaBow] Assigned Meshy Bow Prefab on '{go.name}'.");
    }
}
#endif
