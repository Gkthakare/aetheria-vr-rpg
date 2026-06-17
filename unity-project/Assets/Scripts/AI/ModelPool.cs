using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace Aetheria.AI
{
    /// <summary>
    /// Pre-generates and caches 3D models for infinite dungeon decor.
    /// Fetches from ML backend (Shap-E) at startup, then ChunkManager uses from pool.
    /// </summary>
    public class ModelPool : MonoBehaviour
    {
        [Header("API")]
        [SerializeField] private Model3DRequest modelRequest;

        [Header("Dungeon Prompts (pre-generate at startup)")]
        [Tooltip("Prompts for forest/grass decor")]
        [SerializeField] private string[] treePrompts = { "fantasy dead tree stump", "small rock boulder" };
        [Tooltip("Prompts for dungeon props")]
        [SerializeField] private string[] pillarPrompts = { "fantasy dungeon pillar stone", "medieval torch holder" };

        [Header("Pool Settings")]
        [SerializeField] private bool preloadOnStart = true;

        private readonly Dictionary<string, List<Mesh>> poolByCategory = new Dictionary<string, List<Mesh>>();
        private readonly Dictionary<string, List<GameObject>> prefabByCategory = new Dictionary<string, List<GameObject>>();
        private bool isReady;

        public bool IsReady => isReady;
        public int GetPoolCount(string category) => poolByCategory.TryGetValue(category, out var list) ? list.Count : 0;

        private void Awake()
        {
            if (modelRequest == null)
                modelRequest = GetComponent<Model3DRequest>();
            if (modelRequest == null)
                modelRequest = gameObject.AddComponent<Model3DRequest>();
        }

        private void Start()
        {
            if (preloadOnStart)
                StartCoroutine(PreloadPool());
        }

        private IEnumerator PreloadPool()
        {
            foreach (var p in treePrompts)
            {
                yield return FetchAndAdd(p, "tree");
            }
            foreach (var p in pillarPrompts)
            {
                yield return FetchAndAdd(p, "pillar");
            }
            isReady = true;
            Debug.Log($"[ModelPool] Ready. Trees: {GetPoolCount("tree")}, Pillars: {GetPoolCount("pillar")}");
        }

        private IEnumerator FetchAndAdd(string prompt, string category)
        {
            bool done = false;
            Mesh mesh = null;
            modelRequest.RequestModel(prompt, m => { mesh = m; done = true; }, _ => { done = true; });
            while (!done) yield return null;

            if (mesh != null)
            {
                if (!poolByCategory.ContainsKey(category))
                    poolByCategory[category] = new List<Mesh>();
                poolByCategory[category].Add(mesh);

                var prefab = CreatePrefabFromMesh(mesh, $"{category}_{prompt.GetHashCode():x}");
                if (!prefabByCategory.ContainsKey(category))
                    prefabByCategory[category] = new List<GameObject>();
                prefabByCategory[category].Add(prefab);
            }
        }

        private static GameObject CreatePrefabFromMesh(Mesh mesh, string name)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(URPMaterialHelper.GetLitShader());
            mr.sharedMaterial.color = new Color(0.6f, 0.5f, 0.4f);
            go.SetActive(false);
            return go;
        }

        /// <summary>Get a random prefab from the pool for a category. Returns null if pool empty.</summary>
        public GameObject GetRandom(string category)
        {
            if (!prefabByCategory.TryGetValue(category, out var list) || list.Count == 0)
                return null;
            return list[Random.Range(0, list.Count)];
        }

        /// <summary>Instantiate a decor object at local position under parent. Returns null if pool empty.</summary>
        public GameObject SpawnAt(string category, Vector3 localPosition, Quaternion rotation, Transform parent)
        {
            var prefab = GetRandom(category);
            if (prefab == null) return null;
            var instance = Instantiate(prefab, parent);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = rotation;
            instance.SetActive(true);
            return instance;
        }
    }
}
