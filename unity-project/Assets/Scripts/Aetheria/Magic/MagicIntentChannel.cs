using System;
using UnityEngine;
using UnityEngine.Events;

namespace Aetheria.Magic
{
    /// <summary>
    /// Data-only bridge: external systems (voice, LLM, UI) invoke <see cref="Dispatch"/> with a string id.
    /// Wire each id in the inspector to UnityEvents — no AI inside this class.
    /// </summary>
    [Serializable]
    public sealed class MagicIntentEntry
    {
        public string intentId = "water.push";
        public UnityEvent onInvoked;
    }

    public sealed class MagicIntentChannel : MonoBehaviour
    {
        [SerializeField] MagicIntentEntry[] entries;

        public void Dispatch(string intentId)
        {
            if (string.IsNullOrEmpty(intentId) || entries == null)
                return;
            foreach (var e in entries)
            {
                if (e == null || e.intentId != intentId)
                    continue;
                e.onInvoked?.Invoke();
                return;
            }
        }
    }
}
