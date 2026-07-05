using UnityEngine;

namespace Rise
{
    public sealed class AudioPlaybackHandle
    {
        private readonly AudioSource source;
        private readonly Transform followTarget;
        private readonly Vector3 localOffset;
        private readonly float baseVolume;

        public AudioPlaybackHandle(AudioSource source, AudioCueId cueId, float baseVolume, Transform followTarget = null, Vector3 localOffset = default)
        {
            this.source = source;
            CueId = cueId;
            this.baseVolume = baseVolume;
            this.followTarget = followTarget;
            this.localOffset = localOffset;
        }

        public AudioCueId CueId { get; }
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

        public void SetVolumeMultiplier(float multiplier)
        {
            if (source != null)
            {
                source.volume = baseVolume * multiplier;
            }
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
