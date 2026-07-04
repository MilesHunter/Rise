using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Rise
{
    [RequireComponent(typeof(Collider))]
    public sealed class ClimbSurface : MonoBehaviour
    {
        private static readonly List<ClimbSurface> ActiveSurfacesInternal = new List<ClimbSurface>();
        private const float DefaultGripProbeDepth = 4f;

        [SerializeField] private ClimbSurfaceLabelPreset surfaceLabelPreset = ClimbSurfaceLabelPreset.Rock;
        [FormerlySerializedAs("surfaceLabel")]
        [SerializeField] private string customSurfaceLabel = string.Empty;
        [SerializeField] private SurfaceAudioProfile surfaceAudio;
        [SerializeField] private float grabCost = 2f;
        [SerializeField] private float holdDrainPerSecond = 1f;
        [SerializeField] private float slipCheckInterval;
        [SerializeField] private float slipChance;
        [SerializeField] private bool allowAnchorAttach = true;
        [SerializeField] private bool allowRopeAttach = true;
        [SerializeField] private float gripProbeDepth = DefaultGripProbeDepth;

        private Collider cachedCollider;

        public static IReadOnlyList<ClimbSurface> ActiveSurfaces => ActiveSurfacesInternal;

        public string SurfaceLabel => ResolveSurfaceLabel();
        public SurfaceAudioProfile SurfaceAudio => surfaceAudio;
        public float GrabCost => grabCost;
        public float HoldDrainPerSecond => holdDrainPerSecond;
        public float SlipCheckInterval => slipCheckInterval;
        public float SlipChance => slipChance;
        public bool AllowAnchorAttach => allowAnchorAttach;
        public bool AllowRopeAttach => allowRopeAttach;

        public void Configure(string label, float initialGrabCost, float drainPerSecond, float interval, float chanceToSlip, bool canAnchor, bool canRope)
        {
            customSurfaceLabel = label;
            surfaceLabelPreset = ClimbSurfaceLabelPreset.Custom;
            grabCost = initialGrabCost;
            holdDrainPerSecond = drainPerSecond;
            slipCheckInterval = interval;
            slipChance = chanceToSlip;
            allowAnchorAttach = canAnchor;
            allowRopeAttach = canRope;
        }

        private string ResolveSurfaceLabel()
        {
            if (!string.IsNullOrWhiteSpace(customSurfaceLabel))
            {
                return customSurfaceLabel;
            }

            switch (surfaceLabelPreset)
            {
                case ClimbSurfaceLabelPreset.Rock:
                    return "Rock";
                case ClimbSurfaceLabelPreset.RoughWall:
                    return "Rough wall";
                case ClimbSurfaceLabelPreset.Ledge:
                    return "Ledge";
                case ClimbSurfaceLabelPreset.Shelf:
                    return "Shelf";
                case ClimbSurfaceLabelPreset.IcyWall:
                    return "Icy wall";
                case ClimbSurfaceLabelPreset.WindBurntWall:
                    return "Wind-burnt wall";
                case ClimbSurfaceLabelPreset.MossyRock:
                    return "Mossy rock";
                case ClimbSurfaceLabelPreset.Custom:
                    return "Custom surface";
                default:
                    return "Rock";
            }
        }

        public bool TryGetGripPoint(Vector3 targetWorld, out Vector3 gripPoint)
        {
            Collider targetCollider = GetCollider();
            if (targetCollider == null)
            {
                gripPoint = default;
                return false;
            }

            if (TryGetRaycastGripPoint(targetCollider, targetWorld, out gripPoint))
            {
                return true;
            }

            gripPoint = targetCollider.ClosestPoint(new Vector3(targetWorld.x, targetWorld.y, targetCollider.bounds.center.z));
            gripPoint.z = 0f;
            return true;
        }

        public bool TryGetSupportPoint(Vector3 footWorld, float horizontalReach, float verticalReach, out Vector3 supportPoint)
        {
            Collider targetCollider = GetCollider();
            if (targetCollider == null)
            {
                supportPoint = default;
                return false;
            }

            Bounds bounds = targetCollider.bounds;
            float clampedX = Mathf.Clamp(footWorld.x, bounds.min.x, bounds.max.x);
            float dx = Mathf.Abs(footWorld.x - clampedX);
            float topY = bounds.max.y;
            bool isAboveSurface = footWorld.y >= topY - 0.05f;
            float verticalDrop = footWorld.y - topY;

            if (dx > horizontalReach || !isAboveSurface || verticalDrop > verticalReach)
            {
                supportPoint = default;
                return false;
            }

            supportPoint = new Vector3(clampedX, topY, 0f);
            return true;
        }


        private bool TryGetRaycastGripPoint(Collider targetCollider, Vector3 targetWorld, out Vector3 gripPoint)
        {
            float probeDepth = Mathf.Max(gripProbeDepth, 0.1f);
            Vector3 rayOriginFront = new Vector3(targetWorld.x, targetWorld.y, targetWorld.z - probeDepth);
            Vector3 rayOriginBack = new Vector3(targetWorld.x, targetWorld.y, targetWorld.z + probeDepth);
            float rayDistance = probeDepth * 2f;

            bool hitFront = targetCollider.Raycast(new Ray(rayOriginFront, Vector3.forward), out RaycastHit frontHit, rayDistance);
            bool hitBack = targetCollider.Raycast(new Ray(rayOriginBack, Vector3.back), out RaycastHit backHit, rayDistance);

            if (!hitFront && !hitBack)
            {
                gripPoint = default;
                return false;
            }

            RaycastHit chosenHit = !hitBack || (hitFront && frontHit.distance <= backHit.distance) ? frontHit : backHit;
            gripPoint = chosenHit.point;
            gripPoint.z = 0f;
            return true;
        }

        private Collider GetCollider()
        {
            if (cachedCollider == null)
            {
                cachedCollider = GetComponent<Collider>();
            }

            return cachedCollider;
        }

        private void OnEnable()
        {
            if (!ActiveSurfacesInternal.Contains(this))
            {
                ActiveSurfacesInternal.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveSurfacesInternal.Remove(this);
        }
    }
}
