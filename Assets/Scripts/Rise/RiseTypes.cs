using System;
using UnityEngine;

namespace Rise
{
    public enum HandGrabState
    {
        Idle,
        Reach,
        Holding,
        Releasing,
        AssistedHold
    }

    public enum ClimbHoldType
    {
        Normal,
        Anchor,
        Rope
    }

    public enum ClimbSurfaceLabelPreset
    {
        Rock,
        RoughWall,
        Ledge,
        Shelf,
        IcyWall,
        WindBurntWall,
        MossyRock,
        Custom
    }

    public enum ToolKind
    {
        Anchor,
        Rope
    }

    public enum RestPointType
    {
        ShortRest,
        LongRest
    }

    [Serializable]
    public sealed class HandState
    {
        public string DisplayName;
        public bool IsLeft;
        public HandGrabState State = HandGrabState.Idle;
        public ClimbHold CurrentHold;
        public bool OwnsRuntimeHold;
        public bool IsPressed;
        public Vector3 LocalAnchorOffset;
        public Vector3 WorldTarget;
        public float HoldDrainTimer;
        public float SlipCheckTimer;

        public bool HasHold => CurrentHold != null;
        public bool IsAssistedHold => CurrentHold != null && CurrentHold.ZeroStaminaHold;

        public void Clear()
        {
            State = HandGrabState.Idle;
            CurrentHold = null;
            OwnsRuntimeHold = false;
            IsPressed = false;
            HoldDrainTimer = 0f;
            SlipCheckTimer = 0f;
        }
    }
}
