using UnityEngine;

namespace Aetheria.VR.BodyHub
{
    /// <summary>
    /// Marks a world collider as a gaze-dwell target for <see cref="QuestBodyHubGazeSelector"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestBodyHubGazePedestal : MonoBehaviour
    {
        [Min(0)]
        [SerializeField] private int avatarSlotIndex;

        public int AvatarSlotIndex => avatarSlotIndex;

        private void OnValidate()
        {
            avatarSlotIndex = Mathf.Max(0, avatarSlotIndex);
        }
    }
}
