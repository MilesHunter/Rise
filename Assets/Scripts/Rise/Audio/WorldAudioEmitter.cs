using UnityEngine;

namespace Rise
{
    public sealed class WorldAudioEmitter : MonoBehaviour
    {
        [SerializeField] private AudioCueId cueId = AudioCueId.None;
        [SerializeField] private bool playOnEnable;
        [SerializeField] private bool followTransform = true;

        private AudioPlaybackHandle activeHandle;

        private void OnEnable()
        {
            if (!playOnEnable || cueId == AudioCueId.None)
            {
                return;
            }

            AudioService service = AudioService.EnsureExists();
            activeHandle = followTransform
                ? service.PlayAttached(cueId, transform)
                : service.Play3D(cueId, transform.position);
        }

        private void OnDisable()
        {
            if (activeHandle == null)
            {
                return;
            }

            AudioService.Instance?.Stop(activeHandle);
            activeHandle = null;
        }

        public void PlayOneShot()
        {
            if (cueId == AudioCueId.None)
            {
                return;
            }

            AudioService.EnsureExists().Play3D(cueId, transform.position);
        }
    }
}
