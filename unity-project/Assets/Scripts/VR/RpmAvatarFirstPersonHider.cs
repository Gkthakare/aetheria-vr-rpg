using System.Collections.Generic;
using UnityEngine;

namespace Aetheria.VR
{
    /// <summary>
    /// Hides the Ready Player Me / Wolf3D head, hair, eyes, teeth, glasses,
    /// headwear and beard mesh renderers on this avatar, every frame, so the
    /// player never sees the inside of their own skull from the HMD.
    ///
    /// This is independent of any IK bone scaling tricks (which can be
    /// silently undone by Animator updates on Android IL2CPP builds). It just
    /// flips <see cref="Renderer.enabled"/> = false on matching renderers.
    /// Other systems / outside cameras can still re-enable the renderer if
    /// they want a third-person view; for the player's own camera the result
    /// is "head invisible".
    ///
    /// Optionally also hides the torso/outfit when <see cref="hideTorsoAndOutfit"/>
    /// is true, useful if the body anchor is mis-aligned and the chest mesh
    /// pokes into the HMD field of view.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RpmAvatarFirstPersonHider : MonoBehaviour
    {
        [Header("What to hide")]
        [Tooltip("Hides head, hair, eyes, teeth, glasses, headwear, beard so they never block the HMD view.")]
        [SerializeField] private bool hideHeadAndFace = true;

        [Tooltip("Also hide torso, outfit top, body. Use only if the chest/shoulders clip into the camera.")]
        [SerializeField] private bool hideTorsoAndOutfit = false;

        [Tooltip("Also hide bottom (pants), footwear, legs visuals. Disable if you want to see your own legs walking.")]
        [SerializeField] private bool hideLegsAndOutfitBottom = false;

        [Header("Diagnostics")]
        [SerializeField] private bool logOnce = true;

        private static readonly string[] HeadFaceHints =
        {
            "Wolf3D_Head", "Wolf3D_Hair", "Wolf3D_Beard",
            "Wolf3D_Glasses", "Wolf3D_Headwear", "Wolf3D_Teeth",
            "EyeLeft", "EyeRight", "Eye_Left", "Eye_Right"
        };

        private static readonly string[] TorsoOutfitHints =
        {
            "Wolf3D_Body", "Wolf3D_Outfit_Top"
        };

        private static readonly string[] LegsOutfitHints =
        {
            "Wolf3D_Outfit_Bottom", "Wolf3D_Outfit_Footwear"
        };

        private readonly List<Renderer> _hidden = new();
        private bool _firstApplyDone;

        private void OnEnable()
        {
            CollectAndApply();
        }

        private void LateUpdate()
        {
            if (_hidden.Count == 0)
            {
                CollectAndApply();
                return;
            }

            for (int i = 0; i < _hidden.Count; i++)
            {
                var r = _hidden[i];
                if (r == null) continue;
                if (r.enabled) r.enabled = false;
            }
        }

        private void CollectAndApply()
        {
            _hidden.Clear();
            var renderers = GetComponentsInChildren<Renderer>(true);
            int hidCount = 0;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                string n = r.transform.name;
                bool match = false;

                if (hideHeadAndFace && MatchesAny(n, HeadFaceHints)) match = true;
                if (!match && hideTorsoAndOutfit && MatchesAny(n, TorsoOutfitHints)) match = true;
                if (!match && hideLegsAndOutfitBottom && MatchesAny(n, LegsOutfitHints)) match = true;

                if (!match) continue;
                _hidden.Add(r);
                if (r.enabled)
                {
                    r.enabled = false;
                    hidCount++;
                }
            }

            if (logOnce && !_firstApplyDone)
            {
                _firstApplyDone = true;
                Debug.Log($"[RpmAvatarFirstPersonHider] Hid {hidCount} renderer(s) on '{name}' (head/face={hideHeadAndFace}, torso={hideTorsoAndOutfit}, legs={hideLegsAndOutfitBottom}).");
            }
        }

        private static bool MatchesAny(string name, string[] hints)
        {
            for (int i = 0; i < hints.Length; i++)
            {
                if (name.IndexOf(hints[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
