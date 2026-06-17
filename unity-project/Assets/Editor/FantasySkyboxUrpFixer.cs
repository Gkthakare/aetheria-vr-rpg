using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// One-shot tool: converts every material under Assets/Fantasy Skybox FREE
// to a URP-compatible Skybox shader while keeping their existing textures.
//
// Reads texture references directly from the material's saved properties so it
// keeps working when the original shader is missing under URP (the magenta
// case). Then assigns the URP-compatible Skybox shader and re-binds textures.
//
// Usage:
//   1. Top menu -> Aetheria -> Skybox -> Fix Fantasy Skybox For URP.
//   2. Wait for the console line confirming how many were updated.
//   3. Open Window -> Rendering -> Lighting -> Environment, assign one of the
//      now-fixed FS skybox materials.
public static class FantasySkyboxUrpFixer
{
    private const string MenuPath = "Aetheria/Skybox/Fix Fantasy Skybox For URP";
    private const string SkyboxRoot = "Assets/Fantasy Skybox FREE";

    [MenuItem(MenuPath)]
    public static void FixAllFantasySkyboxes()
    {
        if (!AssetDatabase.IsValidFolder(SkyboxRoot))
        {
            Debug.LogError($"[FantasySkyboxUrpFixer] Folder not found: {SkyboxRoot}");
            return;
        }

        var cubemapShader = Shader.Find("Skybox/Cubemap");
        var panoramicShader = Shader.Find("Skybox/Panoramic");
        var sixSidedShader = Shader.Find("Skybox/6 Sided");
        if (cubemapShader == null || panoramicShader == null || sixSidedShader == null)
        {
            Debug.LogError(
                "[FantasySkyboxUrpFixer] Required Skybox shaders not present in this project.");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Material", new[] { SkyboxRoot });
        int total = 0;
        int converted = 0;
        var skipped = new List<string>();

        foreach (var guid in guids)
        {
            total++;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            var so = new SerializedObject(mat);

            var cubemap = GetSavedTexture(so, "_Tex") as Cubemap;
            var front = GetSavedTexture(so, "_FrontTex");
            var back = GetSavedTexture(so, "_BackTex");
            var left = GetSavedTexture(so, "_LeftTex");
            var right = GetSavedTexture(so, "_RightTex");
            var up = GetSavedTexture(so, "_UpTex");
            var down = GetSavedTexture(so, "_DownTex");
            var hasSixSided = front != null && back != null && left != null
                              && right != null && up != null && down != null;
            var mainTex = GetSavedTexture(so, "_MainTex");

            float exposure = GetSavedFloat(so, "_Exposure", 1f);
            float rotation = GetSavedFloat(so, "_Rotation", 0f);
            Color tint = GetSavedColor(so, "_Tint",
                new Color(0.5f, 0.5f, 0.5f, 0.5f));

            Shader target;
            if (cubemap != null)
            {
                target = cubemapShader;
            }
            else if (hasSixSided)
            {
                target = sixSidedShader;
            }
            else if (mainTex != null)
            {
                target = panoramicShader;
            }
            else
            {
                skipped.Add(path);
                continue;
            }

            mat.shader = target;
            if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", tint);
            if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", exposure);
            if (mat.HasProperty("_Rotation")) mat.SetFloat("_Rotation", rotation);

            if (target == cubemapShader)
            {
                mat.SetTexture("_Tex", cubemap);
            }
            else if (target == sixSidedShader)
            {
                mat.SetTexture("_FrontTex", front);
                mat.SetTexture("_BackTex", back);
                mat.SetTexture("_LeftTex", left);
                mat.SetTexture("_RightTex", right);
                mat.SetTexture("_UpTex", up);
                mat.SetTexture("_DownTex", down);
            }
            else if (target == panoramicShader)
            {
                mat.SetTexture("_MainTex", mainTex);
                if (mat.HasProperty("_Mapping")) mat.SetFloat("_Mapping", 1f);
                if (mat.HasProperty("_ImageType")) mat.SetFloat("_ImageType", 0f);
                if (mat.HasProperty("_Layout")) mat.SetFloat("_Layout", 0f);
                if (mat.HasProperty("_MirrorOnBack")) mat.SetFloat("_MirrorOnBack", 0f);
            }

            EditorUtility.SetDirty(mat);
            converted++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[FantasySkyboxUrpFixer] Scanned {total} materials, converted {converted}, " +
            $"skipped {skipped.Count} (no usable texture).");
        foreach (var skippedPath in skipped)
        {
            Debug.LogWarning($"[FantasySkyboxUrpFixer] Skipped (no usable texture): {skippedPath}");
        }
    }

    private static Texture GetSavedTexture(SerializedObject so, string propertyName)
    {
        var entry = FindTexEnvEntry(so, propertyName);
        if (entry == null) return null;
        var second = entry.FindPropertyRelative("second");
        if (second == null) return null;
        var texProp = second.FindPropertyRelative("m_Texture");
        return texProp != null ? texProp.objectReferenceValue as Texture : null;
    }

    private static float GetSavedFloat(SerializedObject so, string propertyName, float fallback)
    {
        var arr = so.FindProperty("m_SavedProperties.m_Floats");
        if (arr == null) return fallback;
        for (int i = 0; i < arr.arraySize; i++)
        {
            var item = arr.GetArrayElementAtIndex(i);
            if (item.FindPropertyRelative("first").stringValue == propertyName)
            {
                return item.FindPropertyRelative("second").floatValue;
            }
        }
        return fallback;
    }

    private static Color GetSavedColor(SerializedObject so, string propertyName, Color fallback)
    {
        var arr = so.FindProperty("m_SavedProperties.m_Colors");
        if (arr == null) return fallback;
        for (int i = 0; i < arr.arraySize; i++)
        {
            var item = arr.GetArrayElementAtIndex(i);
            if (item.FindPropertyRelative("first").stringValue == propertyName)
            {
                return item.FindPropertyRelative("second").colorValue;
            }
        }
        return fallback;
    }

    private static SerializedProperty FindTexEnvEntry(SerializedObject so, string propertyName)
    {
        var arr = so.FindProperty("m_SavedProperties.m_TexEnvs");
        if (arr == null) return null;
        for (int i = 0; i < arr.arraySize; i++)
        {
            var item = arr.GetArrayElementAtIndex(i);
            if (item.FindPropertyRelative("first").stringValue == propertyName)
            {
                return item;
            }
        }
        return null;
    }
}
