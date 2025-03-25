using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace fourth
{

    /// <summary>
    /// Manages a Vector Flow Field for AI navigation based on Unity's NavMesh
    /// Acts as a central access point for AI agents to sample velocity vectors
    /// </summary>
    public class VectorFlowFieldManager : MonoBehaviour
    {
        [Header("Compute Shaders")]
        [SerializeField] private ComputeShader vffCalculatorShader;
        [SerializeField] private ComputeShader boundaryGeneratorShader;

        [Header("Flow Field Settings")]
        [SerializeField] private Vector2 resolution = new Vector2(256, 256);
        [SerializeField] private float maxInfluenceStrength = 10f;
        [SerializeField] private float viscosityCoefficient = 0.5f;
        [SerializeField] private float pressureCoefficient = 0.5f;
        [SerializeField] private int iterationCount = 20;
        [SerializeField] private bool visualizeField = false;

        [Header("NavMesh Conversion")]
        [SerializeField] private float navMeshDetailLevel = 0.5f;

        [Header("Visualization / debugging")]
        [SerializeField] private bool frameByFrame = false;
        [SerializeField] private Material visualizationMaterial;
        [SerializeField] private float visualizationHeight = 0.1f;
        [SerializeField] private Vector2 visualizationScale = new Vector2(1f, 1f);
        [SerializeField] private RenderTexture boundaryTexture;

        [Header("RenderTextures for Visualization during simulation")]
        [SerializeField] RenderTexture VelocityTexture;
        [SerializeField] RenderTexture VelocityTexturePrev;
        [SerializeField] RenderTexture PressureTexture;
        [SerializeField] RenderTexture PressureTexturePrev;
        [SerializeField] RenderTexture DivergenceTexture;
        [SerializeField] RenderTexture BoundaryTexture;
        [SerializeField] RenderTexture VisualizationTexture;

        // Core system components
        private VFFInterface vffInterface;
        private BoundaryTextureGenerator boundaryGenerator;
        private List<FlowFieldInfluence> influences;
        private Bounds worldBounds;
        private Mesh navMeshAsMesh;
        private GameObject visualizationObject;

        // Cache for quick lookup
        private Dictionary<int, Vector3> lastSampledVelocities = new Dictionary<int, Vector3>();
        private float velocityCacheTime = 0.1f; // Seconds before re-sampling velocity for an agent
        private Dictionary<int, float> lastSampleTimes = new Dictionary<int, float>();

        void Awake()
        {
            // Initialize the influences list
            influences = new List<FlowFieldInfluence>();

            // Calculate world bounds from the navigation mesh
            CalculateWorldBounds();

            // Create mesh representation of NavMesh
            GenerateNavMeshAsMesh();

            // Initialize the boundary generator
            InitializeBoundaryGenerator();

            // Initialize the VFF system
            InitializeVectorFlowField();

            VelocityTexture = vffInterface.GetRenderBuffer(vffInterface.renderNames[0]);
            VelocityTexturePrev = vffInterface.GetRenderBuffer(vffInterface.renderNames[1]);
            PressureTexture = vffInterface.GetRenderBuffer(vffInterface.renderNames[2]);
            PressureTexturePrev = vffInterface.GetRenderBuffer(vffInterface.renderNames[3]);
            DivergenceTexture = vffInterface.GetRenderBuffer(vffInterface.renderNames[4]);
            BoundaryTexture = vffInterface.GetRenderBuffer(vffInterface.renderNames[5]);
            VisualizationTexture = vffInterface.GetRenderBuffer(vffInterface.renderNames[6]);
        }

        void Update()
        {
            // Update the flow field simulation with proper time delta
            vffInterface?.Update(Time.deltaTime);

            // Visualize the flow field if enabled (for debugging)
            if (visualizeField)
            {
                VisualizeFlowField();
            }
        }

        private void LateUpdate()
        {
            if(frameByFrame) Debug.Break();
        }

        /// <summary>
        /// Samples the vector flow field at the specified world position
        /// </summary>
        /// <param name="worldPosition">Position in world space</param>
        /// <param name="agentId">Unique identifier for the agent (used for caching)</param>
        /// <returns>Velocity vector (x,z) mapped to (x,y,z) with y=0</returns>
        public Vector3 SampleFlowField(Vector3 worldPosition, int agentId = 0)
        {
            // Check if we have a recent cached value for this agent
            if (agentId != 0 &&
                lastSampledVelocities.ContainsKey(agentId) &&
                lastSampleTimes.ContainsKey(agentId))
            {
                if (Time.time - lastSampleTimes[agentId] < velocityCacheTime)
                {
                    // Return cached value if it's recent enough
                    return lastSampledVelocities[agentId];
                }
            }

            // No valid cache, sample the flow field
            Vector3 velocity = vffInterface.SampleVelocityField(worldPosition);

            // Cache the result if agent ID is provided
            if (agentId != 0)
            {
                lastSampledVelocities[agentId] = velocity;
                lastSampleTimes[agentId] = Time.time;
            }

            return velocity;
        }

        public Vector2Int GetUVOfPosition(Vector3 worldPosition)
        {
            return vffInterface.GetUV(worldPosition);
        }

        /// <summary>
        /// Adds a flow influence (source or sink) at the specified position
        /// </summary>
        /// <param name="position">World position for the influence</param>
        /// <param name="strength">Strength of the influence (positive for source, negative for sink)</param>
        /// <returns>ID of the created influence for later reference</returns>
        public int AddInfluence(Vector3 position, float strength)
        {
            FlowFieldInfluence influence = new FlowFieldInfluence
            {
                active = true,
                position = position,
                strength = Mathf.Abs(strength),
                type = strength >= 0 ? eInfluenceType.Source : eInfluenceType.Sink
            };

            influences.Add(influence);
            return influences.Count - 1; // Return index as the ID
        }
        public int AddInfluence(FlowFieldInfluence influence)
        {
            influences.Add(influence);
            Debug.Log($"Influence grid pos: {vffInterface.GetUV(influence.position)}");
            return influences.IndexOf(influence);
        }

        /// <summary>
        /// Removes an influence by its ID
        /// </summary>
        /// <param name="influenceId">ID of the influence to remove</param>
        public void RemoveInfluence(int influenceId)
        {
            if (influenceId >= 0 && influenceId < influences.Count)
            {
                // Deactivate rather than remove to avoid reindexing
                influences[influenceId].active = false;
            }
        }

        /// <summary>
        /// Regenerates the NavMesh representation and boundary texture
        /// Call this if the NavMesh changes at runtime
        /// </summary>
        public void RegenerateFromNavMesh()
        {
            CalculateWorldBounds();
            GenerateNavMeshAsMesh();
            RenderTexture boundaryTexture = boundaryGenerator.GenerateTexture(navMeshAsMesh, GetVFFParameters());
            vffInterface.SetBoundaryTexture(boundaryTexture);
        }

        /// <summary>
        /// Calculates the bounds of the navigation mesh
        /// </summary>
        private void CalculateWorldBounds()
        {
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

            if (triangulation.vertices.Length == 0)
            {
                Debug.LogWarning("No NavMesh found. Using default bounds.");
                worldBounds = new Bounds(Vector3.zero, new Vector3(20, 1, 20));
                return;
            }

            // Initialize with the first vertex
            worldBounds = new Bounds(triangulation.vertices[0], Vector3.zero);

            // Expand to include all vertices
            foreach (Vector3 vertex in triangulation.vertices)
            {
                worldBounds.Encapsulate(vertex);
            }

            // Set a reasonable height for the bounds
            float height = worldBounds.size.y;
            worldBounds.min = new Vector3(worldBounds.min.x, 0, worldBounds.min.z);
            worldBounds.max = new Vector3(worldBounds.max.x, height, worldBounds.max.z);
        }

        /// <summary>
        /// Generates a mesh representation of the NavMesh
        /// </summary>
        private void GenerateNavMeshAsMesh()
        {
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

            if (triangulation.vertices.Length == 0 || triangulation.indices.Length == 0)
            {
                Debug.LogError("Failed to generate NavMesh triangulation.");
                return;
            }

            navMeshAsMesh = new Mesh();
            navMeshAsMesh.vertices = triangulation.vertices;
            navMeshAsMesh.triangles = triangulation.indices;
            navMeshAsMesh.RecalculateNormals();
        }

        /// <summary>
        /// Initializes the boundary texture generator
        /// </summary>
        private void InitializeBoundaryGenerator()
        {
            if (boundaryGeneratorShader == null)
            {
                Debug.LogError("Boundary generator shader is not assigned!");
                return;
            }

            boundaryGenerator = new BoundaryTextureGenerator(boundaryGeneratorShader);
        }

        /// <summary>
        /// Initializes the Vector Flow Field system
        /// </summary>
        private void InitializeVectorFlowField()
        {
            if (vffCalculatorShader == null)
            {
                Debug.LogError("VFF calculator shader is not assigned!");
                return;
            }

            if (navMeshAsMesh == null)
            {
                Debug.LogError("NavMesh not successfully converted to mesh!");
                return;
            }

            // Generate the VFF parameters
            VFFParameters parameters = GetVFFParameters();

            // Generate the boundary texture
            RenderTexture boundaryTexture = boundaryGenerator.GenerateTexture(navMeshAsMesh, parameters);
            this.boundaryTexture = boundaryTexture;

            // Create the VFF interface
            vffInterface = new VFFInterface(vffCalculatorShader, parameters, influences);
            vffInterface.SetBoundaryTexture(boundaryTexture);
        }

        /// <summary>
        /// Creates VFF parameters from current settings
        /// </summary>
        private VFFParameters GetVFFParameters()
        {
            VFFParameters temp = new VFFParameters.Builder()
                .WithComputeShader(vffCalculatorShader)
                .WithMaxInfluenceStrength(maxInfluenceStrength)
                .WithResolution(resolution)
                .WithBounds(worldBounds)
                .WithViscosity(viscosityCoefficient)
                .WithPressure(pressureCoefficient)
                .WithIterations(iterationCount)
                .CalculateInversResolution()
                .Build();
            temp.inverseResolution = resolution / new Vector2(worldBounds.size.x, worldBounds.size.z);
            return temp;
        }

        /// <summary>
        /// Visualizes the flow field by displaying the texture representation
        /// </summary>
        private void VisualizeFlowField()
        {
            // Use the VFFInterface's GetVisualization method to display the texture
            if (visualizationObject == null)
            {
                // Create visualization plane if it doesn't exist
                CreateVisualizationPlane();
            }

            // Update visualization texture if needed
            if (visualizationMaterial != null)
            {
                Texture2D visualizationTexture = vffInterface.GetVisualization();
                if (visualizationTexture != null)
                {
                    visualizationMaterial.mainTexture = visualizationTexture;
                }
            }
        }

        /// <summary>
        /// Creates a plane for visualizing the flow field texture
        /// </summary>
        private void CreateVisualizationPlane()
        {
            if (visualizationObject != null)
            {
                Destroy(visualizationObject);
            }

            visualizationObject = new GameObject("VFF_Visualization");
            visualizationObject.transform.SetParent(transform);

            // Position the plane slightly above the terrain to avoid z-fighting
            Vector3 center = worldBounds.center;
            center.y = visualizationHeight;
            visualizationObject.transform.position = center;

            // Create the mesh for the visualization plane
            MeshFilter meshFilter = visualizationObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = visualizationObject.AddComponent<MeshRenderer>();

            // Create a new material if none is assigned
            if (visualizationMaterial == null)
            {
                visualizationMaterial = new Material(Shader.Find("Unlit/Texture"));
            }

            meshRenderer.material = visualizationMaterial;

            // Create a plane mesh sized to match the world bounds
            Mesh planeMesh = new Mesh();
            float width = worldBounds.size.x * visualizationScale.x;
            float height = worldBounds.size.z * visualizationScale.y;

            Vector3[] vertices = new Vector3[4]
            {
            new Vector3(-width/2, 0, -height/2),
            new Vector3(width/2, 0, -height/2),
            new Vector3(-width/2, 0, height/2),
            new Vector3(width/2, 0, height/2)
            };

            int[] triangles = new int[6]
            {
            0, 2, 1,
            2, 3, 1
            };

            Vector2[] uv = new Vector2[4]
            {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
            };

            planeMesh.vertices = vertices;
            planeMesh.triangles = triangles;
            planeMesh.uv = uv;
            planeMesh.RecalculateNormals();

            meshFilter.mesh = planeMesh;
        }

        private void OnDestroy()
        {
            // Clean up resources
            if (navMeshAsMesh != null)
            {
                Destroy(navMeshAsMesh);
            }

            if (visualizationObject != null)
            {
                Destroy(visualizationObject);
            }
        }

#if UNITY_EDITOR
        // Add a custom editor menu option to toggle visualization
        [MenuItem("VectorFlowField/Toggle Visualization")]
        static void ToggleVisualization()
        {
            VectorFlowFieldManager manager = FindObjectOfType<VectorFlowFieldManager>();
            if (manager != null)
            {
                SerializedObject serializedObject = new SerializedObject(manager);
                SerializedProperty visualizeFieldProperty = serializedObject.FindProperty("visualizeField");
                visualizeFieldProperty.boolValue = !visualizeFieldProperty.boolValue;
                serializedObject.ApplyModifiedProperties();

                EditorUtility.SetDirty(manager);
            }
        }
#endif
    }
}