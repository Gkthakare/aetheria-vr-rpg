using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Aetheria.EditorTools
{
    /// <summary>
    /// Aetheria -> VR -> Add XR Hands Runtime Bridge
    ///
    /// Parents the bridge under XR Origin whenever possible so
    /// <see cref="VRPlayerController"/> can find "Hand Tracking Left/Right" via
    /// <see cref="Aetheria.VR.XRHandAttachmentUtil"/>.
    /// </summary>
    public static class AddXRHandsBridgeTool
    {
        private const string MenuPath = "Aetheria/VR/Add XR Hands Runtime Bridge";
        private const string HostName = "XRHandsRuntimeBridge_Host";
        private const string BridgeTypeName = "Aetheria.VR.XRHandsRuntimeBridge, Aetheria.VR.XRHands";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            var bridgeType = Type.GetType(BridgeTypeName);
            if (bridgeType == null)
            {
                EditorUtility.DisplayDialog(
                    "XR Hands Bridge",
                    "Assembly 'Aetheria.VR.XRHands' or type XRHandsRuntimeBridge was not found.\n\n" +
                    "1. Wait for Unity to finish importing packages (XR Hands / Core Utils).\n" +
                    "2. Check the Console for compile errors.\n\n" +
                    "3. In Package Manager, verify 'XR Hands' is installed.",
                    "OK");
                return;
            }

            var origin = FindXrOriginTransform();
            if (origin == null)
                Debug.LogWarning(
                    "[AddXRHandsBridgeTool] No GameObject with XROrigin component and no 'XR Origin' by name. " +
                    "Create the bridge at scene root; at runtime it will try to reparent itself.");

            var existing = GameObject.Find(HostName);
            if (existing != null)
            {
                ReparentBridgeUnderOrigin(existing.transform, origin);
                EditorSceneManager.MarkAllScenesDirty();
                Selection.activeGameObject = existing;
                EditorUtility.DisplayDialog(
                    "XR Hands Bridge",
                    "Bridge already exists. It has been reparented under the XR Origin if one was found.\n\n" +
                    "Save the scene (Ctrl+S), then Build And Run.",
                    "OK");
                return;
            }

            var host = new GameObject(HostName);
            host.AddComponent(bridgeType);
            ReparentBridgeUnderOrigin(host.transform, origin);
            Undo.RegisterCreatedObjectUndo(host, "Add XR Hands Runtime Bridge");
            EditorSceneManager.MarkAllScenesDirty();
            Selection.activeGameObject = host;

            EditorUtility.DisplayDialog(
                "XR Hands Bridge",
                "Added XRHandsRuntimeBridge under your XR Rig.\n\n" +
                "Confirm in Project Settings:\n" +
                "XR Plug-in Management -> OpenXR -> Android -> enable Hand Tracking Subsystem.\n\n" +
                "Save (Ctrl+S), then Build And Run. Raise your hands — blue/green joint spheres " +
                "should appear; L/R Anchor in the HUD should show OK.",
                "OK");
        }

        private static Transform FindXrOriginTransform()
        {
            var xrOriginType = Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            if (xrOriginType != null)
            {
                var found = UnityEngine.Object.FindObjectOfType(xrOriginType) as Component;
                if (found != null)
                    return found.transform;
            }

            var byName = GameObject.Find("XR Origin");
            if (byName != null)
                return byName.transform;

            return null;
        }

        private static void ReparentBridgeUnderOrigin(Transform bridge, Transform origin)
        {
            if (origin == null || bridge == null || bridge.parent == origin)
                return;
            Undo.SetTransformParent(bridge, origin, "Parent XR Hands Runtime Bridge under XR Origin");
        }
    }
}
