#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using Aetheria.AI;

namespace Aetheria.AI.Editor
{
    public static class SaveGeneratedTextureEditor
    {
        [MenuItem("Aetheria/Save Last Generated Texture", false, 100)]
        [MenuItem("Tools/Aetheria/Save Last Generated Texture", false, 100)]
        public static void SaveLastTexture()
        {
            var tex = GeneratedTextureHolder.LastTexture;
            if (tex == null)
            {
                EditorUtility.DisplayDialog("No Texture", "Generate a texture first (Press G in Play mode, or call VFXTextureRequest.RequestTexture).", "OK");
                return;
            }

            var folder = "Assets/GeneratedTextures";
            if (!AssetDatabase.IsValidFolder("Assets/GeneratedTextures"))
                AssetDatabase.CreateFolder("Assets", "GeneratedTextures");

            var prompt = GeneratedTextureHolder.LastPrompt ?? "texture";
            var safeName = SanitizeFileName(prompt);
            var path = $"{folder}/{safeName}.png";
            var bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            AssetDatabase.Refresh();

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }

            EditorUtility.DisplayDialog("Saved", $"Texture saved to:\n{path}", "OK");
        }

        [MenuItem("Aetheria/Save Last Generated Texture as URP Material", false, 101)]
        [MenuItem("Tools/Aetheria/Save Last Generated Texture as URP Material", false, 101)]
        public static void SaveLastTextureAsURPMaterial()
        {
            var tex = GeneratedTextureHolder.LastTexture;
            if (tex == null)
            {
                EditorUtility.DisplayDialog("No Texture", "Generate a texture first (Press G in Play mode, or use voice \"generate mountain\").", "OK");
                return;
            }

            var folder = "Assets/Materials/Generated";
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Generated"))
                AssetDatabase.CreateFolder("Assets/Materials", "Generated");

            var prompt = GeneratedTextureHolder.LastPrompt ?? "texture";
            var safeName = SanitizeFileName(prompt);
            var texPath = $"{folder}/{safeName}.png";
            var bytes = tex.EncodeToPNG();
            File.WriteAllBytes(texPath, bytes);
            AssetDatabase.Refresh();

            var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }

            var texAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var mat = URPMaterialHelper.CreateMaterial(texAsset, safeName + "_URP");
            var matPath = $"{folder}/{safeName}_URP.mat";
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Saved", $"Texture: {texPath}\nURP Material: {matPath}\n\nUse for mountains, grass, weapons, etc.", "OK");
        }

        private static string SanitizeFileName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "texture";
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
                s = s.Replace(c, '_');
            return s.Length > 50 ? s.Substring(0, 50) : s;
        }
    }
}
#endif
