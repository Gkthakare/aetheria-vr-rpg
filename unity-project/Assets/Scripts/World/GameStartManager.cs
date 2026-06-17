using UnityEngine;
using Aetheria.VR;

namespace Aetheria.World
{
    /// <summary>
    /// Configures game start: player spawns in weapon room.
    /// 2 grips = teleport to outside world (on road).
    /// Assign IslandWorldBuilder to get road position.
    /// </summary>
    public class GameStartManager : MonoBehaviour
    {
        [Header("Start Location")]
        [SerializeField] private Transform player;
        [SerializeField] private Vector3 weaponRoomStart = new Vector3(0f, 1.2f, 6f);

        [Header("Outside World (2 grips)")]
        [SerializeField] private IslandWorldBuilder islandBuilder;
        [SerializeField] private GestureTeleport gestureTeleport;

        void Start()
        {
            if (player == null)
            {
                var cam = Camera.main;
                if (cam != null) player = cam.transform.root;
            }
            if (player == null)
            {
                foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (t.name.Contains("XR Origin")) { player = t; break; }
            }

            if (player != null)
                player.position = weaponRoomStart;

            if (gestureTeleport != null && islandBuilder != null)
                gestureTeleport.SetOutsideWorldPosition(islandBuilder.RoadStartWorldPosition);
        }
    }
}
