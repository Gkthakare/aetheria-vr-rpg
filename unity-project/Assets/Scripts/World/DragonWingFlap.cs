using System.Collections.Generic;
using UnityEngine;

namespace Aetheria.World
{
    /// <summary>
    /// Procedural wing flap: oscillates local rotation on two wing transforms.
    /// Auto-finds children whose names contain "wing" (optional left/right in name).
    /// </summary>
    public sealed class DragonWingFlap : MonoBehaviour
    {
        [Header("Optional explicit bones (leave empty to auto-find)")]
        [SerializeField] private Transform leftWing;
        [SerializeField] private Transform rightWing;

        [Header("Motion")]
        [SerializeField] private float flapSpeed = 3.2f;
        [SerializeField] private float flapAmplitudeDegrees = 30f;
        [Tooltip("Local axis to flap around (e.g. 1,0,0 for many rigs).")]
        [SerializeField] private Vector3 localFlapAxis = new Vector3(1f, 0f, 0f);
        [SerializeField] private bool mirrorRightWing = true;

        [Header("Debug")]
        [SerializeField] private bool logAutoResolve = true;

        private Quaternion _baseLeft = Quaternion.identity;
        private Quaternion _baseRight = Quaternion.identity;
        private bool _resolved;
        private float _phase;

        private void Awake()
        {
            ResolveWingsIfNeeded();
            CacheBaseRotations();
        }

        private void Start()
        {
            // Nested FBX hierarchy is guaranteed by now; retry auto-find if first pass missed.
            if (leftWing == null || rightWing == null)
            {
                _resolved = false;
                ResolveWingsIfNeeded();
                CacheBaseRotations();
            }
        }

        private void OnEnable()
        {
            _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (!_resolved)
            {
                ResolveWingsIfNeeded();
                CacheBaseRotations();
            }

            if (leftWing == null && rightWing == null)
                return;

            _phase += Time.deltaTime * flapSpeed;
            float leftAngle = Mathf.Sin(_phase) * flapAmplitudeDegrees;
            float rightAngle = mirrorRightWing ? -Mathf.Sin(_phase) * flapAmplitudeDegrees : Mathf.Sin(_phase + Mathf.PI) * flapAmplitudeDegrees;

            var axis = localFlapAxis.sqrMagnitude > 1e-6f ? localFlapAxis.normalized : Vector3.right;

            if (leftWing != null)
                leftWing.localRotation = _baseLeft * Quaternion.AngleAxis(leftAngle, axis);
            if (rightWing != null)
                rightWing.localRotation = _baseRight * Quaternion.AngleAxis(rightAngle, axis);
        }

        private void CacheBaseRotations()
        {
            if (leftWing != null)
                _baseLeft = leftWing.localRotation;
            if (rightWing != null)
                _baseRight = rightWing.localRotation;
        }

        private void ResolveWingsIfNeeded()
        {
            if (leftWing != null && rightWing != null)
            {
                _resolved = true;
                return;
            }

            var candidates = new List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform) continue;
                var n = t.name.ToLowerInvariant();
                if (n.Contains("wing"))
                    candidates.Add(t);
            }

            if (candidates.Count == 0)
            {
                if (logAutoResolve)
                    Debug.LogWarning($"[DragonWingFlap] No transforms with 'wing' in name under '{name}'. Assign Left/Right Wing in inspector.");
                _resolved = true;
                return;
            }

            Transform pickLeft = null;
            Transform pickRight = null;
            foreach (var t in candidates)
            {
                var n = t.name.ToLowerInvariant();
                if (pickLeft == null && (n.Contains("left") || n.Contains("_l") || n.Contains(".l") || n.Contains("l_")))
                    pickLeft = t;
                if (pickRight == null && (n.Contains("right") || n.Contains("_r") || n.Contains(".r") || n.Contains("r_")))
                    pickRight = t;
            }

            if (pickLeft == null && candidates.Count > 0)
                pickLeft = candidates[0];
            if (pickRight == null && candidates.Count > 1)
            {
                foreach (var t in candidates)
                {
                    if (t != pickLeft)
                    {
                        pickRight = t;
                        break;
                    }
                }
            }

            if (leftWing == null)
                leftWing = pickLeft;
            if (rightWing == null)
                rightWing = pickRight;

            _resolved = true;
            if (logAutoResolve && (leftWing != null || rightWing != null))
                Debug.Log($"[DragonWingFlap] Wings: L={Describe(leftWing)} R={Describe(rightWing)}");
        }

        private static string Describe(Transform t) => t != null ? t.name : "null";
    }
}
