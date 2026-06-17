using UnityEngine;

namespace Aetheria.Gameplay
{
    /// <summary>
    /// Floating objective text for dungeon progression (floor, remaining enemies, portal prompt).
    /// </summary>
    public sealed class DungeonRunObjectiveHud : MonoBehaviour
    {
        [SerializeField] private Transform cameraAnchor;
        [SerializeField] private Vector3 panelLocalPos = new Vector3(0.55f, 0.18f, 1.15f);
        [SerializeField] private float panelScale = 0.0022f;

        private TextMesh _text;
        private DungeonFloorCombatController _controller;
        private NavMeshSetupValidator _validator;

        private void Start()
        {
            if (cameraAnchor == null && Camera.main != null)
                cameraAnchor = Camera.main.transform;
            _controller = FindObjectOfType<DungeonFloorCombatController>();
            if (cameraAnchor == null)
                return;

            var root = GameObject.CreatePrimitive(PrimitiveType.Quad);
            root.name = "DungeonObjectiveScreen";
            root.transform.SetParent(cameraAnchor, false);
            root.transform.localPosition = panelLocalPos;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * panelScale;
            if (root.TryGetComponent<Collider>(out var c))
                Destroy(c);

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.06f, 0.07f, 0.15f, 0.86f));
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0.06f, 0.07f, 0.15f, 0.86f));
            root.GetComponent<Renderer>().sharedMaterial = mat;

            var t = new GameObject("Text");
            t.transform.SetParent(root.transform, false);
            t.transform.localPosition = new Vector3(-0.45f, 0.25f, -0.01f);
            t.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
            _text = t.AddComponent<TextMesh>();
            _text.anchor = TextAnchor.UpperLeft;
            _text.alignment = TextAlignment.Left;
            _text.fontSize = 42;
            _text.characterSize = 0.08f;
            _text.color = new Color(0.8f, 0.95f, 1f, 1f);
        }

        private void Update()
        {
            if (_text == null)
                return;
            if (_controller == null)
                _controller = FindObjectOfType<DungeonFloorCombatController>();
            if (_validator == null)
                _validator = FindObjectOfType<NavMeshSetupValidator>();
            if (_controller == null)
            {
                _text.text = "Dungeon HUD\nController missing";
                return;
            }

            string setup = "Setup: --";
            if (_validator != null)
            {
                setup = (_validator.LastNavMeshOk && _validator.LastEnemyTagOk)
                    ? "Setup: NavMesh+Tag OK"
                    : "Setup: Fix NavMesh/EnemyTag";
            }
            float floorRatio = _controller.FloorCount > 0 ? (_controller.CurrentFloor / (float)_controller.FloorCount) : 0f;
            string floorBar = BuildBar(floorRatio);
            string assets = _validator == null
                ? "Assets: --"
                : (_validator.LastImportedAssetsOk ? "Assets: Meshy Linked" : "Assets: Missing Meshy Prefabs");

            _text.text =
                $"DUNGEON RUN\n" +
                $"Floor: {_controller.CurrentFloor}/{_controller.FloorCount}\n" +
                $"Progress: {floorBar}\n" +
                $"Enemies Left: {_controller.RemainingEnemies}\n" +
                $"Portal Ready: {(_controller.AwaitingPortal ? "YES" : "NO")}\n" +
                $"{setup}\n" +
                $"{assets}\n" +
                $"{_controller.CurrentObjective}";
        }

        private static string BuildBar(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            const int width = 12;
            int fill = Mathf.RoundToInt(ratio * width);
            return "[" + new string('|', fill) + new string('.', width - fill) + "]";
        }
    }
}
