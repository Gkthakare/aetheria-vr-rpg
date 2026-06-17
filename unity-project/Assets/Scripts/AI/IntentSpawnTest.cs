using UnityEngine;
using Aetheria.Gameplay;

namespace Aetheria.AI
{
    /// <summary>
    /// Dev test: calls /api/v1/intent/parse and spawns via SpawnDirector.
    /// - Press I with Game view focused (legacy Input), or
    /// - Click the on-screen "Test Wolf Spawn" button, or
    /// - Right-click this component header → Test Intent Spawn
    /// Requires: backend running (python main.py in 3d-monster-gen/web/backend, port 8001), SpawnDirector + catalog prefab.
    /// </summary>
    public class IntentSpawnTest : MonoBehaviour
    {
        [TextArea(2, 6)]
        [SerializeField] private string testPhrase = "spawn a black big wolf weak to mythic weapons";

        [SerializeField] private SpawnDirector spawnDirector;

        [Tooltip("Draws a small button in Game view so you do not rely on keyboard (VR / unfocused Game view).")]
        [SerializeField] private bool showOnScreenTestButton = true;

        private void Start()
        {
            if (spawnDirector == null)
                spawnDirector = FindObjectOfType<SpawnDirector>();

            Debug.Log(
                "[IntentSpawnTest] Ready. " +
                "1) Run backend: 3d-monster-gen/web/backend → python main.py (port 8001). " +
                "2) AIClient apiBaseUrl should be http://127.0.0.1:8001 or http://localhost:8001. " +
                "3) Click **Test Wolf Spawn** in Game view, press **I** with Game view focused, or use component context menu **Test Intent Spawn**.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.I))
            {
                Debug.Log("[IntentSpawnTest] Key I detected — running intent + spawn test.");
                RunTest();
            }
        }

        private void OnGUI()
        {
            if (!showOnScreenTestButton)
                return;

            const int w = 220;
            const int h = 36;
            if (GUI.Button(new Rect(12, 12, w, h), "Test Wolf Spawn (intent API)"))
            {
                Debug.Log("[IntentSpawnTest] On-screen button — running intent + spawn test.");
                RunTest();
            }
        }

        [ContextMenu("Test Intent Spawn")]
        public void RunTest()
        {
            if (spawnDirector == null)
                spawnDirector = FindObjectOfType<SpawnDirector>();

            Debug.Log($"[IntentSpawnTest] Requesting intent for: \"{testPhrase}\"");

            AIClient.Instance.ParseIntent(
                testPhrase,
                response =>
                {
                    Debug.Log($"[IntentSpawnTest] Intent OK: type={response.intentType}, confidence={response.confidence}");
                    if (spawnDirector != null)
                        spawnDirector.SpawnFromIntent(response);
                    else
                        Debug.LogError("[IntentSpawnTest] No SpawnDirector in scene.");
                },
                err => Debug.LogError($"[IntentSpawnTest] API failed: {err}")
            );
        }
    }
}
