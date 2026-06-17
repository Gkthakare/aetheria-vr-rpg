using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aetheria.World
{
    /// <summary>
    /// One-shot setup after SUIMONO_Surface is active: water collision layers + flowing URP ocean visuals.
    /// </summary>
    public static class SuimonoOceanRuntimeSetup
    {
        public static void ConfigureSurfaceRoot(GameObject surfaceRoot)
        {
            if (surfaceRoot == null)
                return;

            ApplyWaterLayers(surfaceRoot.transform);
            if (surfaceRoot.GetComponent<UrpFlowingOceanDriver>() == null)
                surfaceRoot.AddComponent<UrpFlowingOceanDriver>();
        }

        static void ApplyWaterLayers(Transform root)
        {
            var layer = LayerMask.NameToLayer("Suimono_Water");
            if (layer < 0)
                layer = LayerMask.NameToLayer("Water");
            if (layer < 0)
                return;

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name;
                if (n.StartsWith("cam_", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (n.IndexOf("Reflection", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                var hasWaterMesh = t.GetComponent<MeshRenderer>() != null || t.GetComponent<SkinnedMeshRenderer>() != null;
                var hasWaterCollider = t.GetComponent<MeshCollider>() != null || t.GetComponent<BoxCollider>() != null;
                if (hasWaterMesh || hasWaterCollider)
                    t.gameObject.layer = layer;
            }
        }
    }

    /// <summary>
    /// URP replacement for Suimono's built-in water shader: procedural textures, multi-layer UV scroll,
    /// smoothness shimmer, and extra motion when global <c>_MagicWaterStrength</c> (see WaterWorldInfluenceHub) is active.
    /// Retries binding for a few frames because Suimono assigns URP Lit on Start.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class UrpFlowingOceanDriver : MonoBehaviour
    {
        const string UrpLitName = "Universal Render Pipeline/Lit";

        [SerializeField] float baseMapScrollSpeed = 0.022f;
        [SerializeField] float bumpScrollSpeed = 0.03f;
        [SerializeField] float detailScrollSpeed = 0.041f;
        [SerializeField] float baseMapTile = 0.0035f;
        [SerializeField] float bumpScale = 0.55f;
        [SerializeField] float detailTile = 0.018f;
        [SerializeField] int materialBindRetries = 48;

        static Texture2D _sharedAlbedoNoise;
        static Texture2D _sharedBumpNoise;
        static Texture2D _sharedDetailNoise;
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int MagicWaterStrengthGlobal = Shader.PropertyToID("_MagicWaterStrength");

        Material[] _animated;
        int _count;

        void Start()
        {
            EnsureSharedTextures();
            StartCoroutine(CoBindMaterials());
        }

        IEnumerator CoBindMaterials()
        {
            for (var i = 0; i < materialBindRetries; i++)
            {
                CollectAndUpgradeLitMaterials();
                if (_count > 0)
                    yield break;
                yield return null;
            }
        }

        void Update()
        {
            if (_animated == null || _count == 0)
                return;

            var magic = Mathf.Clamp01(Shader.GetGlobalFloat(MagicWaterStrengthGlobal));
            var boost = 1f + magic * 3.2f;
            var t = Time.time * boost;
            var o0 = new Vector2(t * baseMapScrollSpeed, t * baseMapScrollSpeed * 0.73f);
            var o1 = new Vector2(-t * bumpScrollSpeed * 1.1f, t * bumpScrollSpeed * 0.88f);
            var o2 = new Vector2(t * detailScrollSpeed * 0.6f, -t * detailScrollSpeed);

            var smooth = 0.74f + 0.12f * Mathf.Sin(Time.time * 1.85f) + magic * 0.22f;
            smooth = Mathf.Clamp(smooth, 0.58f, 0.98f);
            var metal = 0.02f + magic * 0.09f;

            for (var i = 0; i < _count; i++)
            {
                var m = _animated[i];
                if (m == null)
                    continue;
                if (m.HasProperty("_BaseMap"))
                    m.SetTextureOffset("_BaseMap", o0);
                if (m.HasProperty("_BumpMap"))
                    m.SetTextureOffset("_BumpMap", o1);
                if (m.HasProperty("_DetailAlbedoMap"))
                    m.SetTextureOffset("_DetailAlbedoMap", o2);
                if (m.HasProperty(SmoothnessId))
                    m.SetFloat(SmoothnessId, smooth);
                if (m.HasProperty(MetallicId))
                    m.SetFloat(MetallicId, metal);
            }
        }

        void CollectAndUpgradeLitMaterials()
        {
            var list = new List<Material>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null)
                    continue;
                var mats = r.materials;
                for (var mi = 0; mi < mats.Length; mi++)
                {
                    var mat = mats[mi];
                    if (mat == null || mat.shader == null || mat.shader.name != UrpLitName)
                        continue;

                    ApplyOceanTextures(mat);
                    list.Add(mat);
                }
            }

            _animated = list.ToArray();
            _count = _animated.Length;
        }

        void ApplyOceanTextures(Material mat)
        {
            mat.SetTextureScale("_BaseMap", new Vector2(baseMapTile, baseMapTile));
            mat.SetTexture("_BaseMap", _sharedAlbedoNoise);
            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTextureScale("_BumpMap", new Vector2(baseMapTile * 1.12f, baseMapTile * 1.12f));
                mat.SetTexture("_BumpMap", _sharedBumpNoise);
                mat.SetFloat("_BumpScale", bumpScale);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (mat.HasProperty("_DetailAlbedoMap"))
            {
                mat.SetTextureScale("_DetailAlbedoMap", new Vector2(detailTile, detailTile));
                mat.SetTexture("_DetailAlbedoMap", _sharedDetailNoise);
                mat.EnableKeyword("_DETAIL_MULX2");
            }

            mat.SetColor("_BaseColor", new Color(0.02f, 0.1f, 0.18f, 0.9f));
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", mat.GetColor("_BaseColor"));
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", new Color(0.015f, 0.06f, 0.1f, 1f));
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty(SmoothnessId))
                mat.SetFloat(SmoothnessId, 0.86f);
            if (mat.HasProperty(MetallicId))
                mat.SetFloat(MetallicId, 0.035f);
        }

        static void EnsureSharedTextures()
        {
            if (_sharedAlbedoNoise != null)
                return;

            _sharedAlbedoNoise = BuildNoiseTexture(192, false, 1.7f);
            _sharedAlbedoNoise.wrapMode = TextureWrapMode.Repeat;
            _sharedAlbedoNoise.filterMode = FilterMode.Bilinear;
            _sharedAlbedoNoise.anisoLevel = 4;
            _sharedAlbedoNoise.hideFlags = HideFlags.DontSave;

            _sharedBumpNoise = BuildNoiseTexture(192, true, 2.1f);
            _sharedBumpNoise.wrapMode = TextureWrapMode.Repeat;
            _sharedBumpNoise.filterMode = FilterMode.Bilinear;
            _sharedBumpNoise.anisoLevel = 4;
            _sharedBumpNoise.hideFlags = HideFlags.DontSave;

            _sharedDetailNoise = BuildNoiseTexture(256, false, 4.5f);
            _sharedDetailNoise.wrapMode = TextureWrapMode.Repeat;
            _sharedDetailNoise.filterMode = FilterMode.Bilinear;
            _sharedDetailNoise.anisoLevel = 2;
            _sharedDetailNoise.hideFlags = HideFlags.DontSave;
        }

        static Texture2D BuildNoiseTexture(int size, bool packAsNormalish, float freqMul)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var nx = x / (float)size;
                    var ny = y / (float)size;
                    var n = Mathf.PerlinNoise(nx * 6.2f * freqMul, ny * 6.2f * freqMul) * 0.55f +
                            Mathf.PerlinNoise(nx * 14f * freqMul + 13.1f, ny * 14f * freqMul + 7.3f) * 0.35f +
                            Mathf.PerlinNoise(nx * 28f * freqMul, ny * 28f * freqMul + 2.2f) * 0.1f;
                    n = Mathf.Clamp01(n);
                    if (packAsNormalish)
                    {
                        var dx = Mathf.PerlinNoise(nx * 10f * freqMul + 0.1f, ny * 10f * freqMul) - 0.5f;
                        var dy = Mathf.PerlinNoise(nx * 10f * freqMul, ny * 10f * freqMul + 0.1f) - 0.5f;
                        var nz = Mathf.Sqrt(Mathf.Clamp01(1f - dx * dx - dy * dy));
                        tex.SetPixel(x, y, new Color(dx * 0.5f + 0.5f, dy * 0.5f + 0.5f, nz, 1f));
                    }
                    else
                    {
                        var blue = Mathf.Lerp(0.18f, 0.72f, n);
                        tex.SetPixel(x, y, new Color(blue * 0.28f, blue * 0.52f, blue * 0.95f, 1f));
                    }
                }
            }

            tex.Apply(true, true);
            return tex;
        }
    }
}
