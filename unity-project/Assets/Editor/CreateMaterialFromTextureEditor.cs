#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Aetheria.AI.Editor
{
    public static class CreateMaterialFromTextureEditor
    {
        [MenuItem("Assets/Aetheria/Create Material from Texture", false, 200)]
        public static void CreateMaterial()
        {
            var tex = Selection.activeObject as Texture2D;
            if (tex == null)
            {
                EditorUtility.DisplayDialog("No Texture", "Select a texture in the Project window first.", "OK");
                return;
            }

            var mat = Aetheria.AI.URPMaterialHelper.CreateMaterial(tex, tex.name + "_URP");

            var path = AssetDatabase.GetAssetPath(tex);
            var dir = System.IO.Path.GetDirectoryName(path);
            var matPath = $"{dir}/{mat.name}.mat";
            AssetDatabase.CreateAsset(mat, matPath);
            EditorUtility.DisplayDialog("Created", $"Material saved to:\n{matPath}\n\nAssign it to DungeonRoomVisualizer (matSand, matGrass, etc.)", "OK");
        }

        [MenuItem("Assets/Aetheria/Create Material from Texture", true)]
        public static bool ValidateCreateMaterial()
        {
            return Selection.activeObject is Texture2D;
        }
    }
}
#endif
