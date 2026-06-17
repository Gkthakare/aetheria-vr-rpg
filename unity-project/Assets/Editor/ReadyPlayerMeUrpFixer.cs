using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// One-shot tool: converts the materials on the selected Ready Player Me avatar
// (the GLB or its instantiated GameObject) to URP/Lit, copying textures from the
// sibling files in the avatar folder by naming convention.
//
// Usage:
//   1. In Hierarchy, select the avatar root GameObject (e.g. readyplayerme_cyberpunk).
//   2. Top menu -> Aetheria -> Avatar -> Fix RPM Materials For URP.
//   3. Save scene with Ctrl+S.
public static class ReadyPlayerMeUrpFixer
{
    private const string MenuPath = "Aetheria/Avatar/Fix RPM Materials For URP";

    private static readonly Dictionary<string, string[]> BaseMapHints = new()
    {
        { "Wolf3D_Body", new[] { "body_texture", "Wolf3D_Body" } },
        { "Wolf3D_Head", new[] { "face_texture", "Wolf3D_Head" } },
        { "Wolf3D_Hair", new[] { "hair_color", "Wolf3D_Hair" } },
        { "Wolf3D_Outfit_Top", new[] { "Shirt_Base", "Top_Base" } },
        { "Wolf3D_Outfit_Bottom", new[] { "Pants_Base", "Bottom_Base" } },
        { "Wolf3D_Outfit_Footwear", new[] { "Shoes_Base", "Footwear_Base" } },
        { "Wolf3D_Glasses", new[] { "Cyberpunk_Glasses", "glasses" } },
        { "Wolf3D_Teeth", new[] { "teeth", "Wolf3D_Teeth" } },
        { "EyeLeft", new[] { "Cyberpunk_Eye", "eye_white", "EyeLeft" } },
        { "EyeRight", new[] { "Cyberpunk_Eye", "eye_white", "EyeRight" } },
    };

    private static readonly Dictionary<string, string[]> NormalMapHints = new()
    {
        { "Wolf3D_Body", new[] { "body_normal", "Body_Norm", "Body_Nor" } },
        { "Wolf3D_Head", new[] { "face_normal", "Head_Norm" } },
        { "Wolf3D_Hair", new[] { "hair_32_nm" } },
        { "Wolf3D_Outfit_Top", new[] { "Shirt_Nor", "Shirt_Norm" } },
        { "Wolf3D_Outfit_Bottom", new[] { "Pants_Nor", "Pants_Norm" } },
        { "Wolf3D_Outfit_Footwear", new[] { "Shoes_Nor", "Shoes_Norm" } },
    };

    [MenuItem(MenuPath)]
    public static void FixMaterials()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog(
                "Fix RPM Materials",
                "Select the avatar root GameObject in the Hierarchy first " +
                "(e.g. readyplayerme_cyberpunk).",
                "OK");
            return;
        }

        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            EditorUtility.DisplayDialog(
                "Fix RPM Materials",
                "URP/Lit shader not found. Make sure the Universal RP package " +
                "is installed and the project's Render Pipeline Asset is set.",
                "OK");
            return;
        }

        var avatarFolder = FindAvatarFolderForSelection(go);
        var textureLookup = BuildTextureLookup(avatarFolder);

        var renderers = go.GetComponentsInChildren<Renderer>(includeInactive: true);
        var materialsFolder = EnsureMaterialsFolder(avatarFolder);
        int fixedCount = 0;

        foreach (var renderer in renderers)
        {
            var sharedMaterials = renderer.sharedMaterials;
            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                var key = renderer.gameObject.name;
                var newMat = CreateOrUpdateUrpMaterial(
                    key,
                    sharedMaterials[i],
                    materialsFolder,
                    lit,
                    textureLookup);
                if (newMat != null)
                {
                    sharedMaterials[i] = newMat;
                    fixedCount++;
                }
            }
            renderer.sharedMaterials = sharedMaterials;
            EditorUtility.SetDirty(renderer);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ReadyPlayerMeUrpFixer] Updated {fixedCount} material slots on '{go.name}'.");
    }

    private static string FindAvatarFolderForSelection(GameObject go)
    {
        var prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
        var path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : null;
        if (string.IsNullOrEmpty(path))
        {
            return "Assets/ImportedContent/Characters/full-body-cyberpunk-male";
        }
        return Path.GetDirectoryName(path).Replace("\\", "/");
    }

    private static Dictionary<string, Texture2D> BuildTextureLookup(string folder)
    {
        var dict = new Dictionary<string, Texture2D>(System.StringComparer.OrdinalIgnoreCase);
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return dict;
        }
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        foreach (var guid in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var name = Path.GetFileNameWithoutExtension(p);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (tex != null && !dict.ContainsKey(name))
            {
                dict[name] = tex;
            }
        }
        return dict;
    }

    private static string EnsureMaterialsFolder(string avatarFolder)
    {
        var path = avatarFolder + "/Materials_URP";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(avatarFolder, "Materials_URP");
        }
        return path;
    }

    private static Material CreateOrUpdateUrpMaterial(
        string slotKey,
        Material original,
        string materialsFolder,
        Shader lit,
        Dictionary<string, Texture2D> textureLookup)
    {
        if (original == null) return null;

        var matName = $"{slotKey}_URP.mat";
        var matPath = $"{materialsFolder}/{matName}";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(lit);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else
        {
            mat.shader = lit;
        }

        var baseTex = FindFirst(textureLookup, slotKey, BaseMapHints);
        if (baseTex != null)
        {
            mat.SetTexture("_BaseMap", baseTex);
        }
        mat.SetColor("_BaseColor", Color.white);

        var normalTex = FindFirst(textureLookup, slotKey, NormalMapHints);
        if (normalTex != null)
        {
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_BumpMap", normalTex);
        }

        // Conservative defaults that look good on RPM avatars under URP/Lit.
        mat.SetFloat("_Smoothness", 0.25f);
        mat.SetFloat("_Metallic", 0.0f);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Texture2D FindFirst(
        Dictionary<string, Texture2D> textureLookup,
        string slotKey,
        Dictionary<string, string[]> hintMap)
    {
        if (!hintMap.TryGetValue(slotKey, out var hints)) return null;
        foreach (var hint in hints)
        {
            foreach (var pair in textureLookup)
            {
                if (pair.Key.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return pair.Value;
                }
            }
        }
        return null;
    }
}
