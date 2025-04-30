using System.Collections.Generic;
using UnityEngine;

namespace MipmapPathfinding
{
    /// <summary>
    /// Interface for agents that use the pathfinding system
    /// </summary>
    public interface IPathfindingAgent
    {
        /// <summary>
        /// Returns the current world position of the agent
        /// </summary>
        Vector3 GetPosition();

        /// <summary>
        /// Returns the importance value of the agent (0.0 to 1.0)
        /// Higher values may use higher resolution data
        /// </summary>
        float GetImportance();
    }

    /// <summary>
    /// Provides a simple interface for agents to access the vector field data for navigation
    /// </summary>
    public class AgentQuerySystem : MonoBehaviour
    {
        [Header("Component References")]
        [SerializeField] private MipmapGenerator mipmapGenerator;
        [SerializeField] private ResolutionBiasController biasController;
        [SerializeField] private VectorFieldStorage vectorFieldStorage;

        [Header("Query Settings")]
        [Tooltip("Whether to use batch processing for multiple agent queries")]
        [SerializeField] private bool enableBatchQueries = true;

        [Tooltip("Whether to use resolution bias when selecting data sources")]
        [SerializeField] private bool useResolutionBias = true;

        [Tooltip("Whether to cache query results briefly for better performance")]
        [SerializeField] private bool cacheSamplingResults = true;

        [Header("Debug Settings")]
        [Tooltip("Whether to display debug visualizations of agent queries")]
        [SerializeField] private bool debugQueryVisualization = false;

        // Internal state
        private bool initialized = false;
        private Bounds navigationBounds;
        private HashSet<IPathfindingAgent> registeredAgents = new HashSet<IPathfindingAgent>();
        private Dictionary<Vector3Int, Vector3> directionCache = new Dictionary<Vector3Int, Vector3>();
        private float cacheClearTimer = 0f;
        private const float CACHE_CLEAR_INTERVAL = 0.5f; // Clear cache every half second

        // For navigation validity checks
        private RenderTexture baseNavTexture;
        private Texture2D navTextureReadback;

        /// <summary>
        /// Indicates whether the agent query system has been initialized
        /// </summary>
        public bool IsInitialized => initialized;

        /// <summary>
        /// Returns the number of currently registered agents
        /// </summary>
        public int AgentCount => registeredAgents.Count;

        /// <summary>
        /// Initializes the agent query system
        /// </summary>
        public void Initialize()
        {
            if (initialized)
                return;

            // Verify dependencies
            if (mipmapGenerator == null)
            {
                Debug.LogError("AgentQuerySystem: MipmapGenerator is null");
                return;
            }

            if (vectorFieldStorage == null)
            {
                Debug.LogError("AgentQuerySystem: VectorFieldStorage is null");
                return;
            }

            if (biasController == null)
            {
                Debug.LogWarning("AgentQuerySystem: ResolutionBiasController is null. Resolution bias will not be applied.");
                useResolutionBias = false;
            }

            // Cache navigation bounds for quick access
            navigationBounds = mipmapGenerator.GetNavigationBounds();

            // Get base navigation texture for validity checks
            baseNavTexture = mipmapGenerator.GetMipmapLevel(0);

            // Create readback texture for navigation checks if needed
            if (baseNavTexture != null)
            {
                navTextureReadback = new Texture2D(1, 1, TextureFormat.R8, false);
            }

            initialized = true;

            Debug.Log("AgentQuerySystem initialized successfully");
        }

        /// <summary>
        /// Updates internal state like caches
        /// </summary>
        private void Update()
        {
            if (!initialized)
                return;

            // Clear direction cache periodically to prevent stale data
            if (cacheSamplingResults)
            {
                cacheClearTimer += Time.deltaTime;
                if (cacheClearTimer >= CACHE_CLEAR_INTERVAL)
                {
                    directionCache.Clear();
                    cacheClearTimer = 0f;
                }
            }
        }

        /// <summary>
        /// Returns the optimal movement direction for an agent at the specified position
        /// </summary>
        /// <param name="worldPosition">The world-space position of the agent</param>
        /// <param name="agentImportance">A value from 0.0 to 1.0 indicating the agent's importance</param>
        /// <returns>A normalized vector representing the optimal movement direction (Y = 0)</returns>
        public Vector3 GetFlowDirection(Vector3 worldPosition, float agentImportance = 1.0f)
        {
            if (!initialized)
                return Vector3.zero;

            // Check if position is within navigation bounds
            if (!navigationBounds.Contains(worldPosition))
                return Vector3.zero;

            // Check if position is valid for navigation
            if (!IsPositionNavigable(worldPosition))
                return Vector3.zero;

            // Check cache first if enabled
            if (cacheSamplingResults)
            {
                // Create a quantized position key for the cache (1 unit precision)
                Vector3Int cacheKey = new Vector3Int(
                    Mathf.RoundToInt(worldPosition.x),
                    Mathf.RoundToInt(worldPosition.y),
                    Mathf.RoundToInt(worldPosition.z)
                );

                if (directionCache.TryGetValue(cacheKey, out Vector3 cachedDirection))
                {
                    return cachedDirection;
                }
            }

            // Determine resolution level based on bias and importance
            int resolutionLevel = 0;

            if (useResolutionBias && biasController != null)
            {
                float bias = biasController.SampleBias(worldPosition);

                // Adjust bias by agent importance (more important agents get higher resolution)
                bias *= agentImportance;

                // Convert bias to resolution level (0-4)
                // Higher bias = lower level number = higher resolution
                resolutionLevel = Mathf.Clamp(
                    Mathf.FloorToInt(4.0f - bias),
                    0,
                    mipmapGenerator.GetMipmapLevelCount() - 1
                );
            }

            // Sample the vector field
            Vector2 flowDirection = vectorFieldStorage.SampleVectorField(worldPosition, resolutionLevel);

            // Convert to 3D vector in XZ plane
            Vector3 result = new Vector3(flowDirection.x, 0, flowDirection.y);

            // Normalize to ensure consistent movement speed
            if (result.sqrMagnitude > 0.01f)
            {
                result.Normalize();
            }

            // Cache the result if caching is enabled
            if (cacheSamplingResults)
            {
                Vector3Int cacheKey = new Vector3Int(
                    Mathf.RoundToInt(worldPosition.x),
                    Mathf.RoundToInt(worldPosition.y),
                    Mathf.RoundToInt(worldPosition.z)
                );

                directionCache[cacheKey] = result;
            }

            // Visualize if debugging is enabled
            if (debugQueryVisualization)
            {
                Debug.DrawRay(worldPosition, result * 2.0f, Color.green, Time.deltaTime);
                Debug.DrawRay(worldPosition, Vector3.up * 0.5f,
                    resolutionLevel == 0 ? Color.red :
                    resolutionLevel == 1 ? Color.yellow :
                    resolutionLevel == 2 ? Color.green :
                    resolutionLevel == 3 ? Color.cyan :
                    Color.blue, Time.deltaTime);
            }

            return result;
        }

        /// <summary>
        /// Efficiently retrieves movement directions for multiple agents in a single call
        /// </summary>
        /// <param name="positions">Array of world-space positions for the agents</param>
        /// <param name="results">Pre-allocated array that will be filled with the resulting direction vectors</param>
        /// <param name="importanceValues">Optional array of importance values for each agent</param>
        public void GetFlowDirectionBatch(Vector3[] positions, Vector3[] results, float[] importanceValues = null)
        {
            if (!initialized || positions == null || results == null || positions.Length != results.Length)
                return;

            if (!enableBatchQueries)
            {
                // Fall back to individual queries if batch processing is disabled
                for (int i = 0; i < positions.Length; i++)
                {
                    float importance = importanceValues != null && i < importanceValues.Length ?
                        importanceValues[i] : 1.0f;

                    results[i] = GetFlowDirection(positions[i], importance);
                }
                return;
            }

            // Batch processing implementation
            // This is where optimizations for memory access patterns and cache utilization would go
            // For now, we'll just iterate through the arrays with some minimal optimizations

            // Pre-check which positions are valid to avoid unnecessary work
            bool[] validPositions = new bool[positions.Length];
            int validCount = 0;

            for (int i = 0; i < positions.Length; i++)
            {
                // Quick bounds check
                validPositions[i] = navigationBounds.Contains(positions[i]);
                if (validPositions[i])
                    validCount++;
            }

            // If no valid positions, early exit
            if (validCount == 0)
                return;

            // Process valid positions
            for (int i = 0; i < positions.Length; i++)
            {
                if (!validPositions[i])
                {
                    results[i] = Vector3.zero;
                    continue;
                }

                float importance = importanceValues != null && i < importanceValues.Length ?
                    importanceValues[i] : 1.0f;

                // Check cache first if enabled
                bool foundInCache = false;

                if (cacheSamplingResults)
                {
                    Vector3Int cacheKey = new Vector3Int(
                        Mathf.RoundToInt(positions[i].x),
                        Mathf.RoundToInt(positions[i].y),
                        Mathf.RoundToInt(positions[i].z)
                    );

                    if (directionCache.TryGetValue(cacheKey, out Vector3 cachedDirection))
                    {
                        results[i] = cachedDirection;
                        foundInCache = true;
                    }
                }

                if (!foundInCache)
                {
                    // Determine resolution level based on bias and importance
                    int resolutionLevel = 0;

                    if (useResolutionBias && biasController != null)
                    {
                        float bias = biasController.SampleBias(positions[i]);
                        bias *= importance;
                        resolutionLevel = Mathf.Clamp(
                            Mathf.FloorToInt(4.0f - bias),
                            0,
                            mipmapGenerator.GetMipmapLevelCount() - 1
                        );
                    }

                    // Sample the vector field
                    Vector2 flowDirection = vectorFieldStorage.SampleVectorField(positions[i], resolutionLevel);

                    // Convert to 3D vector and normalize
                    Vector3 result = new Vector3(flowDirection.x, 0, flowDirection.y);
                    if (result.sqrMagnitude > 0.01f)
                    {
                        result.Normalize();
                    }

                    results[i] = result;

                    // Cache the result
                    if (cacheSamplingResults)
                    {
                        Vector3Int cacheKey = new Vector3Int(
                            Mathf.RoundToInt(positions[i].x),
                            Mathf.RoundToInt(positions[i].y),
                            Mathf.RoundToInt(positions[i].z)
                        );

                        directionCache[cacheKey] = result;
                    }

                    // Visualize if debugging is enabled
                    if (debugQueryVisualization)
                    {
                        Debug.DrawRay(positions[i], result * 2.0f, Color.green, Time.deltaTime);
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a position is within the navigable area
        /// </summary>
        /// <param name="worldPosition">The world-space position to check</param>
        /// <returns>True if the position is navigable, false otherwise</returns>
        public bool IsPositionNavigable(Vector3 worldPosition)
        {
            if (!initialized || baseNavTexture == null)
                return false;

            // Check if position is within navigation bounds first (fast rejection)
            if (!navigationBounds.Contains(worldPosition))
                return false;

            // Convert world position to texture coordinates
            Vector2 texCoord = WorldToTextureCoordinates(worldPosition);

            // Clamp to texture bounds
            texCoord.x = Mathf.Clamp01(texCoord.x);
            texCoord.y = Mathf.Clamp01(texCoord.y);

            // Convert to pixel coordinates
            int x = Mathf.RoundToInt(texCoord.x * (baseNavTexture.width - 1));
            int y = Mathf.RoundToInt(texCoord.y * (baseNavTexture.height - 1));

            // Read pixel value from the navigation texture
            RenderTexture.active = baseNavTexture;
            navTextureReadback.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
            RenderTexture.active = null;
            Color pixelColor = navTextureReadback.GetPixel(0, 0);

            // In the BoundsCalculator_Optimized.compute shader:
            // CELL = float4(0, 0, 0, 1) for navigable areas
            // BOUNDARY = float4(0, 0, 1, 0) for non-navigable areas

            // Check if the position is navigable (based on the R channel in our case)
            return pixelColor.b < 0.5f; // Cell is navigable, boundary is not
        }

        /// <summary>
        /// Finds the nearest navigable position to the specified world position
        /// </summary>
        /// <param name="worldPosition">The world-space position to start from</param>
        /// <param name="maxSearchDistance">Maximum search distance</param>
        /// <returns>The nearest navigable position if found, otherwise the original position</returns>
        public Vector3 GetNearestNavigablePosition(Vector3 worldPosition, float maxSearchDistance = 10.0f)
        {
            if (!initialized || !navigationBounds.Contains(worldPosition))
                return worldPosition;

            // If the position is already navigable, return it
            if (IsPositionNavigable(worldPosition))
                return worldPosition;

            // Simple spiral search for a navigable position
            // Start with 8 directions (compass directions)
            Vector3[] directions = new Vector3[]
            {
                Vector3.forward,
                Vector3.forward + Vector3.right,
                Vector3.right,
                Vector3.right + Vector3.back,
                Vector3.back,
                Vector3.back + Vector3.left,
                Vector3.left,
                Vector3.left + Vector3.forward
            };

            // Normalize the diagonal directions
            for (int i = 0; i < directions.Length; i++)
            {
                directions[i].Normalize();
            }

            // Spiral search with increasing distance
            for (float distance = 1.0f; distance <= maxSearchDistance; distance += 0.5f)
            {
                foreach (Vector3 dir in directions)
                {
                    Vector3 testPos = worldPosition + dir * distance;
                    if (IsPositionNavigable(testPos))
                    {
                        return testPos;
                    }
                }

                // Double the number of directions every few iterations to get finer granularity
                if (distance % 2.0f < 0.1f && directions.Length < 32)
                {
                    Vector3[] newDirections = new Vector3[directions.Length * 2];
                    for (int i = 0; i < directions.Length; i++)
                    {
                        newDirections[i * 2] = directions[i];

                        // Insert a direction halfway between this one and the next
                        Vector3 nextDir = directions[(i + 1) % directions.Length];
                        newDirections[i * 2 + 1] = (directions[i] + nextDir).normalized;
                    }
                    directions = newDirections;
                }
            }

            // If no navigable position found within the max distance, return the original
            return worldPosition;
        }

        /// <summary>
        /// Provides an estimate of the distance to the nearest target following the vector field
        /// </summary>
        /// <param name="worldPosition">The world-space position of the agent</param>
        /// <param name="maxDistance">The maximum search distance (to prevent infinite loops)</param>
        /// <returns>An approximate distance value following the flow field to the nearest target</returns>
        public float GetApproximateDistanceToTarget(Vector3 worldPosition, float maxDistance = 1000.0f)
        {
            if (!initialized || !navigationBounds.Contains(worldPosition))
                return -1.0f;

            if (!IsPositionNavigable(worldPosition))
                return -1.0f;

            // This is an approximation by following the vector field direction
            // It won't be exact but will be much faster than a full path calculation

            Vector3 currentPos = worldPosition;
            float totalDistance = 0f;
            float stepSize = 0.5f; // Smaller steps for more accuracy, larger for performance
            float minStepSize = 0.1f;

            // Used to detect if we're stuck in a loop or dead end
            HashSet<Vector3Int> visitedPositions = new HashSet<Vector3Int>();

            // Maximum iterations to prevent infinite loops
            const int MAX_ITERATIONS = 2000;

            for (int i = 0; i < MAX_ITERATIONS && totalDistance < maxDistance; i++)
            {
                // Get the flow direction at current position
                Vector3 direction = GetFlowDirection(currentPos);

                // If we hit a dead end or are getting really weak directions, stop
                if (direction.magnitude < 0.1f)
                {
                    break;
                }

                // Near targets, the vector field often spirals or creates complex patterns
                // Dynamically adjust step size based on direction changes
                if (i > 5)
                {
                    Vector3 prevDirection = GetFlowDirection(currentPos - direction * stepSize);
                    float directionChange = Vector3.Angle(direction, prevDirection);

                    // If direction is changing rapidly, reduce step size
                    if (directionChange > 45f)
                    {
                        stepSize = Mathf.Max(minStepSize, stepSize * 0.5f);
                    }
                    else if (directionChange < 5f && stepSize < 1.0f)
                    {
                        // If direction is stable, can use larger steps
                        stepSize = Mathf.Min(1.0f, stepSize * 1.2f);
                    }
                }

                // Take a step in the flow direction
                Vector3 nextPos = currentPos + direction * stepSize;

                // Check if we're making progress or stuck in a loop
                Vector3Int quantizedPos = new Vector3Int(
                    Mathf.RoundToInt(nextPos.x * 5f),
                    Mathf.RoundToInt(nextPos.y * 5f),
                    Mathf.RoundToInt(nextPos.z * 5f)
                );

                if (visitedPositions.Contains(quantizedPos))
                {
                    // We're in a loop, likely circling around a target
                    // Use the current distance as an approximation
                    break;
                }

                visitedPositions.Add(quantizedPos);

                // Update distance and position
                totalDistance += stepSize;
                currentPos = nextPos;

                // If we're very close to a target, we can stop
                // This would need to be replaced with actual target positions
                // For now, we'll use field strength as a proxy
                Vector2 fieldValue = vectorFieldStorage.SampleVectorField(currentPos);
                if (fieldValue.magnitude < 0.1f)
                {
                    break;
                }
            }

            return totalDistance;
        }

        /// <summary>
        /// Returns the resolution level that would be used for a query at the specified position
        /// </summary>
        /// <param name="worldPosition">The world-space position to check</param>
        /// <param name="agentImportance">A value from 0.0 to 1.0 indicating the agent's importance</param>
        /// <returns>The resolution level that would be used (0 = highest resolution)</returns>
        public int GetResolutionLevelAtPosition(Vector3 worldPosition, float agentImportance = 1.0f)
        {
            if (!initialized || !navigationBounds.Contains(worldPosition))
                return -1;

            if (!useResolutionBias || biasController == null)
                return 0;

            // Sample the bias at this position
            float bias = biasController.SampleBias(worldPosition);

            // Adjust by agent importance
            bias *= agentImportance;

            // Convert to resolution level (0-4)
            int resolutionLevel = Mathf.Clamp(
                Mathf.FloorToInt(4.0f - bias),
                0,
                mipmapGenerator.GetMipmapLevelCount() - 1
            );

            return resolutionLevel;
        }

        /// <summary>
        /// Registers an agent with the query system for tracking and optimization
        /// </summary>
        /// <param name="agent">An object implementing the IPathfindingAgent interface</param>
        public void RegisterAgent(IPathfindingAgent agent)
        {
            if (!initialized || agent == null)
                return;

            registeredAgents.Add(agent);
        }

        /// <summary>
        /// Removes an agent from the query system's tracking
        /// </summary>
        /// <param name="agent">The agent to remove</param>
        public void UnregisterAgent(IPathfindingAgent agent)
        {
            if (!initialized || agent == null)
                return;

            registeredAgents.Remove(agent);
        }

        /// <summary>
        /// Converts a world position to texture coordinates (0-1 range)
        /// </summary>
        private Vector2 WorldToTextureCoordinates(Vector3 worldPosition)
        {
            // Calculate the normalized position within the bounds
            Vector3 localPos = worldPosition - navigationBounds.min;
            Vector3 normalizedPos = new Vector3(
                localPos.x / navigationBounds.size.x,
                0,
                localPos.z / navigationBounds.size.z
            );

            // Convert to texture coordinates (0-1 range)
            return new Vector2(normalizedPos.x, normalizedPos.z);
        }

        /// <summary>
        /// Cleanup when the component is destroyed
        /// </summary>
        private void OnDestroy()
        {
            // Clean up resources
            if (navTextureReadback != null)
            {
                Destroy(navTextureReadback);
                navTextureReadback = null;
            }

            registeredAgents.Clear();
            directionCache.Clear();
        }
    }
}