using UnityEngine;

namespace Aetheria.WaterMagic
{
    /// <summary>
    /// Casts a <see cref="WaterSpellDefinition"/> from the camera or hand forward.
    /// Hook <see cref="TryCastPrimary"/> to XR controller action (select / trigger).
    /// </summary>
    public sealed class WaterMagicCaster : MonoBehaviour
    {
        [SerializeField] WaterGatherController gather;
        [SerializeField] Transform castOrigin;
        [SerializeField] WaterSpellDefinition primarySpell;
        [SerializeField] AudioSource optionalAudio;
        [SerializeField] float cooldownSeconds = 0.35f;

        float _nextCastTime;

        public void TryCastPrimary()
        {
            if (primarySpell == null || gather == null || castOrigin == null)
                return;
            if (Time.time < _nextCastTime)
                return;
            if (!gather.TryConsume(primarySpell.chargeCost))
                return;

            _nextCastTime = Time.time + cooldownSeconds;

            if (primarySpell.castSound != null && optionalAudio != null)
                optionalAudio.PlayOneShot(primarySpell.castSound);

            if (primarySpell.spawnPrefab == null)
                return;

            var pos = castOrigin.position + castOrigin.forward * primarySpell.spawnForwardOffset;
            var rot = Quaternion.LookRotation(castOrigin.forward);
            var go = Instantiate(primarySpell.spawnPrefab, pos, rot);

            if (go.TryGetComponent<WaterProjectile>(out var bolt))
                bolt.Launch(castOrigin.forward);
        }
    }
}
