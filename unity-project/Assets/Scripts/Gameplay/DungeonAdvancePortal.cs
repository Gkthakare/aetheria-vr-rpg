using UnityEngine;

namespace Aetheria.Gameplay
{
    /// <summary>
    /// Trigger portal used by DungeonFloorCombatController to move to next floor.
    /// </summary>
    public sealed class DungeonAdvancePortal : MonoBehaviour
    {
        private DungeonFloorCombatController _controller;
        private Transform _playerRoot;

        public void Configure(DungeonFloorCombatController controller, Transform playerRoot)
        {
            _controller = controller;
            _playerRoot = playerRoot;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_controller == null || _playerRoot == null) return;
            if (other.transform == _playerRoot || other.transform.IsChildOf(_playerRoot))
                _controller.OnAdvancePortalEntered();
        }
    }
}
