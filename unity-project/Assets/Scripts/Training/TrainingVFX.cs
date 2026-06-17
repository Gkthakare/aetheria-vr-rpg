using UnityEngine;

namespace Aetheria.Training
{
    /// <summary>
    /// Focused VFX feedback for training: success (green), fail (red), correction (yellow).
    /// Attach to a station or use from TrainingRoomManager.
    /// </summary>
    public class TrainingVFX : MonoBehaviour
    {
        [Header("Particles")]
        [SerializeField] private ParticleSystem successParticles;
        [SerializeField] private ParticleSystem failParticles;
        [SerializeField] private ParticleSystem correctionParticles;

        [Header("Optional lights")]
        [SerializeField] private Light feedbackLight;
        [SerializeField] private float lightDuration = 0.5f;

        private float _lightOffAt;

        private void Update()
        {
            if (feedbackLight != null && Time.time > _lightOffAt && feedbackLight.enabled)
                feedbackLight.enabled = false;
        }

        public void PlaySuccess()
        {
            if (successParticles != null) successParticles.Play();
            SetFeedbackColor(Color.green);
        }

        public void PlayFail()
        {
            if (failParticles != null) failParticles.Play();
            SetFeedbackColor(Color.red);
        }

        public void PlayCorrection()
        {
            if (correctionParticles != null) correctionParticles.Play();
            SetFeedbackColor(new Color(1f, 0.9f, 0.2f));
        }

        private void SetFeedbackColor(Color c)
        {
            if (feedbackLight == null) return;
            feedbackLight.color = c;
            feedbackLight.enabled = true;
            _lightOffAt = Time.time + lightDuration;
        }
    }
}
