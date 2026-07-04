using UnityEngine;

namespace Rise
{
    [CreateAssetMenu(fileName = "AudioCue", menuName = "Rise/Audio/Audio Cue")]
    public sealed class AudioCue : ScriptableObject
    {
        [SerializeField] private AudioCueId cueId = AudioCueId.None;
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private bool loop;
        [SerializeField] private bool spatialized = true;
        [SerializeField] private float baseVolume = 1f;
        [SerializeField] private Vector2 randomVolumeRange = Vector2.one;
        [SerializeField] private Vector2 randomPitchRange = Vector2.one;
        [SerializeField] private float minDistance = 2f;
        [SerializeField] private float maxDistance = 12f;
        [SerializeField] private int priority = 128;
        [SerializeField] private float cooldown = 0.05f;

        public AudioCueId CueId => cueId;
        public AudioClip[] Clips => clips;
        public bool Loop => loop;
        public bool Spatialized => spatialized;
        public float BaseVolume => baseVolume;
        public Vector2 RandomVolumeRange => randomVolumeRange;
        public Vector2 RandomPitchRange => randomPitchRange;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;
        public int Priority => priority;
        public float Cooldown => cooldown;

        public bool TryPickClip(out AudioClip clip)
        {
            clip = null;
            if (clips == null || clips.Length == 0)
            {
                return false;
            }

            clip = clips[Random.Range(0, clips.Length)];
            return clip != null;
        }
    }
}
