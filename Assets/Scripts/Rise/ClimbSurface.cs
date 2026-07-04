using System.Collections.Generic;
using UnityEngine;

namespace Rise
{
    [RequireComponent(typeof(Collider))]
    public sealed class ClimbSurface : MonoBehaviour
    {
        private static readonly List<ClimbSurface> ActiveSurfacesInternal = new List<ClimbSurface>();

        [SerializeField] private string surfaceLabel = "Rock";
        [SerializeField] private float grabCost = 2f;
        [SerializeField] private float holdDrainPerSecond = 1f;
        [SerializeField] private float slipCheckInterval;
        [SerializeField] private float slipChance;
        [SerializeField] private bool allowAnchorAttach = true;
        [SerializeField] private bool allowRopeAttach = true;

        private Collider cachedCollider;

        public static IReadOnlyList<ClimbSurface> ActiveSurfaces => ActiveSurfacesInternal;

        public string SurfaceLabel => surfaceLabel;
        public float GrabCost => grabCost;
        public float HoldDrainPerSecond => holdDrainPerSecond;
        public float SlipCheckInterval => slipCheckInterval;
        public float SlipChance => slipChance;
        public bool AllowAnchorAttach => allowAnchorAttach;
        public bool AllowRopeAttach => allowRopeAttach;

        public void Configure(string label, float initialGrabCost, float drainPerSecond, float interval, float chanceToSlip, bool canAnchor, bool canRope)
        {
            surfaceLabel = label;
            grabCost = initialGrabCost;
            holdDrainPerSecond = drainPerSecond;
            slipCheckInterval = interval;
            slipChance = chanceToSlip;
            allowAnchorAttach = canAnchor;
            allowRopeAttach = canRope;
        }

        public bool TryGetGripPoint(Vector3 cursorWorld, out Vector3 gripPoint)
        {
            Collider targetCollider = GetCollider();
            if (targetCollider == null)
            {
                gripPoint = default;
                return false;
            }

            gripPoint = targetCollider.ClosestPoint(new Vector3(cursorWorld.x, cursorWorld.y, targetCollider.bounds.center.z));
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
