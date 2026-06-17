using UnityEngine;

namespace Aetheria.AI
{
    /// <summary>
    /// Helper to create URP-compatible materials for AI-generated textures.
    /// Use for mountains, grass, weapons, etc. - like PolyOne-style materials.
    /// </summary>
    public static class URPMaterialHelper
    {
        /// <summary>Get the best Lit shader (URP Lit when available, else Standard).</summary>
        public static Shader GetLitShader()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null) return shader;
            shader = Shader.Find("Standard");
            if (shader != null) return shader;
            return Shader.Find("Legacy Shaders/Diffuse");
        }

        /// <summary>Create a URP Lit material from a texture.</summary>
        public static Material CreateMaterial(Texture2D texture, string name = null)
        {
            var shader = GetLitShader();
            var mat = new Material(shader);
            mat.mainTexture = texture;
            mat.name = name ?? (texture != null ? texture.name + "_URP" : "Generated_URP");
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.5f);
            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", 0.5f);
            return mat;
        }
    }
}
