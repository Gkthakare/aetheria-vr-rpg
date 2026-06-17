using System;
using UnityEngine;
using Aetheria.AI;

namespace Aetheria.Gameplay
{
    /// <summary>
    /// Inline catalog row: drag a **prefab** here (no ScriptableObject required).
    /// catalogId must match the backend (e.g. creature.wolf.v1).
    /// </summary>
    [Serializable]
    public class InlineCatalogEntry
    {
        [Tooltip("Must match IntentParseResponse.spawn.catalogId from /api/v1/intent/parse (default wolf id).")]
        public string catalogId = "creature.wolf.v1";

        [Tooltip("Drag a prefab from the Project window (e.g. Capsule).")]
        public GameObject prefab;

        [Tooltip("Optional URP Lit / Standard material for tinting.")]
        public Material materialOverride;
    }

    /// <summary>
    /// Resolves catalogId to a prefab and spawns near the player using placement hints.
    /// </summary>
    public class SpawnDirector : MonoBehaviour
    {
        [SerializeField] private Transform player;

        [Header("Catalog (easiest — drag prefabs here)")]
        [Tooltip("Set Size to 1+, then assign catalogId + prefab per row.")]
        [SerializeField] private InlineCatalogEntry[] catalogEntries;

        [Header("Optional: ScriptableObject assets")]
        [Tooltip("Alternative: create Assets → Create → Aetheria → Creature Catalog Entry, then drag those .asset files here.")]
        [SerializeField] private CreatureCatalogEntry[] scriptableCatalog;

        [Header("Placement")]
        [Tooltip("Server may request 12–28m; clamp so test spawns stay in view (raise if you want farther spawns).")]
        [SerializeField] private float maxForwardSpawnMeters = 8f;

        private void Awake()
        {
            if (player == null && Camera.main != null)
                player = Camera.main.transform;
        }

        public void SpawnFromIntent(IntentParseResponse intent)
        {
            if (intent == null || intent.spawn == null)
            {
                Debug.LogWarning("[SpawnDirector] No spawn payload.");
                return;
            }

            if (intent.intentType != "spawn_enemy")
            {
                Debug.Log($"[SpawnDirector] intentType={intent.intentType}, skipping spawn.");
                return;
            }

            var payload = intent.spawn;
            if (!TryResolveEntry(payload.catalogId, out var prefab, out var materialOverride))
            {
                Debug.LogError(
                    $"[SpawnDirector] No catalog entry for catalogId=\"{payload.catalogId}\". " +
                    "Add a row under **Catalog (easiest)** with that exact id and a prefab, or add a matching Creature Catalog Entry asset.");
                return;
            }

            Vector3 pos = ComputeSpawnPosition(payload);
            var rot = Quaternion.identity;
            var instance = Instantiate(prefab, pos, rot);
            instance.name = string.IsNullOrEmpty(payload.displayName) ? payload.catalogId : payload.displayName;

            ApplyScale(instance.transform, payload.scale);

            if (materialOverride != null)
            {
                ApplyMaterialTint(instance, materialOverride, payload);
            }

            Debug.Log($"[SpawnDirector] Spawned {instance.name} at {pos} (tier={payload.difficultyTier})");
        }

        /// <summary>
        /// Direct catalog spawn helper (bypasses intent API) so gameplay systems can use Meshy catalog IDs directly.
        /// </summary>
        public bool TrySpawnFromCatalogId(string catalogId, Vector3 worldPosition, Quaternion rotation, out GameObject instance, float uniformScale = 1f)
        {
            instance = null;
            if (!TryResolveEntry(catalogId, out var prefab, out var materialOverride))
            {
                Debug.LogWarning($"[SpawnDirector] Catalog id not found: {catalogId}");
                return false;
            }

            instance = Instantiate(prefab, worldPosition, rotation);
            instance.name = string.IsNullOrEmpty(catalogId) ? instance.name : catalogId;

            if (uniformScale > 0f)
                instance.transform.localScale = Vector3.one * uniformScale;

            if (materialOverride != null)
            {
                var payload = new SpawnPayload
                {
                    tintR = 1f,
                    tintG = 1f,
                    tintB = 1f
                };
                ApplyMaterialTint(instance, materialOverride, payload);
            }

            return true;
        }

        private bool TryResolveEntry(string catalogId, out GameObject prefab, out Material materialOverride)
        {
            prefab = null;
            materialOverride = null;
            if (string.IsNullOrEmpty(catalogId)) return false;

            if (catalogEntries != null)
            {
                foreach (var e in catalogEntries)
                {
                    if (e == null || e.prefab == null) continue;
                    if (string.Equals(e.catalogId, catalogId, StringComparison.Ordinal))
                    {
                        prefab = e.prefab;
                        materialOverride = e.materialOverride;
                        return true;
                    }
                }
            }

            if (scriptableCatalog != null)
            {
                foreach (var e in scriptableCatalog)
                {
                    if (e == null || e.prefab == null) continue;
                    if (string.Equals(e.catalogId, catalogId, StringComparison.Ordinal))
                    {
                        prefab = e.prefab;
                        materialOverride = e.materialOverride;
                        return true;
                    }
                }
            }

            return false;
        }

        private Vector3 ComputeSpawnPosition(SpawnPayload payload)
        {
            if (player == null)
                return Vector3.zero;

            var forward = player.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            forward.Normalize();

            float mid = (payload.minDistanceM + payload.maxDistanceM) * 0.5f;
            if (mid < 0.5f) mid = 5f;
            mid = Mathf.Min(mid, Mathf.Max(2f, maxForwardSpawnMeters));

            var offset = forward * mid + Vector3.up * 0.5f;
            return player.position + offset;
        }

        private static void ApplyScale(Transform root, float scale)
        {
            if (scale <= 0f) return;
            root.localScale = Vector3.one * scale;
        }

        private static void ApplyMaterialTint(GameObject root, Material baseMat, SpawnPayload payload)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var color = new Color(payload.tintR, payload.tintG, payload.tintB, 1f);
            foreach (var r in renderers)
            {
                r.material = new Material(baseMat);
                if (r.material.HasProperty("_BaseColor"))
                    r.material.SetColor("_BaseColor", color);
                else if (r.material.HasProperty("_Color"))
                    r.material.SetColor("_Color", color);
            }
        }
    }
}
