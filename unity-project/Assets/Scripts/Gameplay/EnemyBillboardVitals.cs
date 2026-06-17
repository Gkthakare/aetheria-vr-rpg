using Aetheria.Combat;
using UnityEngine;

namespace Aetheria.Gameplay
{
    /// <summary>
    /// Tiny floating HP/MP label over enemies, always facing camera.
    /// </summary>
    public sealed class EnemyBillboardVitals : MonoBehaviour
    {
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);
        [SerializeField] private float textScale = 0.06f;
        [SerializeField] private float barLerpSpeed = 9f;

        private Transform _cam;
        private EnemyController _enemy;
        private TextMesh _tm;
        private Transform _hpFill;
        private float _hpRatio;

        private void Start()
        {
            _cam = Camera.main != null ? Camera.main.transform : null;
            _enemy = GetComponentInParent<EnemyController>();
            if (_enemy == null) _enemy = GetComponent<EnemyController>();

            var go = new GameObject("EnemyVitalsText");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = worldOffset;
            _tm = go.AddComponent<TextMesh>();
            _tm.anchor = TextAnchor.MiddleCenter;
            _tm.alignment = TextAlignment.Center;
            _tm.fontSize = 40;
            _tm.characterSize = textScale;
            _tm.color = new Color(1f, 0.35f, 0.35f, 1f);
            _hpFill = BuildHpBar(go.transform);
        }

        private void LateUpdate()
        {
            if (_enemy == null || _tm == null) return;
            if (_cam == null && Camera.main != null) _cam = Camera.main.transform;

            float hp01 = _enemy.MaxHp > 0.001f ? _enemy.CurrentHp / _enemy.MaxHp : 0f;
            float mp01 = _enemy.MaxMp > 0.001f ? _enemy.CurrentMp / _enemy.MaxMp : 0f;
            _hpRatio = Mathf.Lerp(_hpRatio, hp01, Time.deltaTime * barLerpSpeed);
            _tm.text =
                $"HP {BuildBar(hp01)} {_enemy.CurrentHp:0}/{_enemy.MaxHp:0}\n" +
                $"MP {BuildBar(mp01)} {_enemy.CurrentMp:0}/{_enemy.MaxMp:0}";
            _tm.color = Color.Lerp(new Color(1f, 0.25f, 0.25f, 1f), new Color(0.4f, 1f, 0.5f, 1f), hp01);
            SetBarFill(_hpFill, _hpRatio);

            if (_cam != null)
            {
                var dir = _tm.transform.position - _cam.position;
                if (dir.sqrMagnitude > 0.001f)
                    _tm.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        private static string BuildBar(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            const int width = 8;
            int fill = Mathf.RoundToInt(ratio * width);
            return "[" + new string('|', fill) + new string('.', width - fill) + "]";
        }

        private static Transform BuildHpBar(Transform root)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "HPBarBG";
            bg.transform.SetParent(root, false);
            bg.transform.localPosition = new Vector3(0f, -0.12f, 0f);
            bg.transform.localScale = new Vector3(0.5f, 0.05f, 1f);
            if (bg.TryGetComponent<Collider>(out var c0)) Destroy(c0);
            var bgMat = new Material(shader);
            if (bgMat.HasProperty("_BaseColor")) bgMat.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.1f, 0.8f));
            if (bgMat.HasProperty("_Color")) bgMat.SetColor("_Color", new Color(0.1f, 0.1f, 0.1f, 0.8f));
            bg.GetComponent<Renderer>().sharedMaterial = bgMat;

            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fill.name = "HPBarFill";
            fill.transform.SetParent(bg.transform, false);
            fill.transform.localPosition = new Vector3(-0.25f, 0f, -0.001f);
            fill.transform.localScale = new Vector3(0.5f, 0.05f, 1f);
            if (fill.TryGetComponent<Collider>(out var c1)) Destroy(c1);
            var fillMat = new Material(shader);
            if (fillMat.HasProperty("_BaseColor")) fillMat.SetColor("_BaseColor", new Color(1f, 0.2f, 0.2f, 0.95f));
            if (fillMat.HasProperty("_Color")) fillMat.SetColor("_Color", new Color(1f, 0.2f, 0.2f, 0.95f));
            fill.GetComponent<Renderer>().sharedMaterial = fillMat;
            return fill.transform;
        }

        private static void SetBarFill(Transform fill, float ratio)
        {
            if (fill == null) return;
            ratio = Mathf.Clamp01(ratio);
            fill.localScale = new Vector3(0.5f * ratio, 0.05f, 1f);
            fill.localPosition = new Vector3(-0.25f + (0.25f * ratio), 0f, -0.001f);
        }
    }
}
