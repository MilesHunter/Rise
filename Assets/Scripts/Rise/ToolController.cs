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
        [SerializeField] private int ropeVisualSegments = 18;
        [SerializeField] private float ropeSag = 0.45f;

        private PlayerClimbController controller;
        private ClimbHold activeAnchor;
        private RopeState activeRope;
        private GameObject generatedRoot;

        public event Action<ToolKind, Vector3> ToolPlaced;

        public ToolKind SelectedTool => selectedTool;
        public bool ToolMode => toolMode;
        public bool IsPointerToolMode => toolMode && selectedTool == ToolKind.Rope;

        public void Initialize(PlayerClimbController owner, Transform root)
        {
            controller = owner;
            if (generatedRoot == null)
            {
                generatedRoot = new GameObject("GeneratedTools");
            }

            generatedRoot.transform.SetParent(root, false);
        }

        public void ToggleToolMode()
        {
            toolMode = !toolMode;
            if (!toolMode)
            {
                ClearActiveRope();
            }
        }

        public void CycleTool(float scrollValue)
        {
            if (Mathf.Approximately(scrollValue, 0f))
            {
                return;
            }

            selectedTool = scrollValue > 0f ? ToolKind.Rope : ToolKind.Anchor;
            if (selectedTool != ToolKind.Rope)
            {
                ClearActiveRope();
            }
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
                ToolKind.Rope => BeginRopePlacement(targetWorld, hoveredHold, hoveredSurface),
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
            return activeRope != null && activeRope.Hand == hand;
        }

        public bool TryFireRope(HandState preferredHand, Vector3 targetWorld, ClimbHold hoveredHold, ClimbSurface hoveredSurface)
        {
            if (!toolMode || selectedTool != ToolKind.Rope || preferredHand == null)
            {
                return false;
            }

            HandState ropeHand = ResolveRopeHand(preferredHand);
            if (ropeHand == null)
            {
                return false;
            }

            Vector3 anchorPoint;
            if (hoveredHold != null && hoveredHold.AllowRopeAttach)
            {
                anchorPoint = hoveredHold.Position;
            }
            else if (hoveredSurface != null && hoveredSurface.AllowRopeAttach && hoveredSurface.TryGetGripPoint(targetWorld, out Vector3 surfacePoint))
            {
                anchorPoint = surfacePoint;
            }
            else
            {
                return false;
            }

            Vector3 source = controller.GetHandAnchorWorld(ropeHand);
            anchorPoint.z = 0f;
            if (Vector3.Distance(source, anchorPoint) > ropeRange || !controller.Vitals.TrySpendStamina(4f))
            {
                return false;
            }

            ClearActiveRope();

            GameObject ropeRoot = new GameObject("RopeTether");
            ropeRoot.transform.SetParent(generatedRoot.transform, false);

            GameObject anchorObject = new GameObject("RopeAnchor");
            anchorObject.transform.SetParent(ropeRoot.transform, false);
            anchorObject.transform.position = anchorPoint;

            ClimbHold anchorHold = anchorObject.AddComponent<ClimbHold>();
            anchorHold.Configure(ClimbHoldType.Rope, true, true, 0f, 0f, 0f, 0f, "Rope");

            LineRenderer line = ropeRoot.AddComponent<LineRenderer>();
            line.positionCount = Mathf.Max(ropeVisualSegments, 2);
            line.startWidth = 0.045f;
            line.endWidth = 0.035f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = new Color(0.85f, 0.78f, 0.62f);
            line.endColor = new Color(0.7f, 0.63f, 0.48f);
            line.sortingOrder = 4;
            line.useWorldSpace = true;

            activeRope = new RopeState
            {
                Hand = ropeHand,
                Root = ropeRoot,
                AnchorHold = anchorHold,
                Line = line
            };

            UpdateRopeVisual();
            ToolPlaced?.Invoke(ToolKind.Rope, anchorPoint);
            return true;
        }

        public void UpdateRuntime()
        {
            UpdateRopeVisual();
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
                ? "Hold RMB fire rope  release RMB retract"
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

            if (!controller.Vitals.TrySpendStamina(2f))
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

        private bool BeginRopePlacement(Vector3 targetWorld, ClimbHold hoveredHold, ClimbSurface hoveredSurface)
        {
            return TryFireRope(controller.RightHand, targetWorld, hoveredHold, hoveredSurface);
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

        private void UpdateRopeVisual()
        {
            if (activeRope == null || activeRope.Line == null || activeRope.AnchorHold == null)
            {
                return;
            }

            Vector3 start = controller.GetHandAnchorWorld(activeRope.Hand);
            Vector3 end = activeRope.AnchorHold.Position;
            int segments = activeRope.Line.positionCount;
            float span = Vector3.Distance(start, end);

            for (int i = 0; i < segments; i++)
            {
                float t = segments == 1 ? 0f : i / (float)(segments - 1);
                Vector3 point = Vector3.Lerp(start, end, t);
                point.y -= Mathf.Sin(t * Mathf.PI) * ropeSag * Mathf.Max(1f, span * 0.35f);
                activeRope.Line.SetPosition(i, point);
            }
        }

        private void ClearActiveRope()
        {
            if (activeRope?.Root != null)
            {
                Object.Destroy(activeRope.Root);
            }

            activeRope = null;
        }

        private sealed class RopeState
        {
            public HandState Hand;
            public GameObject Root;
            public ClimbHold AnchorHold;
            public LineRenderer Line;
        }
    }
}
