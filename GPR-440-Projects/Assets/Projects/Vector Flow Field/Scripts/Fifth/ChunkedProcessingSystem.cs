using System;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace MipmapPathfinding
{
    /// <summary>
    /// Distributes vector field computation across multiple frames for consistent performance
    /// </summary>
    public class ChunkedProcessingSystem : MonoBehaviour
    {
        [Header("Required Components")]
        [SerializeField] private MipmapGenerator mipmapGenerator;
        [SerializeField] private ResolutionBiasController biasController;
        [SerializeField] private MultiResolutionVectorFieldGenerator vectorFieldGenerator;

        [Header("Chunk Settings")]
        [SerializeField] private int chunkSize = 256;
        [SerializeField] private float frameBudgetMs = 8.0f;
        [SerializeField] private bool adaptiveProcessing = true;
        [SerializeField] private bool visualizeChunks = false;

        [Header("Priority Weights")]
        [SerializeField] private float agentDensityWeight = 1.0f;
        [SerializeField] private float targetProximityWeight = 1.5f;
        [SerializeField] private float timeSinceUpdateWeight = 0.8f;

        // Internal data structures
        private List<ChunkData> allChunks = new List<ChunkData>();
        private PriorityQueue<ChunkData> chunkQueue = new PriorityQueue<ChunkData>();
        private HashSet<ChunkData> dirtyChunks = new HashSet<ChunkData>();
        private Dictionary<ChunkData, float> lastUpdateTime = new Dictionary<ChunkData, float>();

        // Agent tracking
        private List<IPathfindingAgent> registeredAgents = new List<IPathfindingAgent>();
        private Dictionary<ChunkData, int> agentCountMap = new Dictionary<ChunkData, int>();

        // Target tracking
        private Vector3[] currentTargets = new Vector3[0];

        // Performance tracking
        private Stopwatch processingStopwatch = new Stopwatch();
        private int chunksProcessedThisFrame = 0;
        private int maxChunksPerFrame = 4;  // Initial value, will adapt
        private float avgProcessingTimeMs = 0f;

        // State tracking
        private bool initialized = false;
        private bool processingActive = false;

        /// <summary>
        /// Indicates whether the system has been initialized
        /// </summary>
        public bool IsInitialized => initialized;

        /// <summary>
        /// Indicates whether the system is currently processing chunks
        /// </summary>
        public bool IsBusy => chunkQueue.Count > 0;

        private void OnValidate()
        {
            // Ensure chunk size is reasonable
            chunkSize = Mathf.Clamp(chunkSize, 64, 512);
        }

        /// <summary>
        /// Initializes the chunked processing system and sets up the initial chunk distribution
        /// </summary>
        public void Initialize()
        {
            if (initialized)
            {
                Debug.LogWarning("ChunkedProcessingSystem is already initialized.");
                return;
            }

            if (mipmapGenerator == null || biasController == null || vectorFieldGenerator == null)
            {
                Debug.LogError("ChunkedProcessingSystem requires references to MipmapGenerator, ResolutionBiasController, and MultiResolutionVectorFieldGenerator.");
                return;
            }

            CreateChunks();
            initialized = true;
            Debug.Log($"ChunkedProcessingSystem initialized with {allChunks.Count} chunks.");
        }

        /// <summary>
        /// Sets the target positions and marks affected chunks as dirty
        /// </summary>
        /// <param name="targetPositions">Array of target world positions</param>
        public void SetTargets(Vector3[] targetPositions)
        {
            if (!initialized)
            {
                Debug.LogWarning("ChunkedProcessingSystem must be initialized before setting targets.");
                return;
            }

            currentTargets = targetPositions;
            vectorFieldGenerator.SetTargets(targetPositions);

            // Mark chunks near targets as dirty
            foreach (var target in targetPositions)
            {
                MarkChunkDirty(target, 10f); // Use a default radius of 10 units
            }
        }

        /// <summary>
        /// Marks chunks within the specified radius as dirty and needing reprocessing
        /// </summary>
        /// <param name="worldPosition">World position center</param>
        /// <param name="radius">Radius in world units</param>
        public void MarkChunkDirty(Vector3 worldPosition, float radius)
        {
            if (!initialized) return;

            // Convert world position to texture coordinates
            Bounds navBounds = mipmapGenerator.GetNavigationBounds();
            Vector2 relativePos = new Vector2(
                (worldPosition.x - navBounds.min.x) / navBounds.size.x,
                (worldPosition.z - navBounds.min.z) / navBounds.size.z
            );

            int baseWidth = mipmapGenerator.GetBaseWidth();
            int baseHeight = mipmapGenerator.GetBaseHeight();

            // Convert radius to pixel coordinates
            float pixelRadius = radius * Mathf.Min(
                baseWidth / navBounds.size.x,
                baseHeight / navBounds.size.z
            );

            Vector2Int pixelPos = new Vector2Int(
                Mathf.FloorToInt(relativePos.x * baseWidth),
                Mathf.FloorToInt(relativePos.y * baseHeight)
            );

            // Find chunks within radius
            int chunksMarkedDirty = 0;
            foreach (var chunk in allChunks)
            {
                Vector2Int chunkMinPixel = chunk.PixelMin;
                Vector2Int chunkMaxPixel = chunk.PixelMin + chunk.PixelSize;
                Vector2Int chunkCenterPixel = chunkMinPixel + chunk.PixelSize / 2;

                // Simple distance check to determine if chunk is affected
                float distance = Vector2Int.Distance(pixelPos, chunkCenterPixel);
                if (distance <= pixelRadius + chunk.PixelSize.magnitude / 2)
                {
                    if (!dirtyChunks.Contains(chunk))
                    {
                        dirtyChunks.Add(chunk);
                        UpdateChunkPriority(chunk);
                        chunksMarkedDirty++;
                    }
                }
            }

            if (chunksMarkedDirty > 0)
            {
                Debug.Log($"Marked {chunksMarkedDirty} chunks as dirty near {worldPosition}");
            }
        }

        /// <summary>
        /// Main update method that processes chunks within the frame budget
        /// </summary>
        public void Update()
        {
            if (!initialized || frameBudgetMs <= 0f) return;

            // Check if we need to enqueue chunks for initial processing
            if (!processingActive && dirtyChunks.Count > 0)
            {
                EnqueueDirtyChunks();
                processingActive = true;
            }

            // If nothing to process, return early
            if (chunkQueue.Count == 0)
            {
                processingActive = false;
                return;
            }

            // Start timing for this frame's processing
            processingStopwatch.Reset();
            processingStopwatch.Start();
            chunksProcessedThisFrame = 0;

            // Process chunks until we run out or hit the frame budget
            while (chunkQueue.Count > 0 && chunksProcessedThisFrame < maxChunksPerFrame)
            {
                if (processingStopwatch.ElapsedMilliseconds >= frameBudgetMs)
                {
                    break;
                }

                ChunkData chunk = chunkQueue.Dequeue();
                vectorFieldGenerator.ProcessChunk(chunk);

                lastUpdateTime[chunk] = Time.time;
                dirtyChunks.Remove(chunk);
                chunksProcessedThisFrame++;
            }

            // Stop timing and update performance metrics
            processingStopwatch.Stop();
            float elapsedMs = processingStopwatch.ElapsedMilliseconds;

            // Adapt the number of chunks to process next frame
            if (adaptiveProcessing && chunksProcessedThisFrame > 0)
            {
                avgProcessingTimeMs = Mathf.Lerp(avgProcessingTimeMs, elapsedMs / chunksProcessedThisFrame, 0.2f);

                if (elapsedMs < frameBudgetMs * 0.7f && avgProcessingTimeMs > 0)
                {
                    maxChunksPerFrame = Mathf.Min(maxChunksPerFrame + 1, 20);
                }
                else if (elapsedMs > frameBudgetMs)
                {
                    maxChunksPerFrame = Mathf.Max(maxChunksPerFrame - 1, 1);
                }
            }

            // Re-prioritize remaining chunks every few frames
            if (Time.frameCount % 10 == 0 && chunkQueue.Count > 0)
            {
                UpdateAllChunkPriorities();
            }
        }

        /// <summary>
        /// Returns the overall completion percentage of chunk processing
        /// </summary>
        /// <returns>Progress from 0 to 1</returns>
        public float GetChunkProgress()
        {
            if (!initialized || allChunks.Count == 0) return 0f;

            int processedChunks = allChunks.Count - dirtyChunks.Count - chunkQueue.Count;
            return (float)processedChunks / allChunks.Count;
        }

        /// <summary>
        /// Returns the agent density at a specific world position
        /// </summary>
        /// <param name="worldPosition">World position to check</param>
        /// <returns>Agent density value</returns>
        public float GetAgentDensity(Vector3 worldPosition)
        {
            if (!initialized) return 0f;

            ChunkData chunk = GetChunkAtPosition(worldPosition);
            if (chunk == null) return 0f;

            // Check if we have density information for this chunk
            int agentCount = 0;
            agentCountMap.TryGetValue(chunk, out agentCount);

            // Normalize by chunk area
            float chunkArea = chunk.PixelSize.x * chunk.PixelSize.y;
            return agentCount / chunkArea * 1000f; // Scaled for better range
        }

        /// <summary>
        /// Sets the target frame budget for chunk processing
        /// </summary>
        /// <param name="milliseconds">Maximum time in milliseconds to spend on processing per frame</param>
        public void SetFrameBudget(float milliseconds)
        {
            frameBudgetMs = Mathf.Max(0f, milliseconds);
            Debug.Log($"Frame budget set to {frameBudgetMs}ms");
        }

        /// <summary>
        /// Registers an agent with the chunked processing system for density tracking
        /// </summary>
        /// <param name="agent">Agent to register</param>
        public void RegisterAgent(IPathfindingAgent agent)
        {
            if (!initialized) return;
            if (registeredAgents.Contains(agent)) return;

            registeredAgents.Add(agent);
            UpdateAgentDensity();
        }

        /// <summary>
        /// Removes an agent from the density tracking system
        /// </summary>
        /// <param name="agent">Agent to remove</param>
        public void UnregisterAgent(IPathfindingAgent agent)
        {
            if (!initialized) return;
            if (!registeredAgents.Contains(agent)) return;

            registeredAgents.Remove(agent);
            UpdateAgentDensity();
        }

        #region Private Methods

        private void CreateChunks()
        {
            allChunks.Clear();
            dirtyChunks.Clear();
            chunkQueue.Clear();
            lastUpdateTime.Clear();

            int baseWidth = mipmapGenerator.GetBaseWidth();
            int baseHeight = mipmapGenerator.GetBaseHeight();
            Bounds navBounds = mipmapGenerator.GetNavigationBounds();

            // Calculate how many chunks in each dimension
            int chunksX = Mathf.CeilToInt((float)baseWidth / chunkSize);
            int chunksY = Mathf.CeilToInt((float)baseHeight / chunkSize);

            for (int y = 0; y < chunksY; y++)
            {
                for (int x = 0; x < chunksX; x++)
                {
                    // Calculate pixel coordinates
                    Vector2Int pixelMin = new Vector2Int(x * chunkSize, y * chunkSize);
                    Vector2Int pixelSize = new Vector2Int(
                        Mathf.Min(chunkSize, baseWidth - pixelMin.x),
                        Mathf.Min(chunkSize, baseHeight - pixelMin.y)
                    );

                    // Calculate world center
                    float worldX = navBounds.min.x + (pixelMin.x + pixelSize.x / 2f) * navBounds.size.x / baseWidth;
                    float worldZ = navBounds.min.z + (pixelMin.y + pixelSize.y / 2f) * navBounds.size.z / baseHeight;
                    Vector3 worldCenter = new Vector3(worldX, 0, worldZ);

                    ChunkData chunk = new ChunkData(pixelMin, pixelSize, worldCenter);
                    allChunks.Add(chunk);
                    dirtyChunks.Add(chunk);
                    lastUpdateTime[chunk] = -1f; // Never updated
                }
            }

            Debug.Log($"Created {allChunks.Count} chunks ({chunksX}x{chunksY})");
        }

        private void EnqueueDirtyChunks()
        {
            // Clear queue and rebuild
            chunkQueue.Clear();

            // Enqueue all dirty chunks with priorities
            foreach (var chunk in dirtyChunks)
            {
                UpdateChunkPriority(chunk);
            }

            Debug.Log($"Enqueued {chunkQueue.Count} chunks for processing");
        }

        private void UpdateAllChunkPriorities()
        {
            var tempQueue = new PriorityQueue<ChunkData>();

            // Rebuild the priority queue with updated priorities
            while (chunkQueue.Count > 0)
            {
                ChunkData chunk = chunkQueue.Dequeue();
                float priority = CalculateChunkPriority(chunk);
                tempQueue.Enqueue(chunk, priority);
            }

            chunkQueue = tempQueue;
        }

        private void UpdateChunkPriority(ChunkData chunk)
        {
            float priority = CalculateChunkPriority(chunk);
            chunkQueue.Enqueue(chunk, priority);
        }

        private float CalculateChunkPriority(ChunkData chunk)
        {
            // Higher priority = processed earlier
            float priority = 0f;

            // Factor 1: Agent density
            float agentDensity = 0f;
            if (agentCountMap.TryGetValue(chunk, out int agentCount))
            {
                agentDensity = agentCount / (float)(chunk.PixelSize.x * chunk.PixelSize.y) * 1000f;
            }
            priority += agentDensity * agentDensityWeight;

            // Factor 2: Target proximity
            float targetProximity = 0f;
            foreach (var target in currentTargets)
            {
                float distance = Vector3.Distance(chunk.WorldCenter, target);
                float proximityFactor = 1f / (1f + distance); // Closer = higher value
                targetProximity = Mathf.Max(targetProximity, proximityFactor);
            }
            priority += targetProximity * targetProximityWeight;

            // Factor 3: Time since last update
            float timeSinceUpdate = Time.time - lastUpdateTime.GetValueOrDefault(chunk, -10f);
            priority += Mathf.Min(timeSinceUpdate * 0.1f, 5f) * timeSinceUpdateWeight;

            // Factor 4: Resolution bias
            float biasFactor = biasController.SampleBias(chunk.WorldCenter) / 4f; // Normalize to 0-1
            priority += biasFactor * 0.5f; // Minor boost for high-resolution areas

            return priority;
        }

        private void UpdateAgentDensity()
        {
            // Reset agent count map
            agentCountMap.Clear();

            // Count agents per chunk
            foreach (var agent in registeredAgents)
            {
                ChunkData chunk = GetChunkAtPosition(agent.GetPosition());
                if (chunk != null)
                {
                    if (!agentCountMap.ContainsKey(chunk))
                    {
                        agentCountMap[chunk] = 0;
                    }
                    agentCountMap[chunk] += Mathf.CeilToInt(agent.GetImportance());

                    // If chunk is already in queue, update its priority
                    if (dirtyChunks.Contains(chunk) && !chunkQueue.Contains(chunk))
                    {
                        UpdateChunkPriority(chunk);
                    }
                }
            }
        }

        private ChunkData GetChunkAtPosition(Vector3 worldPosition)
        {
            if (!initialized) return null;

            // Convert world position to texture coordinates
            Bounds navBounds = mipmapGenerator.GetNavigationBounds();

            // Check if position is within navigation bounds
            if (!navBounds.Contains(new Vector3(worldPosition.x, navBounds.center.y, worldPosition.z)))
            {
                return null;
            }

            Vector2 relativePos = new Vector2(
                (worldPosition.x - navBounds.min.x) / navBounds.size.x,
                (worldPosition.z - navBounds.min.z) / navBounds.size.z
            );

            int baseWidth = mipmapGenerator.GetBaseWidth();
            int baseHeight = mipmapGenerator.GetBaseHeight();

            Vector2Int pixelPos = new Vector2Int(
                Mathf.FloorToInt(relativePos.x * baseWidth),
                Mathf.FloorToInt(relativePos.y * baseHeight)
            );

            // Find the chunk that contains this pixel
            foreach (var chunk in allChunks)
            {
                Vector2Int chunkMinPixel = chunk.PixelMin;
                Vector2Int chunkMaxPixel = chunk.PixelMin + chunk.PixelSize;

                if (pixelPos.x >= chunkMinPixel.x && pixelPos.x < chunkMaxPixel.x &&
                    pixelPos.y >= chunkMinPixel.y && pixelPos.y < chunkMaxPixel.y)
                {
                    return chunk;
                }
            }

            return null;
        }

        // For debugging visualization
        private void OnDrawGizmos()
        {
            if (!visualizeChunks || !initialized || !Application.isPlaying) return;

            foreach (var chunk in allChunks)
            {
                Vector3 worldCenter = chunk.WorldCenter;
                Bounds navBounds = mipmapGenerator.GetNavigationBounds();
                float chunkWidthWorld = (chunk.PixelSize.x / (float)mipmapGenerator.GetBaseWidth()) * navBounds.size.x;
                float chunkHeightWorld = (chunk.PixelSize.y / (float)mipmapGenerator.GetBaseHeight()) * navBounds.size.z;

                Gizmos.color = Color.white;

                // Use different colors based on chunk state
                if (chunkQueue.Contains(chunk))
                {
                    Gizmos.color = Color.yellow; // In queue
                }
                else if (dirtyChunks.Contains(chunk))
                {
                    Gizmos.color = Color.red; // Dirty
                }
                else
                {
                    Gizmos.color = Color.green; // Processed
                }

                // Draw chunk bounds
                Gizmos.DrawWireCube(worldCenter, new Vector3(chunkWidthWorld, 0.1f, chunkHeightWorld));

                // Show priority as text if in queue
                if (chunkQueue.Contains(chunk))
                {
                    float priority = 0;
                    foreach (var kvp in chunkQueue.GetItems())
                    {
                        if (kvp.Item1.Equals(chunk))
                        {
                            priority = kvp.Item2;
                            break;
                        }
                    }

#if UNITY_EDITOR
                    UnityEditor.Handles.Label(worldCenter + Vector3.up * 0.5f, priority.ToString("F1"));
#endif
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents a chunk of the navigation space for processing
    /// </summary>
    public class ChunkData : IEquatable<ChunkData>
    {
        private readonly Vector2Int pixelMin;
        private readonly Vector2Int pixelSize;
        private readonly Vector3 worldCenter;
        private readonly int hashCode;

        /// <summary>
        /// Minimum pixel coordinates at base resolution
        /// </summary>
        public Vector2Int PixelMin => pixelMin;

        /// <summary>
        /// Size in pixels at base resolution
        /// </summary>
        public Vector2Int PixelSize => pixelSize;

        /// <summary>
        /// World-space center position
        /// </summary>
        public Vector3 WorldCenter => worldCenter;

        /// <summary>
        /// Creates a new chunk data instance
        /// </summary>
        /// <param name="pixelMin">Minimum pixel coordinates at base resolution</param>
        /// <param name="pixelSize">Size in pixels at base resolution</param>
        /// <param name="worldCenter">World-space center position</param>
        public ChunkData(Vector2Int pixelMin, Vector2Int pixelSize, Vector3 worldCenter)
        {
            this.pixelMin = pixelMin;
            this.pixelSize = pixelSize;
            this.worldCenter = worldCenter;

            // Precompute hash code for efficient dictionary and set operations
            unchecked
            {
                hashCode = pixelMin.GetHashCode();
                hashCode = (hashCode * 397) ^ pixelSize.GetHashCode();
            }
        }

        /// <summary>
        /// Gets the minimum texture coordinates at a specific resolution level
        /// </summary>
        /// <param name="level">Resolution level (0 = highest)</param>
        /// <returns>Minimum texture coordinates</returns>
        public Vector2Int GetTextureMin(int level)
        {
            int factor = 1 << level; // 2^level
            return new Vector2Int(pixelMin.x / factor, pixelMin.y / factor);
        }

        /// <summary>
        /// Gets the maximum texture coordinates at a specific resolution level
        /// </summary>
        /// <param name="level">Resolution level (0 = highest)</param>
        /// <returns>Maximum texture coordinates</returns>
        public Vector2Int GetTextureMax(int level)
        {
            int factor = 1 << level; // 2^level
            return new Vector2Int(
                (pixelMin.x + pixelSize.x + factor - 1) / factor,
                (pixelMin.y + pixelSize.y + factor - 1) / factor
            );
        }

        /// <summary>
        /// Gets the size of the chunk at a specific resolution level
        /// </summary>
        /// <param name="level">Resolution level (0 = highest)</param>
        /// <returns>Size in pixels at the specified level</returns>
        public Vector2Int GetTextureSize(int level)
        {
            Vector2Int min = GetTextureMin(level);
            Vector2Int max = GetTextureMax(level);
            return max - min;
        }

        /// <summary>
        /// Returns true if objects are equal
        /// </summary>
        public bool Equals(ChunkData other)
        {
            return pixelMin.Equals(other.pixelMin) && pixelSize.Equals(other.pixelSize);
        }

        /// <summary>
        /// Returns true if objects are equal
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is ChunkData other && Equals(other);
        }

        /// <summary>
        /// Returns a hash code for the object
        /// </summary>
        public override int GetHashCode()
        {
            return hashCode;
        }
    }

    /// <summary>
    /// Simple priority queue implementation for chunk processing
    /// </summary>
    public class PriorityQueue<T>
    {
        private readonly List<(T Item, float Priority)> items = new List<(T, float)>();

        /// <summary>
        /// Gets the number of items in the queue
        /// </summary>
        public int Count => items.Count;

        /// <summary>
        /// Adds an item to the queue with the specified priority
        /// </summary>
        /// <param name="item">Item to add</param>
        /// <param name="priority">Priority value (higher = processed first)</param>
        public void Enqueue(T item, float priority)
        {
            // Check if item already exists in queue
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Item.Equals(item))
                {
                    // Update priority if item already exists
                    items[i] = (item, priority);
                    items.Sort((a, b) => b.Priority.CompareTo(a.Priority)); // Sort in descending order
                    return;
                }
            }

            // Add new item
            items.Add((item, priority));
            items.Sort((a, b) => b.Priority.CompareTo(a.Priority)); // Sort in descending order
        }

        /// <summary>
        /// Removes and returns the highest priority item
        /// </summary>
        /// <returns>The highest priority item</returns>
        public T Dequeue()
        {
            if (items.Count == 0)
                throw new InvalidOperationException("Queue is empty");

            T item = items[0].Item;
            items.RemoveAt(0);
            return item;
        }

        /// <summary>
        /// Checks if an item is in the queue
        /// </summary>
        /// <param name="item">Item to check</param>
        /// <returns>True if the item is in the queue</returns>
        public bool Contains(T item)
        {
            if (item == null) return false;
            return items.Exists(i => i.Item.Equals(item));
        }

        /// <summary>
        /// Gets all items and their priorities (for debugging)
        /// </summary>
        /// <returns>List of items and priorities</returns>
        public List<(T Item, float Priority)> GetItems()
        {
            return new List<(T, float)>(items);
        }

        /// <summary>
        /// Clears all items from the queue
        /// </summary>
        public void Clear()
        {
            items.Clear();
        }
    }
}