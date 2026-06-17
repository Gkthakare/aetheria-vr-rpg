using System.Collections.Generic;
using Aetheria.Magic;
using Aetheria.VR;
using Aetheria.WaterMagic;
using UnityEngine;
using UnityEngine.XR;

namespace Aetheria.ElementalMagic
{
    /// <summary>
    /// VR + voice: ambient mana regen, faster gather near open water, global shader hints for an invisible mana field,
    /// spawn water / fire / shadow / earth orbs on the right hand from voice, then throw or resize via voice.
    /// Phrases use a magic context (cast / summon / magic / spell / orb / ball) so plain "water" still maps to world gen.
    /// Uses namespace <c>Aetheria.ElementalMagic</c> (Assembly-CSharp) so it does not collide with the <c>Aetheria.Magic</c> asmdef assembly.
    /// </summary>
    [DefaultExecutionOrder(-35)]
    public sealed class ElementalManaAndMagicHub : MonoBehaviour
    {
        public static ElementalManaAndMagicHub Instance { get; private set; }

        public static readonly int PlayerBodyManaId = Shader.PropertyToID("_PlayerBodyMana");
        public static readonly int WorldAmbientManaId = Shader.PropertyToID("_WorldAmbientMana");
        public static readonly int ManaAffinityWorldPosId = Shader.PropertyToID("_ManaAffinityWorldPos");

        enum MagicElement
        {
            None,
            Water,
            Fire,
            Shadow,
            Earth,
        }

        [Header("Mana")]
        [SerializeField] float maxMana = 1f;
        [SerializeField] float startingMana = 0.35f;
        [SerializeField] float ambientRegenPerSecond = 0.04f;
        [SerializeField] float waterProximityBonusPerSecond = 0.22f;
        [SerializeField] float manaCostPerOrb = 0.22f;
        [SerializeField] float waterGatherRadius = 5f;

        [Header("Orb")]
        [SerializeField] float orbScale = 0.14f;
        [SerializeField] float throwImpulse = 6.5f;
        [SerializeField] Transform rightHandAnchorOverride;

        [Header("Water pulse (global ocean)")]
        [SerializeField] float waterPulseStrength = 1.1f;

        float _mana;
        GameObject _heldOrb;
        Rigidbody _heldBody;
        MagicElement _heldElement;
        WaterWorldInfluenceHub _waterHub;
        LayerMask _waterMask;
        readonly Collider[] _overlapHits = new Collider[16];

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            _mana = Mathf.Clamp(startingMana, 0f, maxMana);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            _waterHub = FindObjectOfType<WaterWorldInfluenceHub>();
            _waterMask = BuildWaterSurfaceMask();
        }

        void Update()
        {
            ApplyManaRegen();
            PushGlobalManaUniforms();
        }

        void ApplyManaRegen()
        {
            _mana = Mathf.Min(maxMana, _mana + ambientRegenPerSecond * Time.deltaTime);
            if (_waterMask.value == 0)
                return;
            var hand = GetRightHandWorldPosition();
            var n = Physics.OverlapSphereNonAlloc(hand, waterGatherRadius, _overlapHits, _waterMask, QueryTriggerInteraction.Collide);
            if (n > 0)
                _mana = Mathf.Min(maxMana, _mana + waterProximityBonusPerSecond * Time.deltaTime);
        }

        void PushGlobalManaUniforms()
        {
            var hp = GetRightHandWorldPosition();
            Shader.SetGlobalFloat(PlayerBodyManaId, maxMana > 0.001f ? _mana / maxMana : 0f);
            Shader.SetGlobalFloat(WorldAmbientManaId, 0.32f + 0.08f * Mathf.Sin(Time.time * 0.17f));
            Shader.SetGlobalVector(ManaAffinityWorldPosId, new Vector4(hp.x, hp.y, hp.z, 1f));
        }

        /// <summary>
        /// Called from voice integration before world-gen partial matches.
        /// Returns true when the line was handled as elemental magic (including "not enough mana" for a magic phrase).
        /// </summary>
        public static bool TryConsumeVoiceCommand(string raw)
        {
            if (Instance == null || string.IsNullOrWhiteSpace(raw))
                return false;
            return Instance.InternalTryCommand(raw.Trim().ToLowerInvariant());
        }

        bool InternalTryCommand(string c)
        {
            if (TryThrowOrShape(c))
                return true;

            var element = ClassifyElement(c);
            if (element == MagicElement.None)
                return false;

            if (_mana < manaCostPerOrb)
            {
                Debug.Log("[ElementalMana] Not enough mana for an orb. Gather near the ocean or wait for ambient regen.");
                return true;
            }

            SpawnOrbOnHand(element);
            _mana = Mathf.Max(0f, _mana - manaCostPerOrb);
            return true;
        }

        bool TryThrowOrShape(string c)
        {
            if (_heldOrb == null)
                return false;

            if (ContainsAny(c, "throw", "release", "fling", "hurl", "launch", "shoot"))
            {
                ThrowHeldOrb();
                return true;
            }

            if (ContainsAny(c, "grow", "bigger", "enlarge", "expand"))
            {
                _heldOrb.transform.localScale *= 1.18f;
                return true;
            }

            if (ContainsAny(c, "shrink", "smaller", "reduce"))
            {
                _heldOrb.transform.localScale *= 0.85f;
                return true;
            }

            return false;
        }

        void ThrowHeldOrb()
        {
            if (_heldBody == null || _heldOrb == null)
                return;

            _heldOrb.transform.SetParent(null, true);
            _heldBody.isKinematic = false;
            _heldBody.useGravity = true;
            _heldBody.AddForce(GetThrowDirection() * throwImpulse, ForceMode.VelocityChange);
            _heldOrb = null;
            _heldBody = null;
            _heldElement = MagicElement.None;
        }

        Vector3 GetThrowDirection()
        {
            var list = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, list);
            if (list.Count > 0 && list[0].TryGetFeatureValue(CommonUsages.deviceRotation, out var q))
                return q * Vector3.forward;

            var cam = Camera.main;
            return cam != null ? cam.transform.forward : transform.forward;
        }

        void SpawnOrbOnHand(MagicElement element)
        {
            if (_heldOrb != null)
                Destroy(_heldOrb);

            var anchor = GetRightHandAnchor();
            if (anchor == null)
            {
                Debug.LogWarning("[ElementalMana] No right-hand anchor — assign Right Hand Anchor Override or add 'Right Controller' under the XR rig.");
                return;
            }

            var root = new GameObject($"ElementalOrb_{element}");
            root.transform.SetParent(anchor, false);
            root.transform.localPosition = new Vector3(0f, 0.02f, 0.06f);
            root.transform.localRotation = Quaternion.identity;
            var visScale = orbScale * (Application.isMobilePlatform ? 1.55f : 1f);
            root.transform.localScale = Vector3.one * visScale;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Mesh";
            sphere.transform.SetParent(root.transform, false);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one;
            Destroy(sphere.GetComponent<Collider>());

            var rend = sphere.GetComponent<Renderer>();
            ApplyElementMaterial(rend, element);

            var col = root.AddComponent<SphereCollider>();
            col.radius = 0.5f;
            col.isTrigger = false;

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.mass = 0.35f;

            if (element == MagicElement.Fire)
                AddFireExtras(root);

            if (element == MagicElement.Water && _waterHub != null)
                _waterHub.ApplyPulse(anchor.position + GetThrowDirection() * 0.6f, waterPulseStrength);

            if (element == MagicElement.Water && SuimonoWaterRuntimeLink.RippleNormalsPrefab != null)
                TrySpawnHandRipple(anchor.position);

            _heldOrb = root;
            _heldBody = rb;
            _heldElement = element;
        }

        static void TrySpawnHandRipple(Vector3 worldPos)
        {
            var prefab = SuimonoWaterRuntimeLink.RippleNormalsPrefab;
            if (prefab == null)
                return;
            var fx = Instantiate(prefab, worldPos, Quaternion.identity);
            Destroy(fx, 6f);
        }

        static void AddFireExtras(GameObject root)
        {
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.45f, 0.12f);
            light.range = 2.2f;
            light.intensity = 1.4f;
            light.shadows = LightShadows.None;

            var trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.startWidth = 0.06f;
            trail.endWidth = 0.01f;
            trail.material = CreateTrailMaterial();
            trail.emitting = true;
            trail.generateLightingData = false;
        }

        static Material CreateTrailMaterial()
        {
            var s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (s == null)
                s = Shader.Find("Sprites/Default");
            var m = new Material(s);
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", new Color(1f, 0.5f, 0.1f, 0.75f));
            else if (m.HasProperty("_Color"))
                m.SetColor("_Color", new Color(1f, 0.5f, 0.1f, 0.75f));
            return m;
        }

        static void ApplyElementMaterial(Renderer rend, MagicElement element)
        {
            if (rend == null)
                return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                return;
            var m = new Material(shader);
            switch (element)
            {
                case MagicElement.Water:
                    m.SetColor("_BaseColor", new Color(0.25f, 0.72f, 0.95f, 0.45f));
                    m.SetFloat("_Smoothness", 0.88f);
                    m.SetFloat("_Metallic", 0.05f);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(0.08f, 0.35f, 0.5f, 1f));
                    break;
                case MagicElement.Fire:
                    m.SetColor("_BaseColor", new Color(1f, 0.35f, 0.05f, 1f));
                    m.SetFloat("_Smoothness", 0.55f);
                    m.SetFloat("_Metallic", 0f);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.08f, 1f) * 2.2f);
                    break;
                case MagicElement.Shadow:
                    m.SetColor("_BaseColor", new Color(0.12f, 0.05f, 0.22f, 0.55f));
                    m.SetFloat("_Smoothness", 0.92f);
                    m.SetFloat("_Metallic", 0.2f);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(0.25f, 0.05f, 0.45f, 1f));
                    break;
                case MagicElement.Earth:
                    m.SetColor("_BaseColor", new Color(0.35f, 0.22f, 0.12f, 1f));
                    m.SetFloat("_Smoothness", 0.22f);
                    m.SetFloat("_Metallic", 0f);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(0.08f, 0.05f, 0.02f, 1f));
                    break;
            }

            if (m.HasProperty("_Color"))
                m.SetColor("_Color", m.GetColor("_BaseColor"));
            rend.sharedMaterial = m;
        }

        Transform GetRightHandAnchor()
        {
            if (rightHandAnchorOverride != null)
                return rightHandAnchorOverride;
            var hand = XRHandAttachmentUtil.FindHandController(transform, true);
            return hand != null ? XRHandAttachmentUtil.FindIndexTipOrController(hand) : null;
        }

        Vector3 GetRightHandWorldPosition()
        {
            var anchor = GetRightHandAnchor();
            if (anchor != null)
                return anchor.position;
            var list = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, list);
            if (list.Count > 0 && list[0].TryGetFeatureValue(CommonUsages.devicePosition, out var pos))
                return pos;
            return transform.position;
        }

        static MagicElement ClassifyElement(string c)
        {
            if (!IsElementalMagicPhrase(c))
                return MagicElement.None;

            if (c.Contains("water") || c.Contains("aqua") || c.Contains("hydro"))
                return MagicElement.Water;
            if (c.Contains("fire") || c.Contains("flame") || c.Contains("inferno") || c.Contains("pyro"))
                return MagicElement.Fire;
            if (c.Contains("shadow") || c.Contains("void") || c.Contains("umbra") || c.Contains("dark"))
                return MagicElement.Shadow;
            if (c.Contains("earth") || c.Contains("stone") || c.Contains("rock") || c.Contains("geo"))
                return MagicElement.Earth;

            return MagicElement.None;
        }

        static bool IsElementalMagicPhrase(string c)
        {
            if (c.Contains("water magic") || c.Contains("fire magic") || c.Contains("shadow magic") || c.Contains("earth magic"))
                return true;
            if (ContainsAny(c, "cast", "summon", "magic", "spell", "ball"))
                return true;
            return c.Contains(" orb") || c.StartsWith("orb ") || c == "orb";
        }

        static bool ContainsAny(string hay, params string[] needles)
        {
            foreach (var n in needles)
            {
                if (hay.IndexOf(n, System.StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
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

        static LayerMask BuildWaterSurfaceMask()
        {
            var mask = 0;
            AddLayer(ref mask, "Water");
            AddLayer(ref mask, "Suimono_Water");
            return mask;
        }

        static void AddLayer(ref int mask, string layerName)
        {
            var i = LayerMask.NameToLayer(layerName);
            if (i >= 0)
                mask |= 1 << i;
        }
    }
}
