using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace sixth{

    /// <summary>
    /// Data structure for agent direction information (used by the compute shader)
    /// </summary>
    public struct AgentDirectionData
    {
        public Vector2 position;    // Agent position in world space
        public Vector2 direction;   // Direction vector (normalized)
        public float fieldStrength; // Strength of the vector field at this position
    }

    /// <summary>
    /// Manager class for VFF pathfinding that provides direction guidance to registered agents
    /// </summary>
    public class AgentManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private VectorFieldManager vectorFieldManager;

        [Header("Update Settings")]
        [SerializeField] private float updateInterval = 0.1f; // Time between direction updates

        // Internal data
        private List<VFFAgent> registeredAgents = new List<VFFAgent>();
        private ComputeBuffer agentDirectionBuffer;
        private AgentDirectionData[] directionData;
        private float updateTimer;

        private void Awake()
        {
            // Start with a modest buffer size that will grow as needed
            InitializeBuffer(100);
        }

        private void Update()
        {
            // Update directions at the specified interval
            updateTimer += Time.deltaTime;
            if (updateTimer >= updateInterval)
            {
                updateTimer = 0f;
                UpdateAgentDirections();
                Debug.Log($"Updating {registeredAgents.Count} agent direction(s)");
            }
        }

        private void OnDestroy()
        {
            ReleaseBuffer();
        }

        /// <summary>
        /// Initialize the compute buffer with the specified capacity
        /// </summary>
        private void InitializeBuffer(int capacity)
        {
            ReleaseBuffer(); // Release existing buffer if any

            agentDirectionBuffer = new ComputeBuffer(capacity, System.Runtime.InteropServices.Marshal.SizeOf<AgentDirectionData>());
            directionData = new AgentDirectionData[capacity];
        }

        /// <summary>
        /// Release the compute buffer
        /// </summary>
        private void ReleaseBuffer()
        {
            if (agentDirectionBuffer != null)
            {
                agentDirectionBuffer.Release();
                agentDirectionBuffer = null;
            }
        }

        /// <summary>
        /// Ensure the buffer is large enough for the specified capacity
        /// </summary>
        private void EnsureBufferCapacity(int requiredCapacity)
        {
            if (agentDirectionBuffer == null || agentDirectionBuffer.count < requiredCapacity)
            {
                // Create a new buffer with double the required capacity for future growth
                int newCapacity = Mathf.Max(requiredCapacity * 2, 100);
                InitializeBuffer(newCapacity);
            }
        }

        /// <summary>
        /// Register a new agent with the system
        /// </summary>
        public void RegisterAgent(VFFAgent agent)
        {
            if (!registeredAgents.Contains(agent))
            {
                registeredAgents.Add(agent);

                // Ensure buffer is large enough
                EnsureBufferCapacity(registeredAgents.Count);
            }
        }

        /// <summary>
        /// Unregister an agent from the system
        /// </summary>
        public void UnregisterAgent(VFFAgent agent)
        {
            registeredAgents.Remove(agent);
        }

        /// <summary>
        /// Update the directions for all registered agents
        /// </summary>
        private void UpdateAgentDirections()
        {
            if (registeredAgents.Count == 0 || vectorFieldManager == null)
                return;

            // Ensure buffer is large enough
            EnsureBufferCapacity(registeredAgents.Count);

            // Update direction data from agent positions
            for (int i = 0; i < registeredAgents.Count; i++)
            {
                VFFAgent agent = registeredAgents[i];
                if (agent != null)
                {
                    Vector3 position = agent.transform.position;
                    directionData[i] = new AgentDirectionData
                    {
                        position = new Vector2(position.x, position.z),
                        direction = Vector2.zero,
                        fieldStrength = 0f
                    };
                }
            }

            // Upload data to compute buffer
            agentDirectionBuffer.SetData(directionData);

            // Let vector field manager update the directions
            vectorFieldManager.UpdateAgentDirections(agentDirectionBuffer, registeredAgents.Count);

            // Read back the updated directions
            agentDirectionBuffer.GetData(directionData);

            // Apply directions to agents
            for (int i = 0; i < registeredAgents.Count; i++)
            {
                VFFAgent agent = registeredAgents[i];
                if (agent != null)
                {
                    Vector2 direction = directionData[i].direction;
                    float fieldStrength = directionData[i].fieldStrength;

                    // Send the direction data to the agent
                    agent.UpdateDirection(direction, fieldStrength);
                    Debug.Log($"Gameobject {agent.gameObject.GetInstanceID()} just recieved the direction {direction}");
                }
            }
        }
    }
}