using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
        private const float DefaultFreeHandsStaminaRecoveryDelay = 3f;
        private const float DefaultFreeHandsStaminaRecoveryPerSecond = 2f;

        [SerializeField] private float holdReach = 3.25f;
        [SerializeField] private float holdSpringStrength = 55f;
        [SerializeField] private float holdSpringDamping = 7f;
        [SerializeField] private float mouseDeltaWorldScale = 0.015f;
        [SerializeField] private float maxBodyDriveOffset = 1.8f;
        [SerializeField] private float bodyDriveForceStrength = 45f;
        [SerializeField] private float bodyDriveDamping = 5f;
        [SerializeField] private float bodyDriveReturnSpeed = 8f;
        [SerializeField] private float heldHandReachSlack = 0.03f;
        [SerializeField] private float movePlaneZ = 0f;
        [SerializeField] private float kickForce = 7f;
        [SerializeField] private float fallY = -4f;
        [SerializeField] private float restDuration = 1.15f;
        [SerializeField] private float surfaceGrabTolerance = 0.9f;
        [SerializeField] private float handReachLimit = 3.25f;
        [SerializeField] private float freeHandReachWhenOtherHandHeld = 6f;
        [SerializeField] private float maxHeldHandCorrectionPerSecond = 4f;
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
        [SerializeField] private float climbExposureTickInterval = 10f;
        [SerializeField] private float highAltitudeY = 45f;
        [SerializeField] private float lowSanityAimJitter = 0.12f;
        [SerializeField] private float freeHandsStaminaRecoveryDelay = DefaultFreeHandsStaminaRecoveryDelay;
        [SerializeField] private float freeHandsStaminaRecoveryPerSecond = DefaultFreeHandsStaminaRecoveryPerSecond;
        [SerializeField] private float ropeHandFeedbackStrength = 0.35f;
        [SerializeField] private float capsuleHeight = 1.9f;
        [SerializeField] private float capsuleRadius = 0.35f;
        [SerializeField] private Vector3 capsuleCenter = new Vector3(0f, 0.95f, 0f);
        [SerializeField] private Vector3 centerOfMass = new Vector3(0f, 0.85f, 0f);
        [SerializeField] private float levelResetHoldDuration = 1.5f;

        private Rigidbody body;
        private Camera mainCamera;
        private RestPoint currentRestPoint;
        private ResourceNode currentResourceNode;
        private GoalPoint currentGoalPoint;
        private Vector3 checkpoint;
        private Vector3 bodyDriveOffset;
        private Coroutine restRoutine;
        private bool hasWon;
        private bool initialized;
        private bool restFrozen;
        private bool levelResetRequested;
        private AudioCueId activeBreathingCue;
        private bool cursorLocked;
        private RestSessionController restSession;
        private float climbExposureTimer;
        private float freeHandsTimer;
        private float levelResetHoldTimer;
        private CapsuleCollider bodyCollider;
        private bool upwardClimbCollisionBypassActive;
        private readonly List<Collider> ignoredUpwardClimbColliders = new List<Collider>();

        public HandState LeftHand { get; } = new HandState { DisplayName = "Left", IsLeft = true, LocalAnchorOffset = new Vector3(-0.45f, 0.55f, 0f) };
        public HandState RightHand { get; } = new HandState { DisplayName = "Right", IsLeft = false, LocalAnchorOffset = new Vector3(0.45f, 0.55f, 0f) };
        public PlayerVitals Vitals { get; private set; }
        public ToolController Tools { get; private set; }
        public PlayerInventory Inventory { get; private set; }
        public string CurrentPrompt { get; private set; }
        public Vector3 CursorWorld { get; private set; }
        public Vector3 BodyVelocity => body != null ? body.linearVelocity : Vector3.zero;
        public bool HasWon => hasWon;
        public ResourceNode CurrentResourceNode => currentResourceNode;
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
        public event Action<ResourceNode, Vector3> ResourceSearchStarted;
        public event Action<ResourceNode, string, int, Vector3> ResourceItemFound;
        public event Action<ResourceNode, string, int, Vector3> ResourceItemTaken;

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
            SetUpwardClimbCollisionBypass(false);
            SetCursorLock(false);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            SetCursorLock(hasFocus);
        }

        private void Update()
        {
            UpdateCursorWorld();
            UpdateFreeHandsStaminaRecovery(Time.deltaTime);
            if (restFrozen)
            {
                UpdateLevelResetInput();
                UpdateBreathingState();
                Tools.UpdateRuntime();
                return;
            }

            UpdateMouseDrivenTargets();
            UpdateToolInput();
            UpdateHandInput();
            UpdateKickInput();
            UpdateResourceInput();
            UpdateRestInput();
            UpdatePrompt();
            UpdateLevelResetInput();
            UpdateClimbExposure();
            DecayKickVisuals();
            UpdateBreathingState();
            Tools.UpdateRuntime();
        }

        private void FixedUpdate()
        {
            ConstrainToPlane();
            UpdateUpwardClimbCollisionBypass();
            ApplyHoldForce(LeftHand);
            ApplyHoldForce(RightHand);
            ApplyBodyDrive();
            EnforceHeldHandReachConstraints();
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

            if (other.TryGetComponent(out ResourceNode resourceNode))
            {
                if (currentResourceNode != null && currentResourceNode != resourceNode)
                {
                    currentResourceNode.ItemRevealed -= OnResourceItemRevealed;
                }

                currentResourceNode = resourceNode;
                currentResourceNode.ItemRevealed -= OnResourceItemRevealed;
                currentResourceNode.ItemRevealed += OnResourceItemRevealed;
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

            if (other.TryGetComponent(out ResourceNode resourceNode) && currentResourceNode == resourceNode)
            {
                currentResourceNode.ItemRevealed -= OnResourceItemRevealed;
                currentResourceNode = null;
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

            HandleHand(LeftHand, Mouse.current.leftButton.wasPressedThisFrame, Mouse.current.leftButton.wasReleasedThisFrame);
            HandleHand(RightHand, Mouse.current.rightButton.wasPressedThisFrame, Mouse.current.rightButton.wasReleasedThisFrame);
        }

        private void HandleHand(HandState hand, bool pressed, bool released)
        {
            if (Tools != null && Tools.IsHandReservedForRope(hand) && !(Tools.IsPointerToolMode && hand == RightHand))
            {
                return;
            }

            if (Tools != null && Tools.IsPointerToolMode && hand == RightHand && !hand.HasHold)
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
                ClimbSurface hoveredSurface = hoveredHold == null ? FindHoveredSurface(targetWorld) : FindSurfaceForPoint(hoveredHold.Position);
                ClimbHold runtimeGrip = hoveredHold != null ? CreateHandAnchoredGrip(targetWorld, hoveredHold) : CreateSurfaceGrip(targetWorld);

                if (Tools != null && Tools.TryUseTool(targetWorld, hoveredHold, hoveredSurface))
                {
                    ReleaseRuntimeHoldIfNeeded(runtimeGrip);
                    return;
                }

                TryGrabHand(hand, hoveredHold, runtimeGrip);
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

                bool hasSurfacePoint = TryFindBestSurface(CursorWorld, out ClimbSurface hoveredSurface, out Vector3 surfacePoint);
                if (hasSurfacePoint && Tools.TryFireRopeAtPoint(hand, surfacePoint, hoveredSurface, out string failureReason))
                {
                    HandState ropeHand = Tools.ActiveRopeHand;
                    ClimbHold ropeHold = ropeHand != null ? Tools.GetRopeHold(ropeHand) : null;
                    if (ropeHold != null)
                    {
                        if (ropeHand.HasHold)
                        {
                            ReleaseHand(ropeHand);
                        }

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
                    string displayFailureReason = hasSurfacePoint ? Tools.LastToolFailure : "Aim at a rope-ready wall";
                    CurrentPrompt = displayFailureReason;
                    GrabFailed?.Invoke(hand, hand.WorldTarget);
                }
            }

            if (released)
            {
                hand.IsPressed = false;
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
            if (Vector3.Distance(anchor, targetHold.Position) > GetGrabReach(hand))
            {
                hand.State = HandGrabState.Reach;
                ReleaseRuntimeHoldIfNeeded(surfaceGrip);
                GrabFailed?.Invoke(hand, targetHold.Position);
                return;
            }

            float grabCost = targetHold.InitialGrabCost * Vitals.GrabCostMultiplier;
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
                float multiplier = (Inventory != null ? Inventory.KickForceMultiplier : 1f) * Vitals.KickForceMultiplier;
                body.AddForce(new Vector3(-1.2f, 2.5f, 0f).normalized * kickForce * multiplier, ForceMode.Impulse);
                LeftKickVisual = 1f;
                KickPerformed?.Invoke(true, transform.position + new Vector3(-0.35f, 0.25f, 0f));
            }

            if (Keyboard.current.eKey.wasPressedThisFrame && Vitals.TrySpendStamina(4f))
            {
                float multiplier = (Inventory != null ? Inventory.KickForceMultiplier : 1f) * Vitals.KickForceMultiplier;
                body.AddForce(new Vector3(1.2f, 2.5f, 0f).normalized * kickForce * multiplier, ForceMode.Impulse);
                RightKickVisual = 1f;
                KickPerformed?.Invoke(false, transform.position + new Vector3(0.35f, 0.25f, 0f));
            }
        }

        private void UpdateResourceInput()
        {
            if (Keyboard.current == null || currentResourceNode == null || IsRestLocked || hasWon)
            {
                return;
            }

            if (!Keyboard.current.fKey.wasPressedThisFrame)
            {
                return;
            }

            if (currentResourceNode.RevealedCount > 0)
            {
                currentResourceNode.TryGetRevealed(0, out string takenItemId, out int takenQuantity);
                bool allowLargeFallback = currentRestPoint != null && currentRestPoint.RestType == RestPointType.LongRest;
                bool taken = currentResourceNode.TryTakeRevealed(Inventory, 0, allowLargeFallback);
                CurrentPrompt = taken
                    ? currentResourceNode.BuildStatusText()
                    : allowLargeFallback ? "Packs full" : "Small pack full. Organize at a long rest";
                if (taken)
                {
                    ResourceItemTaken?.Invoke(currentResourceNode, takenItemId, takenQuantity, currentResourceNode.transform.position);
                }
                return;
            }

            currentResourceNode.StartSearch(this);
            CurrentPrompt = currentResourceNode.BuildStatusText();
            ResourceSearchStarted?.Invoke(currentResourceNode, currentResourceNode.transform.position);
        }

        private void OnResourceItemRevealed(ResourceNode node, string itemId, int quantity, Vector3 origin)
        {
            ResourceItemFound?.Invoke(node, itemId, quantity, origin);
        }

        private void UpdateRestInput()
        {
            if (Keyboard.current == null || currentRestPoint == null || currentResourceNode != null || IsRestLocked || hasWon)
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

        private void UpdateLevelResetInput()
        {
            if (levelResetRequested || Keyboard.current == null)
            {
                return;
            }

            if (!Keyboard.current.rKey.isPressed)
            {
                levelResetHoldTimer = 0f;
                return;
            }

            levelResetHoldTimer += Time.unscaledDeltaTime;
            float duration = Mathf.Max(0.1f, levelResetHoldDuration);
            float progress = Mathf.Clamp01(levelResetHoldTimer / duration);
            CurrentPrompt = $"Hold R to reset level {Mathf.RoundToInt(progress * 100f)}%";

            if (levelResetHoldTimer >= duration)
            {
                levelResetRequested = true;
                CurrentPrompt = "Resetting level";
                StartCoroutine(ReloadCurrentLevel());
            }
        }

        private IEnumerator ReloadCurrentLevel()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            AsyncOperation operation = activeScene.buildIndex >= 0
                ? SceneManager.LoadSceneAsync(activeScene.buildIndex, LoadSceneMode.Single)
                : SceneManager.LoadSceneAsync(activeScene.name, LoadSceneMode.Single);

            if (operation == null)
            {
                if (activeScene.buildIndex >= 0)
                {
                    SceneManager.LoadScene(activeScene.buildIndex, LoadSceneMode.Single);
                }
                else
                {
                    SceneManager.LoadScene(activeScene.name, LoadSceneMode.Single);
                }

                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }
        }

        private IEnumerator PerformRest(RestPoint restPoint)
        {
            Tools.ClearActiveRope();
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

            if (currentResourceNode != null)
            {
                CurrentPrompt = currentResourceNode.BuildStatusText();
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
            ApplyRopeHandFeedback(hand, anchorWorld, force);
            hand.WorldTarget = GetHoldTarget(hand);
        }

        private void ApplyRopeHandFeedback(HandState hand, Vector3 anchorWorld, Vector3 force)
        {
            if (hand.CurrentHold == null || hand.CurrentHold.HoldType != ClimbHoldType.Rope || ropeHandFeedbackStrength <= 0f)
            {
                return;
            }

            Rigidbody ropeBody = hand.CurrentHold.GetComponent<Rigidbody>();
            if (ropeBody == null || ropeBody.isKinematic)
            {
                return;
            }

            ropeBody.AddForceAtPosition(-force * ropeHandFeedbackStrength, anchorWorld, ForceMode.Acceleration);
        }

        private void ApplyBodyDrive()
        {
            if (bodyDriveOffset.sqrMagnitude <= 0.000001f || IsRestLocked || hasWon)
            {
                return;
            }

            Vector3 velocity = body.linearVelocity;
            velocity.z = 0f;
            Vector3 force = bodyDriveOffset * bodyDriveForceStrength - velocity * bodyDriveDamping;
            force = LimitBodyDriveByHeldHands(force);
            force.z = 0f;
            body.AddForce(force, ForceMode.Acceleration);
            bodyDriveOffset = Vector3.MoveTowards(bodyDriveOffset, Vector3.zero, bodyDriveReturnSpeed * Time.fixedDeltaTime);
        }

        private Vector3 LimitBodyDriveByHeldHands(Vector3 force)
        {
            force = LimitBodyDriveByHeldHand(LeftHand, force);
            force = LimitBodyDriveByHeldHand(RightHand, force);
            return force;
        }

        private Vector3 LimitBodyDriveByHeldHand(HandState hand, Vector3 force)
        {
            if (hand == null || !hand.HasHold || hand.CurrentHold == null)
            {
                return force;
            }

            Vector3 shoulder = transform.position + hand.LocalAnchorOffset;
            Vector3 holdToShoulder = shoulder - hand.CurrentHold.Position;
            holdToShoulder.z = 0f;
            float reachLimit = GetReachLimit(hand);
            if (holdToShoulder.sqrMagnitude < reachLimit * reachLimit * 0.92f)
            {
                return force;
            }

            Vector3 awayFromHold = holdToShoulder.normalized;
            float outwardForce = Vector3.Dot(force, awayFromHold);
            return outwardForce > 0f ? force - awayFromHold * outwardForce : force;
        }

        private void EnforceHeldHandReachConstraints()
        {
            if (body.isKinematic || IsRestLocked || hasWon)
            {
                return;
            }

            Vector3 position = body.position;
            position.z = movePlaneZ;
            for (int i = 0; i < 2; i++)
            {
                position = ProjectBodyInsideHeldHandReach(LeftHand, position);
                position = ProjectBodyInsideHeldHandReach(RightHand, position);
            }

            if ((position - body.position).sqrMagnitude > 0.000001f)
            {
                float maxCorrection = Mathf.Max(0.01f, maxHeldHandCorrectionPerSecond) * Time.fixedDeltaTime;
                Vector3 correctedPosition = Vector3.MoveTowards(body.position, position, maxCorrection);
                correctedPosition.z = movePlaneZ;
                body.MovePosition(correctedPosition);
            }

            body.linearVelocity = RemoveOutwardVelocityAtHeldLimit(LeftHand, body.linearVelocity);
            body.linearVelocity = RemoveOutwardVelocityAtHeldLimit(RightHand, body.linearVelocity);
        }

        private void UpdateUpwardClimbCollisionBypass()
        {
            bool shouldBypass = ShouldBypassClimbSurfaceCollision();
            SetUpwardClimbCollisionBypass(shouldBypass);
        }

        private bool ShouldBypassClimbSurfaceCollision()
        {
            if (bodyCollider == null || body == null || body.isKinematic || IsRestLocked || hasWon)
            {
                return false;
            }

            if (!LeftHand.HasHold && !RightHand.HasHold)
            {
                return false;
            }

            return body.linearVelocity.y > 0.05f || bodyDriveOffset.y > 0.05f;
        }

        private void SetUpwardClimbCollisionBypass(bool active)
        {
            if (bodyCollider == null)
            {
                return;
            }

            bool stateChanged = active != upwardClimbCollisionBypassActive;
            upwardClimbCollisionBypassActive = active;

            for (int i = ignoredUpwardClimbColliders.Count - 1; i >= 0; i--)
            {
                Collider ignored = ignoredUpwardClimbColliders[i];
                if (ignored == null)
                {
                    ignoredUpwardClimbColliders.RemoveAt(i);
                    continue;
                }

                if (!active || !ignored.enabled)
                {
                    Physics.IgnoreCollision(bodyCollider, ignored, false);
                    ignoredUpwardClimbColliders.RemoveAt(i);
                }
            }

            if (!active || !stateChanged)
            {
                return;
            }

            foreach (ClimbSurface surface in ClimbSurface.ActiveSurfaces)
            {
                Collider surfaceCollider = surface != null ? surface.SurfaceCollider : null;
                if (surfaceCollider == null || !surfaceCollider.enabled || surfaceCollider == bodyCollider || ignoredUpwardClimbColliders.Contains(surfaceCollider))
                {
                    continue;
                }

                Physics.IgnoreCollision(bodyCollider, surfaceCollider, true);
                ignoredUpwardClimbColliders.Add(surfaceCollider);
            }
        }

        private Vector3 ProjectBodyInsideHeldHandReach(HandState hand, Vector3 bodyPosition)
        {
            if (hand == null || !hand.HasHold || hand.CurrentHold == null)
            {
                return bodyPosition;
            }

            Vector3 holdPoint = hand.CurrentHold.Position;
            holdPoint.z = movePlaneZ;
            Vector3 shoulder = bodyPosition + hand.LocalAnchorOffset;
            shoulder.z = movePlaneZ;
            Vector3 holdToShoulder = shoulder - holdPoint;
            float maxReach = Mathf.Max(GetReachLimit(hand) - heldHandReachSlack, 0.05f);
            if (holdToShoulder.sqrMagnitude <= maxReach * maxReach)
            {
                return bodyPosition;
            }

            Vector3 projectedShoulder = holdPoint + holdToShoulder.normalized * maxReach;
            Vector3 projectedBody = projectedShoulder - hand.LocalAnchorOffset;
            projectedBody.z = movePlaneZ;
            return projectedBody;
        }

        private Vector3 RemoveOutwardVelocityAtHeldLimit(HandState hand, Vector3 velocity)
        {
            if (hand == null || !hand.HasHold || hand.CurrentHold == null)
            {
                return velocity;
            }

            Vector3 holdPoint = hand.CurrentHold.Position;
            holdPoint.z = movePlaneZ;
            Vector3 shoulder = body.position + hand.LocalAnchorOffset;
            shoulder.z = movePlaneZ;
            Vector3 holdToShoulder = shoulder - holdPoint;
            float maxReach = Mathf.Max(GetReachLimit(hand) - heldHandReachSlack, 0.05f);
            if (holdToShoulder.sqrMagnitude < maxReach * maxReach * 0.99f || holdToShoulder.sqrMagnitude <= 0.000001f)
            {
                return velocity;
            }

            Vector3 outward = holdToShoulder.normalized;
            float outwardSpeed = Vector3.Dot(velocity, outward);
            if (outwardSpeed > 0f)
            {
                velocity -= outward * outwardSpeed;
            }

            velocity.z = 0f;
            return velocity;
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
            float drainMultiplier = (Inventory != null ? Inventory.HoldDrainMultiplier : 1f) * Vitals.HoldDrainMultiplier;
            if (!Vitals.TrySpendStamina(hand.CurrentHold.HoldDrainPerSecond * drainMultiplier))
            {
                ReleaseHand(hand);
                if (!LeftHand.HasHold && !RightHand.HasHold)
                {
                    body.AddForce(Vector3.down * 2f, ForceMode.Impulse);
                }
            }

            UpdateSlip(hand);
        }

        private void UpdateClimbExposure()
        {
            if (hasWon || IsRestLocked || Vitals == null)
            {
                return;
            }

            bool isHardClimbing = LeftHand.HasHold || RightHand.HasHold || body.linearVelocity.sqrMagnitude > 4f;
            if (!isHardClimbing)
            {
                return;
            }

            climbExposureTimer += Time.deltaTime;
            if (climbExposureTimer < climbExposureTickInterval)
            {
                return;
            }

            climbExposureTimer = 0f;
            Vitals.ApplyClimbExposureTick(isHardClimbing, transform.position.y >= highAltitudeY);
        }

        private void UpdateFreeHandsStaminaRecovery(float deltaTime)
        {
            if (Vitals == null)
            {
                Vitals = GetComponent<PlayerVitals>();
            }

            if (hasWon || IsRestLocked || Vitals == null || LeftHand.HasHold || RightHand.HasHold)
            {
                freeHandsTimer = 0f;
                return;
            }

            freeHandsTimer += Mathf.Max(0f, deltaTime);
            float recoveryDelay = freeHandsStaminaRecoveryDelay > 0f ? freeHandsStaminaRecoveryDelay : DefaultFreeHandsStaminaRecoveryDelay;
            if (freeHandsTimer < recoveryDelay)
            {
                return;
            }

            float recoveryPerSecond = freeHandsStaminaRecoveryPerSecond > 0f ? freeHandsStaminaRecoveryPerSecond : DefaultFreeHandsStaminaRecoveryPerSecond;
            Vitals.RestoreStamina(recoveryPerSecond * deltaTime);
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
            body.rotation = Quaternion.identity;
            transform.rotation = Quaternion.identity;

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

            if (worldDelta.sqrMagnitude > 0.000001f)
            {
                bool heldHandDriveRequested = false;
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
                LeftHand.WorldTarget = GetReachLimitedTarget(LeftHand, LeftHand.WorldTarget);
            }

            if (RightHand.HasHold)
            {
                RightHand.WorldTarget = GetHoldTarget(RightHand);
            }
            else
            {
                RightHand.WorldTarget = GetReachLimitedTarget(RightHand, RightHand.WorldTarget);
            }
        }

        private bool ApplyMouseDeltaToHand(HandState hand, Vector3 worldDelta)
        {
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

            return CreateRuntimeGrip("RuntimeSurfaceGrip", bestPoint, ClimbHoldType.Normal,
                bestSurface.AllowAnchorAttach,
                bestSurface.AllowRopeAttach,
                bestSurface.GrabCost,
                bestSurface.HoldDrainPerSecond,
                bestSurface.SlipCheckInterval,
                bestSurface.SlipChance,
                bestSurface.SurfaceLabel);
        }

        private ClimbHold CreateHandAnchoredGrip(Vector3 targetWorld, ClimbHold template)
        {
            if (template == null)
            {
                return null;
            }

            return CreateRuntimeGrip("RuntimeHandGrip", targetWorld, template.HoldType,
                template.AllowAnchorAttach,
                template.AllowRopeAttach,
                template.InitialGrabCost,
                template.HoldDrainPerSecond,
                template.SlipCheckInterval,
                template.SlipChance,
                template.GripLabel);
        }

        private ClimbHold CreateRuntimeGrip(string objectName, Vector3 targetWorld, ClimbHoldType holdType, bool canAnchor, bool canRope, float grabCost, float drainPerSecond, float slipInterval, float slipChance, string label)
        {
            targetWorld.z = movePlaneZ;
            GameObject runtimeHoldObject = new GameObject(objectName);
            runtimeHoldObject.transform.position = targetWorld;
            ClimbHold runtimeHold = runtimeHoldObject.AddComponent<ClimbHold>();
            runtimeHold.Configure(holdType, canAnchor, canRope, grabCost, drainPerSecond, slipInterval, slipChance, label);
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
            float slipChance = hand.CurrentHold.SlipChance;
            if (Random.value <= slipChance)
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
                return $"{surface.SurfaceLabel} surface  grab {surface.GrabCost * Vitals.GrabCostMultiplier:0.#}  drain {surface.HoldDrainPerSecond * Vitals.HoldDrainMultiplier:0.#}/s{slip}{BuildVitalPromptSuffix()}";
            }

            return $"Climb to the next hold{BuildVitalPromptSuffix()}";
        }

        private Vector3 ApplySanityJitter(Vector3 target, HandState hand)
        {
            if (Vitals == null || !Vitals.LowSanity || hand.HasHold)
            {
                return target;
            }

            float strength = lowSanityAimJitter * (Vitals.Sanity <= 0.01f ? 1.75f : 1f);
            float seed = hand.IsLeft ? 11.7f : 27.3f;
            float x = Mathf.PerlinNoise(Time.time * 6f, seed) - 0.5f;
            float y = Mathf.PerlinNoise(seed, Time.time * 6f) - 0.5f;
            return target + new Vector3(x, y, 0f) * strength;
        }

        private float GetVitalSlipPenalty()
        {
            if (Vitals == null)
            {
                return 0f;
            }

            float penalty = 0f;
            if (Vitals.LowHealth) penalty += 0.04f;
            if (Vitals.LowWarmth) penalty += 0.03f;
            if (Vitals.LowSanity) penalty += 0.025f;
            return penalty;
        }

        private string BuildVitalPromptSuffix()
        {
            if (Vitals == null)
            {
                return string.Empty;
            }

            string suffix = string.Empty;
            if (Vitals.LowHealth) suffix += "  injured";
            if (Vitals.LowHunger) suffix += "  hungry";
            if (Vitals.LowWarmth) suffix += "  cold";
            if (Vitals.LowSanity) suffix += "  shaken";
            return suffix;
        }

        private void RespawnAtCheckpoint()
        {
            Tools.ClearActiveRope();
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

        private float GetGrabReach(HandState hand)
        {
            return Mathf.Max(holdReach, handReachLimit);
        }

        private Vector3 GetHandGrabProbeWorld(HandState hand)
        {
            Vector3 probe = hand.WorldTarget;
            probe.z = movePlaneZ;
            return probe;
        }

        private Vector3 GetHoldForceAnchorWorld(HandState hand)
        {
            Vector3 anchor = transform.position + hand.LocalAnchorOffset;
            anchor.z = movePlaneZ;
            return anchor;
        }

        private Vector3 GetDesiredAnchorWorld(HandState hand)
        {
            Vector3 desiredAnchorWorld = hand.CurrentHold.Position;
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

        private void EnsureInitialized(Transform generatedRoot)
        {
            body = GetComponent<Rigidbody>();
            NormalizePhysicsBody();
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

        private void NormalizePhysicsBody()
        {
            bodyCollider = GetComponent<CapsuleCollider>();
            if (bodyCollider != null)
            {
                bodyCollider.direction = 1;
                bodyCollider.height = capsuleHeight;
                bodyCollider.radius = capsuleRadius;
                bodyCollider.center = capsuleCenter;
            }

            if (body != null)
            {
                body.centerOfMass = centerOfMass;
                body.inertiaTensorRotation = Quaternion.identity;
            }
        }

        private void UpdateBreathingState()
        {
            AudioCueId nextCue = AudioCueId.None;
            if (!IsRestLocked && !hasWon)
            {
                bool isHoldingOrMovingHard = LeftHand.HasHold || RightHand.HasHold || body.linearVelocity.sqrMagnitude > 4f;
                bool lowLongTermResource = Vitals.LowHealth || Vitals.LowHunger || Vitals.LowWarmth || Vitals.LowSanity;
                bool warningLongTermResource = Vitals.WarningHealth || Vitals.WarningHunger || Vitals.WarningWarmth || Vitals.WarningSanity;
                if ((Vitals.LowStamina || lowLongTermResource) && isHoldingOrMovingHard)
                {
                    nextCue = AudioCueId.BreathingHeavyLoop;
                }
                else if (isHoldingOrMovingHard || warningLongTermResource)
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
            Tools.ClearActiveRope();
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

        public void ReportRestCompleted(RestPointType restType, Vector3 origin)
        {
            RestCompleted?.Invoke(restType, origin);
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
