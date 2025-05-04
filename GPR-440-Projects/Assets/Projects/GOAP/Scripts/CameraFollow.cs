using UnityEngine;

namespace GOAP
{
    public class CameraFollow : MonoBehaviour
    {
        [Tooltip("The target to watch")]
        public Transform target;

        [Tooltip("How quickly the camera rotates to look at the target")]
        [Range(0.01f, 1.0f)]
        public float rotationSpeed = 0.1f;

        [Tooltip("Whether to use smooth rotation")]
        public bool smoothRotation = true;

        private void LateUpdate()
        {
            if (target == null)
            {
                Debug.LogWarning("CameraFollow: No target assigned");
                return;
            }

            if (smoothRotation)
            {
                // Calculate the direction to the target
                Vector3 direction = target.position - transform.position;
                
                // Calculate the rotation to look at the target
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                
                // Smoothly rotate towards the target
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed);
            }
            else
            {
                // Instantly look at the target
                transform.LookAt(target);
            }
        }
    }
}
