using UnityEngine;

public class PortalVFX : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private float scrollSpeedU = 0.1f;
    [SerializeField] private float scrollSpeedV = 0.2f;
    [SerializeField] private float rotationSpeed = 30f;

    private Material _mat;
    private float _rotation;

    void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        if (targetRenderer != null)
            _mat = targetRenderer.material;
    }

    void Update()
    {
        if (_mat == null) return;

        // Scroll UVs
        var offset = _mat.mainTextureOffset;
        offset.x += scrollSpeedU * Time.deltaTime;
        offset.y += scrollSpeedV * Time.deltaTime;
        _mat.mainTextureOffset = offset;

        // Rotate the portal slightly over time
        _rotation += rotationSpeed * Time.deltaTime;
        _mat.mainTextureScale = new Vector2(1, 1);
        _mat.SetFloat("_Rotation", _rotation * Mathf.Deg2Rad);
    }
}