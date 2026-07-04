using UnityEngine;

namespace Rise
{
    [CreateAssetMenu(fileName = "SurfaceAudioProfile", menuName = "Rise/Audio/Surface Audio Profile")]
    public sealed class SurfaceAudioProfile : ScriptableObject
    {
        [SerializeField] private AudioCueId grabCue = AudioCueId.GrabSuccess;
        [SerializeField] private AudioCueId holdLoopCue = AudioCueId.HoldLoop;
        [SerializeField] private AudioCueId slipCue = AudioCueId.Slip;
        [SerializeField] private AudioCueId releaseCue = AudioCueId.None;

        public AudioCueId GrabCue => grabCue;
        public AudioCueId HoldLoopCue => holdLoopCue;
        public AudioCueId SlipCue => slipCue;
        public AudioCueId ReleaseCue => releaseCue;
    }
}
