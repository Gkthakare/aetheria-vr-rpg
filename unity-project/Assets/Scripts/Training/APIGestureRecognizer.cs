using UnityEngine;
using System.Collections;

namespace Aetheria.Training
{
    /// <summary>
    /// ML gesture recognizer via HTTP API. Call your Python/ML backend with hand pose, get gesture back.
    /// Works alongside AIClient. No Barracuda required.
    /// </summary>
    public class APIGestureRecognizer : MonoBehaviour, IGestureRecognizer
    {
        [Header("API")]
        [SerializeField] private string apiUrl = "http://localhost:8001/gesture";
        [SerializeField] private float timeoutSeconds = 0.5f;
        [SerializeField] private float cacheSeconds = 0.1f;

        [Header("Fallback")]
        [SerializeField] private MonoBehaviour fallbackRecognizer;

        [Header("Debug")]
        [SerializeField] private bool logRequests;

        private GestureType _cached;
        private float _cacheUntil;
        private bool _requestInFlight;

        public GestureType Recognize(HandPoseData pose)
        {
            if (!pose.IsTracked)
            {
                _cached = GestureType.None;
                return GestureType.None;
            }

            if (Time.time < _cacheUntil)
                return _cached;

            if (!_requestInFlight)
                StartCoroutine(SendRequest(pose));
            else if (logRequests)
                Debug.Log("[APIGesture] Request in flight, using cache");

            return _cached;
        }

        private IEnumerator SendRequest(HandPoseData pose)
        {
            _requestInFlight = true;
            var json = $"{{\"pinch\":{pose.PinchStrength},\"grip\":{pose.GripStrength},\"lx\":{pose.PalmPosition.x},\"ly\":{pose.PalmPosition.y},\"lz\":{pose.PalmPosition.z}}}";
            if (logRequests)
                Debug.Log($"[APIGesture] Sending to {apiUrl}: {json}");
            var req = new UnityEngine.Networking.UnityWebRequest(apiUrl, "POST");
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(body);
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = (int)timeoutSeconds;

            yield return req.SendWebRequest();

            _requestInFlight = false;
            _cacheUntil = Time.time + cacheSeconds;

            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success && req.downloadHandler.text != null)
            {
                var text = req.downloadHandler.text.Trim().ToLower();
                _cached = ParseResponse(text);
                if (logRequests)
                    Debug.Log($"[APIGesture] Response: {req.downloadHandler.text} -> {_cached}");
            }
            else if (fallbackRecognizer is IGestureRecognizer fallback)
            {
                _cached = fallback.Recognize(pose);
                if (logRequests)
                    Debug.Log($"[APIGesture] API failed ({req.error}), using fallback: {_cached}");
            }
            else
            {
                _cached = GestureType.None;
                if (logRequests)
                    Debug.LogWarning($"[APIGesture] API failed: {req.error}");
            }
        }

        private static GestureType ParseResponse(string text)
        {
            if (text.Contains("pinch")) return GestureType.Pinch;
            if (text.Contains("fist")) return GestureType.Fist;
            if (text.Contains("open")) return GestureType.OpenPalm;
            if (text.Contains("point")) return GestureType.Point;
            if (text.Contains("thrust")) return GestureType.Thrust;
            if (text.Contains("shield")) return GestureType.Shield;
            return GestureType.None;
        }
    }
}
