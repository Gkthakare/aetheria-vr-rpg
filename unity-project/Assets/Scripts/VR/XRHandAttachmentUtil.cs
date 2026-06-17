using UnityEngine;

namespace Aetheria.VR
{
    /// <summary>
    /// Resolves XR rig hand transforms and index fingertip bones when present (Meta / OpenXR hand meshes).
    /// </summary>
    public static class XRHandAttachmentUtil
    {
        static readonly string[] IndexTipHints =
        {
            "Index Tip", "Index Finger Tip", "IndexTip", "index_tip",
            "RightHand Index Tip", "LeftHand Index Tip", "Hand Index Tip",
            "PointerTip"
        };
        static readonly string[] ThumbTipHints =
        {
            "Thumb Tip", "ThumbTip", "thumb_tip", "RightHand Thumb Tip", "LeftHand Thumb Tip"
        };

        public static Transform FindHandController(Transform xrRigRoot, bool rightHand)
        {
            var exact = rightHand
                ? new[]
                {
                    "Right Controller", "RightHand Controller", "Right Hand", "RightHand", "Hand Tracking Right", "R Hand",
                    "RightHandAnchor", "Right Hand Anchor", "RightHand Anchor", "Right Controller (OpenXR)"
                }
                : new[]
                {
                    "Left Controller", "LeftHand Controller", "Left Hand", "LeftHand", "Hand Tracking Left", "L Hand",
                    "LeftHandAnchor", "Left Hand Anchor", "LeftHand Anchor", "Left Controller (OpenXR)"
                };

            foreach (var name in exact)
            {
                var t = FindChildTransformByName(xrRigRoot, name);
                if (t != null)
                    return t;
            }

            // Broad fallback by partial name matching for runtime hand-tracking rigs.
            foreach (var t in xrRigRoot.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name;
                bool hasHand = n.IndexOf("hand", System.StringComparison.OrdinalIgnoreCase) >= 0;
                bool hasSide = rightHand
                    ? n.IndexOf("right", System.StringComparison.OrdinalIgnoreCase) >= 0 || n.StartsWith("R ")
                    : n.IndexOf("left", System.StringComparison.OrdinalIgnoreCase) >= 0 || n.StartsWith("L ");
                if (hasHand && hasSide)
                    return t;
            }

            return null;
        }

        public static Transform FindIndexTipOrController(Transform handController)
        {
            if (handController == null)
                return null;

            foreach (var hint in IndexTipHints)
            {
                var deep = FindDescendantByNameContains(handController, hint);
                if (deep != null && deep != handController)
                    return deep;
            }

            foreach (var t in handController.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name;
                if (n.IndexOf("index", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && n.IndexOf("tip", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            }

            return handController;
        }

        public static Transform FindThumbTipOrController(Transform handController)
        {
            if (handController == null)
                return null;

            foreach (var hint in ThumbTipHints)
            {
                var deep = FindDescendantByNameContains(handController, hint);
                if (deep != null && deep != handController)
                    return deep;
            }

            foreach (var t in handController.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name;
                if (n.IndexOf("thumb", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && n.IndexOf("tip", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            }

            return handController;
        }

        static Transform FindChildTransformByName(Transform root, string exactName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == exactName)
                    return t;
            }

            return null;
        }

        static Transform FindDescendantByNameContains(Transform root, string substring)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.IndexOf(substring, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            }

            return null;
        }
    }
}
