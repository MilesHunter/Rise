using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rise
{
    public sealed class ToolController : MonoBehaviour
    {
        [SerializeField] private ToolKind selectedTool = ToolKind.Anchor;
        [SerializeField] private bool toolMode;
        [SerializeField] private float ropeRange = 5f;
        [SerializeField] private float ropeLength = 5f;
        [SerializeField] private int ropeSegmentCount = 12;

        private PlayerClimbController controller;
        private PlayerInventory inventory;
        private ClimbHold activeAnchor;
        private RopeState activeRope;
        private GameObject generatedRoot;

        public event Action<ToolKind, Vector3> ToolPlaced;

        public ToolKind SelectedTool => selectedTool;
        public bool ToolMode => toolMode;
        public bool IsPointerToolMode => toolMode && selectedTool == ToolKind.Rope;
        public string LastToolFailure { get; private set; }

        public void Initialize(PlayerClimbController owner, Transform root)
        {
            controller = owner;
            inventory = owner != null ? owner.Inventory : null;
            if (inventory == null && owner != null)
            {
                inventory = owner.GetComponent<PlayerInventory>();
            }

            if (generatedRoot == null)
            {
                generatedRoot = new GameObject("GeneratedTools");
            }

            generatedRoot.transform.SetParent(root, false);
        }

        public void ToggleToolMode()
        {
            toolMode = !toolMode;
        }

        public void CycleTool(float scrollValue)
        {
            if (Mathf.Approximately(scrollValue, 0f))
            {
                return;
            }

            ToolKind[] tools = (ToolKind[])Enum.GetValues(typeof(ToolKind));
            int currentIndex = Array.IndexOf(tools, selectedTool);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int direction = scrollValue > 0f ? 1 : -1;
            int nextIndex = (currentIndex + direction + tools.Length) % tools.Length;
            selectedTool = tools[nextIndex];
        }

        public bool TryUseTool(Vector3 targetWorld, ClimbHold hoveredHold, ClimbSurface hoveredSurface)
        {
            if (!toolMode)
            {
                return false;
            }

            bool used = selectedTool switch
            {
                ToolKind.Anchor => TryPlaceAnchor(targetWorld, hoveredHold, hoveredSurface),
                ToolKind.Rope => false,
                _ => false
            };

            if (used && selectedTool == ToolKind.Anchor)
            {
                toolMode = false;
            }

            return used;
        }

        public bool HasActiveRope(HandState hand)
        {
            return activeRope != null && activeRope.Hand == hand && activeRope.AnchorHold != null;
        }

        public HandState ActiveRopeHand => activeRope?.Hand;

        public bool IsHandReservedForRope(HandState hand)
        {
            return false;
        }

        public bool TryFireRope(HandState preferredHand, Vector3 targetWorld, ClimbHold hoveredHold, ClimbSurface hoveredSurface)
        {
            return TryFireRope(preferredHand, targetWorld, hoveredHold, hoveredSurface, out _);
        }

        public bool TryFireRope(HandState preferredHand, Vector3 targetWorld, ClimbHold hoveredHold, ClimbSurface hoveredSurface, out string failureReason)
        {
            Vector3 anchorPoint = targetWorld;
            if (hoveredSurface != null && hoveredSurface.TryGetGripPoint(targetWorld, out Vector3 surfacePoint))
            {
                anchorPoint = surfacePoint;
            }
            else if (hoveredHold != null)
            {
                anchorPoint = hoveredHold.Position;
            }

            return TryFireRopeAtPoint(preferredHand, anchorPoint, hoveredSurface, hoveredHold, out failureReason);
        }

        public bool TryFireRopeAtPoint(HandState preferredHand, Vector3 anchorPoint, ClimbSurface anchorSurface, out string failureReason)
        {
            return TryFireRopeAtPoint(preferredHand, anchorPoint, anchorSurface, null, out failureReason);
        }

        private bool TryFireRopeAtPoint(HandState preferredHand, Vector3 anchorPoint, ClimbSurface anchorSurface, ClimbHold anchorHold, out string failureReason)
        {
            LastToolFailure = string.Empty;
            failureReason = string.Empty;

            if (!toolMode || selectedTool != ToolKind.Rope || preferredHand == null)
            {
                failureReason = "Rope tool is not armed";
                LastToolFailure = failureReason;
                return false;
            }

            HandState ropeHand = ResolveRopeHand(preferredHand);
            if (ropeHand == null)
            {
                failureReason = "Free one hand to fire rope";
                LastToolFailure = failureReason;
                return false;
            }

            if (anchorSurface != null)
            {
                if (!anchorSurface.AllowRopeAttach)
                {
                    failureReason = "Aim at a rope-ready wall";
                    LastToolFailure = failureReason;
                    return false;
                }
            }
            else if (anchorHold != null)
            {
                if (!anchorHold.AllowRopeAttach)
                {
                    failureReason = "Aim at a rope-ready hold or wall";
                    LastToolFailure = failureReason;
                    return false;
                }

                anchorPoint = anchorHold.Position;
            }
            else
            {
                failureReason = "Aim at a rope-ready wall";
                LastToolFailure = failureReason;
                return false;
            }

            Vector3 source = controller.GetHandAnchorWorld(ropeHand);
            anchorPoint.z = 0f;
            if (Vector3.Distance(source, anchorPoint) > ropeRange)
            {
                failureReason = "Rope target is too far";
                LastToolFailure = failureReason;
                return false;
            }

            if (!HasToolItem(PlayerInventory.RopeItemId))
            {
                failureReason = "No rope in small pack";
                LastToolFailure = failureReason;
                return false;
            }

            if (!controller.Vitals.TrySpendStamina(4f))
            {
                failureReason = "Not enough stamina to fire rope";
                LastToolFailure = failureReason;
                return false;
            }

            if (!ConsumeToolItem(PlayerInventory.RopeItemId))
            {
                failureReason = "No rope in small pack";
                LastToolFailure = failureReason;
                return false;
            }

            ClearActiveRope();

            GameObject ropeRoot = new GameObject("RopeTether");
            ropeRoot.transform.SetParent(generatedRoot.transform, false);
            RopeTether ropeTether = ropeRoot.AddComponent<RopeTether>();
            ropeTether.Build(anchorPoint, source, ropeLength, ropeSegmentCount);

            activeRope = new RopeState
            {
                Hand = ropeHand,
                Root = ropeRoot,
                Tether = ropeTether,
                AnchorHold = ropeTether.FreeEndHold
            };

            ToolPlaced?.Invoke(ToolKind.Rope, anchorPoint);
            return true;
        }

        public void UpdateRuntime()
        {
        }

        public void ReleaseRope(HandState hand)
        {
            if (activeRope == null || activeRope.Hand != hand)
            {
                return;
            }

            ClearActiveRope();
        }

        public ClimbHold GetRopeHold(HandState hand)
        {
            return activeRope != null && activeRope.Hand == hand ? activeRope.AnchorHold : null;
        }

        public string GetToolPromptSuffix()
        {
            if (!toolMode)
            {
                return string.Empty;
            }

            return selectedTool == ToolKind.Rope
                ? "RMB fire rope  keep one hand free"
                : "Click a hold or wall point";
        }

        private bool TryPlaceAnchor(Vector3 targetWorld, ClimbHold hoveredHold, ClimbSurface hoveredSurface)
        {
            Vector3 anchorPosition;
            if (hoveredHold != null && hoveredHold.AllowAnchorAttach)
            {
                anchorPosition = hoveredHold.Position;
            }
            else if (hoveredSurface != null && hoveredSurface.AllowAnchorAttach && hoveredSurface.TryGetGripPoint(targetWorld, out Vector3 surfacePoint))
            {
                anchorPosition = surfacePoint;
            }
            else
            {
                return false;
            }

            if (!HasToolItem(PlayerInventory.AnchorItemId) || !controller.Vitals.TrySpendStamina(2f))
            {
                return false;
            }

            if (!ConsumeToolItem(PlayerInventory.AnchorItemId))
            {
                return false;
            }

            if (activeAnchor != null)
            {
                Object.Destroy(activeAnchor.gameObject);
            }

            GameObject anchorObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            anchorObject.name = "AnchorHold";
            anchorObject.transform.SetParent(generatedRoot.transform, false);
            anchorObject.transform.position = anchorPosition + new Vector3(0f, 0f, -0.1f);
            anchorObject.transform.localScale = Vector3.one * 0.32f;
            Object.Destroy(anchorObject.GetComponent<SphereCollider>());

            Renderer renderer = anchorObject.GetComponent<Renderer>();
            renderer.material.color = new Color(0.95f, 0.7f, 0.2f);

            activeAnchor = anchorObject.AddComponent<ClimbHold>();
            activeAnchor.Configure(ClimbHoldType.Anchor, true, true, 0f, 0f, 0f, 0f, "Anchor");
            ToolPlaced?.Invoke(ToolKind.Anchor, anchorObject.transform.position);
            return true;
        }

        private HandState ResolveRopeHand(HandState preferredHand)
        {
            if (!preferredHand.HasHold)
            {
                return preferredHand;
            }

            HandState fallback = preferredHand == controller.RightHand ? controller.LeftHand : controller.RightHand;
            return fallback.HasHold ? null : fallback;
        }

        public void ClearActiveRope()
        {
            if (activeRope?.Root != null)
            {
                Object.Destroy(activeRope.Root);
            }

            activeRope = null;
        }

        private bool HasToolItem(string itemId)
        {
            if (inventory == null && controller != null)
            {
                inventory = controller.Inventory != null ? controller.Inventory : controller.GetComponent<PlayerInventory>();
            }

            return inventory == null || inventory.CountSmall(itemId) > 0;
        }

        private bool ConsumeToolItem(string itemId)
        {
            if (inventory == null && controller != null)
            {
                inventory = controller.Inventory != null ? controller.Inventory : controller.GetComponent<PlayerInventory>();
            }

            return inventory == null || inventory.TryConsumeSmall(itemId, 1);
        }

        private sealed class RopeState
        {
            public HandState Hand;
            public GameObject Root;
            public RopeTether Tether;
            public ClimbHold AnchorHold;
        }
    }
}
