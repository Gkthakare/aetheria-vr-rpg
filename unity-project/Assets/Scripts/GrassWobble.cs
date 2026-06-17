using UnityEngine;

/// <summary>
/// Simple sway for grass quads. Add to grass blade GameObjects.
/// </summary>
public class GrassWobble : MonoBehaviour
{
    [SerializeField] private float amplitude = 5f;
    [SerializeField] private float speed = 1f;

    private Quaternion _startRotation;

    void Start()
    {
        _startRotation = transform.localRotation;
    }

    void Update()
    {
        float angle = Mathf.Sin(Time.time * speed + GetInstanceID() * 0.1f) * amplitude;
        transform.localRotation = _startRotation * Quaternion.Euler(0f, 0f, angle);
    }
}
