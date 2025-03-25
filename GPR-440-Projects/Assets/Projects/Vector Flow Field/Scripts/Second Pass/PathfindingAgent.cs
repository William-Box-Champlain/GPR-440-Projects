using UnityEngine;

namespace VFF
{
    /// <summary>
    /// A sample agent that uses the vector flow field for navigation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PathfindingAgent : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Maximum movement speed of the agent")]
        [SerializeField] private float maxSpeed = 5.0f;

        [Tooltip("How quickly the agent accelerates")]
        [SerializeField] private float acceleration = 10.0f;

        [Tooltip("How quickly the agent rotates to face movement direction")]
        [SerializeField] private float rotationSpeed = 10.0f;

        [Tooltip("Multiplier for the vector field influence")]
        [Range(0.1f, 5.0f)]
        [SerializeField] private float fieldInfluence = 1.0f;

        [Header("Collision Avoidance")]
        [Tooltip("Radius for obstacle detection")]
        [SerializeField] private float obstacleDetectionRadius = 1.0f;

        [Tooltip("Layers to check for obstacles")]
        [SerializeField] private LayerMask obstacleLayers;

        [Tooltip("Strength of obstacle avoidance")]
        [Range(0.1f, 5.0f)]
        [SerializeField] private float avoidanceStrength = 2.0f;

        [Header("Debug Visualization")]
        [Tooltip("Whether to show debug visualization")]
        [SerializeField] private bool showDebug = true;

        [Tooltip("Color for the velocity vector")]
        [SerializeField] private Color velocityColor = Color.blue;

        [Tooltip("Color for the field direction vector")]
        [SerializeField] private Color fieldDirectionColor = Color.green;

        [Tooltip("Color for the obstacle avoidance vector")]
        [SerializeField] private Color avoidanceColor = Color.red;

        [SerializeField] private Vector3 direction = Vector3.zero;

        // References
        private Rigidbody rb;
        private SecondPassVectorFieldManager vectorFieldManager;

        // Movement state
        private Vector3 currentVelocity;
        private Vector3 targetVelocity;
        private Vector3 fieldDirection;
        private Vector3 avoidanceDirection;
        private Vector3 lastValidFieldDirection; // Store the last valid field direction
        private float stuckTimer = 0f; // Timer to detect when agent is stuck
        private const float STUCK_THRESHOLD = 1.5f; // Time threshold to consider agent stuck
        private const float MIN_VELOCITY_THRESHOLD = 0.1f; // Minimum velocity magnitude to consider agent moving

        /// <summary>
        /// Initializes the agent.
        /// </summary>
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

            // Auto-register with the manager if it exists
            if (SecondPassVectorFieldManager.GetInstance() != null)
            {
                SecondPassVectorFieldManager.GetInstance().RegisterAgent(this);
            }
        }

        /// <summary>
        /// Auto-unregisters from the VectorFieldManager singleton on destroy.
        /// </summary>
        private void OnDestroy()
        {
            // Auto-unregister when destroyed
            if (SecondPassVectorFieldManager.GetInstance() != null && vectorFieldManager == SecondPassVectorFieldManager.GetInstance())
            {
                SecondPassVectorFieldManager.GetInstance().UnregisterAgent(this);
            }
        }

        /// <summary>
        /// Sets up the agent with a reference to the vector field manager.
        /// </summary>
        /// <param name="manager">The vector field manager to use for navigation.</param>
        public void SetVectorFieldManager(SecondPassVectorFieldManager manager)
        {
            vectorFieldManager = manager;
        }

        /// <summary>
        /// Updates the agent's movement.
        /// </summary>
        private void FixedUpdate()
        {
            SecondPassVectorFieldManager manager = vectorFieldManager != null ? vectorFieldManager : SecondPassVectorFieldManager.GetInstance();

            if (manager == null)
                return;

            // Sample the field using the cached texture method for better performance
            fieldDirection = manager.SampleFieldFromCachedTexture(transform.position);

            direction = fieldDirection.normalized;

            Debug.Log("FieldDirection: " + fieldDirection);
        }

        /// <summary>
        /// Calculates a direction to avoid obstacles.
        /// </summary>
        /// <returns>A normalized direction vector away from obstacles.</returns>
        private Vector3 CalculateObstacleAvoidance()
        {
            Vector3 avoidance = Vector3.zero;
            Collider[] colliders = Physics.OverlapSphere(transform.position, obstacleDetectionRadius, obstacleLayers);

            foreach (Collider collider in colliders)
            {
                if (collider.transform == transform)
                    continue;

                Vector3 dirToObstacle = collider.ClosestPoint(transform.position) - transform.position;
                float distance = dirToObstacle.magnitude;

                if (distance > obstacleDetectionRadius)
                    continue;

                float weight = 1.0f - Mathf.Clamp01(distance / obstacleDetectionRadius);
                avoidance -= dirToObstacle.normalized * weight;
            }

            if (avoidance.sqrMagnitude > 0.001f)
                avoidance.Normalize();

            return new Vector3(avoidance.x, 0, avoidance.z);
        }

        /// <summary>
        /// Draws debug visualization.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!showDebug)
                return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, obstacleDetectionRadius);

            Gizmos.color = velocityColor;
            Gizmos.DrawLine(transform.position, transform.position + currentVelocity);

            Gizmos.color = fieldDirectionColor;
            Gizmos.DrawLine(transform.position, transform.position + fieldDirection * fieldInfluence);

            Gizmos.color = avoidanceColor;
            Gizmos.DrawLine(transform.position, transform.position + avoidanceDirection * avoidanceStrength);
        }
    }
}
