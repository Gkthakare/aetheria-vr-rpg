using UnityEngine;

namespace Aetheria.Magic
{
    /// <summary>
    /// Editor / Windows desktop: keyword "water orb" → invokes <see cref="onWaterOrbHeard"/>.
    /// Quest: disable this component and drive the same events from Convai / Meta Voice / your backend.
    /// </summary>
    public sealed class WindowsSpeechKeywordDriver : MonoBehaviour
    {
        [SerializeField] UnityEngine.Events.UnityEvent onWaterOrbHeard;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        UnityEngine.Windows.Speech.KeywordRecognizer _recognizer;
#endif

        void OnEnable()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var phrases = new[] { "water orb", "water ball" };
            _recognizer = new UnityEngine.Windows.Speech.KeywordRecognizer(phrases);
            _recognizer.OnPhraseRecognized += OnPhrase;
            _recognizer.Start();
#endif
        }

        void OnDisable()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_recognizer == null)
                return;
            _recognizer.OnPhraseRecognized -= OnPhrase;
            _recognizer.Stop();
            _recognizer.Dispose();
            _recognizer = null;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        void OnPhrase(UnityEngine.Windows.Speech.PhraseRecognizedEventArgs args)
        {
            var t = args.text.ToLowerInvariant();
            if (t.Contains("water"))
                onWaterOrbHeard?.Invoke();
        }
#endif

        /// <summary>Bind XR button for Quest testing without cloud speech.</summary>
        public void SimulateWaterOrbPhrase()
        {
            onWaterOrbHeard?.Invoke();
        }
    }
}
