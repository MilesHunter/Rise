using UnityEngine;
using System;
using Object = UnityEngine.Object;

namespace Rise
{
    public sealed class ToolController : MonoBehaviour
    {
        [SerializeField] private ToolKind selectedTool = ToolKind.Anchor;
        [SerializeField] private bool toolMode;
        [SerializeField] private float ropeRange = 5f;
        [SerializeField] private int ropeSegments = 4;

        private PlayerClimbController controller;
        private ClimbHold activeAnchor;
        private GameObject activeRopeRoot;
        private GameObject generatedRoot;

        public event Action<ToolKind, Vector3> ToolPlaced;

        public ToolKind SelectedTool => selectedTool;
        public bool ToolMode => toolMode;

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
        }

        public void CycleTool(float scrollValue)
        {
            if (Mathf.Approximately(scrollValue, 0f))
            {
                return;
            }

            selectedTool = scrollValue > 0f ? ToolKind.Rope : ToolKind.Anchor;
        }

        public bool TryUseTool(Vector3 targetWorld, ClimbHold hoveredHold, ClimbSurface hoveredSurface)
        {
            if (!toolMode)
            {
                return false;
            }

            bool used;
            switch (selectedTool)
            {
                case ToolKind.Anchor:
                    used = TryPlaceAnchor(targetWorld, hoveredHold, hoveredSurface);
                    break;
                case ToolKind.Rope:
                    used = TryPlaceRope(targetWorld, hoveredHold, hoveredSurface);
                    break;
                default:
                    used = false;
                    break;
            }

            if (used)
            {
                toolMode = false;
            }

            return used;
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
                Destroy(activeAnchor.gameObject);
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

        private bool TryPlaceRope(Vector3 targetWorld, ClimbHold hoveredHold, ClimbSurface hoveredSurface)
        {
            Vector3 source = controller.transform.position + new Vector3(0f, 1.25f, 0f);
            Vector3 target = targetWorld;
            if (hoveredHold != null && hoveredHold.AllowRopeAttach)
            {
                target = hoveredHold.Position;
            }
            else if (hoveredSurface != null && hoveredSurface.AllowRopeAttach && hoveredSurface.TryGetGripPoint(targetWorld, out Vector3 surfacePoint))
            {
                target = surfacePoint;
            }
            target.z = 0f;

            if (Vector3.Distance(source, target) > ropeRange || !controller.Vitals.TrySpendStamina(4f))
            {
                return false;
            }

            if (activeRopeRoot != null)
            {
                Destroy(activeRopeRoot);
            }

            activeRopeRoot = new GameObject("RopePath");
            activeRopeRoot.transform.SetParent(generatedRoot.transform, false);

            for (int i = 1; i <= ropeSegments; i++)
            {
                float t = i / (float)ropeSegments;
                Vector3 position = Vector3.Lerp(source, target, t) + Vector3.right * Mathf.Sin(t * Mathf.PI) * 0.25f;

                GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                segment.name = $"RopeHold_{i}";
                segment.transform.SetParent(activeRopeRoot.transform, false);
                segment.transform.position = position;
                segment.transform.localScale = Vector3.one * 0.2f;
                Object.Destroy(segment.GetComponent<SphereCollider>());

                Renderer renderer = segment.GetComponent<Renderer>();
                renderer.material.color = new Color(0.25f, 0.85f, 0.95f);

                ClimbHold hold = segment.AddComponent<ClimbHold>();
                hold.Configure(ClimbHoldType.Rope, true, true, 0f, 0f, 0f, 0f, "Rope");
            }

            ToolPlaced?.Invoke(ToolKind.Rope, target);
            return true;
        }
    }
}
