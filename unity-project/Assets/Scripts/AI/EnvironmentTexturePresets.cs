using UnityEngine;

namespace Aetheria.AI
{
    /// <summary>
    /// Preset prompts for generating realistic environment textures.
    /// Use with VFXTextureRequest to generate terrain, water, sky, etc.
    /// </summary>
    [CreateAssetMenu(fileName = "EnvironmentPresets", menuName = "Aetheria/Environment Texture Presets")]
    public class EnvironmentTexturePresets : ScriptableObject
    {
        [System.Serializable]
        public class Preset
        {
            public string id;
            public string displayName;
            [TextArea(1, 3)]
            public string prompt;
        }

        public Preset[] presets = new Preset[]
        {
            new Preset { id = "mountain", displayName = "Mountain", prompt = "photorealistic mountain rock texture, detailed, 4k" },
            new Preset { id = "water", displayName = "Water", prompt = "realistic water surface, reflections, blue, 4k" },
            new Preset { id = "grass", displayName = "Grass", prompt = "photorealistic grass texture, green, detailed" },
            new Preset { id = "sky", displayName = "Sky", prompt = "realistic sky with clouds, blue, photorealistic" },
            new Preset { id = "sand", displayName = "Sand", prompt = "photorealistic sand texture, desert, detailed" },
            new Preset { id = "dungeon_wall", displayName = "Dungeon Wall", prompt = "dark stone dungeon wall texture, medieval, detailed" },
            new Preset { id = "forest_floor", displayName = "Forest Floor", prompt = "forest floor with leaves and dirt, photorealistic" },
            new Preset { id = "lava", displayName = "Lava", prompt = "molten lava texture, glowing, orange and red" },
        };
    }
}
