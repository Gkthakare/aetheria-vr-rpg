#if UNITY_EDITOR
using System.IO;
using Aetheria.Dungeons;
using Aetheria.Gameplay;
using Aetheria.VR;
using Aetheria.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;

namespace Aetheria.EditorTools
{
    /// <summary>
    /// Builds a minimal HelloVR-style island test scene: one XR rig, one IslandWorldManager (island + dungeon + Suimono),
    /// no duplicate WorldManager / ChunkManager. Use this as the “fresh” playtest scene.
    /// </summary>
    public static class IslandTestSceneWizard
    {
        const string DefaultScenePath = "Assets/Scenes/IslandTest.unity";

        [MenuItem("Aetheria/Scenes/Create Island Test Scene (Fresh)")]
        static void MenuCreate()
        {
            if (!EditorUtility.DisplayDialog(
                    "Island Test Scene",
                    "Creates a new minimal VR island scene and saves it to:\n" + DefaultScenePath +
                    "\n\n(Overwrites that asset if it already exists.)\n\n" +
                    "Requires XR Interaction Toolkit sample prefab **XR Origin (XR Rig)** " +
                    "(add samples from the Package Manager if the wizard logs an error).",
                    "Create",
                    "Cancel"))
                return;

            CreateIslandTestScene(DefaultScenePath);
        }

        public static void CreateIslandTestScene(string scenePath)
        {
            EnsureFolder(Path.GetDirectoryName(scenePath));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sunGo = new GameObject("Directional Light");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.956f, 0.839f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var xrimGo = new GameObject("XR Interaction Manager");
            xrimGo.AddComponent<XRInteractionManager>();

            var xrPrefab = FindXrOriginRigPrefab();
            if (xrPrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Island Test Scene",
                    "Could not find prefab **XR Origin (XR Rig)**.\n\n" +
                    "Package Manager → **XR Interaction Toolkit** → Samples → import **Starter Assets**, then run this menu again.",
                    "OK");
                Debug.LogError(
                    "[IslandTestScene] Missing XR Origin prefab. Import XRI Starter Assets (see dialog), then retry.");
                return;
            }

            var xrRoot = (GameObject)PrefabUtility.InstantiatePrefab(xrPrefab);
            xrRoot.name = "XR Origin (XR Rig)";

            var cam = xrRoot.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.farClipPlane = 5000f;
            }

            if (xrRoot.GetComponent<VRPlayerController>() == null)
            {
                var vpc = xrRoot.AddComponent<VRPlayerController>();
                var soVpc = new SerializedObject(vpc);
                SetIfExists(soVpc, "xrOrigin", xrRoot.transform);
                SetIfExists(soVpc, "xrCamera", cam);
                soVpc.ApplyModifiedPropertiesWithoutUndo();
            }

            if (xrRoot.GetComponent<FingertipMeshyNpcCompanion>() == null)
                xrRoot.AddComponent<FingertipMeshyNpcCompanion>();

            var islandGo = new GameObject("IslandWorldManager");
            islandGo.transform.position = Vector3.zero;

            var nav = islandGo.AddComponent<NavMeshSurface>();
            var islandBuilder = islandGo.AddComponent<IslandWorldBuilder>();
            var dungeonGen = islandGo.AddComponent<DungeonGenerator>();
            var dungeonViz = islandGo.AddComponent<DungeonRoomVisualizer>();
            var combat = islandGo.AddComponent<DungeonFloorCombatController>();
            var spawnDir = islandGo.AddComponent<SpawnDirector>();
            islandGo.AddComponent<NavMeshSetupValidator>();
            islandGo.AddComponent<FinalVrSceneBootstrap>();
            var suimono = islandGo.AddComponent<SuimonoInfinityWaterBootstrap>();

            var ibSo = new SerializedObject(islandBuilder);
            SetIfExists(ibSo, "xrOrigin", xrRoot.transform);
            SetIfExists(ibSo, "islandNavMeshSurface", nav);
            ibSo.ApplyModifiedPropertiesWithoutUndo();

            var brick = TryLoadFirstPrefabNameContains("Assets/ImportedContent", "brick");
            var dvSo = new SerializedObject(dungeonViz);
            SetIfExists(dvSo, "dungeonGenerator", dungeonGen);
            SetIfExists(dvSo, "player", cam != null ? cam.transform : xrRoot.transform);
            SetIfExists(dvSo, "sceneDirectionalLight", sun);
            if (brick != null)
                SetIfExists(dvSo, "roomPadPrefab", brick);
            dvSo.ApplyModifiedPropertiesWithoutUndo();

            var dcSo = new SerializedObject(combat);
            SetIfExists(dcSo, "dungeonGenerator", dungeonGen);
            SetIfExists(dcSo, "dungeonVisualizer", dungeonViz);
            SetIfExists(dcSo, "spawnDirector", spawnDir);
            SetIfExists(dcSo, "player", cam != null ? cam.transform : xrRoot.transform);
            dcSo.ApplyModifiedPropertiesWithoutUndo();

            var suSo = new SerializedObject(suimono);
            SetIfExists(suSo, "islandWorldForSeaLevel", islandBuilder);
            SetIfExists(suSo, "manualCameraTransform", cam != null ? cam.transform : null);
            SetIfExists(suSo, "sceneDirectionalLight", sun);
            TryAssignSuimonoPrefabs(suSo);
            TryAssignSkyboxMaterial(suSo);
            suSo.ApplyModifiedPropertiesWithoutUndo();

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            var sky = TryFindProceduralSkyboxMaterial();
            if (sky != null)
                RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            Selection.activeGameObject = islandGo;
            Debug.Log(
                "[IslandTestScene] Saved " + scenePath +
                ". Open it, press Play. Dungeon auto-start stays off (IslandWorldBuilder); use combat controller menu to begin a run if needed.");
        }

        static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parts = folder.Split('/');
            if (parts.Length == 0) return;
            var cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        static GameObject FindXrOriginRigPrefab()
        {
            foreach (var guid in AssetDatabase.FindAssets("XR Origin (XR Rig) t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var n = path.Replace('\\', '/');
                if (n.IndexOf("xr.interaction.toolkit", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            foreach (var guid in AssetDatabase.FindAssets("XR Origin (XR Rig) t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return null;
        }

        static GameObject TryLoadFirstPrefabNameContains(string root, string namePart)
        {
            if (!AssetDatabase.IsValidFolder(root.Replace('\\', '/')))
                return null;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var file = Path.GetFileNameWithoutExtension(path);
                if (file.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        static Material TryFindProceduralSkyboxMaterial()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m != null && m.shader != null &&
                    m.shader.name.IndexOf("sky", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return m;
            }
            return null;
        }

        static void TryAssignSuimonoPrefabs(SerializedObject suimonoSo)
        {
            GameObject mod = null, surf = null;
            foreach (var guid in AssetDatabase.FindAssets("SUIMONO_Module t:Prefab"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                mod = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (mod != null) break;
            }
            foreach (var guid in AssetDatabase.FindAssets("SUIMONO_Surface t:Prefab"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                surf = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (surf != null) break;
            }

            SetIfExists(suimonoSo, "suimonoModulePrefab", mod);
            SetIfExists(suimonoSo, "suimonoSurfacePrefab", surf);
        }

        static void TryAssignSkyboxMaterial(SerializedObject suimonoSo)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.IndexOf("sky", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m != null)
                {
                    SetIfExists(suimonoSo, "skyboxMaterialEnsure", m);
                    return;
                }
            }
        }

        static void SetIfExists(SerializedObject so, string propertyPath, Object value)
        {
            var p = so.FindProperty(propertyPath);
            if (p != null)
                p.objectReferenceValue = value;
        }
    }
}
#endif
