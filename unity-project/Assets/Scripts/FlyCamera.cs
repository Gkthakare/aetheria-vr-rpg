using UnityEngine;
public class FlyCamera : MonoBehaviour
{
    [SerializeField] float moveSpeed = 8f;
    [SerializeField] float lookSpeed = 2f;
    void Update()
    {
        if (Input.GetMouseButton(1))
        {
            float yaw = Input.GetAxis("Mouse X") * lookSpeed;
            float pitch = -Input.GetAxis("Mouse Y") * lookSpeed;
            transform.eulerAngles += new Vector3(pitch, yaw, 0f);
        }
        Vector3 dir = new Vector3(
            Input.GetKey(KeyCode.D) ? 1 : Input.GetKey(KeyCode.A) ? -1 : 0,
            Input.GetKey(KeyCode.E) ? 1 : Input.GetKey(KeyCode.Q) ? -1 : 0,
            Input.GetKey(KeyCode.W) ? 1 : Input.GetKey(KeyCode.S) ? -1 : 0
        );
        transform.position += transform.TransformDirection(dir.normalized) * moveSpeed * Time.deltaTime;
    }
}