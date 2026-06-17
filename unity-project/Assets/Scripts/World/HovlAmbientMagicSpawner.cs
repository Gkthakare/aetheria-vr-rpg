using System.Collections;
using UnityEngine;

namespace Aetheria.World
{
    /// <summary>
    /// Spawns looping Hovl Studio magic prefabs near the player over open water (ambient "magic" in the infinite world).
    /// Assign prefabs from Assets/Hovl Studio/Magic effects pack/Prefabs/.
    /// </summary>
    public class HovlAmbientMagicSpawner : MonoBehaviour
    {
        [SerializeField] private bool enableSpawner = true;
        [SerializeField] private Transform player;
        [SerializeField] private float heightAboveWater = 1.2f;
        [SerializeField] private float spawnRadiusMin = 8f;
        [SerializeField] private float spawnRadiusMax = 35f;
        [SerializeField] private float intervalSeconds = 4.5f;
        [SerializeField] private float effectLifetimeSeconds = 8f;
        [SerializeField] private GameObject[] hovlPrefabs;

        private void Start()
        {
            if (!enableSpawner || hovlPrefabs == null || hovlPrefabs.Length == 0) return;
            StartCoroutine(SpawnLoop());
        }

        private IEnumerator SpawnLoop()
        {
            yield return null;
            if (player == null)
            {
                var cm = FindFirstObjectByType<ChunkManager>();
                if (cm != null) player = cm.PlayerTransform;
            }
            if (player == null && Camera.main != null)
                player = Camera.main.transform.root;
            if (player == null) yield break;

            var wait = new WaitForSeconds(intervalSeconds);
            while (true)
            {
                yield return wait;
                if (player == null) continue;
                var prefab = hovlPrefabs[Random.Range(0, hovlPrefabs.Length)];
                if (prefab == null) continue;
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(spawnRadiusMin, spawnRadiusMax);
                var pos = player.position + new Vector3(Mathf.Cos(ang) * r, heightAboveWater, Mathf.Sin(ang) * r);
                var o = UnityEngine.Object.Instantiate((UnityEngine.Object)prefab);
                if (o == null) continue;
                GameObject inst = o as GameObject;
                if (inst == null && o is Component c) inst = c.gameObject;
                if (inst == null) continue;
                inst.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                if (effectLifetimeSeconds > 0f)
                    Destroy(inst, effectLifetimeSeconds);
            }
        }
    }
}
