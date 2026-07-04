using System.Collections;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Rise
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(PlayerVitals))]
    [RequireComponent(typeof(ToolController))]
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class PlayerClimbController : MonoBehaviour
    {
        [SerializeField] private float holdReach = 3.25f;
        [SerializeField] private float holdSpringStrength = 55f;
        [SerializeField] private float holdSpringDamping = 7f;
        [SerializeField] private float mouseDeltaWorldScale = 0.015f;
        [SerializeField] private float bodyDriveReturnSpeed = 4f;
        [SerializeField] private float maxBodyDriveOffset = 1.8f;
        [SerializeField] private float movePlaneZ = 0f;
        [SerializeField] private float kickForce = 7f;
        [SerializeField] private float fallY = -4f;
        [SerializeField] private float restDuration = 1.15f;
        [SerializeField] private float surfaceGrabTolerance = 0.9f;
        [SerializeField] private float handReachLimit = 3.25f;
        [SerializeField] private float freeHandReachWhenOtherHandHeld = 6f;
        [SerializeField] private Vector3 leftCursorHandOffset = new Vector3(-0.48f, 0.12f, 0f);
        [SerializeField] private Vector3 rightCursorHandOffset = new Vector3(0.48f, 0.12f, 0f);
        [SerializeField] private Vector3 leftFreeHandRestOffset = new Vector3(-0.75f, 0.25f, 0f);
        [SerializeField] private Vector3 rightFreeHandRestOffset = new Vector3(0.75f, 0.25f, 0f);
        [SerializeField] private Vector3 leftFootSupportOffset = new Vector3(-0.22f, -0.95f, 0f);
        [SerializeField] private Vector3 rightFootSupportOffset = new Vector3(0.22f, -0.95f, 0f);
        [SerializeField] private float footSupportHorizontalReach = 0.55f;
        [SerializeField] private float footSupportVerticalReach = 0.75f;
        [SerializeField] private float footSupportHeight = 0.06f;
        [SerializeField] private float footSupportSpringStrength = 75f;
        [SerializeField] private float footSupportDamping = 11f;
        [SerializeField] private float maxFootSupportAcceleration = 28f;

        private Rigidbody body;
        private Camera mainCamera;
        private RestPoint currentRestPoint;
        private GoalPoint currentGoalPoint;
        private Vector3 checkpoint;
        private Vector3 bodyDriveOffset;
        private Coroutine restRoutine;
        private bool hasWon;
        private bool initialized;
        private bool restFrozen;
        private AudioCueId activeBreathingCue;
        private bool cursorLocked;
        private RestSessionController restSession;

        public HandState LeftHand { get; } = new HandState { DisplayName = "Left", IsLeft = true, LocalAnchorOffset = new Vector3(-0.45f, 0.55f, 0f) };
        public HandState RightHand { get; } = new HandState { DisplayName = "Right", IsLeft = false, LocalAnchorOffset = new Vector3(0.45f, 0.55f, 0f) };
        public PlayerVitals Vitals { get; private set; }
        public ToolController Tools { get; private set; }
        public PlayerInventory Inventory { get; private set; }
        public string CurrentPrompt { get; private set; }
        public Vector3 CursorWorld { get; private set; }
        public Vector3 BodyVelocity => body != null ? body.linearVelocity : Vector3.zero;
        public bool HasWon => hasWon;
        public float LeftKickVisual { get; private set; }
        public float RightKickVisual { get; private set; }
        public float HandReachLimit => handReachLimit;
        public event Action<HandState, ClimbHold, ClimbSurface, Vector3> GrabSuccess;
        public event Action<HandState, Vector3> GrabFailed;
        public event Action<HandState, ClimbHold, ClimbSurface, Vector3> HoldReleased;
        public event Action<HandState, ClimbHold, ClimbSurface, Vector3> SlipOccurred;
        public event Action<bool, Vector3> KickPerformed;
        public event Action<RestPointType, Vector3> RestStarted;
        public event Action<RestPointType, Vector3> RestCompleted;
        public event Action<Vector3> Respawned;
        public event Action<Vector3> GoalReached;
        public event Action<AudioCueId> BreathingStateChanged;

        public void Initialize(Camera sceneCamera, Vector3 spawnPoint, Transform generatedRoot)
        {
            transform.position = spawnPoint;
            mainCamera = sceneCamera;
            EnsureInitialized(generatedRoot);
            checkpoint = spawnPoint;
            initialized = true;
        }

        private void Awake()
        {
            EnsureInitialized(transform.parent != null ? transform.parent : transform);
            body.constraints = RigidbodyConstraints.FreezePositionZ |
                               RigidbodyConstraints.FreezeRotationX |
                               RigidbodyConstraints.FreezeRotationY |
                               RigidbodyConstraints.FreezeRotationZ;
        }

        private void OnEnable()
        {
            SetCursorLock(true);
        }

        private void OnDisable()
        {
            SetCursorLock(false);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            SetCursorLock(hasFocus);
        }

        private void Update()
        {
            UpdateCursorWorld();
            if (restFrozen)
            {
                UpdateBreathingState();
                Tools.UpdateRuntime();
                return;
            }

            UpdateMouseDrivenTargets();
            UpdateToolInput();
            UpdateHandInput();
            UpdateKickInput();
            UpdateRestInput();
            UpdatePrompt();
            DecayKickVisuals();
            UpdateBreathingState();
            Tools.UpdateRuntime();
        }

        private void FixedUpdate()
        {
            ConstrainToPlane();
            ApplyHoldForce(LeftHand);
            ApplyHoldForce(RightHand);
            ApplyFootSupport();
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
                GoalReached?.Invoke(goalPoint.transform.position);
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

            bool shouldLockCursor = Tools == null || !Tools.IsPointerToolMode;
            if (cursorLocked != shouldLockCursor)
            {
                SetCursorLock(shouldLockCursor);
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

            if (Tools == null || Tools.ActiveRopeHand != LeftHand)
            {
                HandleHand(LeftHand, Mouse.current.leftButton.wasPressedThisFrame, Mouse.current.leftButton.wasReleasedThisFrame);
            }
            HandleHand(RightHand, Mouse.current.rightButton.wasPressedThisFrame, Mouse.current.rightButton.wasReleasedThisFrame);
        }

        private void HandleHand(HandState hand, bool pressed, bool released)
        {
            if (Tools != null && Tools.IsHandReservedForRope(hand) && !(Tools.IsPointerToolMode && hand == RightHand))
            {
                return;
            }

            if (Tools != null && Tools.IsPointerToolMode && hand == RightHand)
            {
                HandleRopeHand(hand, pressed, released);
                return;
            }

            if (hand.HasHold)
            {
                hand.WorldTarget = GetHoldTarget(hand);
            }
            else
            {
                hand.WorldTarget = GetReachLimitedTarget(hand, hand.WorldTarget);
            }

            if (pressed)
            {
                hand.IsPressed = true;
                Vector3 targetWorld = GetHandGrabProbeWorld(hand);
                ClimbHold hoveredHold = FindHoveredHold(targetWorld);
                ClimbSurface hoveredSurface = FindHoveredSurface(targetWorld);
                ClimbHold surfaceGrip = hoveredSurface != null ? CreateSurfaceGrip(targetWorld) : null;

                if (Tools != null && Tools.TryUseTool(targetWorld, hoveredHold, hoveredSurface))
                {
                    ReleaseRuntimeHoldIfNeeded(surfaceGrip);
                    return;
                }

                TryGrabHand(hand, surfaceGrip == null ? hoveredHold : null, surfaceGrip);
            }

            if (released)
            {
                hand.IsPressed = false;
                ReleaseHand(hand);
            }
        }

        private void HandleRopeHand(HandState hand, bool pressed, bool released)
        {
            hand.WorldTarget = GetReachLimitedTarget(hand, CursorWorld);

            if (pressed)
            {
                hand.IsPressed = true;
                ClimbHold hoveredHold = FindHoveredHold(CursorWorld);
                ClimbSurface hoveredSurface = hoveredHold == null ? FindHoveredSurface(CursorWorld) : null;

                if (Tools.TryFireRope(hand, CursorWorld, hoveredHold, hoveredSurface))
                {
                    HandState ropeHand = Tools.ActiveRopeHand;
                    ClimbHold ropeHold = ropeHand != null ? Tools.GetRopeHold(ropeHand) : null;
                    if (ropeHold != null)
                    {
                        ReleaseHand(ropeHand);
                        ropeHand.CurrentHold = ropeHold;
                        ropeHand.OwnsRuntimeHold = false;
                        ropeHand.State = HandGrabState.AssistedHold;
                        ropeHand.WorldTarget = GetHoldTarget(ropeHand);
                        ropeHand.HoldDrainTimer = 0f;
                        ropeHand.SlipCheckTimer = 0f;
                        GrabSuccess?.Invoke(ropeHand, ropeHold, FindSurfaceForPoint(ropeHold.Position), ropeHold.Position);
                    }
                }
                else
                {
                    GrabFailed?.Invoke(hand, hand.WorldTarget);
                }
            }

            if (released)
            {
                hand.IsPressed = false;
                HandState ropeHand = Tools.ActiveRopeHand;
                if (ropeHand != null && Tools.HasActiveRope(ropeHand))
                {
                    Tools.ReleaseRope(ropeHand);
                    ReleaseHand(ropeHand);
                }
            }
        }

        private void TryGrabHand(HandState hand, ClimbHold hoveredHold, ClimbHold surfaceGrip)
        {
            ClimbHold targetHold = hoveredHold != null ? hoveredHold : surfaceGrip;
            ClimbSurface targetSurface = hoveredHold == null ? FindSurfaceForPoint(targetHold != null ? targetHold.Position : hand.WorldTarget) : null;
            if (targetHold == null)
            {
                hand.State = HandGrabState.Reach;
                GrabFailed?.Invoke(hand, GetHandGrabProbeWorld(hand));
                return;
            }

            Vector3 anchor = transform.position + hand.LocalAnchorOffset;
            if (Vector3.Distance(anchor, targetHold.Position) > GetHoldReach(hand))
            {
                hand.State = HandGrabState.Reach;
                ReleaseRuntimeHoldIfNeeded(surfaceGrip);
                GrabFailed?.Invoke(hand, targetHold.Position);
                return;
            }

            float grabCost = targetHold.InitialGrabCost;
            if (!Vitals.TrySpendStamina(grabCost))
            {
                ReleaseRuntimeHoldIfNeeded(surfaceGrip);
                GrabFailed?.Invoke(hand, targetHold.Position);
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
            hand.WorldTarget = GetHoldTarget(hand);
            hand.HoldDrainTimer = 0f;
            hand.SlipCheckTimer = 0f;
            GrabSuccess?.Invoke(hand, targetHold, targetSurface, targetHold.Position);
        }

        private void ReleaseHand(HandState hand)
        {
            hand.State = HandGrabState.Releasing;
            Vector3 releasedTarget = hand.CurrentHold != null ? hand.CurrentHold.Position : hand.WorldTarget;
            ClimbHold releasedHold = hand.CurrentHold;
            ClimbSurface releasedSurface = FindSurfaceForPoint(releasedTarget);
            if (hand.OwnsRuntimeHold && hand.CurrentHold != null)
            {
                Destroy(hand.CurrentHold.gameObject);
            }
            hand.CurrentHold = null;
            hand.OwnsRuntimeHold = false;
            hand.HoldDrainTimer = 0f;
            hand.SlipCheckTimer = 0f;
            hand.WorldTarget = GetReachLimitedTarget(hand, releasedTarget);
            hand.State = HandGrabState.Idle;
            HoldReleased?.Invoke(hand, releasedHold, releasedSurface, releasedTarget);
        }

        private void UpdateKickInput()
        {
            if (Keyboard.current == null || IsRestLocked || hasWon)
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
                KickPerformed?.Invoke(true, transform.position + new Vector3(-0.35f, 0.25f, 0f));
            }

            if (Keyboard.current.eKey.wasPressedThisFrame && Vitals.TrySpendStamina(4f))
            {
                body.AddForce(new Vector3(1.2f, 2.5f, 0f).normalized * kickForce, ForceMode.Impulse);
                RightKickVisual = 1f;
                KickPerformed?.Invoke(false, transform.position + new Vector3(0.35f, 0.25f, 0f));
            }
        }

        private void UpdateRestInput()
        {
            if (Keyboard.current == null || currentRestPoint == null || IsRestLocked || hasWon)
            {
                return;
            }

            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                if (restSession != null)
                {
                    restSession.BeginRest(currentRestPoint);
                    return;
                }

                restRoutine = StartCoroutine(PerformRest(currentRestPoint));
            }
        }

        private IEnumerator PerformRest(RestPoint restPoint)
        {
            ReleaseHand(LeftHand);
            ReleaseHand(RightHand);
            body.isKinematic = true;
            CurrentPrompt = restPoint.PromptText;
            RestStarted?.Invoke(restPoint.RestType, restPoint.transform.position);
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
            RestCompleted?.Invoke(restPoint.RestType, restPoint.transform.position);
        }

        private void UpdatePrompt()
        {
            if (hasWon)
            {
                CurrentPrompt = currentGoalPoint != null ? currentGoalPoint.PromptText : "Summit reached";
                return;
            }

            if (IsRestLocked)
            {
                return;
            }

            if (currentRestPoint != null)
            {
                CurrentPrompt = $"{currentRestPoint.PromptText}  {currentRestPoint.DetailText}";
                return;
            }

            CurrentPrompt = Tools.ToolMode
                ? $"Tool mode: {Tools.SelectedTool}. {Tools.GetToolPromptSuffix()}"
                : BuildGripPrompt();
        }

        private void ApplyHoldForce(HandState hand)
        {
            if (!hand.HasHold || IsRestLocked || hasWon)
            {
                if (!hand.HasHold)
                {
                    hand.WorldTarget = GetReachLimitedTarget(hand, hand.WorldTarget);
                }
                return;
            }

            Vector3 anchorWorld = GetHoldForceAnchorWorld(hand);
            Vector3 desiredAnchorWorld = GetDesiredAnchorWorld(hand);
            Vector3 delta = desiredAnchorWorld - anchorWorld;
            Vector3 velocityAtAnchor = body.GetPointVelocity(anchorWorld);
            Vector3 force = delta * holdSpringStrength - velocityAtAnchor * holdSpringDamping;
            force.z = 0f;
            body.AddForceAtPosition(force, anchorWorld, ForceMode.Acceleration);
            hand.WorldTarget = GetHoldTarget(hand);
        }

        private void DrainStaminaWhileHolding(HandState hand)
        {
            if (!hand.HasHold || hand.CurrentHold.ZeroStaminaHold || IsRestLocked)
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

        private void ApplyFootSupport()
        {
            if (IsRestLocked || hasWon || body.isKinematic)
            {
                return;
            }

            bool leftSupported = TryApplyFootSupport(leftFootSupportOffset);
            bool rightSupported = TryApplyFootSupport(rightFootSupportOffset);
            if (!leftSupported && !rightSupported)
            {
                return;
            }
        }

        private bool TryApplyFootSupport(Vector3 localFootOffset)
        {
            Vector3 footWorld = transform.position + localFootOffset;
            footWorld.z = movePlaneZ;

            if (!TryFindBestFootSupport(footWorld, out Vector3 supportPoint))
            {
                return false;
            }

            float desiredFootY = supportPoint.y + footSupportHeight;
            float compression = desiredFootY - footWorld.y;
            if (compression <= 0f)
            {
                return true;
            }

            float downwardVelocity = Mathf.Max(0f, -body.GetPointVelocity(footWorld).y);
            float upwardAcceleration = compression * footSupportSpringStrength + downwardVelocity * footSupportDamping;
            upwardAcceleration = Mathf.Min(upwardAcceleration, maxFootSupportAcceleration);
            body.AddForce(Vector3.up * upwardAcceleration, ForceMode.Acceleration);
            return true;
        }

        private bool TryFindBestFootSupport(Vector3 footWorld, out Vector3 bestPoint)
        {
            bestPoint = Vector3.zero;
            float bestDistance = float.MaxValue;

            foreach (ClimbSurface surface in ClimbSurface.ActiveSurfaces)
            {
                if (surface == null || !surface.TryGetSupportPoint(footWorld, footSupportHorizontalReach, footSupportVerticalReach, out Vector3 supportPoint))
                {
                    continue;
                }

                float distance = Vector2.Distance(new Vector2(footWorld.x, footWorld.y), new Vector2(supportPoint.x, supportPoint.y));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPoint = supportPoint;
                }
            }

            return bestDistance < float.MaxValue;
        }

        private void ConstrainToPlane()
        {
            Vector3 position = body.position;
            position.z = movePlaneZ;
            body.position = position;

            if (body.isKinematic)
            {
                return;
            }

            Vector3 velocity = body.linearVelocity;
            velocity.z = 0f;
            body.linearVelocity = velocity;

            Vector3 angularVelocity = body.angularVelocity;
            angularVelocity.x = 0f;
            angularVelocity.y = 0f;
            angularVelocity.z = 0f;
            body.angularVelocity = angularVelocity;
        }

        private void UpdateMouseDrivenTargets()
        {
            Vector3 worldDelta = Vector3.zero;
            bool pointerMode = Tools != null && Tools.IsPointerToolMode;
            if (Mouse.current != null && !pointerMode)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                worldDelta = new Vector3(mouseDelta.x, mouseDelta.y, 0f) * mouseDeltaWorldScale;
            }

            bool heldHandDriveRequested = false;
            if (worldDelta.sqrMagnitude > 0.000001f)
            {
                heldHandDriveRequested |= ApplyMouseDeltaToHand(LeftHand, worldDelta);
                heldHandDriveRequested |= ApplyMouseDeltaToHand(RightHand, worldDelta);
                if (heldHandDriveRequested)
                {
                    bodyDriveOffset += worldDelta;
                    bodyDriveOffset = Vector3.ClampMagnitude(bodyDriveOffset, maxBodyDriveOffset);
                    bodyDriveOffset.z = 0f;
                }
            }

            bool hasAnyHold = LeftHand.HasHold || RightHand.HasHold;
            if (!heldHandDriveRequested && hasAnyHold)
            {
                bodyDriveOffset = Vector3.MoveTowards(bodyDriveOffset, Vector3.zero, bodyDriveReturnSpeed * Time.deltaTime);
            }

            if (!hasAnyHold)
            {
                bodyDriveOffset = Vector3.zero;
            }

            if (LeftHand.HasHold)
            {
                LeftHand.WorldTarget = GetHoldTarget(LeftHand);
            }
            else
            {
                LeftHand.WorldTarget = UpdateFreeHandTarget(LeftHand, pointerMode);
            }

            if (RightHand.HasHold)
            {
                RightHand.WorldTarget = GetHoldTarget(RightHand);
            }
            else
            {
                RightHand.WorldTarget = UpdateFreeHandTarget(RightHand, pointerMode);
            }
        }

        private Vector3 UpdateFreeHandTarget(HandState hand, bool pointerMode)
        {
            if (pointerMode)
            {
                return GetReachLimitedTarget(hand, GetCursorHandTarget(hand));
            }

            Vector3 target = Mouse.current != null ? GetCursorHandTarget(hand) : GetFreeHandRestTarget(hand);
            return GetReachLimitedTarget(hand, target);
        }

        private bool ApplyMouseDeltaToHand(HandState hand, Vector3 worldDelta)
        {
            if (!hand.IsPressed)
            {
                return false;
            }

            if (hand.HasHold)
            {
                return true;
            }

            hand.WorldTarget = GetReachLimitedTarget(hand, hand.WorldTarget + worldDelta);
            return false;
        }

        private ClimbHold FindHoveredHold(Vector3 targetWorld)
        {
            ClimbHold bestHold = null;
            float bestDistance = float.MaxValue;

            foreach (ClimbHold hold in ClimbHold.ActiveHolds)
            {
                if (hold == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(new Vector2(targetWorld.x, targetWorld.y), new Vector2(hold.Position.x, hold.Position.y));
                if (distance <= surfaceGrabTolerance && distance < bestDistance)
                {
                    bestHold = hold;
                    bestDistance = distance;
                }
            }

            return bestHold;
        }

        private ClimbHold CreateSurfaceGrip(Vector3 targetWorld)
        {
            if (!TryFindBestSurface(targetWorld, out ClimbSurface bestSurface, out Vector3 bestPoint))
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

        private ClimbSurface FindHoveredSurface(Vector3 targetWorld)
        {
            TryFindBestSurface(targetWorld, out ClimbSurface surface, out _);
            return surface;
        }

        private bool TryFindBestSurface(Vector3 targetWorld, out ClimbSurface bestSurface, out Vector3 bestPoint)
        {
            bestSurface = null;
            bestPoint = Vector3.zero;
            float bestDistance = float.MaxValue;

            foreach (ClimbSurface surface in ClimbSurface.ActiveSurfaces)
            {
                if (surface == null || !surface.TryGetGripPoint(targetWorld, out Vector3 gripPoint))
                {
                    continue;
                }

                float distance = Vector2.Distance(new Vector2(targetWorld.x, targetWorld.y), new Vector2(gripPoint.x, gripPoint.y));
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
                ClimbHold slippingHold = hand.CurrentHold;
                Vector3 slipPoint = slippingHold.Position;
                ClimbSurface slipSurface = FindSurfaceForPoint(slipPoint);
                ReleaseHand(hand);
                SlipOccurred?.Invoke(hand, slippingHold, slipSurface, slipPoint);
            }
        }

        private string BuildGripPrompt()
        {
            if (TryFindBestSurface(GetPromptTargetWorld(), out ClimbSurface surface, out _))
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
            bodyDriveOffset = Vector3.zero;
            ResetFreeHandTargets();
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
            }
            Vitals.ResetToCheckpoint();
            Respawned?.Invoke(checkpoint);
        }

        private void DecayKickVisuals()
        {
            LeftKickVisual = Mathf.MoveTowards(LeftKickVisual, 0f, Time.deltaTime * 4f);
            RightKickVisual = Mathf.MoveTowards(RightKickVisual, 0f, Time.deltaTime * 4f);
        }

        private Vector3 GetReachLimitedTarget(HandState hand, Vector3 desiredTarget)
        {
            Vector3 shoulder = transform.position + hand.LocalAnchorOffset;
            Vector3 offset = desiredTarget - shoulder;
            offset.z = 0f;
            float reachLimit = GetReachLimit(hand);

            if (offset.sqrMagnitude <= reachLimit * reachLimit)
            {
                desiredTarget.z = movePlaneZ;
                return desiredTarget;
            }

            Vector3 limited = shoulder + offset.normalized * reachLimit;
            limited.z = movePlaneZ;
            return limited;
        }

        private float GetReachLimit(HandState hand)
        {
            bool oppositeHandHeld = hand.IsLeft ? RightHand.HasHold : LeftHand.HasHold;
            return !hand.HasHold && oppositeHandHeld ? freeHandReachWhenOtherHandHeld : handReachLimit;
        }

        private float GetHoldReach(HandState hand)
        {
            return Mathf.Max(holdReach, GetReachLimit(hand));
        }

        private Vector3 GetHandGrabProbeWorld(HandState hand)
        {
            Vector3 probe = hand.HasVisibleWorldPoint ? hand.VisibleWorldPoint : hand.WorldTarget;
            probe.z = movePlaneZ;
            return probe;
        }

        private Vector3 GetHoldForceAnchorWorld(HandState hand)
        {
            return hand.HasVisibleWorldPoint ? GetHandGrabProbeWorld(hand) : transform.position + hand.LocalAnchorOffset;
        }

        private Vector3 GetDesiredAnchorWorld(HandState hand)
        {
            Vector3 desiredAnchorWorld = hand.CurrentHold.Position + bodyDriveOffset;
            desiredAnchorWorld.z = movePlaneZ;
            return desiredAnchorWorld;
        }

        private Vector3 GetHoldTarget(HandState hand)
        {
            Vector3 target = hand.CurrentHold != null ? hand.CurrentHold.Position : hand.WorldTarget;
            target.z = movePlaneZ;
            return target;
        }

        private Vector3 GetPromptTargetWorld()
        {
            if (!LeftHand.HasHold && RightHand.HasHold)
            {
                return LeftHand.WorldTarget;
            }

            if (!RightHand.HasHold && LeftHand.HasHold)
            {
                return RightHand.WorldTarget;
            }

            if (!LeftHand.HasHold && !RightHand.HasHold)
            {
                return (LeftHand.WorldTarget + RightHand.WorldTarget) * 0.5f;
            }

            Vector3 bodyTarget = transform.position + bodyDriveOffset;
            bodyTarget.z = movePlaneZ;
            return bodyTarget;
        }

        private void ResetFreeHandTargets()
        {
            LeftHand.WorldTarget = GetFreeHandRestTarget(LeftHand);
            RightHand.WorldTarget = GetFreeHandRestTarget(RightHand);

            LeftHand.WorldTarget = GetReachLimitedTarget(LeftHand, LeftHand.WorldTarget);
            RightHand.WorldTarget = GetReachLimitedTarget(RightHand, RightHand.WorldTarget);
        }

        private Vector3 GetFreeHandRestTarget(HandState hand)
        {
            Vector3 target = transform.position + (hand.IsLeft ? leftFreeHandRestOffset : rightFreeHandRestOffset);
            target.z = movePlaneZ;
            return target;
        }

        private Vector3 GetCursorHandTarget(HandState hand)
        {
            Vector3 target = CursorWorld + (hand.IsLeft ? leftCursorHandOffset : rightCursorHandOffset);
            target.z = movePlaneZ;
            return target;
        }

        private void EnsureInitialized(Transform generatedRoot)
        {
            body = GetComponent<Rigidbody>();
            Vitals = GetComponent<PlayerVitals>();
            Tools = GetComponent<ToolController>();
            restSession = GetComponent<RestSessionController>();
            Inventory = GetComponent<PlayerInventory>();
            if (Inventory == null)
            {
                Inventory = gameObject.AddComponent<PlayerInventory>();
            }

            Inventory.EnsureInitialized();

            Transform toolRoot = generatedRoot != null ? generatedRoot : (transform.parent != null ? transform.parent : transform);
            Tools.Initialize(this, toolRoot);

            if (!initialized)
            {
                checkpoint = transform.position;
                bodyDriveOffset = Vector3.zero;
                ResetFreeHandTargets();
                initialized = true;
            }
        }

        private void UpdateBreathingState()
        {
            AudioCueId nextCue = AudioCueId.None;
            if (!IsRestLocked && !hasWon)
            {
                bool isHoldingOrMovingHard = LeftHand.HasHold || RightHand.HasHold || body.linearVelocity.sqrMagnitude > 4f;
                if (Vitals.LowStamina && isHoldingOrMovingHard)
                {
                    nextCue = AudioCueId.BreathingHeavyLoop;
                }
                else if (isHoldingOrMovingHard)
                {
                    nextCue = AudioCueId.BreathingLightLoop;
                }
            }

            if (activeBreathingCue == nextCue)
            {
                return;
            }

            activeBreathingCue = nextCue;
            BreathingStateChanged?.Invoke(nextCue);
        }

        public Vector3 GetHandAnchorWorld(HandState hand)
        {
            return transform.position + hand.LocalAnchorOffset;
        }

        public void SetVisibleHandWorldPoint(HandState hand, Vector3 worldPoint)
        {
            if (hand == null)
            {
                return;
            }

            worldPoint.z = movePlaneZ;
            hand.VisibleWorldPoint = worldPoint;
            hand.HasVisibleWorldPoint = true;
        }

        public void BeginRestFreeze(RestPoint restPoint)
        {
            if (restFrozen)
            {
                return;
            }

            if (restRoutine != null)
            {
                StopCoroutine(restRoutine);
                restRoutine = null;
            }

            currentRestPoint = restPoint;
            ReleaseHand(LeftHand);
            ReleaseHand(RightHand);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            bodyDriveOffset = Vector3.zero;
            restFrozen = true;
            CurrentPrompt = restPoint != null ? restPoint.PromptText : "Resting";
            if (restPoint != null)
            {
                RestStarted?.Invoke(restPoint.RestType, restPoint.transform.position);
            }
        }

        public void EndRestFreeze()
        {
            if (!restFrozen)
            {
                return;
            }

            restFrozen = false;
            body.isKinematic = false;
            ResetFreeHandTargets();
        }

        public void SetCheckpoint(Vector3 position)
        {
            checkpoint = position;
        }

        private ClimbSurface FindSurfaceForPoint(Vector3 point)
        {
            for (int i = 0; i < ClimbSurface.ActiveSurfaces.Count; i++)
            {
                ClimbSurface surface = ClimbSurface.ActiveSurfaces[i];
                if (surface == null || !surface.TryGetGripPoint(point, out Vector3 gripPoint))
                {
                    continue;
                }

                if (Vector2.Distance(new Vector2(point.x, point.y), new Vector2(gripPoint.x, gripPoint.y)) <= surfaceGrabTolerance + 0.05f)
                {
                    return surface;
                }
            }

            return null;
        }

        private void SetCursorLock(bool shouldLock)
        {
            cursorLocked = shouldLock;

            if (shouldLock)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private bool IsRestLocked => restRoutine != null || restFrozen;
    }
}
