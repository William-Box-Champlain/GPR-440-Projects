using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace fourth
{
    /// <summary>
    /// Simple agent that uses a Vector Flow Field for movement
    /// </summary>
    public class VectorFlowFieldAgent : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float rotationSpeed = 5f;
        [SerializeField] private float fieldInfluence = 1f;
        [SerializeField] private float randomVariance = 0.2f;
        [SerializeField] private float debugScalar = 2.0f;

        private VectorFlowFieldManager flowFieldManager;
        private int agentId;

        void Start()
        {
            // Find the flow field manager in the scene
            flowFieldManager = FindObjectOfType<VectorFlowFieldManager>();

            if (flowFieldManager == null)
            {
                Debug.LogError("VectorFlowFieldManager not found in the scene!");
                enabled = false;
                return;
            }

            // Generate a unique ID for this agent
            agentId = GetInstanceID();
        }

        void Update()
        {
            if (flowFieldManager == null) return;

            // Sample the flow field at the agent's position
            Vector3 currentPosition = transform.position;
            Vector3 flowVector = flowFieldManager.SampleFlowField(currentPosition, agentId);

            Debug.Log($"World Position is: {currentPosition}, Grid Position is: {flowFieldManager.GetUVOfPosition(currentPosition)}, Sampled Velocity is: {flowVector}");
            Debug.DrawRay( this.transform.position, flowVector * debugScalar );
        }

        // Visualize the flow vector in the editor for debugging
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || flowFieldManager == null) return;

            Vector3 flowVector = flowFieldManager.SampleFlowField(transform.position, agentId);

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, flowVector);

            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
        }
    }
}