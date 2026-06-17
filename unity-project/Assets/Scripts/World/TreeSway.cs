using UnityEngine;

namespace Aetheria.World
{
    /// <summary>
    /// Makes procedural trees sway in the wind. Add to tree root (parent of trunk + foliage).
    /// ALP prefab trees use their own shader-based wind.
    /// </summary>
    public class TreeSway : MonoBehaviour
    {
        [SerializeField] private float swayAmount = 1.5f;
        [SerializeField] private float swaySpeed = 1.2f;
        [SerializeField] private float windTurbulence = 0.25f;
        [SerializeField] private float swaySmoothing = 4f;

        private Vector3 _baseEuler;
        private float _phase;
        private Vector3 _currentSway;

        void Start()
        {
            _baseEuler = transform.eulerAngles;
            _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        void Update()
        {
            _phase += Time.deltaTime * swaySpeed;
            float sway = Mathf.Sin(_phase) * swayAmount;
            float turb = Mathf.PerlinNoise(Time.time * 0.4f, _phase) * windTurbulence * swayAmount;
            Vector3 target = new Vector3(sway * 0.2f, 0f, sway + turb);
            _currentSway = Vector3.Lerp(_currentSway, target, Time.deltaTime * swaySmoothing);
            transform.eulerAngles = _baseEuler + _currentSway;
        }
    }
}
