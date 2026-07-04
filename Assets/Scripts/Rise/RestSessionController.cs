using UnityEngine;

namespace Rise
{
    public sealed class RestSessionController : MonoBehaviour
    {
        [SerializeField] private float restCameraYOffset = 1.5f;

        private PlayerClimbController climb;
        private PlayerVitals vitals;
        private RestPoint activeRestPoint;
        private Camera sceneCamera;
        private Vector3 cameraBeforeRest;
        private bool cameraSaved;

        public bool IsResting { get; private set; }
        public RestPoint ActiveRestPoint => activeRestPoint;
        public bool AllowsLargePack => IsResting && activeRestPoint != null && activeRestPoint.RestType == RestPointType.LongRest;
        public bool AllowsCooking => AllowsLargePack;

        private void Awake()
        {
            climb = GetComponent<PlayerClimbController>();
            vitals = GetComponent<PlayerVitals>();
        }

        public void BeginRest(RestPoint restPoint)
        {
            if (restPoint == null || IsResting)
            {
                return;
            }

            activeRestPoint = restPoint;
            IsResting = true;
            climb.BeginRestFreeze(restPoint);
            SaveAndMoveCamera(restPoint.transform.position);
        }

        public void FinishRest()
        {
            if (!IsResting)
            {
                return;
            }

            if (activeRestPoint.RestType == RestPointType.ShortRest)
            {
                vitals.RecoverForShortRest();
            }
            else
            {
                vitals.RestoreAllForLongRest();
                climb.SetCheckpoint(transform.position);
            }

            ExitRest();
        }

        public void ExitRest()
        {
            if (!IsResting)
            {
                return;
            }

            RestoreCamera();
            activeRestPoint = null;
            IsResting = false;
            climb.EndRestFreeze();
        }

        private void SaveAndMoveCamera(Vector3 restPosition)
        {
            sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                return;
            }

            cameraBeforeRest = sceneCamera.transform.position;
            cameraSaved = true;
            CameraFollowSideView follow = sceneCamera.GetComponent<CameraFollowSideView>();
            if (follow != null)
            {
                follow.enabled = false;
            }

            sceneCamera.transform.position = new Vector3(restPosition.x, restPosition.y + restCameraYOffset, sceneCamera.transform.position.z);
        }

        private void RestoreCamera()
        {
            if (sceneCamera == null)
            {
                return;
            }

            if (cameraSaved)
            {
                sceneCamera.transform.position = cameraBeforeRest;
            }

            CameraFollowSideView follow = sceneCamera.GetComponent<CameraFollowSideView>();
            if (follow != null)
            {
                follow.enabled = true;
            }
        }
    }
}
