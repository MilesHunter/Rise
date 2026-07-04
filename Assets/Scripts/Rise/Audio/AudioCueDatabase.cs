using System.Collections.Generic;
using UnityEngine;

namespace Rise
{
    [CreateAssetMenu(fileName = "AudioCueDatabase", menuName = "Rise/Audio/Audio Cue Database")]
    public sealed class AudioCueDatabase : ScriptableObject
    {
        [SerializeField] private AudioCue[] cues;

        private Dictionary<AudioCueId, AudioCue> cache;

        public bool TryGetCue(AudioCueId cueId, out AudioCue cue)
        {
            EnsureCache();
            return cache.TryGetValue(cueId, out cue);
        }

        private void EnsureCache()
        {
            if (cache != null)
            {
                return;
            }

            cache = new Dictionary<AudioCueId, AudioCue>();
            if (cues == null)
            {
                return;
            }

            for (int i = 0; i < cues.Length; i++)
            {
                AudioCue cue = cues[i];
                if (cue == null || cue.CueId == AudioCueId.None)
                {
                    continue;
                }

                cache[cue.CueId] = cue;
            }
        }
    }
}
