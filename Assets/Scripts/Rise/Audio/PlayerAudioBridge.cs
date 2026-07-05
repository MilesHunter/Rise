using UnityEngine;

namespace Rise
{
    [RequireComponent(typeof(PlayerClimbController))]
    public sealed class PlayerAudioBridge : MonoBehaviour
    {
        [SerializeField] private Transform breathingAnchor;
        [SerializeField] private Vector3 breathingOffset = new Vector3(0f, 1.2f, 0f);

        private PlayerClimbController controller;
        private ToolController toolController;
        private AudioPlaybackHandle breathingHandle;
        private AudioCueId activeBreathingCue = AudioCueId.None;

        private void Awake()
        {
            controller = GetComponent<PlayerClimbController>();
            toolController = GetComponent<ToolController>();
            if (breathingAnchor == null)
            {
                breathingAnchor = transform;
            }
        }

        private void OnEnable()
        {
            if (controller == null)
            {
                controller = GetComponent<PlayerClimbController>();
            }

            if (toolController == null)
            {
                toolController = GetComponent<ToolController>();
            }

            if (controller != null)
            {
                controller.GrabSuccess += OnGrabSuccess;
                controller.GrabFailed += OnGrabFailed;
                controller.SlipOccurred += OnSlipOccurred;
                controller.KickPerformed += OnKickPerformed;
                controller.RestStarted += OnRestStarted;
                controller.RestCompleted += OnRestCompleted;
                controller.Respawned += OnRespawned;
                controller.GoalReached += OnGoalReached;
                controller.HoldReleased += OnHoldReleased;
                controller.BreathingStateChanged += OnBreathingStateChanged;
            }

            if (toolController != null)
            {
                toolController.ToolPlaced += OnToolPlaced;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.GrabSuccess -= OnGrabSuccess;
                controller.GrabFailed -= OnGrabFailed;
                controller.SlipOccurred -= OnSlipOccurred;
                controller.KickPerformed -= OnKickPerformed;
                controller.RestStarted -= OnRestStarted;
                controller.RestCompleted -= OnRestCompleted;
                controller.Respawned -= OnRespawned;
                controller.GoalReached -= OnGoalReached;
                controller.HoldReleased -= OnHoldReleased;
                controller.BreathingStateChanged -= OnBreathingStateChanged;
            }

            if (toolController != null)
            {
                toolController.ToolPlaced -= OnToolPlaced;
            }
        }

        private void OnGrabSuccess(HandState hand, ClimbHold hold, ClimbSurface surface, Vector3 point)
        {
            AudioCueId cueId = surface != null && surface.SurfaceAudio != null
                ? surface.SurfaceAudio.GrabCue
                : AudioCueId.GrabSuccess;

            AudioService.EnsureExists().Play3D(cueId, point);
        }

        private void OnGrabFailed(HandState hand, Vector3 point)
        {
            AudioService.EnsureExists().Play3D(AudioCueId.GrabFail, point);
        }

        private void OnSlipOccurred(HandState hand, ClimbHold hold, ClimbSurface surface, Vector3 point)
        {
            AudioCueId cueId = surface != null && surface.SurfaceAudio != null
                ? surface.SurfaceAudio.SlipCue
                : AudioCueId.Slip;

            AudioService.EnsureExists().Play3D(cueId, point);
        }

        private void OnKickPerformed(bool isLeft, Vector3 origin)
        {
            AudioService.EnsureExists().Play3D(isLeft ? AudioCueId.KickLeft : AudioCueId.KickRight, origin);
        }

        private void OnRestStarted(RestPointType type, Vector3 origin)
        {
            AudioService.EnsureExists().Play3D(type == RestPointType.ShortRest ? AudioCueId.ShortRestStart : AudioCueId.LongRestStart, origin);
        }

        private void OnRestCompleted(RestPointType type, Vector3 origin)
        {
            AudioService.EnsureExists().Play3D(type == RestPointType.ShortRest ? AudioCueId.ShortRestComplete : AudioCueId.LongRestComplete, origin);
        }

        private void OnRespawned(Vector3 origin)
        {
            AudioService.EnsureExists().Play3D(AudioCueId.FallRespawn, origin);
        }

        private void OnGoalReached(Vector3 origin)
        {
            AudioService.EnsureExists().Play3D(AudioCueId.GoalReached, origin);
        }

        private void OnHoldReleased(HandState hand, ClimbHold hold, ClimbSurface surface, Vector3 point)
        {
            if (surface == null || surface.SurfaceAudio == null || surface.SurfaceAudio.ReleaseCue == AudioCueId.None)
            {
                return;
            }

            AudioService.EnsureExists().Play3D(surface.SurfaceAudio.ReleaseCue, point);
        }

        private void OnBreathingStateChanged(AudioCueId cueId)
        {
            AudioService service = AudioService.EnsureExists();

            if (activeBreathingCue == cueId)
            {
                return;
            }

            if (breathingHandle != null)
            {
                service.Stop(breathingHandle);
                breathingHandle = null;
            }

            activeBreathingCue = cueId;
            if (cueId == AudioCueId.None)
            {
                return;
            }

            breathingHandle = service.PlayAttached(cueId, breathingAnchor, breathingOffset);
        }

        private void OnToolPlaced(ToolKind toolKind, Vector3 position)
        {
            AudioCueId cueId = toolKind == ToolKind.Anchor ? AudioCueId.AnchorPlace : AudioCueId.RopePlace;
            AudioService.EnsureExists().Play3D(cueId, position);
        }
    }
}
