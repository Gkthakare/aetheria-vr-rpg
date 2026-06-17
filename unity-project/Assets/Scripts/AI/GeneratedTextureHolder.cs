using UnityEngine;

namespace Aetheria.AI
{
    /// <summary>
    /// Holds the last generated texture for saving to Assets.
    /// Used by Editor menu: Aetheria > Save Last Generated Texture.
    /// </summary>
    public static class GeneratedTextureHolder
    {
        public static Texture2D LastTexture { get; set; }
        public static string LastPrompt { get; set; }
    }
}
