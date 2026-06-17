using Aetheria.Combat;
using UnityEngine;

namespace Aetheria.Gameplay
{
    /// <summary>
    /// Creates two floating status screens in front of the player camera:
    /// left = player HP/MP, right = nearest enemy HP/MP.
    /// </summary>
    public sealed class PlayerEnemyStatusScreens : MonoBehaviour
    {
        [SerializeField] private Transform cameraAnchor;
        [SerializeField] private Vector3 playerPanelLocalPos = new Vector3(0.36f, -0.1f, 1.05f);
        [SerializeField] private Vector3 enemyPanelLocalPos = new Vector3(0.62f, 0.06f, 1.18f);
        [SerializeField] private float panelScale = 0.0018f;
        [SerializeField] private float barLerpSpeed = 8f;

        private TextMesh _playerText;
        private TextMesh _enemyText;
        private PlayerCombatStats _playerStats;
        private Transform _playerHpFill;
        private Transform _playerMpFill;
        private Transform _enemyHpFill;
        private Transform _enemyMpFill;
        private float _playerHpRatio;
        private float _playerMpRatio;
        private float _enemyHpRatio;
        private float _enemyMpRatio;

        private void Start()
        {
            if (cameraAnchor == null && Camera.main != null)
                cameraAnchor = Camera.main.transform;
            if (cameraAnchor == null)
                return;

            _playerStats = GetComponent<PlayerCombatStats>();
            if (_playerStats == null)
                _playerStats = GetComponentInParent<PlayerCombatStats>();
            if (_playerStats == null)
                _playerStats = FindObjectOfType<PlayerCombatStats>();

            _playerText = BuildPanel("PlayerStatsScreen", playerPanelLocalPos, new Color(0.08f, 0.2f, 0.08f, 0.85f), Color.green, out _playerHpFill, out _playerMpFill);
            _enemyText = BuildPanel("EnemyStatsScreen", enemyPanelLocalPos, new Color(0.22f, 0.08f, 0.08f, 0.85f), new Color(1f, 0.5f, 0.5f), out _enemyHpFill, out _enemyMpFill);
        }

        private void Update()
        {
            if (_playerText == null || _enemyText == null)
                return;

            if (_playerStats != null)
            {
                float hp01 = _playerStats.MaxHp > 0.001f ? _playerStats.CurrentHp / _playerStats.MaxHp : 0f;
                float mp01 = _playerStats.MaxMp > 0.001f ? _playerStats.CurrentMp / _playerStats.MaxMp : 0f;
                _playerHpRatio = Mathf.Lerp(_playerHpRatio, hp01, Time.deltaTime * barLerpSpeed);
                _playerMpRatio = Mathf.Lerp(_playerMpRatio, mp01, Time.deltaTime * barLerpSpeed);
                _playerText.text =
                    $"PLAYER\n" +
                    $"HP {BuildBar(hp01)} {_playerStats.CurrentHp:0}/{_playerStats.MaxHp:0}\n" +
                    $"MP {BuildBar(mp01)} {_playerStats.CurrentMp:0}/{_playerStats.MaxMp:0}";
                SetBarFill(_playerHpFill, _playerHpRatio);
                SetBarFill(_playerMpFill, _playerMpRatio);
            }
            else
            {
                _playerText.text = "PLAYER\nHP: --\nMP: --";
                SetBarFill(_playerHpFill, 0f);
                SetBarFill(_playerMpFill, 0f);
            }

            var enemy = FindNearestEnemy();
            if (enemy == null)
            {
                _enemyText.text = "ENEMY\nHP: --\nMP: --";
                SetBarFill(_enemyHpFill, 0f);
                SetBarFill(_enemyMpFill, 0f);
                return;
            }

            float ehp01 = enemy.MaxHp > 0.001f ? enemy.CurrentHp / enemy.MaxHp : 0f;
            float emp01 = enemy.MaxMp > 0.001f ? enemy.CurrentMp / enemy.MaxMp : 0f;
            _enemyHpRatio = Mathf.Lerp(_enemyHpRatio, ehp01, Time.deltaTime * barLerpSpeed);
            _enemyMpRatio = Mathf.Lerp(_enemyMpRatio, emp01, Time.deltaTime * barLerpSpeed);
            _enemyText.text =
                $"ENEMY: {enemy.name}\n" +
                $"HP {BuildBar(ehp01)} {enemy.CurrentHp:0}/{enemy.MaxHp:0}\n" +
                $"MP {BuildBar(emp01)} {enemy.CurrentMp:0}/{enemy.MaxMp:0}";
            SetBarFill(_enemyHpFill, _enemyHpRatio);
            SetBarFill(_enemyMpFill, _enemyMpRatio);
        }

        private EnemyController FindNearestEnemy()
        {
            EnemyController best = null;
            float bestSqr = float.MaxValue;
            Vector3 from = cameraAnchor != null ? cameraAnchor.position : transform.position;
            foreach (var e in FindObjectsOfType<EnemyController>())
            {
                if (e == null || !e.IsAlive) continue;
                float sqr = (e.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = e;
                }
            }
            return best;
        }

        private TextMesh BuildPanel(string name, Vector3 localPos, Color bgColor, Color textColor, out Transform hpFill, out Transform mpFill)
        {
            hpFill = null;
            mpFill = null;
            var root = GameObject.CreatePrimitive(PrimitiveType.Quad);
            root.name = name;
            root.transform.SetParent(cameraAnchor, false);
            root.transform.localPosition = localPos;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * panelScale;
            if (root.TryGetComponent<Collider>(out var col))
                Destroy(col);

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", bgColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", bgColor);
            root.GetComponent<Renderer>().sharedMaterial = mat;

            // Decorative frame for readability in bright scenes.
            var frame = GameObject.CreatePrimitive(PrimitiveType.Quad);
            frame.name = "Frame";
            frame.transform.SetParent(root.transform, false);
            frame.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            frame.transform.localScale = new Vector3(1.06f, 1.08f, 1f);
            if (frame.TryGetComponent<Collider>(out var frameCol))
                Destroy(frameCol);
            var frameMat = new Material(shader);
            var frameColor = new Color(textColor.r, textColor.g, textColor.b, 0.2f);
            if (frameMat.HasProperty("_BaseColor")) frameMat.SetColor("_BaseColor", frameColor);
            if (frameMat.HasProperty("_Color")) frameMat.SetColor("_Color", frameColor);
            frame.GetComponent<Renderer>().sharedMaterial = frameMat;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(root.transform, false);
            textGo.transform.localPosition = new Vector3(-0.43f, 0.25f, -0.01f);
            textGo.transform.localRotation = Quaternion.identity;
            textGo.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
            var tm = textGo.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.UpperLeft;
            tm.alignment = TextAlignment.Left;
            tm.fontSize = 42;
            tm.characterSize = 0.08f;
            tm.color = textColor;
            tm.text = "--";

            // Animated geometry bars for HP/MP.
            hpFill = BuildBarVisual(root.transform, "HP_Bar", new Vector3(-0.26f, -0.26f, -0.01f), new Color(0.95f, 0.2f, 0.25f, 0.95f));
            mpFill = BuildBarVisual(root.transform, "MP_Bar", new Vector3(-0.26f, -0.34f, -0.01f), new Color(0.25f, 0.7f, 1f, 0.95f));
            return tm;
        }

        private static Transform BuildBarVisual(Transform panel, string name, Vector3 localPos, Color fillColor)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = $"{name}_BG";
            bg.transform.SetParent(panel, false);
            bg.transform.localPosition = localPos;
            bg.transform.localScale = new Vector3(0.5f, 0.05f, 1f);
            if (bg.TryGetComponent<Collider>(out var bgCol))
                Destroy(bgCol);
            var bgMat = new Material(shader);
            if (bgMat.HasProperty("_BaseColor")) bgMat.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.05f, 0.85f));
            if (bgMat.HasProperty("_Color")) bgMat.SetColor("_Color", new Color(0.05f, 0.05f, 0.05f, 0.85f));
            bg.GetComponent<Renderer>().sharedMaterial = bgMat;

            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fill.name = $"{name}_Fill";
            fill.transform.SetParent(bg.transform, false);
            fill.transform.localPosition = new Vector3(-0.25f, 0f, -0.001f);
            fill.transform.localScale = new Vector3(0.5f, 0.05f, 1f);
            if (fill.TryGetComponent<Collider>(out var fillCol))
                Destroy(fillCol);
            var fillMat = new Material(shader);
            if (fillMat.HasProperty("_BaseColor")) fillMat.SetColor("_BaseColor", fillColor);
            if (fillMat.HasProperty("_Color")) fillMat.SetColor("_Color", fillColor);
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

        private static string BuildBar(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            const int width = 10;
            int fill = Mathf.RoundToInt(ratio * width);
            return "[" + new string('|', fill) + new string('.', width - fill) + "]";
        }
    }
}
