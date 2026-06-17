using UnityEngine;

namespace Aetheria.VR.BodyHub
{
    /// <summary>
    /// Controller-free friendly: look at a pedestal collider for <see cref="dwellSeconds"/> to switch avatar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestBodyHubGazeSelector : MonoBehaviour
    {
        [SerializeField] private QuestBodyHubDirector director;
        [SerializeField] private Camera gazeCamera;
        [SerializeField] private float dwellSeconds = 1.15f;
        [SerializeField] private float maxRayDistance = 12f;
        [SerializeField] private LayerMask pedestalLayers = ~0;
        [Tooltip("Optional: small reticle in front of camera for debugging gaze.")]
        [SerializeField] private bool drawDebugRay;

        private float _dwell;
        private QuestBodyHubGazePedestal _active;

        private void Awake()
        {
            if (gazeCamera == null)
                gazeCamera = Camera.main;
            if (gazeCamera == null)
            {
                var xr = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin");
                if (xr != null)
                    gazeCamera = xr.GetComponentInChildren<Camera>(true);
            }
        }

        private void Reset()
        {
            gazeCamera = Camera.main;
        }

        private void Update()
        {
            if (director == null)
                director = FindObjectOfType<QuestBodyHubDirector>();
            if (director == null || gazeCamera == null)
                return;

            var ray = new Ray(gazeCamera.transform.position, gazeCamera.transform.forward);
            if (drawDebugRay)
                Debug.DrawRay(ray.origin, ray.direction * Mathf.Min(maxRayDistance, 4f), Color.cyan);

            if (!Physics.Raycast(ray, out var hit, maxRayDistance, pedestalLayers, QueryTriggerInteraction.Collide))
            {
                ResetDwell();
                return;
            }

            var ped = hit.collider.GetComponentInParent<QuestBodyHubGazePedestal>();
            if (ped == null)
            {
                ResetDwell();
                return;
            }

            if (ped != _active)
            {
                _active = ped;
                _dwell = 0f;
            }

            _dwell += Time.deltaTime;
            if (_dwell >= dwellSeconds)
            {
                director.SelectAvatar(ped.AvatarSlotIndex, true);
                _dwell = 0f;
            }
        }

        private void ResetDwell()
        {
            _dwell = 0f;
            _active = null;
        }
    }
}
