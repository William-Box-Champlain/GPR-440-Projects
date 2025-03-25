using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VFF
{
    /// <summary>
    /// Example script demonstrating how to set up and use the Vector Flow Field system.
    /// </summary>
    public class VectorFieldExample : MonoBehaviour
    {
        [Header("Vector Field Setup")]
        [Tooltip("Reference to the VectorFieldManager component")]
        [SerializeField] private SecondPassVectorFieldManager vectorFieldManager;

        [Tooltip("Prefab for the agent")]
        [SerializeField] private GameObject agentPrefab;

        [Header("Field Configuration")]
        [Tooltip("Size of the field in world units")]
        [SerializeField] private Vector2 fieldSize = new Vector2(20f, 20f);

        [Tooltip("Number of obstacles to generate")]
        [SerializeField] private int obstacleCount = 5;

        [Tooltip("Size range for obstacles")]
        [SerializeField] private Vector2 obstacleSize = new Vector2(0.5f, 2f);

        [Tooltip("Optional mesh to use for field generation")]
        [SerializeField] private Mesh navMesh;

        [Header("Agent Configuration")]
        [Tooltip("Number of agents to spawn")]
        [SerializeField] private int agentCount = 10;

        [Tooltip("Spawn area size")]
        [SerializeField] private Vector2 spawnAreaSize = new Vector2(5f, 5f);

        // List of spawned agents
        private List<PathfindingAgent> agents = new List<PathfindingAgent>();

        // Visualizer component
        private VFF.VectorFieldVisualizer visualizer;

        /// <summary>
        /// Initializes the example.
        /// </summary>
        private void Start()
        {
            if (vectorFieldManager == null)
            {
                Debug.LogError("VectorFieldExample: VectorFieldManager reference is missing.");
                return;
            }

            SetupField();

            SpawnAgents();

            // Create visualizer
            SetupVisualizer();
        }

        /// <summary>
        /// Sets up the vector field with a rectangular area, obstacles, sinks, and sources.
        /// </summary>
        private void SetupField()
        {
            if (navMesh != null)
            {
                SetupFieldFromMesh();
            }
            else
            {
                SetupRectangularField();
            }
        }

        /// <summary>
        /// Sets up the vector field using a mesh.
        /// </summary>
        private void SetupFieldFromMesh()
        {
            Vector2[] sinkLocations = new Vector2[]
            {
                new Vector2(transform.position.x + fieldSize.x / 3f, transform.position.z)
            };

            // Check if there's a NavMesh in the scene
            if (UnityEngine.AI.NavMesh.CalculateTriangulation().vertices.Length > 0)
            {
                // Use the NavMesh directly
                Debug.Log("Using NavMesh for field initialization");
                vectorFieldManager.InitializeFromNavMesh(sinkLocations, 0.05f);
            }
            else if (navMesh != null)
            {
                // Use the provided mesh
                Debug.Log("Using provided mesh for field initialization");
                vectorFieldManager.InitializeFromMesh(navMesh, sinkLocations, 0.05f);
            }
            else
            {
                Debug.LogWarning("No mesh or NavMesh available for field initialization");
                return;
            }

            float sourceX = transform.position.x - fieldSize.x / 3f;
            float sourceZ = transform.position.z;
            vectorFieldManager.AddSource(new Vector3(sourceX, 0f, sourceZ), 0.05f, true);
        }

        /// <summary>
        /// Sets up the vector field with a rectangular area, obstacles, sinks, and sources.
        /// </summary>
        private void SetupRectangularField()
        {
            Vector3 center = transform.position;
            Vector3 size = new Vector3(fieldSize.x, 0f, fieldSize.y);
            vectorFieldManager.SetFieldRect(center, size);

            for (int i = 0; i < obstacleCount; i++)
            {
                float x = Random.Range(center.x - fieldSize.x / 2f + 2f, center.x + fieldSize.x / 2f - 2f);
                float z = Random.Range(center.z - fieldSize.y / 2f + 2f, center.z + fieldSize.y / 2f - 2f);
                float radius = Random.Range(obstacleSize.x, obstacleSize.y);

                vectorFieldManager.AddObstacle(new Vector3(x, 0f, z), radius / Mathf.Max(fieldSize.x, fieldSize.y));
            }

            float sinkX = center.x + fieldSize.x / 3f;
            float sinkZ = center.z;
            vectorFieldManager.AddSink(new Vector3(sinkX, 0f, sinkZ), 0.05f, true);

            float sourceX = center.x - fieldSize.x / 3f;
            float sourceZ = center.z;
            vectorFieldManager.AddSource(new Vector3(sourceX, 0f, sourceZ), 0.05f, true);
        }

        /// <summary>
        /// Spawns agents in the spawn area.
        /// </summary>
        private void SpawnAgents()
        {
            if (agentPrefab == null)
            {
                Debug.LogError("VectorFieldExample: Agent prefab is missing.");
                return;
            }

            Vector3 spawnCenter = transform.position;
            spawnCenter.x -= fieldSize.x / 3f;

            for (int i = 0; i < agentCount; i++)
            {
                float x = Random.Range(spawnCenter.x - spawnAreaSize.x / 2f, spawnCenter.x + spawnAreaSize.x / 2f);
                float z = Random.Range(spawnCenter.z - spawnAreaSize.y / 2f, spawnCenter.z + spawnAreaSize.y / 2f);
                Vector3 spawnPosition = new Vector3(x, 0f, z);

                GameObject agentObject = Instantiate(agentPrefab, spawnPosition, Quaternion.identity);
                agentObject.name = "Agent_" + i;

                PathfindingAgent agent = agentObject.GetComponent<PathfindingAgent>();
                if (agent != null)
                {
                    vectorFieldManager.RegisterAgent(agent);
                    agents.Add(agent);
                }
            }
        }

        /// <summary>
        /// Adds a new sink at the clicked position.
        /// </summary>
        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    vectorFieldManager.AddSink(hit.point, 0.05f, true);
                }
            }

            if (Input.GetMouseButtonDown(1))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    vectorFieldManager.AddSource(hit.point, 0.05f, true);
                }
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                vectorFieldManager.ClearSinksAndSources(true);

                float sinkX = transform.position.x + fieldSize.x / 3f;
                float sinkZ = transform.position.z;
                vectorFieldManager.AddSink(new Vector3(sinkX, 0f, sinkZ), 0.05f, true);

                float sourceX = transform.position.x - fieldSize.x / 3f;
                float sourceZ = transform.position.z;
                vectorFieldManager.AddSource(new Vector3(sourceX, 0f, sourceZ), 0.05f, true);
            }
        }

        /// <summary>
        /// Sets up the vector field visualizer.
        /// </summary>
        private void SetupVisualizer()
        {
            // Check if a visualizer already exists
            visualizer = GetComponent<VectorFieldVisualizer>();
            if (visualizer == null)
            {
                visualizer = gameObject.AddComponent<VectorFieldVisualizer>();
            }

            // Configure the visualizer            
            Debug.Log("Vector field visualizer set up. Use the inspector to toggle visualization modes.");
        }

        /// <summary>
        /// Cleans up when the object is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            foreach (PathfindingAgent agent in agents)
            {
                if (agent != null)
                {
                    vectorFieldManager.UnregisterAgent(agent);
                }
            }
        }
    }
}
