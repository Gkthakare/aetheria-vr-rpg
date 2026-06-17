#if UNITY_EDITOR
using Aetheria.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.EditorTools
{
    public static class IslandRealismPresetWizard
    {
        [MenuItem("Aetheria/Scenes/Apply Island Realism Preset")]
        public static void ApplyPresetMenu()
        {
            var world = FindTargetWorldManager();
            if (world == null)
            {
                EditorUtility.DisplayDialog(
                    "Island Realism Preset",
                    "Could not find IslandWorldManager with IslandWorldBuilder in the open scene.\n\n" +
                    "Open Assets/Scenes/IslandTest.unity, select IslandWorldManager, then run this menu again.",
                    "OK");
                return;
            }

            ApplyPreset(world);
            EditorUtility.SetDirty(world.gameObject);
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(world.gameObject.scene);

            Debug.Log("[IslandRealismPreset] Applied to IslandWorldManager. Press Play to preview.");
        }

        static IslandWorldBuilder FindTargetWorldManager()
        {
            if (Selection.activeGameObject != null)
            {
                var sel = Selection.activeGameObject.GetComponent<IslandWorldBuilder>();
                if (sel != null) return sel;
            }

            var all = Object.FindObjectsByType<IslandWorldBuilder>(FindObjectsSortMode.None);
            if (all != null && all.Length > 0) return all[0];
            return null;
        }

        static void ApplyPreset(IslandWorldBuilder builder)
        {
            var so = new SerializedObject(builder);

            // Ground + vegetation materials
            SetObj(so, "grassMaterial", LoadMat("Assets/Art/Materials/Mat_Grass.mat", "Mat_Grass"));
            SetObj(so, "sandMaterial", LoadMat("Assets/Art/Materials/Mat_Sand.mat", "Mat_Sand"));
            SetObj(so, "grassPatchMaterial", LoadMat("Assets/Art/Materials/Mat_Grass_All.mat", "Mat_Grass_All"));
            SetObj(so, "mountainMaterial", LoadMat("Assets/Art/Materials/Mat_Mountain.mat", "Mat_Mountain"));
            SetObj(so, "treeTrunkMaterial", LoadMat(
                "Assets/Mountain Terrain rocks and tree/Materials/Trees/tree_01/bark_01.mat", "bark_01"));
            SetObj(so, "treeFoliageMaterial", LoadMat(
                "Assets/Mountain Terrain rocks and tree/Materials/Trees/tree_01/leaves_01.mat", "leaves_01"));

            // Trees / mountains / rocks
            var meshyCliff = LoadModelOrPrefab(
                "Assets/ImportedContent/Environments/coastal-cliff-meshy-v1/environment_cliff_coastal_v1.glb",
                "environment_cliff_coastal_v1");
            SetObj(so, "mountainTerrainPrefab", meshyCliff != null
                ? meshyCliff
                : LoadPrefab("Assets/Mountain Terrain rocks and tree/Prefab/mountain_terrain_01.prefab", "mountain_terrain_01"));
            SetPrefabArray(so, "treePrefabs", new[]
            {
                LoadPrefab("Assets/Mountain Terrain rocks and tree/Prefab/tree_01.prefab", "tree_01")
            });
            SetPrefabArray(so, "beachRockPrefabs", new[]
            {
                LoadPrefab("Assets/Mountain Terrain rocks and tree/Prefab/rock_set_01.prefab", "rock_set_01"),
                LoadPrefab("Assets/Mountain Terrain rocks and tree/Prefab/rock_set_02.prefab", "rock_set_02"),
                LoadPrefab("Assets/Mountain Terrain rocks and tree/Prefab/rock_set_03.prefab", "rock_set_03"),
                LoadPrefab("Assets/Mountain Terrain rocks and tree/Prefab/rock_set_04.prefab", "rock_set_04"),
            });

            // Enemies: force scary monster pipeline, boar as fallback
            SetBool(so, "useImportedScaryMonsterOnIsland", true);
            SetObj(so, "enemyPrefab", LoadPrefab("Assets/Prefabs/Enemies/Enemy_Boar.prefab", "Enemy_Boar"));
            var scary = LoadPrefab("Assets/ImportedContent/Creatures/scary-monster/source/Monster.fbx", "Monster");
            if (scary != null)
                SetObj(so, "scaryMonsterIslandPrefab", scary);

            SetFloat(so, "scaryMonsterIslandScale", 0.5f);
            SetInt(so, "enemyCount", 18);

            // Stability for spawn/view
            SetBool(so, "repositionVrOriginOnStart", true);
            SetBool(so, "spawnVrAtIslandCenter", true);
            SetBool(so, "orientVrOriginYawTowardOpenOcean", true);
            SetFloat(so, "centerSpawnYawDegrees", 180f);
            SetBool(so, "autoBakeIslandNavMesh", true);

            // Slightly richer island shape while staying close to existing defaults
            SetFloat(so, "coastlineNoiseStrength", 0.14f);
            SetFloat(so, "coastNoiseScale", 2.9f);
            SetInt(so, "beachRockCount", 42);
            SetInt(so, "treeCount", 95);
            SetInt(so, "mountainCount", 9);
            SetFloat(so, "mountainPrefabScale", 1.25f);
            SetFloat(so, "oceanViewMountainSparseHalfAngleDegrees", 28f);

            // Wire XR + nav refs if missing
            if (GetObj(so, "xrOrigin") == null)
            {
                var xrGo = GameObject.Find("XR Origin (XR Rig)");
                if (xrGo != null) SetObj(so, "xrOrigin", xrGo.transform);
            }
            if (GetObj(so, "islandNavMeshSurface") == null)
            {
                var nav = builder.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
                if (nav != null) SetObj(so, "islandNavMeshSurface", nav);
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            var suimono = builder.GetComponent<SuimonoInfinityWaterBootstrap>();
            if (suimono != null)
                ApplySuimonoPreset(suimono, builder);

            var sky = LoadMat("Assets/Art/Materials/Mat_Sky_Environment.mat", "Mat_Sky_Environment");
            if (sky != null)
            {
                RenderSettings.skybox = sky;
                RenderSettings.ambientMode = AmbientMode.Skybox;
            }
        }

        static void ApplySuimonoPreset(SuimonoInfinityWaterBootstrap suimono, IslandWorldBuilder builder)
        {
            var so = new SerializedObject(suimono);
            SetBool(so, "enableSuimonoInfiniteOcean", true);
            SetObj(so, "islandWorldForSeaLevel", builder);
            SetFloat(so, "waterSurfaceY", 0f);
            SetBool(so, "applyGameplayWaterDefaults", true);
            SetFloat(so, "defaultFlowSpeed", 0.06f);
            SetFloat(so, "defaultWaveScale", 0.5f);
            SetBool(so, "enableSuimonoInteraction", true);
            SetBool(so, "reduceReflectionWorkForVr", true);
            SetFloat(so, "refractStrengthWhenStableVr", 0.5f);
            SetBool(so, "forceMainCameraForwardRendering", true);
            SetBool(so, "extendMainCameraFarClipForHorizon", true);
            SetFloat(so, "mainCameraFarClipPlane", 5000f);
            SetBool(so, "ensureSkyboxClearFlagsOnMainCamera", true);
            SetObj(so, "skyboxMaterialEnsure",
                LoadMat("Assets/Art/Materials/Mat_Sky_Environment.mat", "Mat_Sky_Environment"));
            SetObj(so, "suimonoModulePrefab", LoadPrefab("Assets/Resources/Suimono/SUIMONO_Module.prefab", "SUIMONO_Module"));
            SetObj(so, "suimonoSurfacePrefab", LoadPrefab("Assets/Resources/Suimono/SUIMONO_Surface.prefab", "SUIMONO_Surface"));

            if (GetObj(so, "manualCameraTransform") == null && Camera.main != null)
                SetObj(so, "manualCameraTransform", Camera.main.transform);
            if (GetObj(so, "sceneDirectionalLight") == null)
            {
                var light = Object.FindFirstObjectByType<Light>();
                if (light != null && light.type == LightType.Directional)
                    SetObj(so, "sceneDirectionalLight", light);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material LoadMat(string preferredPath, string fallbackNameContains)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(preferredPath);
            if (m != null) return m;
            foreach (var guid in AssetDatabase.FindAssets($"t:Material {fallbackNameContains}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m != null) return m;
            }
            return null;
        }

        static GameObject LoadPrefab(string preferredPath, string fallbackNameContains)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(preferredPath);
            if (p != null) return p;
            foreach (var guid in AssetDatabase.FindAssets($"{fallbackNameContains} t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (p != null) return p;
            }
            foreach (var guid in AssetDatabase.FindAssets($"{fallbackNameContains} t:Model"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (p != null) return p;
            }
            return null;
        }

        static GameObject LoadModelOrPrefab(string preferredPath, string fallbackNameContains)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(preferredPath);
            if (p != null) return p;
            return LoadPrefab(preferredPath, fallbackNameContains);
        }

        static void SetPrefabArray(SerializedObject so, string name, GameObject[] values)
        {
            var p = so.FindProperty(name);
            if (p == null || !p.isArray) return;
            int count = 0;
            for (int i = 0; i < values.Length; i++)
                if (values[i] != null) count++;
            p.arraySize = count;
            int idx = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null) continue;
                p.GetArrayElementAtIndex(idx).objectReferenceValue = values[i];
                idx++;
            }
        }

        static Object GetObj(SerializedObject so, string name)
        {
            var p = so.FindProperty(name);
            return p != null ? p.objectReferenceValue : null;
        }

        static void SetObj(SerializedObject so, string name, Object value)
        {
            var p = so.FindProperty(name);
            if (p != null && value != null)
                p.objectReferenceValue = value;
        }

        static void SetBool(SerializedObject so, string name, bool value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.boolValue = value;
        }

        static void SetInt(SerializedObject so, string name, int value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.intValue = value;
        }

        static void SetFloat(SerializedObject so, string name, float value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.floatValue = value;
        }
    }
}
#endif
