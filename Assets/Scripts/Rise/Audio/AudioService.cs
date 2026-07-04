using System.Collections.Generic;
using UnityEngine;

namespace Rise
{
    public sealed class AudioService : MonoBehaviour
    {
        private const int InitialPoolSize = 8;

        [SerializeField] private AudioCueDatabase cueDatabase;

        private readonly List<AudioSource> sourcePool = new List<AudioSource>();
        private readonly List<AudioPlaybackHandle> activeHandles = new List<AudioPlaybackHandle>();
        private readonly Dictionary<AudioCueId, float> cooldownByCue = new Dictionary<AudioCueId, float>();

        public static AudioService Instance { get; private set; }

        public static AudioService EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            AudioService existing = FindAnyObjectByType<AudioService>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject serviceObject = new GameObject("AudioService");
            return serviceObject.AddComponent<AudioService>();
        }

        public void SetCueDatabase(AudioCueDatabase database)
        {
            cueDatabase = database;
        }

        public AudioPlaybackHandle Play2D(AudioCueId cueId)
        {
            return PlayInternal(cueId, Vector3.zero, null, 0f, false);
        }

        public AudioPlaybackHandle Play3D(AudioCueId cueId, Vector3 position)
        {
            return PlayInternal(cueId, position, null, 1f, false);
        }

        public AudioPlaybackHandle PlayAttached(AudioCueId cueId, Transform target, Vector3 localOffset = default)
        {
            return PlayInternal(cueId, target != null ? target.position + localOffset : Vector3.zero, target, 1f, true, localOffset);
        }

        public void Stop(AudioPlaybackHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            handle.Stop();
            activeHandles.Remove(handle);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            WarmPool();
        }

        private void LateUpdate()
        {
            for (int i = activeHandles.Count - 1; i >= 0; i--)
            {
                AudioPlaybackHandle handle = activeHandles[i];
                if (handle == null || !handle.IsValid)
                {
                    activeHandles.RemoveAt(i);
                    continue;
                }

                handle.Sync();
                if (!handle.IsPlaying)
                {
                    activeHandles.RemoveAt(i);
                }
            }
        }

        private AudioPlaybackHandle PlayInternal(AudioCueId cueId, Vector3 position, Transform followTarget, float spatialBlendOverride, bool useFollowTarget, Vector3 localOffset = default)
        {
            if (!TryGetCue(cueId, out AudioCue cue) || !cue.TryPickClip(out AudioClip clip) || !CanPlay(cueId, cue))
            {
                return null;
            }

            AudioSource source = GetAvailableSource();
            ConfigureSource(source, cue, clip, position, spatialBlendOverride);

            AudioPlaybackHandle handle = useFollowTarget
                ? new AudioPlaybackHandle(source, followTarget, localOffset)
                : new AudioPlaybackHandle(source);

            handle.Sync();
            source.Play();
            cooldownByCue[cueId] = Time.time;

            if (cue.Loop || useFollowTarget)
            {
                activeHandles.Add(handle);
            }

            return handle;
        }

        private bool TryGetCue(AudioCueId cueId, out AudioCue cue)
        {
            cue = null;
            return cueDatabase != null && cueDatabase.TryGetCue(cueId, out cue);
        }

        private bool CanPlay(AudioCueId cueId, AudioCue cue)
        {
            if (!cooldownByCue.TryGetValue(cueId, out float lastPlayTime))
            {
                return true;
            }

            return Time.time >= lastPlayTime + cue.Cooldown;
        }

        private void ConfigureSource(AudioSource source, AudioCue cue, AudioClip clip, Vector3 position, float spatialBlendOverride)
        {
            source.gameObject.SetActive(true);
            source.transform.position = position;
            source.clip = clip;
            source.loop = cue.Loop;
            source.priority = cue.Priority;
            source.volume = cue.BaseVolume * Random.Range(cue.RandomVolumeRange.x, cue.RandomVolumeRange.y);
            source.pitch = Random.Range(cue.RandomPitchRange.x, cue.RandomPitchRange.y);
            source.spatialBlend = cue.Spatialized ? spatialBlendOverride : 0f;
            source.minDistance = cue.MinDistance;
            source.maxDistance = cue.MaxDistance;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
        }

        private AudioSource GetAvailableSource()
        {
            for (int i = 0; i < sourcePool.Count; i++)
            {
                AudioSource source = sourcePool[i];
                if (!source.isPlaying)
                {
                    return source;
                }
            }

            return CreatePooledSource(sourcePool.Count);
        }

        private void WarmPool()
        {
            if (sourcePool.Count > 0)
            {
                return;
            }

            for (int i = 0; i < InitialPoolSize; i++)
            {
                CreatePooledSource(i);
            }
        }

        private AudioSource CreatePooledSource(int index)
        {
            GameObject sourceObject = new GameObject($"AudioSource_{index}");
            sourceObject.transform.SetParent(transform, false);
            sourceObject.SetActive(false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;

            sourcePool.Add(source);
            return source;
        }
    }
}
