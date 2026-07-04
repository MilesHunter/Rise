using UnityEngine;

namespace Rise
{
    public sealed class AudioPlaybackHandle
    {
        private readonly AudioSource source;
        private readonly Transform followTarget;
        private readonly Vector3 localOffset;

        public AudioPlaybackHandle(AudioSource source, Transform followTarget = null, Vector3 localOffset = default)
        {
            this.source = source;
            this.followTarget = followTarget;
            this.localOffset = localOffset;
        }

        public bool IsValid => source != null;
        public bool IsPlaying => source != null && source.isPlaying;

        public void Sync()
        {
            if (source == null || followTarget == null)
            {
                return;
            }

            source.transform.position = followTarget.position + localOffset;
        }

        public void Stop()
        {
            if (source == null)
            {
                return;
            }

            source.Stop();
            source.gameObject.SetActive(false);
        }
    }
}
