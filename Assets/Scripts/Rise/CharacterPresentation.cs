using UnityEngine;

namespace Rise
{
    public sealed class CharacterPresentation : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform hips;
        [SerializeField] private Transform chest;
        [SerializeField] private Transform leftUpperArm;
        [SerializeField] private Transform leftLowerArm;
        [SerializeField] private Transform leftHandBone;
        [SerializeField] private Transform rightUpperArm;
        [SerializeField] private Transform rightLowerArm;
        [SerializeField] private Transform rightHandBone;
        [SerializeField] private Transform leftUpperLeg;
        [SerializeField] private Transform leftLowerLeg;
        [SerializeField] private Transform rightUpperLeg;
        [SerializeField] private Transform rightLowerLeg;

        [Header("Offsets")]
        [SerializeField] private Vector3 rootFollowOffset;
        [SerializeField] private Vector3 leftHandTargetOffset;
        [SerializeField] private Vector3 rightHandTargetOffset;
        [SerializeField] private Vector3 leftHandRotationOffset = new Vector3(0f, 0f, -90f);
        [SerializeField] private Vector3 rightHandRotationOffset = new Vector3(0f, 0f, -90f);
        [SerializeField] private float modelScale = 0.65f;
        [SerializeField] private float poseBlendSpeed = 9f;
        [SerializeField] private float visualPlaneZ = -0.18f;
        [SerializeField] private float handMarkerSize = 0.18f;
        [SerializeField] private float freeHandMarkerSize = 0.1f;
        [SerializeField] private bool showHandDebugPoints = true;
        [SerializeField] private float debugHandPointSize = 0.16f;
        [SerializeField] private float debugTargetPointSize = 0.1f;
        [SerializeField] private float debugHoldPointSize = 0.22f;
        [SerializeField] private float debugLineWidth = 0.035f;
        [SerializeField] private Color idleHandColor = new Color(0.7f, 0.72f, 0.75f, 0.85f);
        [SerializeField] private Color reachingHandColor = new Color(1f, 0.78f, 0.2f, 1f);
        [SerializeField] private Color grippingHandColor = new Color(0.25f, 1f, 0.42f, 1f);
        [SerializeField] private Color leftHandPointColor = new Color(0.05f, 0.85f, 1f, 1f);
        [SerializeField] private Color leftTargetPointColor = new Color(0.1f, 0.55f, 1f, 0.45f);
        [SerializeField] private Color leftHoldPointColor = new Color(0.65f, 1f, 1f, 1f);
        [SerializeField] private Color rightHandPointColor = new Color(1f, 0.45f, 0.08f, 1f);
        [SerializeField] private Color rightTargetPointColor = new Color(1f, 0.35f, 0.05f, 0.45f);
        [SerializeField] private Color rightHoldPointColor = new Color(1f, 0.82f, 0.2f, 1f);

        private PlayerClimbController controller;
        private Quaternion hipsInitialLocalRotation;
        private Quaternion chestInitialLocalRotation;
        private Quaternion leftUpperArmInitialLocalRotation;
        private Quaternion leftLowerArmInitialLocalRotation;
        private Quaternion rightUpperArmInitialLocalRotation;
        private Quaternion rightLowerArmInitialLocalRotation;
        private Quaternion leftUpperLegInitialLocalRotation;
        private Quaternion leftLowerLegInitialLocalRotation;
        private Quaternion rightUpperLegInitialLocalRotation;
        private Quaternion rightLowerLegInitialLocalRotation;
        private Vector3 visualRootInitialLocalScale;
        private Transform leftMarker;
        private Transform rightMarker;
        private Transform leftTargetMarker;
        private Transform rightTargetMarker;
        private Transform leftHoldMarker;
        private Transform rightHoldMarker;
        private Material leftMarkerMaterial;
        private Material rightMarkerMaterial;
        private Material leftTargetMarkerMaterial;
        private Material rightTargetMarkerMaterial;
        private Material leftHoldMarkerMaterial;
        private Material rightHoldMarkerMaterial;
        private LineRenderer leftHoldLine;
        private LineRenderer rightHoldLine;
        private Material leftHoldLineMaterial;
        private Material rightHoldLineMaterial;
        private float climbPose;
        private float restPose;
        private bool initialized;

        public void Initialize(PlayerClimbController owner)
        {
            controller = owner;
            AutoBindRig();
            CacheInitialRotations();
            EnsureHandStateVisuals();
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

            AutoBindRig();
            CacheInitialRotations();
            EnsureHandStateVisuals();
        }

        private void LateUpdate()
        {
            if (controller == null)
            {
                return;
            }

            Transform followRoot = visualRoot != null ? visualRoot : transform;
            Vector3 followPosition = controller.transform.position + rootFollowOffset;
            followPosition.z = visualPlaneZ;
            followRoot.position = followPosition;
            followRoot.rotation = Quaternion.identity;
            followRoot.localScale = visualRootInitialLocalScale == Vector3.zero ? Vector3.one * modelScale : visualRootInitialLocalScale;

            float horizontalVelocity = controller.BodyVelocity.x;
            float tilt = Mathf.Clamp(horizontalVelocity * -7f, -18f, 18f);
            bool hasHold = controller.LeftHand.HasHold || controller.RightHand.HasHold;
            climbPose = Mathf.MoveTowards(climbPose, hasHold ? 1f : 0f, Time.deltaTime * poseBlendSpeed);
            restPose = Mathf.MoveTowards(restPose, controller.BodyVelocity.sqrMagnitude < 0.001f && !hasHold ? 0.25f : 0f, Time.deltaTime * poseBlendSpeed);

            if (hips != null)
            {
                float bodySwing = Mathf.Clamp(controller.BodyVelocity.x * 3f, -10f, 10f);
                hips.localRotation = hipsInitialLocalRotation *
                                     Quaternion.Euler(Mathf.Lerp(0f, -8f, climbPose) + restPose * 6f, 0f, bodySwing);
            }

            if (chest != null)
            {
                float reachTwist = Mathf.Clamp((controller.RightHand.WorldTarget.y - controller.LeftHand.WorldTarget.y) * 5f, -12f, 12f);
                chest.localRotation = chestInitialLocalRotation *
                                      Quaternion.Euler(Mathf.Lerp(0f, -12f, climbPose) + tilt * 0.35f, 0f, tilt + reachTwist);
            }

            Vector3 leftHandTarget = ToVisualPlane(controller.LeftHand.WorldTarget + leftHandTargetOffset);
            Vector3 rightHandTarget = ToVisualPlane(controller.RightHand.WorldTarget + rightHandTargetOffset);
            AimArmBones(leftUpperArm, leftLowerArm, leftHandBone, leftUpperArmInitialLocalRotation, leftLowerArmInitialLocalRotation, leftHandTarget, true);
            AimArmBones(rightUpperArm, rightLowerArm, rightHandBone, rightUpperArmInitialLocalRotation, rightLowerArmInitialLocalRotation, rightHandTarget, false);
            AimHandBone(leftHandBone, leftHandTarget, leftHandRotationOffset);
            AimHandBone(rightHandBone, rightHandTarget, rightHandRotationOffset);

            AnimateLeg(leftUpperLeg, leftLowerLeg, leftUpperLegInitialLocalRotation, leftLowerLegInitialLocalRotation, true, controller.LeftKickVisual);
            AnimateLeg(rightUpperLeg, rightLowerLeg, rightUpperLegInitialLocalRotation, rightLowerLegInitialLocalRotation, false, controller.RightKickVisual);
            ReportVisibleHandPoint(controller.LeftHand, leftHandBone);
            ReportVisibleHandPoint(controller.RightHand, rightHandBone);
            UpdateHandStateVisual(controller.LeftHand, leftMarker, leftMarkerMaterial, leftHandBone, leftHandTarget);
            UpdateHandStateVisual(controller.RightHand, rightMarker, rightMarkerMaterial, rightHandBone, rightHandTarget);
            UpdateHandDebugVisuals(controller.LeftHand, leftMarker, leftMarkerMaterial, leftTargetMarker, leftTargetMarkerMaterial, leftHoldMarker, leftHoldMarkerMaterial, leftHoldLine, leftHoldLineMaterial);
            UpdateHandDebugVisuals(controller.RightHand, rightMarker, rightMarkerMaterial, rightTargetMarker, rightTargetMarkerMaterial, rightHoldMarker, rightHoldMarkerMaterial, rightHoldLine, rightHoldLineMaterial);
        }

        private void CacheInitialRotations()
        {
            if (initialized)
            {
                return;
            }

            Transform followRoot = visualRoot != null ? visualRoot : transform;
            visualRootInitialLocalScale = Vector3.one * modelScale;

            if (hips != null) hipsInitialLocalRotation = hips.localRotation;
            if (chest != null) chestInitialLocalRotation = chest.localRotation;
            if (leftUpperArm != null) leftUpperArmInitialLocalRotation = leftUpperArm.localRotation;
            if (leftLowerArm != null) leftLowerArmInitialLocalRotation = leftLowerArm.localRotation;
            if (rightUpperArm != null) rightUpperArmInitialLocalRotation = rightUpperArm.localRotation;
            if (rightLowerArm != null) rightLowerArmInitialLocalRotation = rightLowerArm.localRotation;
            if (leftUpperLeg != null) leftUpperLegInitialLocalRotation = leftUpperLeg.localRotation;
            if (leftLowerLeg != null) leftLowerLegInitialLocalRotation = leftLowerLeg.localRotation;
            if (rightUpperLeg != null) rightUpperLegInitialLocalRotation = rightUpperLeg.localRotation;
            if (rightLowerLeg != null) rightLowerLegInitialLocalRotation = rightLowerLeg.localRotation;

            initialized = true;
        }

        private void EnsureHandStateVisuals()
        {
            RemoveLegacyFakeArmVisuals();
            leftMarker ??= CreateMarker("LeftHandGripPoint", out leftMarkerMaterial);
            rightMarker ??= CreateMarker("RightHandGripPoint", out rightMarkerMaterial);
            leftTargetMarker ??= CreateMarker("LeftHandTargetPoint", out leftTargetMarkerMaterial);
            rightTargetMarker ??= CreateMarker("RightHandTargetPoint", out rightTargetMarkerMaterial);
            leftHoldMarker ??= CreateMarker("LeftActualGripPoint", out leftHoldMarkerMaterial);
            rightHoldMarker ??= CreateMarker("RightActualGripPoint", out rightHoldMarkerMaterial);
            leftHoldLine ??= CreateLine("LeftHandToGripLine", out leftHoldLineMaterial);
            rightHoldLine ??= CreateLine("RightHandToGripLine", out rightHoldLineMaterial);
        }

        private void AutoBindRig()
        {
            if (visualRoot == null)
            {
                visualRoot = transform.childCount > 0 ? transform.GetChild(0) : transform;
            }

            hips ??= FindDeepChild(visualRoot, "hips");
            chest ??= FindDeepChild(visualRoot, "chest");
            leftUpperArm ??= FindDeepChild(visualRoot, "upper_arm.L");
            leftLowerArm ??= FindDeepChild(visualRoot, "lower_arm.L");
            leftHandBone ??= FindDeepChild(visualRoot, "lower_arm.L.001");
            rightUpperArm ??= FindDeepChild(visualRoot, "upper_arm.R");
            rightLowerArm ??= FindDeepChild(visualRoot, "lower_arm.R");
            rightHandBone ??= FindDeepChild(visualRoot, "lower_arm.R.001");
            leftUpperLeg ??= FindDeepChild(visualRoot, "upper_leg.L");
            leftLowerLeg ??= FindDeepChild(visualRoot, "lower_leg.L");
            rightUpperLeg ??= FindDeepChild(visualRoot, "upper_leg.R");
            rightLowerLeg ??= FindDeepChild(visualRoot, "lower_leg.R");
        }

        private static void AimArmBones(Transform upperArm, Transform lowerArm, Transform handBone, Quaternion upperInitial, Quaternion lowerInitial, Vector3 target, bool isLeft)
        {
            if (upperArm == null || lowerArm == null || handBone == null)
            {
                return;
            }

            upperArm.localRotation = upperInitial;
            lowerArm.localRotation = lowerInitial;

            Vector3 shoulder = upperArm.position;
            Vector3 relaxedElbow = lowerArm.position;
            Vector3 relaxedHand = handBone.position;
            float upperLength = Vector2.Distance(new Vector2(shoulder.x, shoulder.y), new Vector2(relaxedElbow.x, relaxedElbow.y));
            float lowerLength = Vector2.Distance(new Vector2(relaxedElbow.x, relaxedElbow.y), new Vector2(relaxedHand.x, relaxedHand.y));

            if (upperLength < 0.001f || lowerLength < 0.001f)
            {
                RotateBoneTowards(upperArm, target);
                RotateBoneTowards(lowerArm, target);
                return;
            }

            Vector3 shoulderToTarget = target - shoulder;
            shoulderToTarget.z = 0f;
            float distance = Mathf.Clamp(shoulderToTarget.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.001f, upperLength + lowerLength - 0.001f);
            Vector3 direction = shoulderToTarget.sqrMagnitude > 0.0001f ? shoulderToTarget.normalized : (relaxedHand - shoulder).normalized;
            direction.z = 0f;

            Vector3 reachableTarget = shoulder + direction * distance;
            reachableTarget.z = target.z;
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(upperLength * upperLength - along * along, 0f));
            Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f);
            float bendSide = Mathf.Sign(Vector3.Dot(relaxedElbow - shoulder, perpendicular));
            if (Mathf.Abs(bendSide) < 0.5f)
            {
                bendSide = isLeft ? -1f : 1f;
            }

            Vector3 elbowTarget = shoulder + direction * along + perpendicular * height * bendSide;
            elbowTarget.z = relaxedElbow.z;

            RotateSegmentTowards(upperArm, lowerArm.position - upperArm.position, elbowTarget - upperArm.position);
            RotateSegmentTowards(lowerArm, handBone.position - lowerArm.position, reachableTarget - lowerArm.position);
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

        private static void RotateSegmentTowards(Transform bone, Vector3 currentVector, Vector3 targetVector)
        {
            currentVector.z = 0f;
            targetVector.z = 0f;
            if (currentVector.sqrMagnitude < 0.0001f || targetVector.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion delta = Quaternion.FromToRotation(currentVector.normalized, targetVector.normalized);
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

        private void AnimateLeg(Transform upperLeg, Transform lowerLeg, Quaternion upperInitial, Quaternion lowerInitial, bool left, float kick)
        {
            if (upperLeg == null)
            {
                return;
            }

            float side = left ? -1f : 1f;
            float idleSwing = Mathf.Sin(Time.time * 3.2f + (left ? 0f : Mathf.PI)) * 4f * (1f - climbPose);
            float climbBend = Mathf.Lerp(7f, 46f, climbPose);
            float kickLift = kick * -54f;
            float kickSplay = kick * side * -24f;
            upperLeg.localRotation = upperInitial * Quaternion.Euler(kickLift - climbBend, 0f, idleSwing + side * 18f * climbPose + kickSplay);

            if (lowerLeg != null)
            {
                lowerLeg.localRotation = lowerInitial * Quaternion.Euler(Mathf.Lerp(0f, 42f, climbPose) + kick * 48f, 0f, side * -7f * climbPose);
            }
        }

        private void UpdateHandStateVisual(HandState hand, Transform marker, Material markerMaterial, Transform handBone, Vector3 target)
        {
            if (marker == null || markerMaterial == null)
            {
                return;
            }

            Color color = showHandDebugPoints
                ? (hand.IsLeft ? leftHandPointColor : rightHandPointColor)
                : hand.HasHold ? grippingHandColor : hand.IsPressed ? reachingHandColor : idleHandColor;
            float size = hand.HasHold || hand.IsPressed ? handMarkerSize : freeHandMarkerSize;
            marker.position = hand.HasHold && hand.CurrentHold != null
                ? ToVisualPlane(hand.CurrentHold.Position)
                : handBone != null ? handBone.position : target;
            marker.localScale = Vector3.one * (showHandDebugPoints ? debugHandPointSize : size);
            markerMaterial.color = color;
        }

        private void UpdateHandDebugVisuals(HandState hand, Transform handMarker, Material handMaterial, Transform targetMarker, Material targetMaterial, Transform holdMarker, Material holdMaterial, LineRenderer holdLine, Material lineMaterial)
        {
            SetActiveIfPresent(handMarker, showHandDebugPoints);
            SetActiveIfPresent(targetMarker, showHandDebugPoints);
            SetActiveIfPresent(holdMarker, showHandDebugPoints && hand.HasHold);
            SetLineActive(holdLine, showHandDebugPoints && hand.HasHold);

            if (!showHandDebugPoints)
            {
                return;
            }

            Color handColor = hand.IsLeft ? leftHandPointColor : rightHandPointColor;
            Color targetColor = hand.IsLeft ? leftTargetPointColor : rightTargetPointColor;
            Color holdColor = hand.IsLeft ? leftHoldPointColor : rightHoldPointColor;
            Vector3 handPoint = ToVisualPlane(hand.HasVisibleWorldPoint ? hand.VisibleWorldPoint : hand.WorldTarget);
            Vector3 targetPoint = ToVisualPlane(hand.WorldTarget);

            if (handMarker != null && handMaterial != null)
            {
                handMarker.position = handPoint;
                handMarker.localScale = Vector3.one * debugHandPointSize;
                handMaterial.color = handColor;
            }

            if (targetMarker != null && targetMaterial != null)
            {
                targetMarker.position = targetPoint;
                targetMarker.localScale = Vector3.one * debugTargetPointSize;
                targetMaterial.color = targetColor;
            }

            if (!hand.HasHold || hand.CurrentHold == null)
            {
                return;
            }

            Vector3 holdPoint = ToVisualPlane(hand.CurrentHold.Position);
            if (holdMarker != null && holdMaterial != null)
            {
                holdMarker.position = holdPoint;
                holdMarker.localScale = Vector3.one * debugHoldPointSize;
                holdMaterial.color = holdColor;
            }

            if (holdLine != null)
            {
                holdLine.positionCount = 2;
                holdLine.SetPosition(0, handPoint);
                holdLine.SetPosition(1, holdPoint);
                holdLine.startWidth = debugLineWidth;
                holdLine.endWidth = debugLineWidth;
                holdLine.startColor = handColor;
                holdLine.endColor = holdColor;
                if (lineMaterial != null)
                {
                    lineMaterial.color = holdColor;
                }
            }
        }

        private void ReportVisibleHandPoint(HandState hand, Transform handBone)
        {
            if (controller == null || handBone == null)
            {
                return;
            }

            controller.SetVisibleHandWorldPoint(hand, handBone.position);
        }

        private Transform CreateMarker(string objectName, out Material material)
        {
            Transform existing = transform.Find(objectName);
            if (existing != null && existing.TryGetComponent(out Renderer existingRenderer))
            {
                material = existingRenderer.sharedMaterial != null ? existingRenderer.sharedMaterial : CreateVisualMaterial(idleHandColor);
                existingRenderer.sharedMaterial = material;
                return existing;
            }

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = objectName;
            marker.transform.SetParent(transform, false);
            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null)
            {
                DestroyRuntimeOrImmediate(markerCollider);
            }

            material = CreateVisualMaterial(idleHandColor);
            marker.GetComponent<Renderer>().sharedMaterial = material;
            return marker.transform;
        }

        private LineRenderer CreateLine(string objectName, out Material material)
        {
            Transform existing = transform.Find(objectName);
            LineRenderer line = existing != null ? existing.GetComponent<LineRenderer>() : null;
            if (line == null)
            {
                GameObject lineObject = existing != null ? existing.gameObject : new GameObject(objectName);
                lineObject.transform.SetParent(transform, false);
                line = lineObject.AddComponent<LineRenderer>();
            }

            material = line.sharedMaterial != null ? line.sharedMaterial : CreateVisualMaterial(Color.white);
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.enabled = false;
            return line;
        }

        private static void SetActiveIfPresent(Transform target, bool active)
        {
            if (target != null && target.gameObject.activeSelf != active)
            {
                target.gameObject.SetActive(active);
            }
        }

        private static void SetLineActive(LineRenderer line, bool active)
        {
            if (line != null && line.enabled != active)
            {
                line.enabled = active;
            }
        }

        private static Material CreateVisualMaterial(Color color)
        {
            Material material = new Material(Shader.Find("Sprites/Default"));
            material.color = color;
            material.hideFlags = HideFlags.DontSaveInBuild;
            return material;
        }

        private static void DestroyRuntimeOrImmediate(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private Vector3 ToVisualPlane(Vector3 point)
        {
            point.z = visualPlaneZ;
            return point;
        }

        private void RemoveLegacyFakeArmVisuals()
        {
            DestroyChildIfExists("LeftArmStateLine");
            DestroyChildIfExists("RightArmStateLine");
            DestroyChildIfExists("upper_arm.L_VisualSegment");
            DestroyChildIfExists("lower_arm.L_VisualSegment");
            DestroyChildIfExists("upper_arm.R_VisualSegment");
            DestroyChildIfExists("lower_arm.R_VisualSegment");
        }

        private void DestroyChildIfExists(string childName)
        {
            Transform child = transform.Find(childName);
            if (child != null)
            {
                DestroyRuntimeOrImmediate(child.gameObject);
            }
        }

        private static Transform FindDeepChild(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == targetName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDeepChild(root.GetChild(i), targetName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
