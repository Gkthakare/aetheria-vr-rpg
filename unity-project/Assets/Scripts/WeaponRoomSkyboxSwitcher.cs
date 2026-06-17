using UnityEngine;

namespace Aetheria.Dungeons
{
    /// <summary>
    /// Put on WeaponRoom. Switches skybox when player enters/exits the trigger.
    /// Assign Skybox In (weapon room 360) and Skybox Out (default air).
    /// </summary>
    public class WeaponRoomSkyboxSwitcher : MonoBehaviour
    {
        [SerializeField] private Material skyboxInRoom;
        [SerializeField] private Material skyboxOutside;
        [SerializeField] private Transform player;

        private void Awake()
        {
            if (player == null)
            {
                var cam = Camera.main;
                if (cam != null) player = cam.transform.root;
            }
            if (GetComponent<Collider>() == null)
            {
                var col = gameObject.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(10f, 5f, 10f);
                col.center = Vector3.zero;
            }
        }

        private void Start()
        {
            if (player == null) return;
            var col = GetComponent<Collider>();
            if (col != null && col.bounds.Contains(player.position))
            {
                if (skyboxInRoom != null)
                    RenderSettings.skybox = skyboxInRoom;
            }
            else if (skyboxOutside != null)
            {
                RenderSettings.skybox = skyboxOutside;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;
            if (skyboxInRoom != null)
                RenderSettings.skybox = skyboxInRoom;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;
            if (skyboxOutside != null)
                RenderSettings.skybox = skyboxOutside;
        }

        private bool IsPlayer(Collider other)
        {
            if (player == null) return other.CompareTag("Player") || other.GetComponentInChildren<Camera>() != null;
            return other.transform == player || other.transform.IsChildOf(player);
        }
    }
}
