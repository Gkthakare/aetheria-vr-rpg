using UnityEngine;
using Aetheria.AI;

/// <summary>
/// Generate textures from EnvironmentTexturePresets. Attach to a GameObject with VFXTextureRequest.
/// Press number keys 1-8 to generate corresponding preset, or call GeneratePreset(index).
/// </summary>
[RequireComponent(typeof(VFXTextureRequest))]
public class VFXPresetGenerator : MonoBehaviour
{
    [SerializeField] private EnvironmentTexturePresets presets;
    [SerializeField] private Renderer targetRenderer;

    private VFXTextureRequest _request;
    private static readonly string[] DefaultPrompts = new[]
    {
        "photorealistic mountain rock texture, detailed, 4k",
        "realistic water surface, reflections, blue, 4k",
        "photorealistic grass texture, green, detailed",
        "realistic sky with clouds, blue, photorealistic",
        "photorealistic sand texture, desert, detailed",
        "dark stone dungeon wall texture, medieval, detailed",
        "forest floor with leaves and dirt, photorealistic",
        "molten lava texture, glowing, orange and red"
    };
    private static readonly string[] DefaultNames = new[] { "Mountain", "Water", "Grass", "Sky", "Sand", "Dungeon Wall", "Forest Floor", "Lava" };

    void Awake()
    {
        _request = GetComponent<VFXTextureRequest>();
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();
        if (presets == null)
            presets = Resources.Load<EnvironmentTexturePresets>("EnvironmentTexturePresets");
    }

    void Update()
    {
        int count = GetPresetCount();
        if (count == 0) return;

        for (int i = 0; i < Mathf.Min(8, count); i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || (i == 0 && Input.GetKeyDown(KeyCode.G)))
            {
                GeneratePreset(i);
                break;
            }
        }
    }

    private int GetPresetCount()
    {
        if (presets != null && presets.presets != null && presets.presets.Length > 0)
            return presets.presets.Length;
        return DefaultPrompts.Length;
    }

    public void GeneratePreset(int index)
    {
        int count = GetPresetCount();
        if (index < 0 || index >= count)
        {
            Debug.LogWarning("[VFXPreset] Invalid preset index");
            return;
        }

        string prompt, displayName;
        if (presets != null && presets.presets != null && index < presets.presets.Length)
        {
            prompt = presets.presets[index].prompt;
            displayName = presets.presets[index].displayName;
        }
        else
        {
            prompt = DefaultPrompts[index];
            displayName = DefaultNames[index];
        }

        var req = GetComponent<VFXTextureRequest>();
        if (req == null) return;

        Debug.Log($"[VFXPreset] Generating: {displayName} (1-8 to try others). Press G for mountain.");
        req.RequestTexture(
            prompt,
            tex => Debug.Log($"[VFXPreset] Applied: {displayName}. Menu: Aetheria > Save Last Generated Texture"),
            err => Debug.LogError($"[VFXPreset] Failed: {err}")
        );
    }
}
