using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rise
{
    public sealed class CharacterPresentation : MonoBehaviour
    {
        private const string CharacterAssetPath = "Assets/Arts/CharacterModels/Character.fbx";
        private const float TargetVisualHeight = 1.85f;

        private PlayerClimbController controller;
        private Transform visualRoot;
        private Transform leftMarker;
        private Transform rightMarker;
        private Transform chest;
        private Transform leftUpperArm;
        private Transform leftLowerArm;
        private Transform rightUpperArm;
        private Transform rightLowerArm;
        private Transform leftUpperLeg;
        private Transform rightUpperLeg;
        private LineRenderer leftArmLine;
        private LineRenderer rightArmLine;
        private Quaternion chestInitialLocalRotation;
        private Quaternion leftUpperArmInitialLocalRotation;
        private Quaternion leftLowerArmInitialLocalRotation;
        private Quaternion rightUpperArmInitialLocalRotation;
        private Quaternion rightLowerArmInitialLocalRotation;
        private Quaternion leftUpperLegInitialLocalRotation;
        private Quaternion rightUpperLegInitialLocalRotation;
        private Transform leftHandBone;
        private Transform rightHandBone;
        private bool built;

        public void Initialize(PlayerClimbController owner)
        {
            controller = owner;
            EnsureBuilt();
        }

        private void Start()
        {
            if (controller == null)
            {
                controller = Object.FindAnyObjectByType<PlayerClimbController>();
            }

            EnsureBuilt();
        }

        private void LateUpdate()
        {
            if (controller == null || visualRoot == null)
            {
                return;
            }

            transform.position = controller.transform.position + new Vector3(0f, -0.95f, 0f);
            transform.rotation = Quaternion.identity;

            float horizontalVelocity = controller.BodyVelocity.x;
            float tilt = Mathf.Clamp(horizontalVelocity * -7f, -18f, 18f);

            if (chest != null)
            {
                chest.localRotation = chestInitialLocalRotation * Quaternion.Euler(tilt * 0.35f, 0f, tilt);
            }

            SolveArmIk(leftUpperArm, leftLowerArm, leftHandBone, leftUpperArmInitialLocalRotation, leftLowerArmInitialLocalRotation, controller.LeftHand.WorldTarget, true);
            SolveArmIk(rightUpperArm, rightLowerArm, rightHandBone, rightUpperArmInitialLocalRotation, rightLowerArmInitialLocalRotation, controller.RightHand.WorldTarget, false);
            SnapHandBone(leftHandBone, controller.LeftHand.WorldTarget);
            SnapHandBone(rightHandBone, controller.RightHand.WorldTarget);

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

            if (leftMarker != null)
            {
                leftMarker.position = controller.LeftHand.WorldTarget;
            }

            if (rightMarker != null)
            {
                rightMarker.position = controller.RightHand.WorldTarget;
            }

            UpdateArmLine(leftArmLine, leftUpperArm, controller.LeftHand.WorldTarget);
            UpdateArmLine(rightArmLine, rightUpperArm, controller.RightHand.WorldTarget);
        }

        private void BuildVisual()
        {
            if (built)
            {
                return;
            }

            GameObject visualPrefab = null;
#if UNITY_EDITOR
            visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetPath);
#endif

            GameObject instance;
            if (visualPrefab != null)
            {
                instance = Instantiate(visualPrefab, transform);
                instance.name = "CharacterVisual";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                instance.transform.localScale = Vector3.one;
                NormalizeVisualScale(instance);
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                instance.name = "CharacterFallback";
                instance.transform.SetParent(transform, false);
                instance.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
                instance.GetComponent<Renderer>().material.color = new Color(0.92f, 0.88f, 0.82f);
            }

            visualRoot = instance.transform;
            CacheHumanoidBones(instance);
            BuildHandMarkers();
            built = true;
        }

        private void NormalizeVisualScale(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            if (bounds.size.y <= 0.0001f)
            {
                return;
            }

            float uniformScale = TargetVisualHeight / bounds.size.y;
            instance.transform.localScale = Vector3.one * uniformScale;

            renderers = instance.GetComponentsInChildren<Renderer>();
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float bottomOffset = bounds.min.y - transform.position.y;
            instance.transform.localPosition += new Vector3(0f, -bottomOffset, 0f);
        }

        private void CacheHumanoidBones(GameObject instance)
        {
            Animator animator = instance.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.enabled = false;

                if (animator.isHuman)
                {
                    chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
                    leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                    leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                    rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                    leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    rightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
                }
            }

            if (chest == null) chest = FindBoneByName(instance.transform, "chest", "spine", "upperchest");
            if (leftUpperArm == null) leftUpperArm = FindBoneByName(instance.transform, "leftupperarm", "upperarm_l", "l_upperarm", "leftarm", "upper_arm.l");
            if (leftLowerArm == null) leftLowerArm = FindBoneByName(instance.transform, "leftlowerarm", "lowerarm_l", "l_forearm", "leftforearm", "lower_arm.l", "forearm.l");
            if (rightUpperArm == null) rightUpperArm = FindBoneByName(instance.transform, "rightupperarm", "upperarm_r", "r_upperarm", "rightarm", "upper_arm.r");
            if (rightLowerArm == null) rightLowerArm = FindBoneByName(instance.transform, "rightlowerarm", "lowerarm_r", "r_forearm", "rightforearm", "lower_arm.r", "forearm.r");
            if (leftUpperLeg == null) leftUpperLeg = FindBoneByName(instance.transform, "leftupperleg", "upleg_l", "l_thigh", "leftthigh", "upper_leg.l", "thigh.l");
            if (rightUpperLeg == null) rightUpperLeg = FindBoneByName(instance.transform, "rightupperleg", "upleg_r", "r_thigh", "rightthigh", "upper_leg.r", "thigh.r");
            if (leftHandBone == null) leftHandBone = FindBoneByName(instance.transform, "lefthand", "hand_l", "l_hand", "hand.l");
            if (rightHandBone == null) rightHandBone = FindBoneByName(instance.transform, "righthand", "hand_r", "r_hand", "hand.r");
            if (leftHandBone == null) leftHandBone = FindFirstChildBone(leftLowerArm);
            if (rightHandBone == null) rightHandBone = FindFirstChildBone(rightLowerArm);

            if (chest != null) chestInitialLocalRotation = chest.localRotation;
            if (leftUpperArm != null) leftUpperArmInitialLocalRotation = leftUpperArm.localRotation;
            if (leftLowerArm != null) leftLowerArmInitialLocalRotation = leftLowerArm.localRotation;
            if (rightUpperArm != null) rightUpperArmInitialLocalRotation = rightUpperArm.localRotation;
            if (rightLowerArm != null) rightLowerArmInitialLocalRotation = rightLowerArm.localRotation;
            if (leftUpperLeg != null) leftUpperLegInitialLocalRotation = leftUpperLeg.localRotation;
            if (rightUpperLeg != null) rightUpperLegInitialLocalRotation = rightUpperLeg.localRotation;
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

        private static Vector2 ToPlane(Vector3 point)
        {
            return new Vector2(point.x, point.y);
        }

        private static Vector3 FromPlane(Vector2 point, float z)
        {
            return new Vector3(point.x, point.y, z);
        }

        private static void SnapHandBone(Transform handBone, Vector3 target)
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

            handBone.rotation = Quaternion.LookRotation(Vector3.forward, direction.normalized) * Quaternion.Euler(0f, 0f, -90f);
        }

        private static Transform FindBoneByName(Transform root, params string[] nameCandidates)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string lowered = all[i].name.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
                for (int j = 0; j < nameCandidates.Length; j++)
                {
                    string candidate = nameCandidates[j].Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
                    if (lowered.Contains(candidate))
                    {
                        return all[i];
                    }
                }
            }

            return null;
        }

        private static Transform FindFirstChildBone(Transform parentBone)
        {
            if (parentBone == null || parentBone.childCount == 0)
            {
                return null;
            }

            for (int i = 0; i < parentBone.childCount; i++)
            {
                Transform child = parentBone.GetChild(i);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private void BuildHandMarkers()
        {
            leftMarker = CreateMarker("LeftHandMarker", new Color(0.8f, 0.3f, 0.3f)).transform;
            rightMarker = CreateMarker("RightHandMarker", new Color(0.3f, 0.7f, 1f)).transform;
            leftArmLine = CreateArmLine("LeftArmGuide", new Color(0.95f, 0.45f, 0.45f));
            rightArmLine = CreateArmLine("RightArmGuide", new Color(0.45f, 0.8f, 1f));
        }

        private GameObject CreateMarker(string markerName, Color color)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = markerName;
            marker.transform.SetParent(transform.parent, false);
            marker.transform.localScale = Vector3.one * 0.26f;
            Object.Destroy(marker.GetComponent<SphereCollider>());
            marker.GetComponent<Renderer>().material.color = color;
            return marker;
        }

        private LineRenderer CreateArmLine(string lineName, Color color)
        {
            GameObject lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(transform.parent, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.startWidth = 0.05f;
            line.endWidth = 0.03f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = 5;
            return line;
        }

        private static void UpdateArmLine(LineRenderer line, Transform shoulder, Vector3 target)
        {
            if (line == null)
            {
                return;
            }

            Vector3 start = shoulder != null ? shoulder.position : target;
            line.SetPosition(0, start);
            line.SetPosition(1, target);
        }

        private void EnsureBuilt()
        {
            if (controller != null && !built)
            {
                BuildVisual();
            }
        }
    }
}
