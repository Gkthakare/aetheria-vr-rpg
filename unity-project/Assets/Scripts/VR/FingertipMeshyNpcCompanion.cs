using UnityEngine;

namespace Aetheria.VR
{
    /// <summary>
    /// Left index: a stylized <b>mini-you</b> NPC (always, no Meshy required). Right index: optional Meshy prefab so you can hold a Meshy character on the casting hand.
    /// Elemental magic (F5–F9 / voice) uses index tips when your rig exposes hand bones.
    /// </summary>
    public sealed class FingertipMeshyNpcCompanion : MonoBehaviour
    {
        [Header("Mini-you (left hand)")]
        [Tooltip("Small humanoid on your left index tip so you clearly see 'yourself' as an NPC in VR.")]
        [SerializeField] bool spawnStylizedMiniSelfOnLeftHand = true;

        [Tooltip("On a real index tip this reads as a tiny figure (~5cm). Increase if you want it obvious in Scene view while testing.")]
        [SerializeField] float miniSelfUniformScale = 0.048f;
        [SerializeField] Vector3 miniSelfLocalEuler = new Vector3(0f, 180f, 0f);

        [Header("No XR controllers (editor / desktop)")]
        [Tooltip("When no Left/Right Controller transform exists under this rig, parent companions to Main Camera so they stay visible in Game view.")]
        [SerializeField] bool attachToCameraWhenHandMissing = true;
        [SerializeField] Vector3 miniSelfCameraLocalPos = new Vector3(0.28f, -0.22f, 0.52f);
        [SerializeField] Vector3 miniSelfCameraLocalEuler = new Vector3(12f, -168f, 0f);
        [SerializeField] float miniSelfCameraUniformScale = 0.11f;
        [SerializeField] Vector3 meshyCameraLocalPos = new Vector3(-0.28f, -0.22f, 0.52f);
        [SerializeField] Vector3 meshyCameraLocalEuler = new Vector3(12f, 168f, 0f);
        [SerializeField] float meshyCameraUniformScale = 0.09f;

        [Header("Meshy (right index, optional)")]
        [Tooltip("Meshy FBX prefab — appears on right index tip when assigned.")]
        [SerializeField] GameObject meshyCharacterPrefab;

        [SerializeField] float meshyUniformScale = 0.032f;
        [SerializeField] Vector3 meshyLocalEuler = new Vector3(0f, 180f, 0f);

        [Header("Fallback offsets (no hand bones)")]
        [SerializeField] Vector3 controllerTipLocalPos = new Vector3(0.02f, 0f, 0.1f);

        [Header("Mini-you on ground (while you fly)")]
        [Tooltip("Keeps MiniYou on the terrain under your view instead of on the hand/camera, so it stays grounded when you use head fly. Off by default: bad hits (ocean plane, huge colliders) could move the figure out of sight.")]
        [SerializeField] bool keepMiniSelfGroundedUnderView;
        [SerializeField] LayerMask groundPinLayers = ~0;
        [SerializeField] float groundPinRayStartAboveCamera = 4f;
        [SerializeField] float groundPinRayLength = 400f;
        [Tooltip("Ignore ground hits this far from the XR rig on XZ (stops snapping to distant water/void).")]
        [SerializeField] float groundPinMaxHorizontalFromRig = 90f;
        [Tooltip("Require hit surface to face mostly up (filters vertical walls / triggers).")]
        [SerializeField] float groundPinMinHitNormalY = 0.18f;
        [SerializeField] float miniSelfFeetYOffsetFromHit = 0.02f;

        [Header("Debug")]
        [SerializeField] bool spawnOnStart = true;
        [SerializeField] bool logToConsole = true;

        GameObject _miniSelf;
        GameObject _meshyInstance;

        void Start()
        {
            if (spawnOnStart)
                Respawn();
        }

        [ContextMenu("Respawn fingertip avatars")]
        public void Respawn()
        {
            if (_miniSelf != null)
                Destroy(_miniSelf);
            if (_meshyInstance != null)
                Destroy(_meshyInstance);

            if (spawnStylizedMiniSelfOnLeftHand)
                SpawnMiniSelfOnLeft();

            if (meshyCharacterPrefab != null)
                SpawnMeshyOnRight();

            if (keepMiniSelfGroundedUnderView && _miniSelf != null)
                PinMiniSelfToGroundUnderCamera();

            if (logToConsole)
            {
                if (_miniSelf != null || _meshyInstance != null)
                    Debug.Log(
                        "[FingertipNpc] Spawned mini-you and/or Meshy NPC. " +
                        "They are **very small** on hands (by design). In Hierarchy search **MiniYou_NPC** or **MeshyNpc_RightIndex** and press **F** to frame; " +
                        "or raise **Mini Self Uniform Scale** on this component for tests. With no controllers, companions use **Main Camera** if enabled.");
                else if (spawnStylizedMiniSelfOnLeftHand || meshyCharacterPrefab != null)
                    Debug.LogWarning("[FingertipNpc] Nothing spawned (no hand transforms and no camera fallback — enable Attach To Camera When Hand Missing or add XR controllers).");
            }
        }

        void SpawnMiniSelfOnLeft()
        {
            var hand = XRHandAttachmentUtil.FindHandController(transform, false);
            if (hand == null)
            {
                if (attachToCameraWhenHandMissing && Camera.main != null)
                {
                    var cam = Camera.main.transform;
                    _miniSelf = BuildMiniSelfAvatar(cam);
                    _miniSelf.name = "MiniYou_NPC";
                    _miniSelf.transform.localPosition = miniSelfCameraLocalPos;
                    _miniSelf.transform.localRotation = Quaternion.Euler(miniSelfCameraLocalEuler);
                    _miniSelf.transform.localScale = Vector3.one * miniSelfCameraUniformScale;
                    return;
                }

                Debug.LogWarning("[FingertipNpc] No Left Controller — cannot attach mini-you.");
                return;
            }

            var tip = XRHandAttachmentUtil.FindIndexTipOrController(hand);
            var anchor = tip != null ? tip : hand;
            var useOffset = tip == hand;

            _miniSelf = BuildMiniSelfAvatar(anchor);
            _miniSelf.name = "MiniYou_NPC";
            if (useOffset)
            {
                _miniSelf.transform.localPosition = controllerTipLocalPos;
                _miniSelf.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            else
            {
                _miniSelf.transform.localPosition = Vector3.zero;
                _miniSelf.transform.localRotation = Quaternion.Euler(miniSelfLocalEuler);
            }

            _miniSelf.transform.localScale = Vector3.one * miniSelfUniformScale;
        }

        void SpawnMeshyOnRight()
        {
            var hand = XRHandAttachmentUtil.FindHandController(transform, true);
            if (hand == null)
            {
                if (attachToCameraWhenHandMissing && Camera.main != null)
                {
                    var cam = Camera.main.transform;
                    _meshyInstance = Instantiate(meshyCharacterPrefab, cam, false);
                    _meshyInstance.name = "MeshyNpc_RightIndex";
                    _meshyInstance.transform.localPosition = meshyCameraLocalPos;
                    _meshyInstance.transform.localRotation = Quaternion.Euler(meshyCameraLocalEuler);
                    _meshyInstance.transform.localScale = Vector3.one * meshyCameraUniformScale;
                    return;
                }

                Debug.LogWarning("[FingertipNpc] No Right Controller — cannot attach Meshy prefab.");
                return;
            }

            var tip = XRHandAttachmentUtil.FindIndexTipOrController(hand);
            var anchor = tip != null ? tip : hand;
            var useOffset = tip == hand;

            _meshyInstance = Instantiate(meshyCharacterPrefab, anchor, false);
            _meshyInstance.name = "MeshyNpc_RightIndex";
            if (useOffset)
            {
                _meshyInstance.transform.localPosition = controllerTipLocalPos;
                _meshyInstance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            else
            {
                _meshyInstance.transform.localPosition = Vector3.zero;
                _meshyInstance.transform.localRotation = Quaternion.Euler(meshyLocalEuler);
            }

            _meshyInstance.transform.localScale = Vector3.one * meshyUniformScale;
        }

        void LateUpdate()
        {
            if (!keepMiniSelfGroundedUnderView || _miniSelf == null)
                return;
            PinMiniSelfToGroundUnderCamera();
        }

        void PinMiniSelfToGroundUnderCamera()
        {
            var cam = Camera.main;
            if (cam == null)
                return;
            Transform ct = cam.transform;
            Vector3 o = ct.position;
            o.y += groundPinRayStartAboveCamera;
            if (!Physics.Raycast(o, Vector3.down, out RaycastHit hit, groundPinRayLength, groundPinLayers, QueryTriggerInteraction.Ignore))
                return;
            if (hit.normal.y < groundPinMinHitNormalY)
                return;
            Vector3 horiz = hit.point - transform.position;
            horiz.y = 0f;
            float maxH = Mathf.Max(5f, groundPinMaxHorizontalFromRig);
            if (horiz.sqrMagnitude > maxH * maxH)
                return;

            _miniSelf.transform.SetParent(null, true);
            Vector3 feet = hit.point + Vector3.up * miniSelfFeetYOffsetFromHit;
            _miniSelf.transform.position = feet;
            Vector3 flat = ct.forward;
            flat.y = 0f;
            if (flat.sqrMagnitude > 1e-6f)
                _miniSelf.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
        }

        static GameObject BuildMiniSelfAvatar(Transform parent)
        {
            var root = new GameObject("MiniYouRoot");
            root.transform.SetParent(parent, false);

            void AddPart(string nm, PrimitiveType p, Vector3 lp, Vector3 sc, Color col)
            {
                var go = GameObject.CreatePrimitive(p);
                go.name = nm;
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = lp;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = sc;
                Object.Destroy(go.GetComponent<Collider>());
                ApplyLit(go.GetComponent<Renderer>(), col);
            }

            AddPart("LegL", PrimitiveType.Capsule, new Vector3(-0.06f, -0.04f, 0f), new Vector3(0.12f, 0.22f, 0.12f), new Color(0.2f, 0.35f, 0.75f));
            AddPart("LegR", PrimitiveType.Capsule, new Vector3(0.06f, -0.04f, 0f), new Vector3(0.12f, 0.22f, 0.12f), new Color(0.2f, 0.35f, 0.75f));
            AddPart("Torso", PrimitiveType.Capsule, new Vector3(0f, 0.04f, 0f), new Vector3(0.32f, 0.42f, 0.22f), new Color(0.15f, 0.55f, 0.9f));
            AddPart("ArmL", PrimitiveType.Capsule, new Vector3(-0.22f, 0.06f, 0f), new Vector3(0.1f, 0.28f, 0.1f), new Color(1f, 0.82f, 0.65f));
            AddPart("ArmR", PrimitiveType.Capsule, new Vector3(0.22f, 0.06f, 0f), new Vector3(0.1f, 0.28f, 0.1f), new Color(1f, 0.82f, 0.65f));
            AddPart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0f), new Vector3(0.38f, 0.38f, 0.38f), new Color(1f, 0.78f, 0.62f));

            return root;
        }

        static void ApplyLit(Renderer rend, Color c)
        {
            if (rend == null)
                return;
            var s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (s == null)
                return;
            var m = new Material(s);
            m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color"))
                m.SetColor("_Color", c);
            m.SetFloat("_Smoothness", 0.45f);
            rend.sharedMaterial = m;
        }
    }
}
