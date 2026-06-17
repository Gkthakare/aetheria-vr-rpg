using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
// Adds (or finds) the AutoHumanoidUpgrader component in the active scene so
// glTFast-imported Ready Player Me avatars are automatically promoted from
// Generic to Humanoid at runtime, then VRFullBodyIkController is attached.
//
// Use:
//   Top menu -> Aetheria -> Avatar -> Add Humanoid Upgrader To Scene.
public static class AddAutoHumanoidUpgraderTool
{
    private const string MenuPath = "Aetheria/Avatar/Add Humanoid Upgrader To Scene";
    private const string HostName = "AutoHumanoidUpgraderHost";
    private const string FullTypeName = "Aetheria.VR.AutoHumanoidUpgrader";

    [MenuItem(MenuPath)]
    public static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog(
                "Auto Humanoid Upgrader",
                "No active scene is open. Open CleanVRPreview.unity first.",
                "OK");
            return;
        }

        var type = ResolveType(FullTypeName);
        if (type == null)
        {
            EditorUtility.DisplayDialog(
                "Auto Humanoid Upgrader",
                "Aetheria.VR.AutoHumanoidUpgrader type not found.\n" +
                "Make sure Assets/Scripts/VR/AutoHumanoidUpgrader.cs is in the project and has compiled.",
                "OK");
            return;
        }

        if (UnityEngine.Object.FindAnyObjectByType(type) is Component existing && existing != null)
        {
            var origin = FindXrRigTransform();
            var reparented = false;
            if (origin != null && existing.transform.parent != origin)
            {
                Undo.SetTransformParent(existing.transform, origin, "Parent AutoHumanoidUpgrader under XR Origin");
                existing.transform.localPosition = Vector3.zero;
                existing.transform.localRotation = Quaternion.identity;
                reparented = true;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorUtility.SetDirty(existing.gameObject);
            }

            string detail;
            if (origin == null)
                detail = "No XR Origin was found in the scene — drag AutoHumanoidUpgraderHost under your XR rig manually.";
            else if (reparented)
                detail = "It was reparented under the XR rig. Save the scene (Ctrl+S).";
            else
                detail = "It is already parented under the XR rig.";

            EditorUtility.DisplayDialog(
                "Auto Humanoid Upgrader",
                $"Already present on '{existing.gameObject.name}'.\n\n{detail}",
                "OK");
            EditorGUIUtility.PingObject(existing.gameObject);
            return;
        }

        var host = GameObject.Find(HostName);
        if (host == null)
        {
            host = new GameObject(HostName);
            Undo.RegisterCreatedObjectUndo(host, "Create AutoHumanoidUpgrader Host");
        }

        if (host.GetComponent(type) == null)
        {
            Undo.AddComponent(host, type);
        }

        var rig = FindXrRigTransform();
        if (rig != null)
        {
            Undo.SetTransformParent(host.transform, rig, "Parent AutoHumanoidUpgrader under XR Origin");
            host.transform.localPosition = Vector3.zero;
            host.transform.localRotation = Quaternion.identity;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        EditorGUIUtility.PingObject(host);
        Selection.activeGameObject = host;

        EditorUtility.DisplayDialog(
            "Auto Humanoid Upgrader",
            "Added 'AutoHumanoidUpgrader' to a new GameObject called\n'" + HostName + "' in the open scene.\n\n" +
            "Save the scene (Ctrl+S) and press Play.\n\n" +
            "On Play, watch the Console for:\n" +
            "  • [AutoHumanoidUpgrader] Built humanoid Avatar ...\n" +
            "  • [AutoHumanoidUpgrader] Added VRFullBodyIkController ...",
            "OK");
        Debug.Log("[AddAutoHumanoidUpgraderTool] Installed AutoHumanoidUpgrader on '" + host.name + "'.");
    }

    private static Transform FindXrRigTransform()
    {
        var xrOriginType = Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
        if (xrOriginType != null)
        {
            var found = UnityEngine.Object.FindObjectOfType(xrOriginType) as Component;
            if (found != null)
                return found.transform;
        }

        var byName = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin");
        return byName != null ? byName.transform : null;
    }

    private static System.Type ResolveType(string fullName)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName, throwOnError: false);
            if (t != null) return t;
        }
        return null;
    }
}
