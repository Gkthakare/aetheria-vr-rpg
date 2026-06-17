using UnityEngine;

namespace Aetheria.World
{
    /// <summary>
    /// SAO-style floor theme. Each floor can have a distinct look (forest, desert, snow, etc.).
    /// Assign to FloorManager. Used by TerrainTextureGenerator for AI prompts.
    /// </summary>
    [CreateAssetMenu(fileName = "FloorTheme", menuName = "Aetheria/Floor Theme Config")]
    public class FloorThemeConfig : ScriptableObject
    {
        [Header("Theme")]
        public string themeName = "Forest";
        [TextArea(2, 4)]
        public string description = "Dense forest floor with trees and grass.";

        [Header("AI Terrain Prompts (anime style)")]
        public string mountainPrompt = "anime style mountain rock texture, cel shaded, soft colors, fantasy game";
        public string waterPrompt = "anime style water surface, soft blue, cel shaded, fantasy JRPG";
        public string sandPrompt = "anime style sand texture, golden, soft, fantasy game";
        public string grassPrompt = "anime style grass texture, soft green, cel shaded, JRPG";
        public string forestPrompt = "anime style forest floor, leaves, soft lighting, anime game";
        public string snowPrompt = "anime style snow texture, soft white, cel shaded, fantasy";

        [Header("Optional Override")]
        [Tooltip("Leave empty to use defaults. Add theme-specific keywords.")]
        public string promptSuffix = "";
    }
}
