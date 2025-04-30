using GOAP;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace sixth
{
    /// <summary>
    /// Manager for the Vector Field Flow pathfinding system
    /// </summary>
    public class VectorFieldManager : MonoBehaviour
    {
        [Header("Field Settings")]
        [SerializeField] private int gridWidth = 1024;
        [SerializeField] private int gridHeight = 512;
        [SerializeField] private bool autoCalculateCellSize = true;
        [SerializeField]
        [Tooltip("Only used if autoCalculateCellSize is false")]
        private float manualCellSize = 0.6f;
        [SerializeField] private Vector2 worldOffset = Vector2.zero;

        private float cellSize = 0.6f;      // Size of each grid cell in Unity units
        private Vector2 worldBoundsMin;    // Minimum bounds of the world
        private Vector2 worldBoundsMax;    // Maximum bounds of the world

        [Header("Field Parameters")]
        [SerializeField] private int maxGoals = 32;
        [SerializeField][Range(0.5f, 1.0f)] private float propagationSpeed = 0.95f;
        [SerializeField][Range(0.01f, 1.0f)] private float fieldFalloff = 0.1f;
        [SerializeField][Range(1.0f, 10000.0f)] private float obstacleWeight = 5.0f;

        [Header("Compute Resources")]
        [SerializeField] private ComputeShader vffComputeShader;
        [SerializeField] private int navMeshSampleDistance = 5;  // Distance between NavMesh boundary samples
        [SerializeField] private GoalManager goalManager;

        [Header("Debug")]
        [SerializeField] private bool visualizeField = false;
        [SerializeField] private Transform debugParent;
        [SerializeField] private bool frameByFrame = false;

        // Kernel IDs
        private int initKernel;
        private int distanceFieldKernel;
        private int propagateKernel;
        private int convertKernel;
        private int agentDirectionKernel;

        // Buffers and textures
        private RenderTexture vectorField;
        private RenderTexture distanceField;
        private ComputeBuffer goalBuffer;
        private ComputeBuffer navMeshBuffer;
        private ComputeBuffer agentBuffer;

        // Data structures
        private struct Goal
        {
            public Vector2 position;
            public float weight;
            public int active;  // Using int instead of bool for compute buffer compatibility

            public Goal(Vector2 pos, float w)
            {
                position = pos;
                weight = w;
                active = 0;
            }
        }

        private struct NavMeshData
        {
            public Vector4 boundaryPoint;  // xyz = position, w = is boundary (1 or 0)
        }

        private List<Goal> goals = new List<Goal>();
        private List<NavMeshData> navMeshData = new List<NavMeshData>();

        private bool initialized = false;
        private int propagationSteps = 3;  // Number of propagation passes per frame

        private void OnEnable()
        {
            Initialize();
        }

        private void OnDisable()
        {
            ReleaseResources();
        }

        /// <summary>
        /// Initialize the Vector Field Flow system
        /// </summary>
        public void Initialize()
        {
            if (initialized)
                return;

            // Initialize compute kernels
            initKernel = vffComputeShader.FindKernel("InitializeVectorField");
            distanceFieldKernel = vffComputeShader.FindKernel("GenerateDistanceField");
            propagateKernel = vffComputeShader.FindKernel("PropagateDistanceField");
            convertKernel = vffComputeShader.FindKernel("ConvertToVectorField");
            agentDirectionKernel = vffComputeShader.FindKernel("CalculateAgentDirections");

            // Create textures
            vectorField = new RenderTexture(gridWidth, gridHeight, 0, RenderTextureFormat.ARGBFloat);
            vectorField.enableRandomWrite = true;
            vectorField.Create();

            distanceField = new RenderTexture(gridWidth, gridHeight, 0, RenderTextureFormat.RFloat);
            distanceField.enableRandomWrite = true;
            distanceField.Create();

            // Initialize goals
            goalBuffer = new ComputeBuffer(maxGoals, System.Runtime.InteropServices.Marshal.SizeOf<Goal>());

            // Sample the NavMesh to get boundary data
            SampleNavMesh();

            // Set compute shader parameters
            vffComputeShader.SetInt("GridWidth", gridWidth);
            vffComputeShader.SetInt("GridHeight", gridHeight);
            vffComputeShader.SetFloat("CellSize", cellSize);
            vffComputeShader.SetFloat("PropagationSpeed", propagationSpeed);
            vffComputeShader.SetFloat("FieldFalloff", fieldFalloff);
            vffComputeShader.SetFloat("ObstacleWeight", obstacleWeight);
            vffComputeShader.SetVector("WorldOffset", worldBoundsMin);

            // Bind resources to compute shader
            vffComputeShader.SetTexture(initKernel, "VectorField", vectorField);
            vffComputeShader.SetTexture(initKernel, "DistanceField", distanceField);
            vffComputeShader.SetBuffer(initKernel, "NavMeshBuffer", navMeshBuffer);

            vffComputeShader.SetTexture(distanceFieldKernel, "VectorField", vectorField);
            vffComputeShader.SetTexture(distanceFieldKernel, "DistanceField", distanceField);
            vffComputeShader.SetBuffer(distanceFieldKernel, "GoalBuffer", goalBuffer);

            vffComputeShader.SetTexture(propagateKernel, "VectorField", vectorField);
            vffComputeShader.SetTexture(propagateKernel, "DistanceField", distanceField);

            vffComputeShader.SetTexture(convertKernel, "VectorField", vectorField);
            vffComputeShader.SetTexture(convertKernel, "DistanceField", distanceField);

            // Initialize field
            vffComputeShader.SetInt("NavMeshDataSize", navMeshData.Count);
            vffComputeShader.Dispatch(initKernel, Mathf.CeilToInt(gridWidth / 8f), Mathf.CeilToInt(gridHeight / 8f), 1);

            initialized = true;
        }

        /// <summary>
        /// Sample the NavMesh to extract boundary information and calculate field dimensions
        /// </summary>
        private void SampleNavMesh()
        {
            navMeshData.Clear();

            // Get the NavMesh bounds by sampling its extents
            Bounds navMeshBounds = CalculateNavMeshBounds();

            // If auto-calculating cell size, do it here based on NavMesh bounds
            if (autoCalculateCellSize)
            {
                float worldX = navMeshBounds.size.x;
                float worldZ = navMeshBounds.size.z;

                // Calculate cell size to fit the NavMesh within our grid
                cellSize = Mathf.Max(worldX / gridWidth, worldZ / gridHeight);

                Debug.Log($"Auto-calculated cell size: {cellSize} based on NavMesh dimensions: {worldX}x{worldZ}");
            }
            else
            {
                cellSize = manualCellSize;
            }

            // World offset is now the minimum bounds, not a custom value
            worldOffset = worldBoundsMin;

            // Define the bounds of our world area
            float worldWidth = gridWidth * cellSize;
            float worldHeight = gridHeight * cellSize;

            // Sample points along the perimeter to find NavMesh boundaries
            int perimeterSamples = Mathf.CeilToInt((2 * worldWidth + 2 * worldHeight) / navMeshSampleDistance);

            for (int i = 0; i < perimeterSamples; i++)
            {
                float t = (float)i / perimeterSamples;
                Vector3 perimeterPoint = Vector3.zero;

                // Generate points around the perimeter
                if (t < 0.25f)
                {
                    // Bottom edge
                    perimeterPoint = new Vector3(t * 4 * worldWidth + worldOffset.x, 0, worldOffset.y);
                }
                else if (t < 0.5f)
                {
                    // Right edge
                    perimeterPoint = new Vector3(worldWidth + worldOffset.x, 0, (t - 0.25f) * 4 * worldHeight + worldOffset.y);
                }
                else if (t < 0.75f)
                {
                    // Top edge
                    perimeterPoint = new Vector3((0.75f - t) * 4 * worldWidth + worldOffset.x, 0, worldHeight + worldOffset.y);
                }
                else
                {
                    // Left edge
                    perimeterPoint = new Vector3(worldOffset.x, 0, (1f - t) * 4 * worldHeight + worldOffset.y);
                }

                // Raycast against the NavMesh to find boundaries
                NavMeshHit hit;
                if (NavMesh.Raycast(perimeterPoint, perimeterPoint + Vector3.up * 0.1f, out hit, NavMesh.AllAreas))
                {
                    // This is a boundary point
                    NavMeshData data = new NavMeshData();
                    data.boundaryPoint = new Vector4(hit.position.x, hit.position.z, 0, 1); // Store as x,y in 2D, w=1 means boundary
                    navMeshData.Add(data);
                }
            }

            // Also sample the NavMesh to find interior obstacles
            int samplesX = Mathf.CeilToInt(worldWidth / navMeshSampleDistance);
            int samplesY = Mathf.CeilToInt(worldHeight / navMeshSampleDistance);

            for (int x = 0; x < samplesX; x++)
            {
                for (int y = 0; y < samplesY; y++)
                {
                    Vector3 samplePoint = new Vector3(
                        x * navMeshSampleDistance + worldOffset.x,
                        0,
                        y * navMeshSampleDistance + worldOffset.y
                    );

                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(samplePoint, out hit, navMeshSampleDistance, NavMesh.AllAreas))
                    {
                        if (hit.distance > 0.1f)
                        {
                            // This is close to an obstacle or boundary
                            NavMeshData data = new NavMeshData();
                            data.boundaryPoint = new Vector4(hit.position.x, hit.position.z, 0, 1);
                            navMeshData.Add(data);
                        }
                    }
                }
            }

            // Create the compute buffer for NavMesh data
            navMeshBuffer = new ComputeBuffer(Mathf.Max(1, navMeshData.Count), System.Runtime.InteropServices.Marshal.SizeOf<NavMeshData>());
            navMeshBuffer.SetData(navMeshData);

            Debug.Log($"Generated {navMeshData.Count} NavMesh boundary points");
        }

        /// <summary>
        /// Calculate the bounds of the NavMesh
        /// </summary>
        private Bounds CalculateNavMeshBounds()
        {
            // Start with a minimum-sized bounds
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool foundAnyPoint = false;

            // Get the NavMesh triangulation
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

            // If we have vertices, use them to calculate bounds
            if (triangulation.vertices != null && triangulation.vertices.Length > 0)
            {
                // Initialize bounds with the first vertex
                bounds = new Bounds(triangulation.vertices[0], Vector3.zero);
                foundAnyPoint = true;

                // Expand bounds to include all vertices
                for (int i = 1; i < triangulation.vertices.Length; i++)
                {
                    bounds.Encapsulate(triangulation.vertices[i]);
                }
            }

            // If we couldn't get vertices, use a fallback approach with sampling
            if (!foundAnyPoint)
            {
                Debug.LogWarning("Could not get NavMesh vertices. Using fallback sampling approach.");

                // Sample a grid across a large area to find NavMesh points
                float sampleStep = 5.0f;
                float maxDistance = 1000.0f;
                bool initializedBounds = false;

                for (float x = -maxDistance; x <= maxDistance; x += sampleStep)
                {
                    for (float z = -maxDistance; z <= maxDistance; z += sampleStep)
                    {
                        Vector3 samplePoint = new Vector3(x, 0, z);
                        NavMeshHit hit;

                        if (NavMesh.SamplePosition(samplePoint, out hit, sampleStep, NavMesh.AllAreas))
                        {
                            if (!initializedBounds)
                            {
                                bounds = new Bounds(hit.position, Vector3.zero);
                                initializedBounds = true;
                            }
                            else
                            {
                                bounds.Encapsulate(hit.position);
                            }
                        }
                    }
                }
            }

            // Add a small margin
            bounds.Expand(1.0f);

            // Store the 2D bounds for grid calculations
            worldBoundsMin = new Vector2(bounds.min.x, bounds.min.z);
            worldBoundsMax = new Vector2(bounds.max.x, bounds.max.z);

            Debug.Log($"Calculated NavMesh bounds: {worldBoundsMin} to {worldBoundsMax}, size: {bounds.size}");
            return bounds;
        }

        private void Start()
        {
            goalManager?.ActivateAllGoals();
        }

        private void Update()
        {
            if (!initialized)
                return;

            UpdateVectorField();

            if (visualizeField)
                VisualizeField();
        }

        private void LateUpdate()
        {
            if(frameByFrame) Debug.Break();
        }

        /// <summary>
        /// Update the vector field based on active goals
        /// </summary>
        private void UpdateVectorField()
        {
            // Update goals
            goalBuffer.SetData(goals);
            vffComputeShader.SetInt("NumGoals", goals.Count);

            // Generate distance field
            vffComputeShader.Dispatch(distanceFieldKernel, Mathf.CeilToInt(gridWidth / 8f), Mathf.CeilToInt(gridHeight / 8f), 1);

            // Propagate multiple times for better results
            for (int i = 0; i < propagationSteps; i++)
            {
                vffComputeShader.Dispatch(propagateKernel, Mathf.CeilToInt(gridWidth / 8f), Mathf.CeilToInt(gridHeight / 8f), 1);
            }

            // Convert to vector field
            vffComputeShader.Dispatch(convertKernel, Mathf.CeilToInt(gridWidth / 8f), Mathf.CeilToInt(gridHeight / 8f), 1);
        }

        /// <summary>
        /// Add a new goal to the system
        /// </summary>
        /// <param name="position">World position of the goal</param>
        /// <param name="weight">Weight/priority of the goal (higher = stronger influence)</param>
        /// <returns>Index of the added goal</returns>
        public int AddGoal(Vector2 position, float weight = 1.0f)
        {
            if (goals.Count >= maxGoals)
            {
                Debug.LogWarning("Maximum number of goals reached. Cannot add more goals.");
                return -1;
            }

            goals.Add(new Goal(position, weight));
            int goalId = goals.Count - 1;

            // Debug log
            Debug.Log($"Added goal #{goalId} at world position ({position.x}, {position.y}) with weight {weight}");

            return goalId;
        }

        /// <summary>
        /// Set the active state of a goal
        /// </summary>
        /// <param name="goalIndex">Index of the goal to modify</param>
        /// <param name="active">Whether the goal should be active</param>
        public void SetGoalActive(int goalIndex, bool active)
        {
            if (goalIndex < 0 || goalIndex >= goals.Count)
            {
                Debug.LogWarning($"Invalid goal index: {goalIndex}");
                return;
            }

            Goal goal = goals[goalIndex];
            goal.active = active ? 1 : 0;
            goals[goalIndex] = goal;
        }

        /// <summary>
        /// Set the weight/priority of a goal
        /// </summary>
        /// <param name="goalIndex">Index of the goal to modify</param>
        /// <param name="weight">New weight value (higher = stronger influence)</param>
        public void SetGoalWeight(int goalIndex, float weight)
        {
            if (goalIndex < 0 || goalIndex >= goals.Count)
            {
                Debug.LogWarning($"Invalid goal index: {goalIndex}");
                return;
            }

            Goal goal = goals[goalIndex];
            goal.weight = weight;
            goals[goalIndex] = goal;
        }

        /// <summary>
        /// Update agent directions based on the vector field
        /// </summary>
        /// <param name="agentBuffer">ComputeBuffer containing agent data</param>
        /// <param name="agentCount">Number of agents to update</param>
        public void UpdateAgentDirections(ComputeBuffer agentBuffer, int agentCount)
        {
            if (!initialized)
                return;

            // Set agent buffer and dispatch compute shader
            vffComputeShader.SetBuffer(agentDirectionKernel, "AgentBuffer", agentBuffer);
            vffComputeShader.SetInt("NumAgents", agentCount);
            vffComputeShader.SetTexture(agentDirectionKernel, "VectorField", vectorField);
            vffComputeShader.SetBuffer(agentDirectionKernel, "GoalBuffer", goalBuffer);

            vffComputeShader.Dispatch(agentDirectionKernel, Mathf.CeilToInt(agentCount / 64f), 1, 1);
        }

        /// <summary>
        /// Visualize the vector field for debugging
        /// </summary>
        private void VisualizeField()
        {
            if (debugParent == null)
                return;

            // Clear previous visualization
            foreach (Transform child in debugParent)
            {
                Destroy(child.gameObject);
            }

            // Create a temporary read buffer
            Texture2D readTexture = new Texture2D(gridWidth, gridHeight, TextureFormat.RGBAFloat, false);
            RenderTexture.active = vectorField;
            readTexture.ReadPixels(new Rect(0, 0, gridWidth, gridHeight), 0, 0);
            readTexture.Apply();
            RenderTexture.active = null;

            // Create visualization arrows
            int visStep = 16; // Visualize every Nth cell to avoid overwhelming the scene
            for (int x = 0; x < gridWidth; x += visStep)
            {
                for (int y = 0; y < gridHeight; y += visStep)
                {
                    Color pixel = readTexture.GetPixel(x, y);
                    Vector2 direction = new Vector2(pixel.r, pixel.g);
                    float fieldStrength = pixel.b;
                    float obstacleValue = pixel.a;

                    if (direction.magnitude > 0.01f)
                    {
                        // Create visualization arrow
                        GameObject arrowObj = new GameObject($"Arrow_{x}_{y}");
                        arrowObj.transform.parent = debugParent;

                        // Convert grid position to world position
                        Vector2 worldPos = new Vector2(x * cellSize, y * cellSize) + worldOffset;
                        arrowObj.transform.position = new Vector3(worldPos.x, 0.1f, worldPos.y);

                        // Rotate based on direction
                        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                        arrowObj.transform.rotation = Quaternion.Euler(90, 0, angle);

                        // Scale based on field strength
                        float scale = fieldStrength * 0.5f;
                        arrowObj.transform.localScale = new Vector3(0.1f, 0.1f, scale);

                        // Add a debug line renderer
                        LineRenderer line = arrowObj.AddComponent<LineRenderer>();
                        line.startWidth = 0.05f;
                        line.endWidth = 0f;
                        line.positionCount = 2;
                        line.SetPosition(0, arrowObj.transform.position);
                        line.SetPosition(1, arrowObj.transform.position + arrowObj.transform.forward * scale);

                        // Set color based on field strength (blue to red gradient)
                        line.startColor = Color.Lerp(Color.blue, Color.red, fieldStrength);
                        line.endColor = line.startColor;
                    }

                    // Visualize obstacles
                    if (obstacleValue > 0.1f)
                    {
                        GameObject obstacleObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        obstacleObj.transform.parent = debugParent;

                        // Convert grid position to world position
                        Vector2 worldPos = new Vector2(x * cellSize, y * cellSize) + worldOffset;
                        obstacleObj.transform.position = new Vector3(worldPos.x, obstacleValue * 0.5f, worldPos.y);

                        // Scale based on obstacle value
                        obstacleObj.transform.localScale = new Vector3(cellSize * 0.8f, obstacleValue, cellSize * 0.8f);

                        // Set material color
                        Renderer renderer = obstacleObj.GetComponent<Renderer>();
                        renderer.material.color = new Color(0.5f, 0.5f, 0.5f, obstacleValue);
                    }
                }
            }

            Destroy(readTexture);
        }

        /// <summary>
        /// Convert from world position to grid position, handling negative coordinates
        /// </summary>
        private Vector2 WorldToGrid(Vector2 worldPos)
        {
            // Make sure to use the bounds to properly handle negative coordinates
            return (worldPos - worldBoundsMin) / cellSize;
        }

        /// <summary>
        /// Convert from grid position to world position
        /// </summary>
        private Vector2 GridToWorld(Vector2 gridPos)
        {
            return gridPos * cellSize + worldBoundsMin;
        }

        /// <summary>
        /// Clean up resources
        /// </summary>
        private void ReleaseResources()
        {
            if (vectorField != null)
            {
                vectorField.Release();
                vectorField = null;
            }

            if (distanceField != null)
            {
                distanceField.Release();
                distanceField = null;
            }

            if (goalBuffer != null)
            {
                goalBuffer.Release();
                goalBuffer = null;
            }

            if (navMeshBuffer != null)
            {
                navMeshBuffer.Release();
                navMeshBuffer = null;
            }

            initialized = false;
        }
    }
}     