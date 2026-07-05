using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Rise
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerVitals))]
    [RequireComponent(typeof(RestSessionController))]
    public sealed class PlayerDebugHotkeys : MonoBehaviour
    {
        [SerializeField] private bool enableDebugHotkeys = true;
        [SerializeField] private float resourceDropAmount = 30f;

        private PlayerVitals vitals;
        private RestSessionController restSession;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureOnLoadedPlayer()
        {
            PlayerClimbController climb = FindAnyObjectByType<PlayerClimbController>();
            if (climb != null && climb.GetComponent<PlayerDebugHotkeys>() == null)
            {
                climb.gameObject.AddComponent<PlayerDebugHotkeys>();
            }
        }

        private void Awake()
        {
            vitals = GetComponent<PlayerVitals>();
            restSession = GetComponent<RestSessionController>();
        }

        private void Update()
        {
            if (!enableDebugHotkeys || Keyboard.current == null)
            {
                return;
            }

            if (WasPressed(Keyboard.current.digit1Key, Keyboard.current.numpad1Key))
            {
                BeginNearestRest();
            }
            else if (WasPressed(Keyboard.current.digit6Key, Keyboard.current.numpad6Key))
            {
                vitals.ModifyHealth(-resourceDropAmount);
            }
            else if (WasPressed(Keyboard.current.digit7Key, Keyboard.current.numpad7Key))
            {
                vitals.ModifyHunger(-resourceDropAmount);
            }
            else if (WasPressed(Keyboard.current.digit8Key, Keyboard.current.numpad8Key))
            {
                vitals.ModifyWarmth(-resourceDropAmount);
            }
            else if (WasPressed(Keyboard.current.digit9Key, Keyboard.current.numpad9Key))
            {
                vitals.ModifySanity(-resourceDropAmount);
            }
        }

        private void BeginNearestRest()
        {
            if (restSession.IsResting)
            {
                return;
            }

            RestPoint nearest = FindNearestRestPoint();
            if (nearest != null)
            {
                restSession.BeginRest(nearest);
            }
        }

        private RestPoint FindNearestRestPoint()
        {
            RestPoint[] restPoints = FindObjectsByType<RestPoint>(FindObjectsInactive.Exclude);
            RestPoint nearest = null;
            float nearestDistance = float.PositiveInfinity;
            Vector3 origin = transform.position;

            for (int i = 0; i < restPoints.Length; i++)
            {
                float distance = (restPoints[i].transform.position - origin).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = restPoints[i];
                }
            }

            return nearest;
        }

        private static bool WasPressed(KeyControl topRowKey, KeyControl numpadKey)
        {
            return (topRowKey != null && topRowKey.wasPressedThisFrame) ||
                   (numpadKey != null && numpadKey.wasPressedThisFrame);
        }
    }
}
