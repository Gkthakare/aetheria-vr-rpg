using UnityEngine;

namespace Aetheria.Dungeons
{
    /// <summary>
    /// Put on the starting cube. Sets the player (XR Origin) position to inside the cube on start.
    /// </summary>
    public class StartRoomCube : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [Tooltip("World position where the player spawns (inside the cube)")]
        [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 1.2f, 0f);

        private void Start()
        {
            if (player == null)
            {
                var cam = Camera.main;
                if (cam != null) player = cam.transform.root;
            }
            if (player != null)
                player.position = spawnPosition;
        }
    }
}
