using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.World
{
    /// <summary>
    /// Either assigns pre-made URP materials (recommended for Meshy imports with companion .mat files)
    /// or builds a procedural high-contrast look at runtime.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class CreatureFearsomeSkin : MonoBehaviour
    {
        const string UrpLit = "Universal Render Pipeline/Lit";
        const string UrpSimpleLit = "Universal Render Pipeline/Simple Lit";
        const string BuiltInStandard = "Standard";

        [SerializeField] bool applyOnAwake = true;

        [Header("Authored URP materials (recommended)")]
        [Tooltip("When enabled and the array below is non-empty, those assets are assigned to renderer slots (cycled). " +
                 "Turn off to use the procedural colors instead.")]
        [SerializeField] bool useAuthoredSkinMaterials = false;
        [SerializeField] Material[] authoredSkinMaterials;

        [Header("Matte body + red glow (HDR; keep moderate or bloom reads as solid magenta)")]
        [SerializeField] Color bodyBase = new Color(0.05f, 0.05f, 0.07f, 1f);
        [SerializeField] Color bodyEmission = new Color(0.45f, 0.06f, 0.12f, 1f);

        [Header("Neon accents (cycles per material slot)")]
        [SerializeField] Color neonMagentaEmission = new Color(0.55f, 0.12f, 0.48f, 1f);
        [SerializeField] Color neonCyanEmission = new Color(0.08f, 0.55f, 0.65f, 1f);

        [SerializeField] float smoothness = 0.1f;
        [SerializeField] float metallic = 0.1f;

        void Start()
        {
            // After pipeline init; Awake is too early on some XR / quality setups (current pipeline not ready).
            if (applyOnAwake)
                ApplyNow();
        }

        [ContextMenu("Apply Fearsome Skin")]
        public void ApplyNow()
        {
            if (useAuthoredSkinMaterials && authoredSkinMaterials != null && authoredSkinMaterials.Length > 0)
            {
                ApplyAuthoredSkinMaterials();
                return;
            }

            var shader = ResolveSurfaceShader();
            if (shader == null)
            {
                Debug.LogError(
                    "[CreatureFearsomeSkin] No usable Lit shader found. " +
                    "Use menu: Aetheria → Rendering → Fix magenta materials — create & assign URP pipeline.");
                return;
            }

            var urp = ActiveRenderPipelineIsScriptable();

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                if (shared == null || shared.Length == 0)
                    continue;

                var mats = new Material[shared.Length];
                for (var i = 0; i < shared.Length; i++)
                {
                    if (i % 3 == 0)
                        mats[i] = CreateMat(shader, urp, bodyBase, bodyEmission);
                    else if (i % 3 == 1)
                        mats[i] = CreateMat(shader, urp, bodyBase * 0.45f, neonMagentaEmission);
                    else
                        mats[i] = CreateMat(shader, urp, bodyBase * 0.45f, neonCyanEmission);
                }

                r.materials = mats;
            }
        }

        void ApplyAuthoredSkinMaterials()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                if (shared == null || shared.Length == 0)
                    continue;

                var mats = new Material[shared.Length];
                for (var i = 0; i < shared.Length; i++)
                {
                    var m = authoredSkinMaterials[i % authoredSkinMaterials.Length];
                    mats[i] = m != null ? m : shared[i];
                }

                r.sharedMaterials = mats;
            }
        }

        static bool ActiveRenderPipelineIsScriptable()
        {
            if (GraphicsSettings.defaultRenderPipeline != null)
                return true;
            if (QualitySettings.renderPipeline != null)
                return true;
            if (GraphicsSettings.currentRenderPipeline != null)
                return true;
            return false;
        }

        static bool ShaderIsUniversalRp(Shader s)
        {
            return s != null && s.name.IndexOf("Universal Render Pipeline", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static Shader ResolveSurfaceShader()
        {
            var urpSimple = Shader.Find(UrpSimpleLit);
            var urpLit = Shader.Find(UrpLit);

            // Using Built-in Standard while the active pipeline is URP produces pink materials.
            if (ActiveRenderPipelineIsScriptable())
            {
                var lit = urpSimple ?? urpLit;
                if (lit != null && lit.isSupported)
                    return lit;
                lit = urpLit ?? urpSimple;
                if (lit != null)
                    return lit;
                Debug.LogError(
                    "[CreatureFearsomeSkin] URP Lit/Simple Lit shaders missing or unsupported (stripping / missing URP?).");
                return null;
            }

            var std = Shader.Find(BuiltInStandard);
            if (std != null)
                return std;
            return urpLit;
        }

        Material CreateMat(Shader shader, bool urpPipeline, Color baseColor, Color emissionHdr)
        {
            var m = new Material(shader);
            var treatAsUrp = urpPipeline || ShaderIsUniversalRp(shader);

            if (treatAsUrp)
            {
                m.shaderKeywords = null;
                m.SetFloat("_Surface", 0f);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetFloat("_AlphaClip", 0f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_Smoothness", smoothness);
                m.SetColor("_BaseColor", baseColor);
                if (m.HasProperty("_Color"))
                    m.SetColor("_Color", baseColor);

                var em = emissionHdr;
                var maxChannel = Mathf.Max(em.r, Mathf.Max(em.g, em.b));
                if (maxChannel > 2.5f)
                    em *= 2.5f / maxChannel;

                m.SetColor("_EmissionColor", em);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.EnableKeyword("_EMISSION");
            }
            else
            {
                m.SetColor("_Color", baseColor);
                m.SetFloat("_Glossiness", smoothness);
                m.SetFloat("_Metallic", metallic);
                m.SetColor("_EmissionColor", emissionHdr);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            m.doubleSidedGI = true;

            if (m.HasProperty("_Cull"))
                m.SetFloat("_Cull", 0f);

            return m;
        }
    }
}
