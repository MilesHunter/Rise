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

            SolveArmIk(leftUpperArm, leftLowerArm, leftHandBone, leftUpperArmInitialLocalRotation, leftLowerArmInitialLocalRotation, controller.LeftHand.WorldTarget + leftHandTargetOffset, true);
            SolveArmIk(rightUpperArm, rightLowerArm, rightHandBone, rightUpperArmInitialLocalRotation, rightLowerArmInitialLocalRotation, controller.RightHand.WorldTarget + rightHandTargetOffset, false);
            SnapHandBone(leftHandBone, controller.LeftHand.WorldTarget + leftHandTargetOffset, leftHandRotationOffset);
            SnapHandBone(rightHandBone, controller.RightHand.WorldTarget + rightHandTargetOffset, rightHandRotationOffset);

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

        private void SolveArmIk(Transform upperArm, Transform lowerArm, Transform handBone, Quaternion upperInitial, Quaternion lowerInitial, Vector3 target, bool isLeftArm)
        {
            if (upperArm == null || lowerArm == null)
            {
                return;
            }

            upperArm.localRotation = upperInitial;
            lowerArm.localRotation = lowerInitial;

            Transform handReference = handBone != null ? handBone : FindFirstChildBone(lowerArm);
            if (handReference == null)
            {
                RotateBoneTowards(upperArm, target);
                RotateBoneTowards(lowerArm, target);
                return;
            }

            Vector2 shoulder = ToPlane(upperArm.position);
            Vector2 elbow = ToPlane(lowerArm.position);
            Vector2 wrist = ToPlane(handReference.position);
            Vector2 target2D = ToPlane(target);

            float upperLength = Vector2.Distance(shoulder, elbow);
            float lowerLength = Vector2.Distance(elbow, wrist);
            if (upperLength <= 0.0001f || lowerLength <= 0.0001f)
            {
                RotateBoneTowards(upperArm, target);
                RotateBoneTowards(lowerArm, target);
                return;
            }

            Vector2 toTarget = target2D - shoulder;
            float distanceToTarget = Mathf.Max(0.0001f, toTarget.magnitude);
            float clampedDistance = Mathf.Clamp(distanceToTarget, Mathf.Abs(upperLength - lowerLength) + 0.001f, upperLength + lowerLength - 0.001f);
            Vector2 targetDirection = toTarget / distanceToTarget;
            Vector2 bendNormal = isLeftArm ? Vector2.left : Vector2.right;

            float shoulderToElbowAlongTarget = ((upperLength * upperLength) - (lowerLength * lowerLength) + (clampedDistance * clampedDistance)) / (2f * clampedDistance);
            float elbowHeight = Mathf.Sqrt(Mathf.Max(0f, (upperLength * upperLength) - (shoulderToElbowAlongTarget * shoulderToElbowAlongTarget)));

            Vector2 elbowTarget = shoulder + targetDirection * shoulderToElbowAlongTarget + bendNormal * elbowHeight;
            Vector2 wristTarget = shoulder + targetDirection * clampedDistance;

            RotateBoneTowards(upperArm, FromPlane(elbowTarget, upperArm.position.z));
            RotateBoneTowards(lowerArm, FromPlane(wristTarget, lowerArm.position.z));
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

        private static void SnapHandBone(Transform handBone, Vector3 target, Vector3 eulerOffset)
        {
            if (handBone == null)
            {
                return;
            }

            Vector3 direction = target - handBone.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            handBone.rotation = Quaternion.LookRotation(Vector3.forward, direction.normalized) * Quaternion.Euler(eulerOffset);
        }

        private static Transform FindFirstChildBone(Transform parentBone)
        {
            if (parentBone == null || parentBone.childCount == 0)
            {
                return null;
            }

            return parentBone.GetChild(0);
        }

        private static Vector2 ToPlane(Vector3 point)
        {
            return new Vector2(point.x, point.y);
        }

        private static Vector3 FromPlane(Vector2 point, float z)
        {
            return new Vector3(point.x, point.y, z);
        }
    }
}
