using Aetheria.Combat;
using Aetheria.VR;
using UnityEngine;

namespace Aetheria.Gameplay
{
    /// <summary>
    /// Hand-mounted VR vitals menu (HP/MP/Mana charge) inspired by wrist HUD setups.
    /// Shows when palm is oriented toward the player's view direction.
    /// </summary>
    public sealed class VrHandVitalsMenu : MonoBehaviour
    {
        [Header("Hand anchor")]
        [SerializeField] private Transform xrRigRoot;
        [SerializeField] private bool useLeftHand = true;
        [SerializeField] private Vector3 menuLocalPosition = new Vector3(0.04f, 0.04f, 0.02f);
        [SerializeField] private Vector3 menuLocalEuler = new Vector3(285f, 180f, 0f);
        [SerializeField] private float menuScale = 0.0015f;

        [Header("Visibility")]
        [SerializeField] private bool showOnlyWhenPalmFacingHead = true;
        [SerializeField] private float palmFacingThreshold = 0.25f;
        [SerializeField] private float visibilityLerpSpeed = 8f;

        [Header("Bars")]
        [SerializeField] private float barLerpSpeed = 10f;
        [SerializeField] private Color hpColor = new Color(0.95f, 0.2f, 0.25f, 0.95f);
        [SerializeField] private Color mpColor = new Color(0.3f, 0.7f, 1f, 0.95f);
        [SerializeField] private Color manaChargeColor = new Color(0.35f, 1f, 0.5f, 0.95f);

        private Transform _hand;
        private Transform _menuRoot;
        private Transform _hpFill;
        private Transform _mpFill;
        private Transform _manaFill;
        private TextMesh _label;
        private Renderer _panelRenderer;
        private PlayerCombatStats _stats;
        private WindManaBow _bow;
        private Camera _cam;
        private float _hpRatio;
        private float _mpRatio;
        private float _manaRatio;
        private float _visibleAlpha;

        private void Start()
        {
            if (xrRigRoot == null)
                xrRigRoot = transform;
            _cam = Camera.main;
            _stats = GetComponent<PlayerCombatStats>() ?? GetComponentInParent<PlayerCombatStats>() ?? FindObjectOfType<PlayerCombatStats>();
            _bow = GetComponent<WindManaBow>() ?? GetComponentInParent<WindManaBow>() ?? FindObjectOfType<WindManaBow>();
            ResolveHand();
            BuildMenu();
        }

        private void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_hand == null) ResolveHand();
            if (_menuRoot == null || _stats == null) return;

            UpdatePose();
            UpdateValues();
            UpdateVisibility();
            ApplyVisualState();
        }

        private void ResolveHand()
        {
            var handRoot = XRHandAttachmentUtil.FindHandController(xrRigRoot, !useLeftHand);
            _hand = XRHandAttachmentUtil.FindIndexTipOrController(handRoot);
        }

        private void BuildMenu()
        {
            if (_hand == null)
                return;

            _menuRoot = new GameObject("HandVitalsMenu").transform;
            _menuRoot.SetParent(_hand, false);
            _menuRoot.localPosition = menuLocalPosition;
            _menuRoot.localRotation = Quaternion.Euler(menuLocalEuler);
            _menuRoot.localScale = Vector3.one * Mathf.Max(0.0005f, menuScale);

            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "Panel";
            panel.transform.SetParent(_menuRoot, false);
            panel.transform.localPosition = Vector3.zero;
            panel.transform.localScale = new Vector3(1.08f, 1.2f, 1f);
            if (panel.TryGetComponent<Collider>(out var c0)) Destroy(c0);
            _panelRenderer = panel.GetComponent<Renderer>();
            ApplyPanelColor(0f);

            var frame = GameObject.CreatePrimitive(PrimitiveType.Quad);
            frame.name = "Frame";
            frame.transform.SetParent(_menuRoot, false);
            frame.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            frame.transform.localScale = new Vector3(1.15f, 1.27f, 1f);
            if (frame.TryGetComponent<Collider>(out var c1)) Destroy(c1);
            ApplyUnlit(frame.GetComponent<Renderer>(), new Color(0.35f, 0.9f, 1f, 0.2f));

            _hpFill = BuildBar("HP", new Vector3(0f, 0.26f, -0.02f), hpColor);
            _mpFill = BuildBar("MP", new Vector3(0f, 0.02f, -0.02f), mpColor);
            _manaFill = BuildBar("MANA", new Vector3(0f, -0.22f, -0.02f), manaChargeColor);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(_menuRoot, false);
            labelGo.transform.localPosition = new Vector3(-0.47f, 0.52f, -0.03f);
            labelGo.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
            _label = labelGo.AddComponent<TextMesh>();
            _label.anchor = TextAnchor.UpperLeft;
            _label.alignment = TextAlignment.Left;
            _label.fontSize = 48;
            _label.characterSize = 0.08f;
            _label.color = new Color(0.82f, 0.95f, 1f, 1f);
            _label.text = "VITALS";
        }

        private Transform BuildBar(string title, Vector3 localPos, Color fillColor)
        {
            var group = new GameObject(title).transform;
            group.SetParent(_menuRoot, false);
            group.localPosition = localPos;
            group.localRotation = Quaternion.identity;

            var textGo = new GameObject("Title");
            textGo.transform.SetParent(group, false);
            textGo.transform.localPosition = new Vector3(-0.46f, 0.06f, -0.01f);
            textGo.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
            var text = textGo.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleLeft;
            text.alignment = TextAlignment.Left;
            text.fontSize = 40;
            text.characterSize = 0.08f;
            text.color = new Color(0.9f, 0.95f, 1f, 1f);
            text.text = title;

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "BG";
            bg.transform.SetParent(group, false);
            bg.transform.localPosition = new Vector3(0f, -0.03f, 0f);
            bg.transform.localScale = new Vector3(0.72f, 0.09f, 1f);
            if (bg.TryGetComponent<Collider>(out var c0)) Destroy(c0);
            ApplyUnlit(bg.GetComponent<Renderer>(), new Color(0.04f, 0.05f, 0.08f, 0.92f));

            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fill.name = "Fill";
            fill.transform.SetParent(bg.transform, false);
            fill.transform.localPosition = new Vector3(-0.36f, 0f, -0.001f);
            fill.transform.localScale = new Vector3(0.72f, 0.09f, 1f);
            if (fill.TryGetComponent<Collider>(out var c1)) Destroy(c1);
            ApplyUnlit(fill.GetComponent<Renderer>(), fillColor);
            return fill.transform;
        }

        private void UpdatePose()
        {
            _menuRoot.localPosition = menuLocalPosition;
            _menuRoot.localRotation = Quaternion.Euler(menuLocalEuler);
            _menuRoot.localScale = Vector3.one * Mathf.Max(0.0005f, menuScale);
        }

        private void UpdateValues()
        {
            float hp01 = _stats.MaxHp > 0.001f ? _stats.CurrentHp / _stats.MaxHp : 0f;
            float mp01 = _stats.MaxMp > 0.001f ? _stats.CurrentMp / _stats.MaxMp : 0f;
            float mana01 = (_bow != null && _bow.IsHoldingString) ? _bow.CurrentPull01 : mp01;

            _hpRatio = Mathf.Lerp(_hpRatio, hp01, Time.deltaTime * Mathf.Max(1f, barLerpSpeed));
            _mpRatio = Mathf.Lerp(_mpRatio, mp01, Time.deltaTime * Mathf.Max(1f, barLerpSpeed));
            _manaRatio = Mathf.Lerp(_manaRatio, mana01, Time.deltaTime * Mathf.Max(1f, barLerpSpeed));

            SetFill(_hpFill, _hpRatio);
            SetFill(_mpFill, _mpRatio);
            SetFill(_manaFill, _manaRatio);

            if (_label != null)
            {
                _label.text =
                    "VITALS\n" +
                    $"HP {_stats.CurrentHp:0}/{_stats.MaxHp:0}\n" +
                    $"MP {_stats.CurrentMp:0}/{_stats.MaxMp:0}\n" +
                    $"MANA {(Mathf.Clamp01(_manaRatio) * 100f):0}%";
            }
        }

        private void UpdateVisibility()
        {
            float target = 1f;
            if (showOnlyWhenPalmFacingHead && _cam != null && _hand != null)
            {
                // Assumes palm points roughly opposite hand forward for many controller rigs.
                var toHead = (_cam.transform.position - _hand.position).normalized;
                float dot = Vector3.Dot(-_hand.forward, toHead);
                target = dot >= palmFacingThreshold ? 1f : 0f;
            }
            _visibleAlpha = Mathf.Lerp(_visibleAlpha, target, Time.deltaTime * Mathf.Max(1f, visibilityLerpSpeed));
        }

        private void ApplyVisualState()
        {
            if (_menuRoot == null) return;
            _menuRoot.gameObject.SetActive(_visibleAlpha > 0.03f);
            ApplyPanelColor(_visibleAlpha);
            ApplyFillAlpha(_hpFill, _visibleAlpha);
            ApplyFillAlpha(_mpFill, _visibleAlpha);
            ApplyFillAlpha(_manaFill, _visibleAlpha);
            if (_label != null)
            {
                var c = _label.color;
                c.a = Mathf.Lerp(0f, 1f, _visibleAlpha);
                _label.color = c;
            }
        }

        private void ApplyPanelColor(float alpha01)
        {
            if (_panelRenderer == null) return;
            var c = new Color(0.03f, 0.08f, 0.12f, Mathf.Lerp(0f, 0.88f, alpha01));
            ApplyUnlit(_panelRenderer, c);
        }

        private static void SetFill(Transform fill, float ratio)
        {
            if (fill == null) return;
            ratio = Mathf.Clamp01(ratio);
            fill.localScale = new Vector3(0.72f * ratio, 0.09f, 1f);
            fill.localPosition = new Vector3(-0.36f + (0.36f * ratio), 0f, -0.001f);
        }

        private static void ApplyFillAlpha(Transform fill, float alpha01)
        {
            if (fill == null) return;
            var r = fill.GetComponent<Renderer>();
            if (r == null || r.sharedMaterial == null) return;
            if (r.sharedMaterial.HasProperty("_BaseColor"))
            {
                var c = r.sharedMaterial.GetColor("_BaseColor");
                c.a = Mathf.Lerp(0f, 0.95f, alpha01);
                r.sharedMaterial.SetColor("_BaseColor", c);
            }
            if (r.sharedMaterial.HasProperty("_Color"))
            {
                var c = r.sharedMaterial.GetColor("_Color");
                c.a = Mathf.Lerp(0f, 0.95f, alpha01);
                r.sharedMaterial.SetColor("_Color", c);
            }
        }

        private static void ApplyUnlit(Renderer rend, Color color)
        {
            if (rend == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = rend.sharedMaterial;
            if (mat == null || mat.shader != shader)
                mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            rend.sharedMaterial = mat;
        }
    }
}
