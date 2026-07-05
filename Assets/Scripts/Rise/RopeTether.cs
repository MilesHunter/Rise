using UnityEngine;
using Object = UnityEngine.Object;

namespace Rise
{
    public sealed class RopeTether : MonoBehaviour
    {
        private const string RopeBaseColorPath = "Assets/Arts/Materials/Rope/Bitmaps/Rope_basecolor.png";
        private const string RopeNormalPath = "Assets/Arts/Materials/Rope/Bitmaps/Rope_normal.png";
        private const string RopeRoughnessPath = "Assets/Arts/Materials/Rope/Bitmaps/Rope_roughness.png";

        [SerializeField] private float ropeLength = 5f;
        [SerializeField] private int segmentCount = 12;
        [SerializeField] private float segmentMass = 0.1f;
        [SerializeField] private float segmentDrag = 0.2f;
        [SerializeField] private float segmentAngularDrag = 0.6f;
        [SerializeField] private float segmentRadius = 0.06f;
        [SerializeField] private float visualStartWidth = 0.055f;
        [SerializeField] private float visualEndWidth = 0.045f;

        private Rigidbody anchorBody;
        private Rigidbody[] segmentBodies;
        private ClimbHold[] segmentHolds;
        private LineRenderer line;
        private ClimbHold freeEndHold;

        public ClimbHold FreeEndHold => freeEndHold;
        public Vector3 FreeEndPosition => freeEndHold != null ? freeEndHold.Position : transform.position;

        public void Build(Vector3 anchorPoint, Vector3 launchSource, float length, int segments)
        {
            ropeLength = Mathf.Max(0.5f, length);
            segmentCount = Mathf.Max(2, segments);
            anchorPoint.z = 0f;
            launchSource.z = 0f;

            GameObject anchorObject = new GameObject("RopeAnchor");
            anchorObject.transform.SetParent(transform, false);
            anchorObject.transform.position = anchorPoint;
            anchorBody = anchorObject.AddComponent<Rigidbody>();
            anchorBody.isKinematic = true;
            anchorBody.useGravity = false;
            anchorBody.constraints = RigidbodyConstraints.FreezeAll;

            BuildSegments(anchorPoint, launchSource);
            BuildVisual();
            UpdateVisual();
        }

        private void BuildSegments(Vector3 anchorPoint, Vector3 launchSource)
        {
            segmentBodies = new Rigidbody[segmentCount];
            segmentHolds = new ClimbHold[segmentCount];
            Vector3 initialDirection = launchSource - anchorPoint;
            initialDirection.z = 0f;
            if (initialDirection.sqrMagnitude < 0.0001f)
            {
                initialDirection = Vector3.down;
            }

            initialDirection = initialDirection.normalized;
            float segmentLength = ropeLength / segmentCount;
            Rigidbody previousBody = anchorBody;

            for (int i = 0; i < segmentCount; i++)
            {
                GameObject segmentObject = new GameObject($"RopeSegment_{i:00}");
                segmentObject.transform.SetParent(transform, false);
                segmentObject.transform.position = anchorPoint + initialDirection * segmentLength * (i + 1);
                segmentObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, -initialDirection);

                Rigidbody segmentBody = segmentObject.AddComponent<Rigidbody>();
                segmentBody.mass = segmentMass;
                segmentBody.linearDamping = segmentDrag;
                segmentBody.angularDamping = segmentAngularDrag;
                segmentBody.interpolation = RigidbodyInterpolation.Interpolate;
                segmentBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                segmentBody.constraints = RigidbodyConstraints.FreezePositionZ |
                                          RigidbodyConstraints.FreezeRotationX |
                                          RigidbodyConstraints.FreezeRotationY;

                SphereCollider collider = segmentObject.AddComponent<SphereCollider>();
                collider.radius = segmentRadius;
                collider.isTrigger = true;

                ConfigurableJoint joint = segmentObject.AddComponent<ConfigurableJoint>();
                joint.connectedBody = previousBody;
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = Vector3.up * segmentLength;
                joint.connectedAnchor = Vector3.zero;
                joint.xMotion = ConfigurableJointMotion.Locked;
                joint.yMotion = ConfigurableJointMotion.Locked;
                joint.zMotion = ConfigurableJointMotion.Locked;
                joint.angularXMotion = ConfigurableJointMotion.Free;
                joint.angularYMotion = ConfigurableJointMotion.Free;
                joint.angularZMotion = ConfigurableJointMotion.Free;
                joint.enableCollision = false;
                joint.projectionMode = JointProjectionMode.PositionAndRotation;
                joint.projectionDistance = 0.05f;
                joint.projectionAngle = 5f;

                ClimbHold segmentHold = segmentObject.AddComponent<ClimbHold>();
                segmentHold.Configure(ClimbHoldType.Rope, true, true, 0f, 0f, 0f, 0f, "Rope");

                segmentBodies[i] = segmentBody;
                segmentHolds[i] = segmentHold;
                previousBody = segmentBody;
            }

            GameObject freeEndObject = segmentBodies[segmentBodies.Length - 1].gameObject;
            freeEndHold = segmentHolds[segmentHolds.Length - 1];

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "RopeFreeEndMarker";
            marker.transform.SetParent(freeEndObject.transform, false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = Vector3.one * 0.18f;
            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null)
            {
                DestroyRuntimeOrImmediate(markerCollider);
            }

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateFallbackMaterial(new Color(0.95f, 0.72f, 0.28f));
            }
        }

        private void BuildVisual()
        {
            GameObject visualObject = new GameObject("RopeVisual");
            visualObject.transform.SetParent(transform, false);
            line = visualObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = segmentCount + 1;
            line.startWidth = visualStartWidth;
            line.endWidth = visualEndWidth;
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            line.textureMode = LineTextureMode.Tile;
            line.sortingOrder = 4;
            line.sharedMaterial = CreateRopeMaterial();
            line.startColor = new Color(0.85f, 0.78f, 0.62f);
            line.endColor = new Color(0.7f, 0.63f, 0.48f);
        }

        private void LateUpdate()
        {
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (line == null || anchorBody == null || segmentBodies == null)
            {
                return;
            }

            line.positionCount = segmentBodies.Length + 1;
            line.SetPosition(0, anchorBody.position);
            for (int i = 0; i < segmentBodies.Length; i++)
            {
                if (segmentBodies[i] != null)
                {
                    Vector3 point = segmentBodies[i].position;
                    point.z = 0f;
                    line.SetPosition(i + 1, point);
                }
            }
        }

        private static Material CreateRopeMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material material = new Material(shader);
            material.color = new Color(0.76f, 0.66f, 0.46f);

#if UNITY_EDITOR
            Texture2D baseColor = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(RopeBaseColorPath);
            Texture2D normal = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(RopeNormalPath);
            Texture2D roughness = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(RopeRoughnessPath);
            if (baseColor != null)
            {
                material.mainTexture = baseColor;
            }

            if (normal != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            if (roughness != null && material.HasProperty("_MetallicGlossMap"))
            {
                material.SetTexture("_MetallicGlossMap", roughness);
            }
#endif

            return material;
        }

        private static Material CreateFallbackMaterial(Color color)
        {
            Shader shader = Shader.Find("Sprites/Default");
            Material material = new Material(shader);
            material.color = color;
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
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
