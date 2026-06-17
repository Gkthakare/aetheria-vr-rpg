using UnityEngine;

namespace Aetheria.Dungeons
{
    /// <summary>
    /// Simple trigger that teleports the player Transform to TargetPosition.
    /// Attach is done by the visualizer when spawning portal pads.
    /// </summary>
    public class TeleportPortal : MonoBehaviour
    {
        public Transform Player;
        public Vector3 TargetPosition;

        private void OnTriggerEnter(Collider other)
        {
            if (Player == null) return;

            // Accept either the Player itself or anything in the XR rig hierarchy
            if (other.transform == Player || other.transform.IsChildOf(Player))
            {
                Player.position = TargetPosition;
            }
        }
    }
}