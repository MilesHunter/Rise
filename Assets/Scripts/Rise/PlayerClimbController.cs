using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Rise
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(PlayerVitals))]
    [RequireComponent(typeof(ToolController))]
    public sealed class PlayerClimbController : MonoBehaviour
    {
        [SerializeField] private float holdReach = 2.75f;
        [SerializeField] private float cursorHoldSelectRadius = 0.85f;
        [SerializeField] private float holdSpringStrength = 55f;
        [SerializeField] private float holdSpringDamping = 7f;
        [SerializeField] private float movePlaneZ = 0f;
        [SerializeField] private float kickForce = 7f;
        [SerializeField] private float fallY = -4f;
        [SerializeField] private float restDuration = 1.15f;
        [SerializeField] private float surfaceGrabTolerance = 0.9f;

        private Rigidbody body;
        private Camera mainCamera;
        private RestPoint currentRestPoint;
        private GoalPoint currentGoalPoint;
        private Vector3 checkpoint;
        private Coroutine restRoutine;
        private bool hasWon;

        public HandState LeftHand { get; } = new HandState { DisplayName = "Left", IsLeft = true, LocalAnchorOffset = new Vector3(-0.45f, 0.55f, 0f) };
        public HandState RightHand { get; } = new HandState { DisplayName = "Right", IsLeft = false, LocalAnchorOffset = new Vector3(0.45f, 0.55f, 0f) };
        public PlayerVitals Vitals { get; private set; }
        public ToolController Tools { get; private set; }
        public string CurrentPrompt { get; private set; }
        public Vector3 CursorWorld { get; private set; }
        public Vector3 BodyVelocity => body != null ? body.linearVelocity : Vector3.zero;
        public bool HasWon => hasWon;
        public float LeftKickVisual { get; private set; }
        public float RightKickVisual { get; private set; }

        public void Initialize(Camera sceneCamera, Vector3 spawnPoint, Transform generatedRoot)
        {
            body = GetComponent<Rigidbody>();
            mainCamera = sceneCamera;
            Vitals = GetComponent<PlayerVitals>();
            Tools = GetComponent<ToolController>();
            Tools.Initialize(this, generatedRoot);
            checkpoint = spawnPoint;
            transform.position = spawnPoint;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            Vitals = GetComponent<PlayerVitals>();
            Tools = GetComponent<ToolController>();
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        private void Update()
        {
            UpdateCursorWorld();
            UpdateToolInput();
            UpdateHandInput();
            UpdateKickInput();
            UpdateRestInput();
            UpdatePrompt();
            DecayKickVisuals();
        }

        private void FixedUpdate()
        {
            ConstrainToPlane();
            ApplyHoldForce(LeftHand);
            ApplyHoldForce(RightHand);
            DrainStaminaWhileHolding(LeftHand);
            DrainStaminaWhileHolding(RightHand);

            if (transform.position.y < fallY && !hasWon)
            {
                RespawnAtCheckpoint();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out RestPoint restPoint))
            {
                currentRestPoint = restPoint;
            }

            if (other.TryGetComponent(out GoalPoint goalPoint))
            {
                currentGoalPoint = goalPoint;
                hasWon = true;
                CurrentPrompt = goalPoint.PromptText;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out RestPoint restPoint) && currentRestPoint == restPoint)
            {
                currentRestPoint = null;
            }

            if (other.TryGetComponent(out GoalPoint goalPoint) && currentGoalPoint == goalPoint)
            {
                currentGoalPoint = null;
            }
        }

        private void UpdateCursorWorld()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            Vector2 mousePosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            Ray ray = mainCamera.ScreenPointToRay(mousePosition);
            Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, movePlaneZ));
            if (plane.Raycast(ray, out float enter))
            {
                CursorWorld = ray.GetPoint(enter);
            }
        }

        private void UpdateToolInput()
        {
            if (Keyboard.current != null && Keyboard.current.wKey.wasPressedThisFrame)
            {
                Tools.ToggleToolMode();
            }

            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (!Mathf.Approximately(scroll, 0f))
                {
                    Tools.CycleTool(scroll);
                }
            }
        }

        private void UpdateHandInput()
        {
            if (Mouse.current == null)
            {
                return;
            }

            HandleHand(LeftHand, Mouse.current.leftButton.wasPressedThisFrame, Mouse.current.leftButton.wasReleasedThisFrame);
            HandleHand(RightHand, Mouse.current.rightButton.wasPressedThisFrame, Mouse.current.rightButton.wasReleasedThisFrame);
        }

        private void HandleHand(HandState hand, bool pressed, bool released)
        {
            hand.WorldTarget = hand.HasHold ? hand.CurrentHold.Position : CursorWorld;

            if (pressed)
            {
                hand.IsPressed = true;
                ClimbHold hoveredHold = FindHoveredHold();
                ClimbSurface hoveredSurface = hoveredHold == null ? FindHoveredSurface(CursorWorld) : null;
                ClimbHold surfaceGrip = hoveredHold == null ? CreateSurfaceGrip(CursorWorld) : null;

                if (Tools.TryUseTool(CursorWorld, hoveredHold, hoveredSurface))
                {
                    ReleaseRuntimeHoldIfNeeded(surfaceGrip);
                    return;
                }

                TryGrabHand(hand, hoveredHold, surfaceGrip);
            }

            if (released)
            {
                hand.IsPressed = false;
                ReleaseHand(hand);
            }
        }

        private void TryGrabHand(HandState hand, ClimbHold hoveredHold, ClimbHold surfaceGrip)
        {
            ClimbHold targetHold = hoveredHold != null ? hoveredHold : surfaceGrip;
            if (targetHold == null)
            {
                hand.State = HandGrabState.Reach;
                return;
            }

            Vector3 anchor = transform.position + hand.LocalAnchorOffset;
            if (Vector3.Distance(anchor, targetHold.Position) > holdReach)
            {
                hand.State = HandGrabState.Reach;
                ReleaseRuntimeHoldIfNeeded(surfaceGrip);
                return;
            }

            float grabCost = targetHold.InitialGrabCost;
            if (!Vitals.TrySpendStamina(grabCost))
            {
                ReleaseRuntimeHoldIfNeeded(surfaceGrip);
                return;
            }

            if (surfaceGrip != null && targetHold != surfaceGrip)
            {
                ReleaseRuntimeHoldIfNeeded(surfaceGrip);
            }

            if (hand.OwnsRuntimeHold && hand.CurrentHold != null && hand.CurrentHold != targetHold)
            {
                Destroy(hand.CurrentHold.gameObject);
            }

            hand.CurrentHold = targetHold;
            hand.OwnsRuntimeHold = targetHold == surfaceGrip;
            hand.State = targetHold.ZeroStaminaHold ? HandGrabState.AssistedHold : HandGrabState.Holding;
            hand.WorldTarget = targetHold.Position;
            hand.HoldDrainTimer = 0f;
            hand.SlipCheckTimer = 0f;
        }

        private void ReleaseHand(HandState hand)
        {
            hand.State = HandGrabState.Releasing;
            if (hand.OwnsRuntimeHold && hand.CurrentHold != null)
            {
                Destroy(hand.CurrentHold.gameObject);
            }
            hand.CurrentHold = null;
            hand.OwnsRuntimeHold = false;
            hand.HoldDrainTimer = 0f;
            hand.SlipCheckTimer = 0f;
            hand.WorldTarget = CursorWorld;
            hand.State = HandGrabState.Idle;
        }

        private void UpdateKickInput()
        {
            if (Keyboard.current == null || restRoutine != null || hasWon)
            {
                return;
            }

            bool canKick = LeftHand.HasHold || RightHand.HasHold;
            if (!canKick)
            {
                return;
            }

            if (Keyboard.current.qKey.wasPressedThisFrame && Vitals.TrySpendStamina(4f))
            {
                body.AddForce(new Vector3(-1.2f, 2.5f, 0f).normalized * kickForce, ForceMode.Impulse);
                LeftKickVisual = 1f;
            }

            if (Keyboard.current.eKey.wasPressedThisFrame && Vitals.TrySpendStamina(4f))
            {
                body.AddForce(new Vector3(1.2f, 2.5f, 0f).normalized * kickForce, ForceMode.Impulse);
                RightKickVisual = 1f;
            }
        }

        private void UpdateRestInput()
        {
            if (Keyboard.current == null || currentRestPoint == null || restRoutine != null || hasWon)
            {
                return;
            }

            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                restRoutine = StartCoroutine(PerformRest(currentRestPoint));
            }
        }

        private IEnumerator PerformRest(RestPoint restPoint)
        {
            ReleaseHand(LeftHand);
            ReleaseHand(RightHand);
            body.isKinematic = true;
            CurrentPrompt = restPoint.PromptText;
            yield return new WaitForSeconds(restDuration);

            if (restPoint.RestType == RestPointType.ShortRest)
            {
                Vitals.RecoverForShortRest();
            }
            else
            {
                Vitals.RestoreAllForLongRest();
                checkpoint = transform.position;
            }

            body.isKinematic = false;
            restRoutine = null;
        }

        private void UpdatePrompt()
        {
            if (hasWon)
            {
                CurrentPrompt = currentGoalPoint != null ? currentGoalPoint.PromptText : "Summit reached";
                return;
            }

            if (restRoutine != null)
            {
                return;
            }

            if (currentRestPoint != null)
            {
                CurrentPrompt = $"{currentRestPoint.PromptText}  {currentRestPoint.DetailText}";
                return;
            }

            CurrentPrompt = Tools.ToolMode
                ? $"Tool mode: {Tools.SelectedTool}. Click a hold or wall point."
                : BuildGripPrompt();
        }

        private void ApplyHoldForce(HandState hand)
        {
            if (!hand.HasHold || restRoutine != null || hasWon)
            {
                if (!hand.HasHold)
                {
                    hand.WorldTarget = CursorWorld;
                }
                return;
            }

            Vector3 anchorWorld = transform.position + hand.LocalAnchorOffset;
            Vector3 delta = hand.CurrentHold.Position - anchorWorld;
            Vector3 velocityAtAnchor = body.GetPointVelocity(anchorWorld);
            Vector3 force = delta * holdSpringStrength - velocityAtAnchor * holdSpringDamping;
            force.z = 0f;
            body.AddForceAtPosition(force, anchorWorld, ForceMode.Acceleration);
            hand.WorldTarget = hand.CurrentHold.Position;
        }

        private void DrainStaminaWhileHolding(HandState hand)
        {
            if (!hand.HasHold || hand.CurrentHold.ZeroStaminaHold || restRoutine != null)
            {
                return;
            }

            hand.HoldDrainTimer += Time.fixedDeltaTime;
            if (hand.HoldDrainTimer < 1f)
            {
                UpdateSlip(hand);
                return;
            }

            hand.HoldDrainTimer -= 1f;
            if (!Vitals.TrySpendStamina(hand.CurrentHold.HoldDrainPerSecond))
            {
                ReleaseHand(hand);
                if (!LeftHand.HasHold && !RightHand.HasHold)
                {
                    body.AddForce(Vector3.down * 2f, ForceMode.Impulse);
                }
            }

            UpdateSlip(hand);
        }

        private void ConstrainToPlane()
        {
            Vector3 position = body.position;
            position.z = movePlaneZ;
            body.position = position;

            Vector3 velocity = body.linearVelocity;
            velocity.z = 0f;
            body.linearVelocity = velocity;
        }

        private ClimbHold FindHoveredHold()
        {
            ClimbHold best = null;
            float bestDistance = float.MaxValue;

            foreach (ClimbHold hold in ClimbHold.ActiveHolds)
            {
                if (hold == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(new Vector2(CursorWorld.x, CursorWorld.y), new Vector2(hold.Position.x, hold.Position.y));
                if (distance <= cursorHoldSelectRadius && distance < bestDistance)
                {
                    best = hold;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private ClimbHold CreateSurfaceGrip(Vector3 cursorWorld)
        {
            if (!TryFindBestSurface(cursorWorld, out ClimbSurface bestSurface, out Vector3 bestPoint))
            {
                return null;
            }

            GameObject runtimeHoldObject = new GameObject("RuntimeSurfaceGrip");
            runtimeHoldObject.transform.position = bestPoint;
            ClimbHold runtimeHold = runtimeHoldObject.AddComponent<ClimbHold>();
            runtimeHold.Configure(ClimbHoldType.Normal,
                bestSurface.AllowAnchorAttach,
                bestSurface.AllowRopeAttach,
                bestSurface.GrabCost,
                bestSurface.HoldDrainPerSecond,
                bestSurface.SlipCheckInterval,
                bestSurface.SlipChance,
                bestSurface.SurfaceLabel);
            return runtimeHold;
        }

        private ClimbSurface FindHoveredSurface(Vector3 cursorWorld)
        {
            TryFindBestSurface(cursorWorld, out ClimbSurface surface, out _);
            return surface;
        }

        private bool TryFindBestSurface(Vector3 cursorWorld, out ClimbSurface bestSurface, out Vector3 bestPoint)
        {
            bestSurface = null;
            bestPoint = Vector3.zero;
            float bestDistance = float.MaxValue;

            foreach (ClimbSurface surface in ClimbSurface.ActiveSurfaces)
            {
                if (surface == null || !surface.TryGetGripPoint(cursorWorld, out Vector3 gripPoint))
                {
                    continue;
                }

                float distance = Vector2.Distance(new Vector2(cursorWorld.x, cursorWorld.y), new Vector2(gripPoint.x, gripPoint.y));
                if (distance <= surfaceGrabTolerance && distance < bestDistance)
                {
                    bestSurface = surface;
                    bestPoint = gripPoint;
                    bestDistance = distance;
                }
            }

            return bestSurface != null;
        }

        private void ReleaseRuntimeHoldIfNeeded(ClimbHold runtimeHold)
        {
            if (runtimeHold != null && runtimeHold.gameObject != null)
            {
                Destroy(runtimeHold.gameObject);
            }
        }

        private void UpdateSlip(HandState hand)
        {
            if (!hand.HasHold || hand.CurrentHold.SlipCheckInterval <= 0f || hand.CurrentHold.SlipChance <= 0f)
            {
                return;
            }

            hand.SlipCheckTimer += Time.fixedDeltaTime;
            if (hand.SlipCheckTimer < hand.CurrentHold.SlipCheckInterval)
            {
                return;
            }

            hand.SlipCheckTimer -= hand.CurrentHold.SlipCheckInterval;
            if (Random.value <= hand.CurrentHold.SlipChance)
            {
                ReleaseHand(hand);
            }
        }

        private string BuildGripPrompt()
        {
            ClimbHold hoveredHold = FindHoveredHold();
            if (hoveredHold != null)
            {
                string slip = hoveredHold.SlipChance > 0f ? "  slippery" : string.Empty;
                return $"{hoveredHold.GripLabel}  grab {hoveredHold.InitialGrabCost:0.#}  drain {hoveredHold.HoldDrainPerSecond:0.#}/s{slip}";
            }

            if (TryFindBestSurface(CursorWorld, out ClimbSurface surface, out _))
            {
                string slip = surface.SlipChance > 0f ? "  slippery" : string.Empty;
                return $"{surface.SurfaceLabel} surface  grab {surface.GrabCost:0.#}  drain {surface.HoldDrainPerSecond:0.#}/s{slip}";
            }

            return "Climb to the next hold";
        }

        private void RespawnAtCheckpoint()
        {
            ReleaseHand(LeftHand);
            ReleaseHand(RightHand);
            body.position = checkpoint;
            body.linearVelocity = Vector3.zero;
            Vitals.ResetToCheckpoint();
        }

        private void DecayKickVisuals()
        {
            LeftKickVisual = Mathf.MoveTowards(LeftKickVisual, 0f, Time.deltaTime * 4f);
            RightKickVisual = Mathf.MoveTowards(RightKickVisual, 0f, Time.deltaTime * 4f);
        }
    }
}
