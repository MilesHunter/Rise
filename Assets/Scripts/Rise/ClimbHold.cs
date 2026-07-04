using System.Collections.Generic;
using UnityEngine;

namespace Rise
{
    public sealed class ClimbHold : MonoBehaviour
    {
        private static readonly List<ClimbHold> ActiveHoldsInternal = new List<ClimbHold>();

        [SerializeField] private ClimbHoldType holdType = ClimbHoldType.Normal;
        [SerializeField] private bool allowAnchorAttach = true;
        [SerializeField] private bool allowRopeAttach = true;
        [SerializeField] private float initialGrabCost = 2f;
        [SerializeField] private float holdDrainPerSecond = 1f;
        [SerializeField] private float slipCheckInterval;
        [SerializeField] private float slipChance;
        [SerializeField] private string gripLabel = "Grip";

        public static IReadOnlyList<ClimbHold> ActiveHolds => ActiveHoldsInternal;

        public ClimbHoldType HoldType => holdType;
        public bool AllowAnchorAttach => allowAnchorAttach;
        public bool AllowRopeAttach => allowRopeAttach;
        public bool ZeroStaminaHold => holdType == ClimbHoldType.Anchor || holdType == ClimbHoldType.Rope;
        public float InitialGrabCost => ZeroStaminaHold ? 0f : initialGrabCost;
        public float HoldDrainPerSecond => ZeroStaminaHold ? 0f : holdDrainPerSecond;
        public float SlipCheckInterval => slipCheckInterval;
        public float SlipChance => slipChance;
        public string GripLabel => gripLabel;
        public Vector3 Position => transform.position;

        public void Configure(ClimbHoldType type, bool canAnchor, bool canRope, float grabCost = 2f, float drainPerSecond = 1f, float slipInterval = 0f, float chanceToSlip = 0f, string label = "Grip")
        {
            holdType = type;
            allowAnchorAttach = canAnchor;
            allowRopeAttach = canRope;
            initialGrabCost = grabCost;
            holdDrainPerSecond = drainPerSecond;
            slipCheckInterval = slipInterval;
            slipChance = chanceToSlip;
            gripLabel = label;
        }

        private void OnEnable()
        {
            if (!ActiveHoldsInternal.Contains(this))
            {
                ActiveHoldsInternal.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveHoldsInternal.Remove(this);
        }
    }
}
