#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aetheria.EditorTools
{
    /// <summary>
    /// Assigns Graphics to URP. The pipeline asset must reference a saved <see cref="UniversalRendererData"/>.
    /// Important: while the active pipeline has no default renderer, URP logs errors during asset creation
    /// (e.g. PostProcessData / ResourceReloader). We temporarily clear GraphicsSettings.defaultRenderPipeline first.
    /// </summary>
    public static class AetheriaUrpGraphicsSetup
    {
        const string RendererPath = "Assets/Settings/Aetheria_UniversalRendererData.asset";
        const string PipelinePath = "Assets/Settings/Aetheria_UniversalRenderPipelineAsset.asset";

        [MenuItem("Aetheria/Rendering/Fix magenta materials — create & assign URP pipeline")]
        static void CreateAndAssignUrp()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");

            var rendererDataPath = RendererPath;
            var pipelineAssetPath = PipelinePath;
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererDataPath);
            var pipelineExisting = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelineAssetPath);

            if (rendererData != null && pipelineExisting != null &&
                GraphicsSettings.defaultRenderPipeline == pipelineExisting &&
                PipelineAlreadyReferencesRenderer(pipelineExisting, rendererData))
            {
                Debug.Log(
                    "[Aetheria] URP already assigned (" + pipelineAssetPath + "). No asset save — avoids FBX reimport noise (e.g. Soft_Hills normals).");
                return;
            }

            var previous = GraphicsSettings.defaultRenderPipeline;
            GraphicsSettings.defaultRenderPipeline = null;
            // Batch disk writes so SaveAssets runs once; redundant saves reimport the whole project (FBX warnings).

            var wroteAnyAsset = false;
            try
            {
                AssetDatabase.StartAssetEditing();
                try
                {
                    rendererData = EnsureRendererAssetOnDisk(ref wroteAnyAsset);
                    var pipeline = EnsurePipelineAssetReferencesRenderer(rendererData, ref wroteAnyAsset);

                    GraphicsSettings.defaultRenderPipeline = pipeline;
                    if (wroteAnyAsset)
                        EditorUtility.SetDirty(pipeline);
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                if (wroteAnyAsset || GraphicsSettings.defaultRenderPipeline != previous)
                    AssetDatabase.SaveAssets();

                Debug.Log(
                    "[Aetheria] URP ready: " + PipelinePath + " → default renderer " + RendererPath +
                    ". GraphicsSettings.defaultRenderPipeline is assigned.");
            }
            catch (Exception e)
            {
                GraphicsSettings.defaultRenderPipeline = previous;
                if (wroteAnyAsset)
                    AssetDatabase.SaveAssets();
                Debug.LogError("[Aetheria] URP setup failed; restored previous render pipeline. " + e.Message);
                throw;
            }
        }

        static bool PipelineAlreadyReferencesRenderer(UniversalRenderPipelineAsset pipeline, UniversalRendererData rendererData)
        {
            if (pipeline == null || rendererData == null)
                return false;
            var so = new SerializedObject(pipeline);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null || list.arraySize < 1)
                return false;
            if (list.GetArrayElementAtIndex(0).objectReferenceValue != rendererData)
                return false;
            var def = so.FindProperty("m_DefaultRendererIndex");
            return def == null || def.intValue == 0;
        }

        static UniversalRendererData EnsureRendererAssetOnDisk(ref bool wroteAnyAsset)
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (existing != null)
                return existing;

            var method = typeof(UniversalRenderPipelineAsset).GetMethod(
                "CreateRendererAsset",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                Debug.LogError("[Aetheria] URP CreateRendererAsset not found — update this script for your URP version.");
                return ScriptableObject.CreateInstance<UniversalRendererData>();
            }

            var created = (ScriptableRendererData)method.Invoke(
                null,
                new object[] { RendererPath, RendererType.UniversalRenderer, false, "Renderer" });
            wroteAnyAsset = true;
            return (UniversalRendererData)created;
        }

        static UniversalRenderPipelineAsset EnsurePipelineAssetReferencesRenderer(UniversalRendererData rendererData, ref bool wroteAnyAsset)
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                wroteAnyAsset = true;
                return pipeline;
            }

            if (PipelineAlreadyReferencesRenderer(pipeline, rendererData))
                return pipeline;

            var so = new SerializedObject(pipeline);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null)
            {
                Debug.LogError("[Aetheria] Could not find m_RendererDataList on URP asset.");
                return pipeline;
            }

            if (list.arraySize < 1)
                list.InsertArrayElementAtIndex(0);
            list.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;

            var def = so.FindProperty("m_DefaultRendererIndex");
            if (def != null)
                def.intValue = 0;

            so.ApplyModifiedProperties();
            wroteAnyAsset = true;
            return pipeline;
        }
    }
}
#endif
