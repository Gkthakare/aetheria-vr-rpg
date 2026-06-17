#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.EditorTools
{
    /// <summary>
    /// Built-in Standard and legacy skybox shaders do not render under URP — everything using them turns magenta.
    /// Run after assigning a URP pipeline (Aetheria → Rendering → Fix magenta materials…).
    /// </summary>
    public static class AetheriaBuiltInToUrpMaterialUpgrade
    {
        const string UrpLit = "Universal Render Pipeline/Lit";
        const string UrpSimpleLit = "Universal Render Pipeline/Simple Lit";

        /// <summary>Project-local URP skybox shaders (package sky shaders are not always discoverable by Shader.Find).</summary>
        public const string AetheriaPanoramicSkyShaderName = "Aetheria/Skybox/Panoramic (URP)";
        public const string AetheriaCubemapSkyShaderName = "Aetheria/Skybox/Cubemap (URP)";

        const string AetheriaPanoramicShaderPath = "Assets/Aetheria/Shaders/AetheriaUrpSkyboxPanoramic.shader";
        const string AetheriaCubemapShaderPath = "Assets/Aetheria/Shaders/AetheriaUrpSkyboxCubemap.shader";

        static readonly string[] PanoramicSkyCandidates =
        {
            AetheriaPanoramicSkyShaderName,
            "Universal Render Pipeline/Skybox/Panoramic",
            "Hidden/Universal Render Pipeline/Skybox/Panoramic",
            "Universal Render Pipeline/SkyboxPanoramic"
        };

        static readonly string[] CubemapSkyCandidates =
        {
            AetheriaCubemapSkyShaderName,
            "Universal Render Pipeline/Skybox/Cubemap",
            "Hidden/Universal Render Pipeline/Skybox/Cubemap"
        };

        static readonly string[] SixSidedSkyCandidates =
        {
            "Universal Render Pipeline/Skybox/6 sided",
            "Universal Render Pipeline/Skybox/6 Sided",
            "Hidden/Universal Render Pipeline/Skybox/6 sided"
        };

        static readonly string[] UrpPackageShaderRoots =
        {
            "Packages/com.unity.render-pipelines.universal",
            "Packages/com.unity.render-pipelines.core"
        };

        [MenuItem("Aetheria/Rendering/Upgrade built-in Standard & skybox materials to URP (fix pink world)")]
        static void UpgradeAllUnderAssets()
        {
            EnsureAetheriaSkyShadersImported();

            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                Debug.LogWarning(
                    "[Aetheria] No SRP assigned in Graphics Settings. Assign URP first, then run this again.");
            }

            var urpLit = Shader.Find(UrpLit);
            var urpSimple = Shader.Find(UrpSimpleLit);
            if (urpLit == null)
            {
                Debug.LogError("[Aetheria] Could not find \"" + UrpLit + "\". Is the URP package installed?");
                return;
            }

            var panoramic = ResolvePanoramicSkyShader();
            if (panoramic == null)
                Debug.LogWarning(
                    "[Aetheria] No URP panoramic skybox shader found (Shader.Find + package scan). " +
                    "Panoramic sky materials were skipped.");

            var cubemapSky = ResolveCubemapSkyShader();
            if (cubemapSky == null)
                Debug.LogWarning(
                    "[Aetheria] No URP cubemap skybox shader found (Shader.Find + package scan). " +
                    "Cubemap sky materials were skipped.");

            var sixSidedSky = ResolveSixSidedSkyShader();

            var guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            var upgraded = 0;
            var skipped = new List<string>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in guids)
                {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Packages/"))
                    continue;

                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null)
                    continue;

                var sn = mat.shader.name;
                if (sn == UrpLit || sn == UrpSimpleLit)
                    continue;

                if (IsBuiltInStandardFamily(sn))
                {
                    UpgradeStandardToLit(mat, urpLit);
                    EditorUtility.SetDirty(mat);
                    upgraded++;
                }
                else if (IsBuiltInNatureTreeLeavesShader(sn))
                {
                    UpgradeNatureTreeLeavesToLit(mat, urpLit);
                    EditorUtility.SetDirty(mat);
                    upgraded++;
                }
                else if (sn.IndexOf("Suimono", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    UpgradeSuimonoSurfaceToUrpWater(mat, urpLit);
                    EditorUtility.SetDirty(mat);
                    upgraded++;
                }
                else if (sn == "Legacy Shaders/Diffuse" || sn == "Mobile/Diffuse")
                {
                    UpgradeDiffuseToSimpleLit(mat, urpSimple ?? urpLit);
                    EditorUtility.SetDirty(mat);
                    upgraded++;
                }
                else if (sn == "Skybox/Panoramic" && panoramic != null)
                {
                    UpgradePanoramicSky(mat, panoramic);
                    EditorUtility.SetDirty(mat);
                    upgraded++;
                }
                else if (sn.StartsWith("Skybox/", System.StringComparison.Ordinal) &&
                         sn != "Skybox/Procedural")
                {
                    if (TryUpgradeSkybox(mat, panoramic, cubemapSky, sixSidedSky))
                    {
                        EditorUtility.SetDirty(mat);
                        upgraded++;
                    }
                    else
                        skipped.Add(path + " (" + sn + ")");
                }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (upgraded > 0)
                AssetDatabase.SaveAssets();
            Debug.Log("[Aetheria] URP material upgrade complete. Converted: " + upgraded + ".");
            if (skipped.Count > 0 && skipped.Count <= 12)
                Debug.LogWarning(
                    "[Aetheria] Skipped skyboxes (assign URP sky shaders or upgrade by hand): " +
                    string.Join(", ", skipped));
            else if (skipped.Count > 12)
                Debug.LogWarning("[Aetheria] Skipped " + skipped.Count +
                                 " skybox materials (see first few): " +
                                 string.Join(", ", skipped.GetRange(0, 12)));
        }

        static Shader ResolveFirstShader(IEnumerable<string> names)
        {
            foreach (var n in names)
            {
                var s = Shader.Find(n);
                if (s != null)
                    return s;
            }

            return null;
        }

        static void EnsureAetheriaSkyShadersImported()
        {
            // ForceUpdate reimports the whole database graph (noisy FBX warnings). Import only if assets are missing.
            if (AssetDatabase.LoadAssetAtPath<Shader>(AetheriaPanoramicShaderPath) != null &&
                AssetDatabase.LoadAssetAtPath<Shader>(AetheriaCubemapShaderPath) != null)
                return;

            if (File.Exists(Path.Combine(Application.dataPath, AetheriaPanoramicShaderPath.Substring(7))))
                AssetDatabase.ImportAsset(AetheriaPanoramicShaderPath, ImportAssetOptions.Default);
            if (File.Exists(Path.Combine(Application.dataPath, AetheriaCubemapShaderPath.Substring(7))))
                AssetDatabase.ImportAsset(AetheriaCubemapShaderPath, ImportAssetOptions.Default);
        }

        static Shader ResolvePanoramicSkyShader()
        {
            var s = ResolveFirstShader(PanoramicSkyCandidates);
            if (s != null)
                return s;
            s = FindSkyboxShaderInUrpPackages("Panoramic");
            if (s != null)
                return s;
            return FindLoadedSkyboxShaderPreferUrp("Panoramic");
        }

        static Shader ResolveCubemapSkyShader()
        {
            var s = ResolveFirstShader(CubemapSkyCandidates);
            if (s != null)
                return s;
            s = FindSkyboxShaderInUrpPackages("Cubemap");
            if (s != null)
                return s;
            return FindLoadedSkyboxShaderPreferUrp("Cubemap");
        }

        static Shader ResolveSixSidedSkyShader()
        {
            var s = ResolveFirstShader(SixSidedSkyCandidates);
            if (s != null)
                return s;
            s = FindSixSidedSkyInUrpPackages();
            if (s != null)
                return s;
            foreach (var sh in Resources.FindObjectsOfTypeAll<Shader>())
            {
                if (sh == null || string.IsNullOrEmpty(sh.name))
                    continue;
                if (IsBuiltInSkyboxName(sh.name))
                    continue;
                var n = sh.name;
                if (n.IndexOf("6", StringComparison.Ordinal) < 0)
                    continue;
                if (n.IndexOf("Sided", StringComparison.OrdinalIgnoreCase) < 0 &&
                    n.IndexOf("sided", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (n.IndexOf("Skybox", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (n.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Hidden", StringComparison.OrdinalIgnoreCase) >= 0)
                    return sh;
            }

            return null;
        }

        static Shader FindSkyboxShaderInUrpPackages(string pathSubstring)
        {
            foreach (var root in UrpPackageShaderRoots)
            {
                if (!AssetDatabase.IsValidFolder(root))
                    continue;
                var guids = AssetDatabase.FindAssets("t:Shader", new[] { root });
                foreach (var guid in guids)
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid);
                    if (p.IndexOf(pathSubstring, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    var sh = AssetDatabase.LoadAssetAtPath<Shader>(p);
                    if (sh == null || IsBuiltInSkyboxName(sh.name))
                        continue;
                    if (sh.name.IndexOf("Skybox", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (pathSubstring.IndexOf("Panoramic", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        sh.name.IndexOf("Panoramic", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (pathSubstring.IndexOf("Cubemap", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        sh.name.IndexOf("Cubemap", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    return sh;
                }
            }

            return null;
        }

        static Shader FindSixSidedSkyInUrpPackages()
        {
            foreach (var root in UrpPackageShaderRoots)
            {
                if (!AssetDatabase.IsValidFolder(root))
                    continue;
                var guids = AssetDatabase.FindAssets("t:Shader", new[] { root });
                foreach (var guid in guids)
                {
                    var sh = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                    if (sh == null || IsBuiltInSkyboxName(sh.name))
                        continue;
                    if (sh.name.IndexOf("Skybox", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (sh.name.IndexOf("6", StringComparison.Ordinal) < 0)
                        continue;
                    if (sh.name.IndexOf("Sided", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    return sh;
                }
            }

            return null;
        }

        static Shader FindLoadedSkyboxShaderPreferUrp(string kindSubstring)
        {
            Shader fallback = null;
            foreach (var sh in Resources.FindObjectsOfTypeAll<Shader>())
            {
                if (sh == null || string.IsNullOrEmpty(sh.name))
                    continue;
                if (IsBuiltInSkyboxName(sh.name))
                    continue;
                if (sh.name.IndexOf(kindSubstring, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (sh.name.IndexOf("Skybox", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (sh.name.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    sh.name.IndexOf("Hidden", StringComparison.OrdinalIgnoreCase) >= 0)
                    return sh;
                fallback = fallback ?? sh;
            }

            return fallback;
        }

        static bool IsBuiltInSkyboxName(string shaderName)
        {
            return shaderName == "Skybox/Panoramic" || shaderName == "Skybox/Cubemap" ||
                   shaderName == "Skybox/6 Sided" || shaderName == "Skybox/Procedural";
        }

        static bool IsBuiltInNatureTreeLeavesShader(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName))
                return false;
            return shaderName.IndexOf("Nature/Tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   shaderName.IndexOf("Tree Soft Occlusion", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsBuiltInStandardFamily(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName))
                return false;
            if (shaderName == "Standard")
                return true;
            if (shaderName.StartsWith("Standard (", StringComparison.Ordinal))
                return true;
            if (shaderName.StartsWith("Standard/", StringComparison.Ordinal))
                return true;
            return false;
        }

        static void UpgradeSuimonoSurfaceToUrpWater(Material mat, Shader urpLit)
        {
            mat.shader = urpLit;
            mat.shaderKeywords = null;
            mat.SetFloat("_Surface", 1f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", 1f);
            mat.SetFloat("_DstBlendAlpha", 10f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.SetFloat("_Metallic", 0.04f);
            mat.SetFloat("_Smoothness", 0.88f);
            mat.renderQueue = 3000;
            mat.SetColor("_BaseColor", new Color(0.12f, 0.38f, 0.55f, 0.72f));
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", mat.GetColor("_BaseColor"));
            mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_ALPHATEST_ON");
        }

        static void UpgradeNatureTreeLeavesToLit(Material mat, Shader urpLit)
        {
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            var color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            var bump = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            var cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;
            var smoothness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.15f;

            mat.shader = urpLit;
            mat.shaderKeywords = null;

            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", mainTex);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (bump != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", bump);
                mat.EnableKeyword("_NORMALMAP");
            }

            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_AlphaClip"))
                mat.SetFloat("_AlphaClip", 1f);
            if (mat.HasProperty("_Cutoff"))
                mat.SetFloat("_Cutoff", cutoff);
            if (mat.HasProperty("_Cull"))
                mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Surface", 0f);
        }

        static void UpgradeStandardToLit(Material mat, Shader urpLit)
        {
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            var color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            var metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            var smoothness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
            var bump = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            var emission = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;

            mat.shader = urpLit;
            mat.shaderKeywords = null;

            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", mainTex);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);

            if (bump != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", bump);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (emission.maxColorComponent > 1e-4f || mat.IsKeywordEnabled("_EMISSION"))
            {
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.EnableKeyword("_EMISSION");
            }
        }

        static void UpgradeDiffuseToSimpleLit(Material mat, Shader target)
        {
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            var color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

            mat.shader = target;
            mat.shaderKeywords = null;

            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", mainTex);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.35f);
            mat.SetFloat("_Metallic", 0f);
        }

        static void UpgradePanoramicSky(Material mat, Shader panoramicUrp)
        {
            Texture tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            var tint = mat.HasProperty("_Tint") ? mat.GetColor("_Tint") : Color.white;
            var exposure = mat.HasProperty("_Exposure") ? mat.GetFloat("_Exposure") : 1f;
            var rotation = mat.HasProperty("_Rotation") ? mat.GetFloat("_Rotation") : 0f;
            var mapping = mat.HasProperty("_Mapping") ? mat.GetFloat("_Mapping") : 1f;
            var imageType = mat.HasProperty("_ImageType") ? mat.GetFloat("_ImageType") : 0f;
            var layout = mat.HasProperty("_Layout") ? mat.GetFloat("_Layout") : 0f;
            var mirror = mat.HasProperty("_MirrorOnBack") ? mat.GetFloat("_MirrorOnBack") : 0f;

            mat.shader = panoramicUrp;
            mat.shaderKeywords = null;

            if (tex != null)
            {
                if (mat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", tex);
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", tex);
            }
            if (mat.HasProperty("_Tint"))
                mat.SetColor("_Tint", tint);
            if (mat.HasProperty("_Exposure"))
                mat.SetFloat("_Exposure", exposure);
            if (mat.HasProperty("_Rotation"))
                mat.SetFloat("_Rotation", rotation);
            if (mat.HasProperty("_Mapping"))
                mat.SetFloat("_Mapping", mapping);
            if (mat.HasProperty("_ImageType"))
                mat.SetFloat("_ImageType", imageType);
            if (mat.HasProperty("_Layout"))
                mat.SetFloat("_Layout", layout);
            if (mat.HasProperty("_MirrorOnBack"))
                mat.SetFloat("_MirrorOnBack", mirror);
        }

        static bool TryUpgradeSkybox(Material mat, Shader panoramic, Shader cubemapSky, Shader sixSidedSky)
        {
            var sn = mat.shader.name;
            if (sn == "Skybox/Cubemap" && cubemapSky != null)
            {
                UpgradeCubemapSky(mat, cubemapSky);
                return true;
            }

            if (sn == "Skybox/6 Sided" && sixSidedSky != null)
            {
                UpgradeSixSidedSky(mat, sixSidedSky);
                return true;
            }

            if (panoramic != null && sn != "Skybox/Cubemap" && sn != "Skybox/6 Sided")
            {
                UpgradePanoramicSky(mat, panoramic);
                return true;
            }

            return false;
        }

        static void UpgradeCubemapSky(Material mat, Shader target)
        {
            var tex = mat.HasProperty("_Tex") ? mat.GetTexture("_Tex") : null;
            var tint = mat.HasProperty("_Tint") ? mat.GetColor("_Tint") : Color.white;
            var exposure = mat.HasProperty("_Exposure") ? mat.GetFloat("_Exposure") : 1f;
            var rotation = mat.HasProperty("_Rotation") ? mat.GetFloat("_Rotation") : 0f;

            mat.shader = target;
            mat.shaderKeywords = null;

            if (tex != null && mat.HasProperty("_Tex"))
                mat.SetTexture("_Tex", tex);
            if (mat.HasProperty("_Tint"))
                mat.SetColor("_Tint", tint);
            if (mat.HasProperty("_Exposure"))
                mat.SetFloat("_Exposure", exposure);
            if (mat.HasProperty("_Rotation"))
                mat.SetFloat("_Rotation", rotation);
        }

        static void UpgradeSixSidedSky(Material mat, Shader target)
        {
            Texture ft = mat.HasProperty("_FrontTex") ? mat.GetTexture("_FrontTex") : null;
            Texture bt = mat.HasProperty("_BackTex") ? mat.GetTexture("_BackTex") : null;
            Texture lt = mat.HasProperty("_LeftTex") ? mat.GetTexture("_LeftTex") : null;
            Texture rt = mat.HasProperty("_RightTex") ? mat.GetTexture("_RightTex") : null;
            Texture ut = mat.HasProperty("_UpTex") ? mat.GetTexture("_UpTex") : null;
            Texture dt = mat.HasProperty("_DownTex") ? mat.GetTexture("_DownTex") : null;
            var tint = mat.HasProperty("_Tint") ? mat.GetColor("_Tint") : Color.white;
            var exposure = mat.HasProperty("_Exposure") ? mat.GetFloat("_Exposure") : 1f;
            var rotation = mat.HasProperty("_Rotation") ? mat.GetFloat("_Rotation") : 0f;

            mat.shader = target;
            mat.shaderKeywords = null;

            if (ft != null && mat.HasProperty("_FrontTex")) mat.SetTexture("_FrontTex", ft);
            if (bt != null && mat.HasProperty("_BackTex")) mat.SetTexture("_BackTex", bt);
            if (lt != null && mat.HasProperty("_LeftTex")) mat.SetTexture("_LeftTex", lt);
            if (rt != null && mat.HasProperty("_RightTex")) mat.SetTexture("_RightTex", rt);
            if (ut != null && mat.HasProperty("_UpTex")) mat.SetTexture("_UpTex", ut);
            if (dt != null && mat.HasProperty("_DownTex")) mat.SetTexture("_DownTex", dt);
            if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", tint);
            if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", exposure);
            if (mat.HasProperty("_Rotation")) mat.SetFloat("_Rotation", rotation);
        }
    }
}
#endif
