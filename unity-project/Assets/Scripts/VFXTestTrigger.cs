using UnityEngine;
using Aetheria.AI;

// Lives in Assembly-CSharp (not Aetheria.VR) so it can reference VFXTextureRequest.
public class VFXTestTrigger : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            var req = GetComponent<VFXTextureRequest>();
            if (req != null)
                req.RequestTexture(
                    "photorealistic mountain texture",
                    tex => Debug.Log("Texture applied!"),
                    err => Debug.LogError(err)
                );
        }
    }
}
