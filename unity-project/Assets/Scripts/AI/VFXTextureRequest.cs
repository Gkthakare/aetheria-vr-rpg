using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

namespace Aetheria.AI
{
    /// <summary>
    /// Request VFX textures from ML backend (Stable Diffusion).
    /// Python server generates image from prompt → Unity applies to material.
    /// </summary>
    public class VFXTextureRequest : MonoBehaviour
    {
        [Header("API")]
        [SerializeField] private string apiUrl = "http://localhost:8001/generate_vfx";
        [Tooltip("Stable Diffusion: 30-60s (GPU) or 2-5 min (CPU). Stay in Play mode until done.")]
        [SerializeField] private float timeoutSeconds = 600f;

        [Header("Target (optional)")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private string textureProperty = "_MainTex";

        /// <summary>Request a VFX texture from the ML backend.</summary>
        public void RequestTexture(string prompt, System.Action<Texture2D> onSuccess, System.Action<string> onError = null)
        {
            StartCoroutine(RequestTextureCoroutine(prompt, onSuccess, onError));
        }

        private IEnumerator RequestTextureCoroutine(string prompt, System.Action<Texture2D> onSuccess, System.Action<string> onError)
        {
            var json = $"{{\"prompt\":\"{EscapeJson(prompt)}\",\"width\":256,\"height\":256}}";
            var req = new UnityWebRequest(apiUrl, "POST");
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = (int)timeoutSeconds;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(req.error);
                yield break;
            }

            var response = req.downloadHandler.text;
            Texture2D tex = ParseTextureFromResponse(response);

            if (tex != null)
            {
                GeneratedTextureHolder.LastTexture = tex;
                GeneratedTextureHolder.LastPrompt = prompt;
                onSuccess?.Invoke(tex);
                if (targetRenderer != null && targetRenderer.material.HasProperty(textureProperty))
                    targetRenderer.material.SetTexture(textureProperty, tex);
            }
            else
                onError?.Invoke("Could not parse texture from response");
        }

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }

        private static Texture2D ParseTextureFromResponse(string response)
        {
            // JSON: {"image": "<base64>", "width": 256, "height": 256}
            var wrapper = JsonUtility.FromJson<VFXResponse>(response);
            if (!string.IsNullOrEmpty(wrapper?.image))
            {
                try
                {
                    var bytes = System.Convert.FromBase64String(wrapper.image);
                    var tex = new Texture2D(2, 2);
                    if (tex.LoadImage(bytes))
                        return tex;
                }
                catch { /* fall through */ }
            }

            // Legacy: data:image/png;base64,<data>
            if (response.Contains("base64"))
            {
                var parts = response.Split(',');
                if (parts.Length > 1)
                {
                    try
                    {
                        var bytes = System.Convert.FromBase64String(parts[1].Trim());
                        var tex = new Texture2D(2, 2);
                        if (tex.LoadImage(bytes))
                            return tex;
                    }
                    catch { /* fall through */ }
                }
            }

            return null;
        }

        [System.Serializable]
        private class VFXResponse
        {
            public string image;
            public int width;
            public int height;
        }
    }
}
