using System.Collections;
using UnityEngine;

namespace Rise
{
    public sealed class RestSessionController : MonoBehaviour
    {
        [SerializeField] private float restCameraYOffset = 1.5f;
        [SerializeField] private float enterCameraDuration = 0.6f;
        [SerializeField] private float exitCameraDuration = 0.4f;

        private PlayerClimbController climb;
        private PlayerVitals vitals;
        private CharacterPresentation presentation;
        private RestPoint activeRestPoint;
        private Camera sceneCamera;
        private Vector3 cameraBeforeRest;
        private Coroutine cameraRoutine;
        private bool cameraSaved;

        public bool IsResting { get; private set; }
        public RestPoint ActiveRestPoint => activeRestPoint;
        public bool AllowsLargePack => IsResting && activeRestPoint != null && activeRestPoint.RestType == RestPointType.LongRest;
        public bool AllowsCooking => AllowsLargePack;
        public string LastRestMessage { get; private set; }

        private void Awake()
        {
            climb = GetComponent<PlayerClimbController>();
            vitals = GetComponent<PlayerVitals>();
            presentation = GetComponentInChildren<CharacterPresentation>(true);
        }

        public void BeginRest(RestPoint restPoint)
        {
            if (restPoint == null || IsResting)
            {
                return;
            }

            activeRestPoint = restPoint;
            IsResting = true;
            LastRestMessage = $"{FormatRestType(restPoint.RestType)} started";
            climb.BeginRestFreeze(restPoint);
            SetPresentationVisible(false);
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
                LastRestMessage = "Short rest recovered stamina";
            }
            else
            {
                vitals.RestoreAllForLongRest();
                climb.SetCheckpoint(activeRestPoint.transform.position);
                LastRestMessage = "Long rest restored resources and set checkpoint";
            }

            climb.ReportRestCompleted(activeRestPoint.RestType, activeRestPoint.transform.position);
            ExitRest();
        }

        public string BuildRestPreview()
        {
            if (!IsResting || activeRestPoint == null)
            {
                return "No active rest";
            }

            if (activeRestPoint.RestType == RestPointType.LongRest)
            {
                return "Long rest: +30 health +35 hunger +25 warmth +30 sanity, full stamina, checkpoint";
            }

            return "Short rest: +8 health +12 hunger +8 warmth +10 sanity, +35 stamina";
        }

        public void ExitRest()
        {
            if (!IsResting)
            {
                return;
            }

            activeRestPoint = null;
            IsResting = false;
            RestoreCamera();
            SetPresentationVisible(true);
            climb.EndRestFreeze();
        }

        private void SetPresentationVisible(bool visible)
        {
            if (presentation == null)
            {
                presentation = GetComponentInChildren<CharacterPresentation>(true);
            }

            if (presentation != null)
            {
                presentation.gameObject.SetActive(visible);
            }
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

            Vector3 target = new Vector3(restPosition.x, restPosition.y + restCameraYOffset, sceneCamera.transform.position.z);
            StartCameraMove(target, enterCameraDuration, enableFollowWhenDone: false);
        }

        private void RestoreCamera()
        {
            if (sceneCamera == null)
            {
                return;
            }

            if (cameraSaved)
            {
                StartCameraMove(cameraBeforeRest, exitCameraDuration, enableFollowWhenDone: true);
                return;
            }

            CameraFollowSideView follow = sceneCamera.GetComponent<CameraFollowSideView>();
            if (follow != null)
            {
                follow.enabled = true;
            }
        }

        private void StartCameraMove(Vector3 target, float duration, bool enableFollowWhenDone)
        {
            if (sceneCamera == null)
            {
                return;
            }

            if (cameraRoutine != null)
            {
                StopCoroutine(cameraRoutine);
            }

            cameraRoutine = StartCoroutine(MoveCamera(sceneCamera.transform.position, target, Mathf.Max(0.01f, duration), enableFollowWhenDone));
        }

        private IEnumerator MoveCamera(Vector3 start, Vector3 target, float duration, bool enableFollowWhenDone)
        {
            float elapsed = 0f;
            while (elapsed < duration && sceneCamera != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                sceneCamera.transform.position = Vector3.Lerp(start, target, t);
                yield return null;
            }

            if (sceneCamera != null)
            {
                sceneCamera.transform.position = target;
                if (enableFollowWhenDone)
                {
                    CameraFollowSideView follow = sceneCamera.GetComponent<CameraFollowSideView>();
                    if (follow != null)
                    {
                        follow.enabled = true;
                    }
                }
            }

            cameraRoutine = null;
        }

        private static string FormatRestType(RestPointType restType)
        {
            return restType == RestPointType.LongRest ? "Long rest" : "Short rest";
        }
    }
}
