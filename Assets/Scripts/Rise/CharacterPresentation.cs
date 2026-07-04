using UnityEngine;

namespace Rise
{
    public sealed class CharacterPresentation : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform chest;
        [SerializeField] private Transform leftUpperArm;
        [SerializeField] private Transform leftLowerArm;
        [SerializeField] private Transform leftHandBone;
        [SerializeField] private Transform rightUpperArm;
        [SerializeField] private Transform rightLowerArm;
        [SerializeField] private Transform rightHandBone;
        [SerializeField] private Transform leftUpperLeg;
        [SerializeField] private Transform rightUpperLeg;

        [Header("Offsets")]
        [SerializeField] private Vector3 rootFollowOffset = new Vector3(0f, -0.95f, 0f);
        [SerializeField] private Vector3 leftHandTargetOffset;
        [SerializeField] private Vector3 rightHandTargetOffset;
        [SerializeField] private Vector3 leftHandRotationOffset = new Vector3(0f, 0f, -90f);
        [SerializeField] private Vector3 rightHandRotationOffset = new Vector3(0f, 0f, -90f);

        private PlayerClimbController controller;
        private Quaternion chestInitialLocalRotation;
        private Quaternion leftUpperArmInitialLocalRotation;
        private Quaternion leftLowerArmInitialLocalRotation;
        private Quaternion rightUpperArmInitialLocalRotation;
        private Quaternion rightLowerArmInitialLocalRotation;
        private Quaternion leftUpperLegInitialLocalRotation;
        private Quaternion rightUpperLegInitialLocalRotation;
        private bool initialized;

        public void Initialize(PlayerClimbController owner)
        {
            controller = owner;
            CacheInitialRotations();
        }

        private void Start()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<PlayerClimbController>();
                if (controller == null)
                {
                    controller = Object.FindAnyObjectByType<PlayerClimbController>();
                }
            }

            CacheInitialRotations();
        }

        private void LateUpdate()
        {
            if (controller == null)
            {
                return;
            }

            Transform followRoot = visualRoot != null ? visualRoot : transform;
            followRoot.position = controller.transform.position + rootFollowOffset;

            float horizontalVelocity = controller.BodyVelocity.x;
            float tilt = Mathf.Clamp(horizontalVelocity * -7f, -18f, 18f);

            if (chest != null)
            {
                chest.localRotation = chestInitialLocalRotation * Quaternion.Euler(tilt * 0.35f, 0f, tilt);
            }

            Vector3 leftHandTarget = controller.LeftHand.WorldTarget + leftHandTargetOffset;
            Vector3 rightHandTarget = controller.RightHand.WorldTarget + rightHandTargetOffset;
            AimArmBones(leftUpperArm, leftLowerArm, leftUpperArmInitialLocalRotation, leftLowerArmInitialLocalRotation, leftHandTarget);
            AimArmBones(rightUpperArm, rightLowerArm, rightUpperArmInitialLocalRotation, rightLowerArmInitialLocalRotation, rightHandTarget);
            AimHandBone(leftHandBone, leftHandTarget, leftHandRotationOffset);
            AimHandBone(rightHandBone, rightHandTarget, rightHandRotationOffset);

            if (leftUpperLeg != null)
            {
                leftUpperLeg.localRotation = leftUpperLegInitialLocalRotation *
                                             Quaternion.Euler(controller.LeftKickVisual * -32f, 0f, controller.LeftKickVisual * 14f);
            }

            if (rightUpperLeg != null)
            {
                rightUpperLeg.localRotation = rightUpperLegInitialLocalRotation *
                                              Quaternion.Euler(controller.RightKickVisual * -32f, 0f, controller.RightKickVisual * -14f);
            }
        }

        private void CacheInitialRotations()
        {
            if (initialized)
            {
                return;
            }

            if (chest != null) chestInitialLocalRotation = chest.localRotation;
            if (leftUpperArm != null) leftUpperArmInitialLocalRotation = leftUpperArm.localRotation;
            if (leftLowerArm != null) leftLowerArmInitialLocalRotation = leftLowerArm.localRotation;
            if (rightUpperArm != null) rightUpperArmInitialLocalRotation = rightUpperArm.localRotation;
            if (rightLowerArm != null) rightLowerArmInitialLocalRotation = rightLowerArm.localRotation;
            if (leftUpperLeg != null) leftUpperLegInitialLocalRotation = leftUpperLeg.localRotation;
            if (rightUpperLeg != null) rightUpperLegInitialLocalRotation = rightUpperLeg.localRotation;

            initialized = true;
        }

        private static void AimArmBones(Transform upperArm, Transform lowerArm, Quaternion upperInitial, Quaternion lowerInitial, Vector3 target)
        {
            if (upperArm == null || lowerArm == null)
            {
                return;
            }

            upperArm.localRotation = upperInitial;
            lowerArm.localRotation = lowerInitial;
            RotateBoneTowards(upperArm, target);
            RotateBoneTowards(lowerArm, target);
        }

        private static void RotateBoneTowards(Transform bone, Vector3 target)
        {
            Vector3 direction = target - bone.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 currentAxis = bone.TransformDirection(Vector3.right);
            Quaternion delta = Quaternion.FromToRotation(currentAxis, direction.normalized);
            bone.rotation = delta * bone.rotation;
        }

        private static void AimHandBone(Transform handBone, Vector3 target, Vector3 eulerOffset)
        {
            if (handBone == null)
            {
                return;
            }

            Vector3 aimOrigin = handBone.parent != null ? handBone.parent.position : handBone.position;
            Vector3 direction = target - aimOrigin;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            handBone.rotation = Quaternion.LookRotation(Vector3.forward, direction.normalized) * Quaternion.Euler(eulerOffset);
        }
    }
}
