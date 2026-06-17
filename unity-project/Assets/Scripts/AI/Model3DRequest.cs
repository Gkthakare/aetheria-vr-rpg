using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

namespace Aetheria.AI
{
    /// <summary>
    /// Request 3D meshes from ML backend (Shap-E).
    /// Python server generates mesh from prompt -> Unity creates MeshFilter/MeshRenderer.
    /// Used by ModelPool for infinite dungeon decor.
    /// </summary>
    public class Model3DRequest : MonoBehaviour
    {
        [Header("API")]
        [SerializeField] private string apiUrl = "http://localhost:8001/generate_3d";
        [Tooltip("Shap-E takes ~5-10 min. Stay in Play mode. Set 900+ for slow machines.")]
        [SerializeField] private float timeoutSeconds = 900f;

        /// <summary>Request a 3D mesh from the ML backend.</summary>
        public void RequestModel(string prompt, System.Action<Mesh> onSuccess, System.Action<string> onError = null)
        {
            StartCoroutine(RequestModelCoroutine(prompt, onSuccess, onError));
        }

        private IEnumerator RequestModelCoroutine(string prompt, System.Action<Mesh> onSuccess, System.Action<string> onError)
        {
            var json = $"{{\"prompt\":\"{EscapeJson(prompt)}\"}}";
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
            Mesh mesh = ParseMeshFromResponse(response);

            if (mesh != null)
                onSuccess?.Invoke(mesh);
            else
                onError?.Invoke("Could not parse mesh from response");
        }

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }

        private static Mesh ParseMeshFromResponse(string response)
        {
            var wrapper = JsonUtility.FromJson<Model3DResponse>(response);
            if (!string.IsNullOrEmpty(wrapper?.obj))
            {
                try
                {
                    var bytes = System.Convert.FromBase64String(wrapper.obj);
                    var objText = System.Text.Encoding.UTF8.GetString(bytes);
                    return OBJLoader.ParseOBJ(objText);
                }
                catch (System.Exception)
                {
                    return null;
                }
            }
            return null;
        }

        [System.Serializable]
        private class Model3DResponse
        {
            public string obj;
            public string prompt;
        }
    }
}
