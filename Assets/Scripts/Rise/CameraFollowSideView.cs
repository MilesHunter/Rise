using UnityEngine;

namespace Rise
{
    public sealed class CameraFollowSideView : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, -13f);
        [SerializeField] private float smoothTime = 0.2f;
        [SerializeField] private float lookAhead = 1.75f;

        private Vector3 velocity;

        private void Start()
        {
            if (target == null)
            {
                PlayerClimbController player = Object.FindAnyObjectByType<PlayerClimbController>();
                if (player != null)
                {
                    target = player.transform;
                }
            }
        }

        public void Initialize(Transform followTarget)
        {
            target = followTarget;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Rigidbody rb = target.GetComponent<Rigidbody>();
            float lead = rb != null ? Mathf.Clamp(rb.linearVelocity.x * 0.35f, -lookAhead, lookAhead) : 0f;
            Vector3 desired = target.position + offset + Vector3.right * lead;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            transform.rotation = Quaternion.identity;
        }
    }
}
