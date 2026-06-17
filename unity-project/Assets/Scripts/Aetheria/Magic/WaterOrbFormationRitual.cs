using System.Collections;
using Aetheria.WaterMagic;
using UnityEngine;

namespace Aetheria.Magic
{
    /// <summary>
    /// Beach ritual: point finger at water + voice intent → short "pull" from surface into a full orb in hand.
    /// Wire <see cref="BeginFormation"/> to <see cref="MagicIntentChannel"/> intent id e.g. <c>magic.water.orb</c>.
    /// </summary>
    public sealed class WaterOrbFormationRitual : MonoBehaviour
    {
        [SerializeField] MagicTargetingRay fingerRay;
        [SerializeField] LayerMask waterLayers;
        [SerializeField] Transform handAttachPoint;
        [SerializeField] WaterGatherController gather;

        [SerializeField] float formationDuration = 1.15f;
        [SerializeField] float startScale = 0.06f;
        [SerializeField] float endScale = 0.2f;
        [SerializeField] float liftFromSurface = 0.08f;

        [SerializeField] Material formationSphereMaterial;
        [SerializeField] GameObject optionalBurstFxPrefab;

        bool _busy;

        /// <summary>Runtime wiring (VR bootstrap) so the ritual can be composed without prefab authoring.</summary>
        public void SetRuntimeReferences(MagicTargetingRay ray, LayerMask water, Transform hand, WaterGatherController gatherController)
        {
            fingerRay = ray;
            waterLayers = water;
            handAttachPoint = hand;
            gather = gatherController;
        }

        /// <summary>Call from voice / Convai / UI after saying "water orb". Finger must hit water.</summary>
        public void BeginFormation()
        {
            if (_busy || gather == null || fingerRay == null || handAttachPoint == null)
                return;
            if (!fingerRay.TryGetHitOnLayer(waterLayers, out var hit))
            {
                Debug.Log("[WaterOrbFormation] Aim at the water surface (ray missed).");
                return;
            }

            StartCoroutine(CoForm(hit.point, hit.normal));
        }

        IEnumerator CoForm(Vector3 surfacePoint, Vector3 surfaceNormal)
        {
            _busy = true;
            var startPos = surfacePoint + surfaceNormal.normalized * liftFromSurface;

            if (optionalBurstFxPrefab != null)
                Instantiate(optionalBurstFxPrefab, startPos, Quaternion.LookRotation(surfaceNormal));

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "WaterOrbFormationPreview";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = startPos;
            go.transform.localScale = Vector3.one * startScale;

            if (formationSphereMaterial != null && go.TryGetComponent<Renderer>(out var ren))
                ren.sharedMaterial = formationSphereMaterial;

            float t = 0f;
            var dur = Mathf.Max(0.15f, formationDuration);
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                var u = Mathf.SmoothStep(0f, 1f, t);
                go.transform.position = Vector3.Lerp(startPos, handAttachPoint.position, u);
                var s = Mathf.Lerp(startScale, endScale, u);
                go.transform.localScale = Vector3.one * s;
                yield return null;
            }

            Destroy(go);
            gather.ForceFullOrb();
            _busy = false;
        }
    }
}
