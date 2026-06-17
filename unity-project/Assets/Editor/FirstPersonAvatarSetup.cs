using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Sets up first-person body presence:
//   1) Adds a "AvatarLocal" layer (if missing) and assigns the avatar's head
//      and hair sub-meshes to it.
//   2) Removes "AvatarLocal" from the Main Camera culling mask, so YOU never
//      see your own head from the inside (no clipping into the skull).
//
// Run this AFTER you have converted readyplayerme_cyberpunk to Humanoid rig
// and the RuntimePlayerAvatar is spawned in the scene (Play once or just
// rely on the Editor instance under XR Origin).
//
// Use:
//   Top menu -> Aetheria -> Avatar -> Set Up First Person Body (Hide Own Head).
public static class FirstPersonAvatarSetup
{
    private const string MenuPath = "Aetheria/Avatar/Set Up First Person Body (Hide Own Head)";
    private const string LayerName = "AvatarLocal";

    private static readonly string[] HeadHints =
    {
        "Wolf3D_Head", "Wolf3D_Hair", "Wolf3D_Beard", "Wolf3D_Glasses",
        "Wolf3D_Headwear", "EyeLeft", "EyeRight", "Wolf3D_Teeth"
    };

    [MenuItem(MenuPath)]
    public static void Run()
    {
        var report = new List<string>();

        int layerIndex = EnsureLayer(LayerName, report);
        if (layerIndex < 0)
        {
            EditorUtility.DisplayDialog("First Person Avatar Setup",
                "Could not create or find the 'AvatarLocal' layer (layers full?).\n" +
                "Open Project Settings -> Tags and Layers and free a User Layer.",
                "OK");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("First Person Avatar Setup", "No active scene.", "OK");
            return;
        }

        int taggedCount = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == null) continue;
            taggedCount += AssignHeadHairLayer(root, layerIndex, report);
        }

        int cameraTouched = StripLayerFromMainCamera(layerIndex, report);

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("First Person Avatar Setup",
            $"Layer 'AvatarLocal' index = {layerIndex}\n" +
            $"Head/hair meshes moved to AvatarLocal: {taggedCount}\n" +
            $"Main Camera(s) updated: {cameraTouched}\n\n" +
            "Now press Play. Your own head/hair will be invisible from your POV,\n" +
            "but visible to others (mirrors, screenshots from other cameras).",
            "OK");

        foreach (var line in report) Debug.Log("[FirstPersonAvatarSetup] " + line);
    }

    private static int AssignHeadHairLayer(GameObject root, int layerIndex, List<string> report)
    {
        int count = 0;
        var transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (var t in transforms)
        {
            if (t == null) continue;
            string n = t.name;
            bool match = false;
            for (int i = 0; i < HeadHints.Length; i++)
            {
                if (n.IndexOf(HeadHints[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    match = true;
                    break;
                }
            }
            if (!match) continue;
            if (t.gameObject.layer != layerIndex)
            {
                Undo.RecordObject(t.gameObject, "Set FP layer");
                t.gameObject.layer = layerIndex;
                count++;
                report.Add($"Tagged '{t.name}' as AvatarLocal.");
            }
        }
        return count;
    }

    private static int StripLayerFromMainCamera(int layerIndex, List<string> report)
    {
        int touched = 0;
        var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int mask = ~(1 << layerIndex);
        foreach (var c in cams)
        {
            if (c == null) continue;
            if (c.tag != "MainCamera") continue;
            int newMask = c.cullingMask & mask;
            if (newMask != c.cullingMask)
            {
                Undo.RecordObject(c, "Remove AvatarLocal from Main Camera mask");
                c.cullingMask = newMask;
                touched++;
                report.Add($"Removed 'AvatarLocal' from Camera '{c.name}' culling mask.");
            }
        }
        return touched;
    }

    private static int EnsureLayer(string layerName, List<string> report)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0) return existing;

        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (asset == null || asset.Length == 0) return -1;
        var tagManager = new SerializedObject(asset[0]);
        var layers = tagManager.FindProperty("layers");
        if (layers == null) return -1;

        for (int i = 8; i < layers.arraySize; i++)
        {
            var p = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(p.stringValue))
            {
                p.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                report.Add($"Created layer '{layerName}' at index {i}.");
                return i;
            }
        }
        return -1;
    }
}
