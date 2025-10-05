using UnityEngine;
using Aetheria.World;
public class DevControls : MonoBehaviour
{
    [SerializeField] ChunkGenerator generator;
    [SerializeField] ChunkManager manager;
    [SerializeField] int seed = 12345;
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10,10,260,140), GUI.skin.box);
        GUILayout.Label("Seed:");
        string s = GUILayout.TextField(seed.ToString(), 10);
        if (int.TryParse(s, out int parsed)) seed = parsed;
        if (GUILayout.Button("Apply Seed"))
        {
            generator.SetSeed(seed);
            manager.ClearAllChunks();
            // force immediate reload around camera
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (cam != null)
            {
                var currentChunk = (Vector2) new Vector2(Mathf.FloorToInt(cam.position.x / 16f), Mathf.FloorToInt(cam.position.z / 16f));
            }
        }
        GUILayout.Label("Tips: WASD move, RMB look, QE up/down");
        GUILayout.EndArea();
    }
}