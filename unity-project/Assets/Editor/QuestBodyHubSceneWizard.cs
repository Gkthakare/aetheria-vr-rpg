#if UNITY_EDITOR
using System;
using System.IO;
using Aetheria.VR;
using Aetheria.VR.BodyHub;
using Aetheria.Voice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;

namespace IslandProto.EditorTools
{
    /// <summary>
    /// Creates a Quest-focused "body hub" scene: hand-first locomotion, head/voice fly,
    /// XR Hands bridge, humanoid upgrader, optional gaze-dwell avatar pedestals.
    /// Use as the template rig for other scenes (copy prefab or merge additive).
    /// </summary>
    public static class QuestBodyHubSceneWizard
    {
        private const string ScenePath = "Assets/Scenes/QuestBodyHub.unity";

        /// <summary>Ready Player Me GLB (glTFast) — same as <see cref="QuestBodyHubDirector.DefaultCyberpunkMaleGlbAssetPath"/>.</summary>
        public const string CyberpunkMaleGlbAssetPath =
            "Assets/ImportedContent/Characters/full-body-cyberpunk-male/readyplayerme_cyberpunk.glb";

        [MenuItem("Aetheria/Quest/Create Quest Body Hub Scene (hands-first)")]
        public static void CreateScene()
        {
            EnsureFolder(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.06f, 0.1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = new Color(0.02f, 0.04f, 0.09f);

            var sunGo = new GameObject("Directional Light");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.85f, 0.92f, 1f);
            sun.intensity = 0.95f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(38f, -42f, 0f);

            AddAccentLight("Accent_Cyan", new Vector3(-8f, 3.2f, 6f), new Color(0f, 0.85f, 1f), 2.4f, 14f);
            AddAccentLight("Accent_Magenta", new Vector3(10f, 2.8f, -4f), new Color(0.75f, 0.2f, 1f), 1.9f, 12f);

            var xriMgr = new GameObject("XR Interaction Manager");
            xriMgr.AddComponent<XRInteractionManager>();

            var xrPrefab = FindXrOriginRigPrefab();
            if (xrPrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Quest Body Hub",
                    "XR Origin (XR Rig) prefab not found.\n\nInstall XRI Starter Assets sample and retry.",
                    "OK");
                return;
            }

            var xrRoot = (GameObject)PrefabUtility.InstantiatePrefab(xrPrefab);
            xrRoot.name = "XR Origin (XR Rig)";
            var cam = xrRoot.GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.02f, 0.03f, 0.06f);
                cam.nearClipPlane = 0.03f;
                cam.farClipPlane = 5000f;
            }

            var vpc = xrRoot.GetComponent<VRPlayerController>() ?? xrRoot.AddComponent<VRPlayerController>();
            var voice = xrRoot.GetComponent<VoiceIntegrationSystem>() ?? xrRoot.AddComponent<VoiceIntegrationSystem>();

            var hubRoot = new GameObject("QuestBodyHub");
            var director = hubRoot.AddComponent<QuestBodyHubDirector>();
            var gaze = hubRoot.AddComponent<QuestBodyHubGazeSelector>();

            var avatarMount = new GameObject("AvatarMount");
            avatarMount.transform.SetParent(xrRoot.transform, false);
            avatarMount.transform.localPosition = Vector3.zero;
            avatarMount.transform.localRotation = Quaternion.identity;

            CreateHubWalkableFloor(hubRoot.transform);
            CreateHubRingVisualOnly(hubRoot.transform);

            var bodyPrefab = LoadCyberpunkAvatarPrefabOrWarn();
            BuildPedestals(hubRoot.transform, new Vector3(0f, 0.75f, 3.2f), 1.1f, bodyPrefab != null ? 1 : 3);

            var ev = new GameObject("EventSystem");
            ev.AddComponent<EventSystem>();
            ev.AddComponent<InputSystemUIInputModule>();

            AddAutoHumanoidHost(xrRoot.transform);
            AddXrHandsBridge(xrRoot.transform);
            AddQuestHandPoseInputDeviceBridge(xrRoot.transform);

            ConfigureRig(vpc, voice, xrRoot.transform, cam);
            ConfigureHub(director, gaze, avatarMount.transform, cam, bodyPrefab);

            if (bodyPrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Quest Body Hub",
                    "Cyberpunk body GLB was not found at:\n" + CyberpunkMaleGlbAssetPath +
                    "\n\nScene will still be saved — assign the prefab manually on QuestBodyHubDirector, or run:\n" +
                    "Aetheria → Quest → Wire Cyberpunk Body To Quest Body Hub (open scene).",
                    "OK");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Selection.activeGameObject = xrRoot;

            EditorUtility.DisplayDialog(
                "Quest Body Hub",
                "Saved:\n" + ScenePath + "\n\n" +
                "Body: Ready Player Me cyberpunk male GLB is assigned on QuestBodyHubDirector (slot 0).\n" +
                "Gaze at the pedestal ~1s to re-confirm / dwell (single-avatar hub).\n" +
                "Build Android → Quest; enable Hand Tracking in OpenXR.\n\n" +
                "Reuse: duplicate XR Origin subtree as a prefab, or additive-load this scene.",
                "OK");
            Debug.Log("[QuestBodyHubSceneWizard] Created " + ScenePath);
        }

        [MenuItem("Aetheria/Quest/Wire Cyberpunk Body To Quest Body Hub (open scene)")]
        public static void WireCyberpunkBodyToOpenScene()
        {
            var body = LoadCyberpunkAvatarPrefabOrWarn(true);
            if (body == null)
                return;

            var directors = UnityEngine.Object.FindObjectsOfType<QuestBodyHubDirector>();
            if (directors == null || directors.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Wire Cyberpunk Body",
                    "No QuestBodyHubDirector in the open scene.\n\n" +
                    "Open Assets/Scenes/QuestBodyHub.unity or run:\n" +
                    "Aetheria → Quest → Create Quest Body Hub Scene (hands-first).",
                    "OK");
                return;
            }

            foreach (var d in directors)
            {
                var so = new SerializedObject(d);
                SetAvatarPrefabsArray(so, body);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(d);
            }

            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("[QuestBodyHubSceneWizard] Wired cyberpunk GLB to QuestBodyHubDirector(s). Save the scene (Ctrl+S).");
            EditorUtility.DisplayDialog(
                "Wire Cyberpunk Body",
                "Assigned readyplayerme_cyberpunk.glb as avatar slot 0 on QuestBodyHubDirector.\n\nSave the scene (Ctrl+S).",
                "OK");
        }

        private static GameObject LoadCyberpunkAvatarPrefabOrWarn(bool dialogOnMissing = false)
        {
            var body = AssetDatabase.LoadAssetAtPath<GameObject>(CyberpunkMaleGlbAssetPath);
            if (body == null)
            {
                var msg =
                    "Could not load GameObject at:\n" + CyberpunkMaleGlbAssetPath +
                    "\n\nReimport the GLB or check the path.";
                Debug.LogWarning("[QuestBodyHubSceneWizard] " + msg);
                if (dialogOnMissing)
                    EditorUtility.DisplayDialog("Quest Body Hub", msg, "OK");
            }

            return body;
        }

        private static void SetAvatarPrefabsArray(SerializedObject directorSo, GameObject bodyPrefab)
        {
            var prop = directorSo.FindProperty("avatarPrefabs");
            if (prop == null || !prop.isArray)
                return;
            prop.ClearArray();
            prop.InsertArrayElementAtIndex(0);
            prop.GetArrayElementAtIndex(0).objectReferenceValue = bodyPrefab;
        }

        private static void ConfigureHub(
            QuestBodyHubDirector director,
            QuestBodyHubGazeSelector gaze,
            Transform avatarMount,
            Camera cam,
            GameObject cyberpunkBodyPrefab)
        {
            var dSo = new SerializedObject(director);
            SetObj(dSo, "avatarMount", avatarMount);
            if (cyberpunkBodyPrefab != null)
                SetAvatarPrefabsArray(dSo, cyberpunkBodyPrefab);
            dSo.ApplyModifiedPropertiesWithoutUndo();

            var gSo = new SerializedObject(gaze);
            SetObj(gSo, "director", director);
            SetObj(gSo, "gazeCamera", cam);
            gSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureRig(
            VRPlayerController vpc,
            VoiceIntegrationSystem voice,
            Transform xrRoot,
            Camera cam)
        {
            var vpcSo = new SerializedObject(vpc);
            SetObj(vpcSo, "xrOrigin", xrRoot);
            SetObj(vpcSo, "xrCamera", cam);
            SetBool(vpcSo, "preferHandTrackingOnly", true);
            SetBool(vpcSo, "allowControllersAlongsideHandTracking", false);
            SetBool(vpcSo, "enableHandTrackingLocomotion", true);
            SetBool(vpcSo, "enableHandPinchTeleport", false);
            SetBool(vpcSo, "lockWorldMovementForBodySyncOnly", true);
            SetBool(vpcSo, "enableSmoothLocomotion", true);
            SetBool(vpcSo, "enableSnapTurn", true);
            SetBool(vpcSo, "enableHeadDirectedFly", true);
            SetBool(vpcSo, "allowVoiceLatchedFlight", true);
            SetBool(vpcSo, "driveAvatarFootIkFromHeadFlyState", true);
            SetBool(vpcSo, "snapToGroundOnStart", true);
            SetBool(vpcSo, "showRuntimeStatusHud", true);
            // Thin cylinders overlapped → CharacterController got stuck "between" colliders; flat slab + low fallback fixes stand height.
            SetFloat(vpcSo, "spawnSafetyMinY", 0.02f);
            SetFloat(vpcSo, "groundOffsetY", 0.02f);
            vpcSo.ApplyModifiedPropertiesWithoutUndo();

            var voiceSo = new SerializedObject(voice);
            SetBool(voiceSo, "enableVoiceCommands", true);
            SetFloat(voiceSo, "commandCooldown", 0.45f);
            voiceSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <param name="pedestalCount">Gaze pedestals; use 1 when only one avatar slot exists.</param>
        private static void BuildPedestals(Transform parent, Vector3 center, float spacing, int pedestalCount)
        {
            int n = Mathf.Clamp(pedestalCount, 1, 8);
            for (int i = 0; i < n; i++)
            {
                float x = n <= 1 ? 0f : (i - (n - 1) * 0.5f) * spacing;
                var ped = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ped.name = "AvatarPedestal_" + i;
                ped.transform.SetParent(parent, true);
                ped.transform.position = center + new Vector3(x, 0f, 0f);
                ped.transform.localScale = new Vector3(0.55f, 0.65f, 0.55f);
                var col = ped.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;

                var gp = ped.AddComponent<QuestBodyHubGazePedestal>();
                var so = new SerializedObject(gp);
                var p = so.FindProperty("avatarSlotIndex");
                if (p != null) p.intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();

                ApplyPedestalMaterial(ped.GetComponent<Renderer>(), i);
            }
        }

        [MenuItem("Aetheria/Quest/Fix Body Hub Floor (open scene)")]
        public static void FixBodyHubFloorInOpenScene()
        {
            var hub = GameObject.Find("QuestBodyHub");
            if (hub == null)
            {
                EditorUtility.DisplayDialog(
                    "Fix Body Hub Floor",
                    "No GameObject named 'QuestBodyHub' in the hierarchy.\n\nOpen QuestBodyHub.unity or create the scene from the Quest menu.",
                    "OK");
                return;
            }

            foreach (var name in new[] { "HubFloor_Disc", "HubFloor_RingGlow" })
            {
                var t = hub.transform.Find(name);
                if (t != null)
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
            }

            CreateHubWalkableFloor(hub.transform);
            CreateHubRingVisualOnly(hub.transform);

            var xr = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin");
            if (xr != null && xr.TryGetComponent<VRPlayerController>(out var vpc))
            {
                var so = new SerializedObject(vpc);
                SetBool(so, "preferHandTrackingOnly", true);
                SetBool(so, "allowControllersAlongsideHandTracking", false);
                SetBool(so, "enableHandTrackingLocomotion", true);
                SetBool(so, "enableHandPinchTeleport", false);
                SetBool(so, "lockWorldMovementForBodySyncOnly", true);
                SetFloat(so, "spawnSafetyMinY", 0.02f);
                SetFloat(so, "groundOffsetY", 0.02f);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(vpc);
            }

            ReparentAutoHumanoidHostUnderXrRig(xr);
            if (xr != null)
                AddQuestHandPoseInputDeviceBridge(xr.transform);

            EditorSceneManager.MarkAllScenesDirty();
            EditorUtility.DisplayDialog(
                "Fix Body Hub Floor",
                "Replaced hub floor with a single flat walk collider and a visual-only ring.\n" +
                "Tuned VRPlayerController spawnSafetyMinY / groundOffsetY on the XR rig.\n" +
                "Reparented AutoHumanoidUpgraderHost under the XR rig when possible.\n\nSave the scene (Ctrl+S).",
                "OK");
        }

        [MenuItem("Aetheria/Quest/Repair Quest Body Hub Scene References")]
        public static void RepairQuestBodyHubSceneReferences()
        {
            var xr = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin");
            Transform avatarMountTf = FindAvatarMountTransform(xr);

            var directors = UnityEngine.Object.FindObjectsOfType<QuestBodyHubDirector>();
            if (directors == null || directors.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Repair Body Hub",
                    "No QuestBodyHubDirector in the scene.\n\nOpen QuestBodyHub.unity or add the component.",
                    "OK");
                return;
            }

            foreach (var d in directors)
            {
                var so = new SerializedObject(d);
                if (avatarMountTf != null)
                    SetObj(so, "avatarMount", avatarMountTf);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(d);
            }

            var gaze = UnityEngine.Object.FindObjectOfType<QuestBodyHubGazeSelector>();
            if (gaze != null)
            {
                var gso = new SerializedObject(gaze);
                SetObj(gso, "director", directors[0]);
                var cam = Camera.main;
                if (cam == null && xr != null)
                    cam = xr.GetComponentInChildren<Camera>(true);
                if (cam != null)
                    SetObj(gso, "gazeCamera", cam);
                gso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(gaze);
            }

            ReparentAutoHumanoidHostUnderXrRig(xr);
            if (xr != null)
                AddQuestHandPoseInputDeviceBridge(xr.transform);

            EditorSceneManager.MarkAllScenesDirty();
            EditorUtility.DisplayDialog(
                "Repair Body Hub",
                "Set QuestBodyHubDirector → Avatar Mount to XR rig's AvatarMount (if found).\n" +
                "Refreshed QuestBodyHubGazeSelector → Director / Gaze Camera.\n" +
                "Reparented AutoHumanoidUpgraderHost under XR rig when possible.\n\nSave the scene (Ctrl+S).",
                "OK");
        }

        private static Transform FindAvatarMountTransform(GameObject xr)
        {
            if (xr == null)
                return null;
            var direct = xr.transform.Find("AvatarMount");
            if (direct != null)
                return direct;
            foreach (var t in xr.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "AvatarMount")
                    return t;
            }

            return null;
        }

        private static void ReparentAutoHumanoidHostUnderXrRig(GameObject xr)
        {
            if (xr == null)
                xr = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin");
            var host = GameObject.Find("AutoHumanoidUpgraderHost");
            if (xr == null || host == null || host.transform.parent == xr.transform)
                return;
            Undo.SetTransformParent(host.transform, xr.transform, "Parent AutoHumanoidUpgrader under XR Origin");
            host.transform.localPosition = Vector3.zero;
            host.transform.localRotation = Quaternion.identity;
        }

        /// <summary>One box collider — avoids overlapping cylinder shells that trap CharacterController.</summary>
        private static void CreateHubWalkableFloor(Transform hubRoot)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "HubFloor_Disc";
            floor.layer = 0;
            floor.transform.SetParent(hubRoot, false);
            floor.transform.localPosition = new Vector3(0f, 0.025f, 0f);
            floor.transform.localScale = new Vector3(44f, 0.05f, 44f);
            floor.transform.localRotation = Quaternion.identity;
            ApplyHubFloorMaterial(floor.GetComponent<Renderer>());
        }

        private static void CreateHubRingVisualOnly(Transform hubRoot)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "HubFloor_RingGlow";
            ring.transform.SetParent(hubRoot, false);
            ring.transform.localPosition = new Vector3(0f, 0.052f, 0f);
            ring.transform.localScale = new Vector3(20f, 0.006f, 20f);
            ring.transform.localRotation = Quaternion.identity;
            var col = ring.GetComponent<Collider>();
            if (col != null)
                UnityEngine.Object.DestroyImmediate(col);
            ApplyRingEmissive(ring.GetComponent<Renderer>());
        }

        private static void AddAccentLight(string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            go.transform.position = pos;
        }

        private static void AddAutoHumanoidHost(Transform xrRigParent)
        {
            const string hostName = "AutoHumanoidUpgraderHost";
            var t = ResolveType("Aetheria.VR.AutoHumanoidUpgrader");
            if (t == null)
                return;

            var host = GameObject.Find(hostName);
            if (host == null)
            {
                host = new GameObject(hostName);
                host.AddComponent(t);
            }
            else if (host.GetComponent(t) == null)
            {
                host.AddComponent(t);
            }

            if (xrRigParent != null)
            {
                host.transform.SetParent(xrRigParent, false);
                host.transform.localPosition = Vector3.zero;
                host.transform.localRotation = Quaternion.identity;
            }
        }

        private static void AddXrHandsBridge(Transform xrOrigin)
        {
            const string hostName = "XRHandsRuntimeBridge_Host";
            var existing = GameObject.Find(hostName);
            var bridgeType = Type.GetType("Aetheria.VR.XRHandsRuntimeBridge, Aetheria.VR.XRHands");
            if (bridgeType == null)
                return;

            GameObject host;
            if (existing != null)
            {
                host = existing;
            }
            else
            {
                host = new GameObject(hostName);
                host.AddComponent(bridgeType);
            }

            if (xrOrigin != null)
                host.transform.SetParent(xrOrigin, false);
        }

        private static void AddQuestHandPoseInputDeviceBridge(Transform xrOrigin)
        {
            const string hostName = "QuestHandPoseInputDeviceBridge_Host";
            var existing = GameObject.Find(hostName);
            var bridgeType = ResolveType("Aetheria.VR.QuestHandPoseInputDeviceBridge");
            if (bridgeType == null)
                return;

            GameObject host;
            if (existing != null)
            {
                host = existing;
            }
            else
            {
                host = new GameObject(hostName);
                host.AddComponent(bridgeType);
            }

            if (xrOrigin != null)
            {
                host.transform.SetParent(xrOrigin, false);
                host.transform.localPosition = Vector3.zero;
                host.transform.localRotation = Quaternion.identity;
            }
        }

        private static Type ResolveType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        private static void ApplyHubFloorMaterial(Renderer rend)
        {
            if (rend == null) return;
            rend.sharedMaterial = CreateLitHubMaterial(new Color(0.06f, 0.08f, 0.12f), new Color(0.02f, 0.05f, 0.08f), 0.35f);
        }

        private static void ApplyRingEmissive(Renderer rend)
        {
            if (rend == null) return;
            rend.sharedMaterial = CreateLitHubMaterial(new Color(0.02f, 0.04f, 0.06f), new Color(0f, 0.9f, 1f) * 0.55f, 0.2f);
        }

        private static void ApplyPedestalMaterial(Renderer rend, int slot)
        {
            if (rend == null) return;
            Color em = slot switch
            {
                0 => new Color(0f, 0.85f, 1f),
                1 => new Color(0.75f, 0.25f, 1f),
                _ => new Color(0.3f, 1f, 0.5f)
            };
            rend.sharedMaterial = CreateLitHubMaterial(new Color(0.08f, 0.08f, 0.1f), em * 0.45f, 0.55f);
        }

        private static Material CreateLitHubMaterial(Color baseColor, Color emissionHdr, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", baseColor);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0.35f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", emissionHdr);
            }
            return mat;
        }

        private static GameObject FindXrOriginRigPrefab()
        {
            foreach (var guid in AssetDatabase.FindAssets("XR Origin (XR Rig) t:Prefab"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var o = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (o != null) return o;
            }
            return null;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parts = folder.Split('/');
            var cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        private static void SetObj(SerializedObject so, string path, UnityEngine.Object value)
        {
            var p = so.FindProperty(path);
            if (p != null && value != null) p.objectReferenceValue = value;
        }

        private static void SetBool(SerializedObject so, string path, bool value)
        {
            var p = so.FindProperty(path);
            if (p != null) p.boolValue = value;
        }

        private static void SetFloat(SerializedObject so, string path, float value)
        {
            var p = so.FindProperty(path);
            if (p != null) p.floatValue = value;
        }
    }
}
#endif
