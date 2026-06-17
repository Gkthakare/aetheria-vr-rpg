using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace Aetheria.World
{
    /// <summary>
    /// Downloads a procedural island heightmap PNG from 3d-monster-gen FastAPI, builds a Unity Terrain
    /// centered on <see cref="IslandWorldBuilder"/>, and aligns the base so <see cref="IslandWorldBuilder.SeaLevelWorldY"/>
    /// matches Suimono (via rim height + <c>X-Sea-Level-Normalized</c>).
    /// </summary>
    [DefaultExecutionOrder(-125)]
    public class IslandHeightmapTerrainLoader : MonoBehaviour
    {
        [Header("Island")]
        [SerializeField] private IslandWorldBuilder islandWorld;

        [Header("API (3d-monster-gen FastAPI)")]
        [Tooltip("Host for the dashboard API (uvicorn). AIClient defaults to :8001; this endpoint is often served on :8000.")]
        [SerializeField] private string apiBaseUrl = "http://127.0.0.1:8000";
        [SerializeField] private int heightmapResolution = 513;
        [SerializeField] private int randomSeed;

        [SerializeField] private float beachStart = 0.62f;
        [SerializeField] private float beachWidth = 0.22f;
        [SerializeField] private float noiseScale = 4.5f;
        [SerializeField] private float ridgeBoost = 0.12f;
        [SerializeField] private float oceanRimStart = 0.97f;

        [Header("Terrain")]
        [Tooltip("TerrainData.size.y — meters from normalized height 0 to 1.")]
        [SerializeField] private float terrainHeightMeters = 80f;
        [Tooltip("Optional splat layers. If empty, Unity still renders height; assign materials for sand/grass.")]
        [SerializeField] private TerrainLayer[] terrainLayers;
        [Tooltip("If the coast faces the wrong way, toggle this.")]
        [SerializeField] private bool flipHeightmapZ = true;

        [Header("Cylinder ground (Island_Ground)")]
        [Tooltip("When true, runs before IslandWorldBuilder Awake and skips sand/grass discs (via execution order).")]
        [SerializeField] private bool skipIslandGroundFromStart;
        [Tooltip("After terrain is built, disables Island_Ground so only Terrain + Suimono water show at the shore.")]
        [SerializeField] private bool hideIslandGroundAfterApply = true;

        [Header("Flow")]
        [SerializeField] private bool loadOnStart = true;

        private const string HeightmapPath = "/api/v1/island/heightmap.png";
        private GameObject _terrainInstance;

        private void Awake()
        {
            if (islandWorld == null)
                islandWorld = FindObjectOfType<IslandWorldBuilder>();

            if (skipIslandGroundFromStart && islandWorld != null)
                islandWorld.ConfigureSkipGroundDiscForTerrain(true);
        }

        private void Start()
        {
            if (loadOnStart)
                StartCoroutine(LoadAndBuildTerrain());
        }

        /// <summary>Call after changing seed/URL to rebuild (destroys previous terrain from this loader).</summary>
        public void RequestReload() => StartCoroutine(LoadAndBuildTerrain());

        public IEnumerator LoadAndBuildTerrain()
        {
            if (islandWorld == null)
            {
                Debug.LogError("[IslandHeightmap] Assign IslandWorldBuilder on the loader or in-scene.", this);
                yield break;
            }

            string url = BuildUrl();
            using (var www = UnityWebRequest.Get(url))
            {
                www.timeout = 60;
                yield return www.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                if (www.result != UnityWebRequest.Result.Success)
#else
                if (www.isNetworkError || www.isHttpError)
#endif
                {
                    Debug.LogError($"[IslandHeightmap] GET failed: {www.error} — {url}", this);
                    yield break;
                }

                byte[] data = www.downloadHandler.data;
                if (data == null || data.Length < 32)
                {
                    Debug.LogError("[IslandHeightmap] Empty PNG response.", this);
                    yield break;
                }

                float seaNorm = ParseSeaLevelHeader(www.GetResponseHeader("X-Sea-Level-Normalized"));

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                if (!tex.LoadImage(data))
                {
                    Debug.LogError("[IslandHeightmap] LoadImage failed — not a valid PNG.", this);
                    Destroy(tex);
                    yield break;
                }

                int res = tex.width;
                if (tex.height != res)
                {
                    Debug.LogError($"[IslandHeightmap] PNG must be square; got {tex.width}×{tex.height}.", this);
                    Destroy(tex);
                    yield break;
                }

                if (!IsValidTerrainResolution(res))
                    Debug.LogWarning($"[IslandHeightmap] Unusual resolution {res} (Unity expects 33, 65, … 1025).", this);

                if (res != heightmapResolution)
                    Debug.LogWarning($"[IslandHeightmap] Requested size={heightmapResolution} but PNG is {res} — using PNG.", this);

                BuildTerrainObject(tex, seaNorm, res);
                Destroy(tex);

                if (hideIslandGroundAfterApply)
                    islandWorld.SetGroundDiscsActive(false);
            }
        }

        private string BuildUrl()
        {
            string root = (apiBaseUrl ?? string.Empty).TrimEnd('/');
            return $"{root}{HeightmapPath}?size={heightmapResolution}&seed={randomSeed}" +
                   $"&beach_start={Fmt(beachStart)}&beach_width={Fmt(beachWidth)}" +
                   $"&noise_scale={Fmt(noiseScale)}&ridge_boost={Fmt(ridgeBoost)}" +
                   $"&ocean_rim_start={Fmt(oceanRimStart)}";
        }

        private static string Fmt(float f) => f.ToString(CultureInfo.InvariantCulture);

        private static float ParseSeaLevelHeader(string header)
        {
            if (string.IsNullOrEmpty(header))
                return 0f;
            return float.TryParse(header, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }

        private static bool IsValidTerrainResolution(int n)
        {
            if (n < 33 || n > 1025)
                return false;
            int h = n - 1;
            return h > 0 && (h & (h - 1)) == 0;
        }

        private void BuildTerrainObject(Texture2D tex, float seaLevelNormalized, int res)
        {
            float sizeXZ = Mathf.Max(4f, islandWorld.GroundDiameter);
            float[,] heights = SampleHeights(tex, res);

            if (_terrainInstance != null)
            {
                Destroy(_terrainInstance);
                _terrainInstance = null;
            }

            var terrainData = new TerrainData
            {
                heightmapResolution = res,
                size = new Vector3(sizeXZ, Mathf.Max(1f, terrainHeightMeters), sizeXZ)
            };

            terrainData.SetHeights(0, 0, heights);

            if (terrainLayers != null && terrainLayers.Length > 0)
            {
                terrainData.terrainLayers = terrainLayers;
                PaintUniformAlpha(terrainData, terrainLayers.Length);
            }

            _terrainInstance = Terrain.CreateTerrainGameObject(terrainData);
            _terrainInstance.name = "Island_Terrain_FromApi";

            float baseWorldY = islandWorld.SeaLevelWorldY - seaLevelNormalized * terrainHeightMeters;
            Vector3 ipos = islandWorld.transform.position;
            _terrainInstance.transform.SetPositionAndRotation(
                new Vector3(ipos.x - sizeXZ * 0.5f, baseWorldY, ipos.z - sizeXZ * 0.5f),
                Quaternion.identity);

            _terrainInstance.layer = islandWorld.gameObject.layer;
        }

        private float[,] SampleHeights(Texture2D tex, int res)
        {
            var heights = new float[res, res];
            for (int z = 0; z < res; z++)
            {
                int sampleZ = flipHeightmapZ ? res - 1 - z : z;
                for (int x = 0; x < res; x++)
                {
                    float h = tex.GetPixel(x, sampleZ).grayscale;
                    heights[z, x] = Mathf.Clamp01(h);
                }
            }

            return heights;
        }

        private static void PaintUniformAlpha(TerrainData td, int layerCount)
        {
            int w = td.alphamapWidth;
            int h = td.alphamapHeight;
            var map = new float[h, w, layerCount];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    for (int k = 0; k < layerCount; k++)
                        map[y, x, k] = 0f;
                    map[y, x, 0] = 1f;
                }
            }

            td.SetAlphamaps(0, 0, map);
        }
    }
}
