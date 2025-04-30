using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MipmapPathfinding
{
    /// <summary>
    /// Efficiently stores and provides access to the multi-resolution vector field data,
    /// with CPU-side caching for rapid agent queries.
    /// </summary>
    public class VectorFieldStorage : MonoBehaviour
    {
        [Header("Required Components")]
        [SerializeField] private MipmapGenerator mipmapGenerator;
        [SerializeField] private MultiResolutionVectorFieldGenerator vectorFieldGenerator;

        [Header("CPU Cache Settings")]
        [SerializeField] private bool enableCPUCache = true;
        [SerializeField] private bool asyncCPUUpdate = true;
        [SerializeField] private float cpuCacheUpdateInterval = 0.5f;
        [Tooltip("If false, will use sparse caching to save memory")]
        [SerializeField] private bool cacheAllResolutionLevels = false;

        // Textures for each resolution level
        private RenderTexture[] vectorFieldTextures;
        
        // CPU-side caches for each resolution level
        private Vector2[][,] cpuCaches;
        
        // Tracking for async updates
        private bool[] cpuCacheDirty;
        private float[] timeSinceLastCacheUpdate;
        private bool isUpdatingCache = false;

        // Resolution and bounds info
        private int[] resolutionWidths;
        private int[] resolutionHeights;
        private Bounds navigationBounds;
        private bool initialized = false;

        // Constants
        private const int MAX_RESOLUTION_LEVELS = 5;

        /// <summary>
        /// Initializes the vector field storage with data from the vector field generator.
        /// </summary>
        public void Initialize()
        {
            if (mipmapGenerator == null)
            {
                Debug.LogError("MipmapGenerator reference not set in VectorFieldStorage");
                return;
            }

            if (vectorFieldGenerator == null)
            {
                Debug.LogError("MultiResolutionVectorFieldGenerator reference not set in VectorFieldStorage");
                return;
            }

            int levelCount = vectorFieldGenerator.GetResolutionLevelCount();
            
            // Initialize arrays
            vectorFieldTextures = new RenderTexture[levelCount];
            cpuCaches = new Vector2[levelCount][,];
            cpuCacheDirty = new bool[levelCount];
            timeSinceLastCacheUpdate = new float[levelCount];
            resolutionWidths = new int[levelCount];
            resolutionHeights = new int[levelCount];

            // Get navigation bounds
            navigationBounds = mipmapGenerator.GetNavigationBounds();

            // Set up data for each resolution level
            for (int level = 0; level < levelCount; level++)
            {
                // Reference to vector field textures
                vectorFieldTextures[level] = vectorFieldGenerator.GetVectorFieldTexture(level);
                
                // Get resolution for this level
                int width = Mathf.CeilToInt(mipmapGenerator.GetBaseWidth() / Mathf.Pow(2, level));
                int height = Mathf.CeilToInt(mipmapGenerator.GetBaseHeight() / Mathf.Pow(2, level));
                
                resolutionWidths[level] = width;
                resolutionHeights[level] = height;
                
                // Initialize CPU cache arrays (if enabled)
                if (enableCPUCache && (level == 0 || cacheAllResolutionLevels))
                {
                    cpuCaches[level] = new Vector2[width, height];
                }
                
                cpuCacheDirty[level] = true;
                timeSinceLastCacheUpdate[level] = cpuCacheUpdateInterval;
            }

            initialized = true;
            
            // Do initial CPU cache update
            if (enableCPUCache)
            {
                UpdateCPUCacheImmediate(0); // Always update base level immediately
                
                if (cacheAllResolutionLevels)
                {
                    for (int level = 1; level < levelCount; level++)
                    {
                        UpdateCPUCacheImmediate(level);
                    }
                }
                
                // Debug dump the cache to see if it contains any data
                Debug.Log("VFS: Performing initial CPU cache dump after initialization");
                DebugDumpCPUCache(0);
            }
        }

        /// <summary>
        /// Updates the CPU-side cache for the specified resolution level.
        /// </summary>
        /// <param name="level">The resolution level to update (0 = highest resolution)</param>
        public void UpdateCPUCacheImmediate(int level)
        {
            if (!initialized || !enableCPUCache || level >= vectorFieldTextures.Length)
                return;
                
            // Skip if this level isn't being cached
            if (level > 0 && !cacheAllResolutionLevels)
                return;
                
            int width = resolutionWidths[level];
            int height = resolutionHeights[level];
            
            // Create CPU cache if it doesn't exist
            if (cpuCaches[level] == null)
            {
                cpuCaches[level] = new Vector2[width, height];
            }
            
            // Ensure the texture exists
            if (vectorFieldTextures[level] == null)
                return;
            
            // Create temporary texture for readback
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = vectorFieldTextures[level];
            
            Texture2D tempTexture = new Texture2D(width, height, TextureFormat.RGFloat, false);
            tempTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tempTexture.Apply();
            
            RenderTexture.active = currentRT;
            
            // Copy data to CPU cache
            Color[] pixels = tempTexture.GetPixels();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (index < pixels.Length)
                    {
                        cpuCaches[level][x, y] = new Vector2(pixels[index].r, pixels[index].g);
                    }
                }
            }
            
            // Clean up
            Destroy(tempTexture);
            
            // Mark as up to date
            cpuCacheDirty[level] = false;
            timeSinceLastCacheUpdate[level] = 0;
        }

        /// <summary>
        /// Starts an asynchronous update of the CPU cache for the specified resolution level.
        /// </summary>
        /// <param name="level">The resolution level to update</param>
        public async void UpdateCPUCacheAsync(int level)
        {
            if (!initialized || !enableCPUCache || isUpdatingCache || level >= vectorFieldTextures.Length)
                return;
                
            // Skip if this level isn't being cached
            if (level > 0 && !cacheAllResolutionLevels)
                return;
            
            isUpdatingCache = true;
            
            await Task.Run(() => PrepareAsyncCacheUpdate(level));
            
            // Finish update on main thread
            FinishCPUCacheUpdate(level);
            
            isUpdatingCache = false;
        }

        /// <summary>
        /// Prepares data for async CPU cache update.
        /// </summary>
        /// <param name="level">The resolution level to update</param>
        private void PrepareAsyncCacheUpdate(int level)
        {
            // This method would typically handle CPU-intensive preparation
            // In a real implementation, you would need to extract texture data
            // on the main thread and prepare it for async processing
        }

        /// <summary>
        /// Finishes CPU cache update with data from async operation.
        /// </summary>
        /// <param name="level">The resolution level that was updated</param>
        private void FinishCPUCacheUpdate(int level)
        {
            // In a real implementation, this would finish the update using
            // data prepared in the async phase
            UpdateCPUCacheImmediate(level);
        }

        /// <summary>
        /// Updates the vector field textures and CPU caches.
        /// </summary>
        public void Update()
        {
            if (!initialized || !enableCPUCache)
                return;
                
            // Only update if we're not already updating
            if (isUpdatingCache)
                return;
                
            for (int level = 0; level < vectorFieldTextures.Length; level++)
            {
                // Skip if this level isn't being cached
                if (level > 0 && !cacheAllResolutionLevels)
                    continue;
                    
                timeSinceLastCacheUpdate[level] += Time.deltaTime;
                
                // Check if it's time to update this level
                if (cpuCacheDirty[level] && timeSinceLastCacheUpdate[level] >= cpuCacheUpdateInterval)
                {
                    if (asyncCPUUpdate)
                    {
                        UpdateCPUCacheAsync(level);
                    }
                    else
                    {
                        UpdateCPUCacheImmediate(level);
                    }
                    
                    // Only update one level per frame to avoid spikes
                    break;
                }
            }
        }

        /// <summary>
        /// Samples the vector field at the specified world position.
        /// </summary>
        /// <param name="worldPosition">The world-space position to sample</param>
        /// <param name="resolutionLevel">The resolution level to sample (0 = highest)</param>
        /// <returns>A normalized direction vector</returns>
        public Vector2 SampleVectorField(Vector3 worldPosition, int resolutionLevel = 0)
        {
            // Check if initialized
            if (!initialized)
            {
                Debug.LogWarning("VFS: Cannot sample vector field - not initialized");
                return Vector2.zero;
            }
            
            // Check if vectorFieldTextures is null
            if (vectorFieldTextures == null)
            {
                Debug.LogError("VFS: Cannot sample vector field - vectorFieldTextures array is null");
                return Vector2.zero;
            }
                
            Debug.Log($"VFS: SampleVectorField called at world position {worldPosition}");
            
            // Try all levels to see if any have data
            for (int level = 0; level < vectorFieldTextures.Length; level++)
            {
                // Check if the texture for this level exists
                if (vectorFieldTextures[level] == null)
                {
                    Debug.LogWarning($"VFS: Texture for level {level} is null, skipping...");
                    continue;
                }
                
                Vector2 result;
                
                // If CPU caching is enabled and this level is cached, use the CPU cache
                if (enableCPUCache && (level == 0 || cacheAllResolutionLevels) && cpuCaches[level] != null)
                {
                    Debug.Log($"VFS: Trying to sample from CPU cache at level {level}");
                    result = SampleCPUCache(worldPosition, level);
                }
                else
                {
                    Debug.Log($"VFS: Trying to sample from GPU texture at level {level}");
                    result = SampleGPUVectorField(worldPosition, level);
                }
                
                // If we found a valid vector, return it
                if (result.sqrMagnitude > 0.01f)
                {
                    Debug.Log($"VFS: Found valid vector {result} at level {level}");
                    return result;
                }
                
                Debug.Log($"VFS: No valid vector found at level {level}");
            }
            
            // If we get here, we didn't find a valid vector at any level
            Debug.LogWarning($"VFS: No valid vector found at ANY level for position {worldPosition}");
            
            // Fall back to the requested level
            resolutionLevel = Mathf.Clamp(resolutionLevel, 0, vectorFieldTextures.Length - 1);
            
            // If CPU caching is enabled and this level is cached, use the CPU cache
            if (enableCPUCache && (resolutionLevel == 0 || cacheAllResolutionLevels) && cpuCaches[resolutionLevel] != null)
            {
                return SampleCPUCache(worldPosition, resolutionLevel);
            }
            else
            {
                // Otherwise, sample directly from the GPU textures (less efficient but works for all levels)
                return SampleGPUVectorField(worldPosition, resolutionLevel);
            }
        }

        /// <summary>
        /// Samples the CPU cache at the specified world position.
        /// </summary>
        /// <param name="worldPosition">The world-space position to sample</param>
        /// <param name="level">The resolution level to sample</param>
        /// <returns>A normalized direction vector</returns>
        private Vector2 SampleCPUCache(Vector3 worldPosition, int level)
        {
            // Check if initialized
            if (!initialized)
            {
                Debug.LogError("VFS: Cannot sample CPU cache - not initialized");
                return Vector2.zero;
            }
            
            // Check if cpuCaches is null
            if (cpuCaches == null)
            {
                Debug.LogError("VFS: Cannot sample CPU cache - cpuCaches array is null");
                return Vector2.zero;
            }
            
            // Check if level is valid
            if (level < 0 || level >= cpuCaches.Length)
            {
                Debug.LogError($"VFS: Cannot sample CPU cache - level {level} out of range (max: {cpuCaches.Length - 1})");
                return Vector2.zero;
            }
            
            // Check if the cache for this level exists
            if (cpuCaches[level] == null)
            {
                Debug.LogError($"VFS: Cannot sample CPU cache - cache for level {level} is null");
                return Vector2.zero;
            }
            
            // Check if resolutionWidths and resolutionHeights are valid
            if (resolutionWidths == null || resolutionHeights == null || 
                level >= resolutionWidths.Length || level >= resolutionHeights.Length)
            {
                Debug.LogError($"VFS: Cannot sample CPU cache - resolution data is invalid for level {level}");
                return Vector2.zero;
            }
            
            // Convert world position to normalized position within bounds
            Vector3 localPos = worldPosition - navigationBounds.min;
            Vector2 normalizedPos = new Vector2(
                localPos.x / navigationBounds.size.x,
                localPos.z / navigationBounds.size.z
            );
            
            // Also try the alternative calculation method to check for differences
            Vector2 altNormalizedPos = new Vector2(
                (worldPosition.x - navigationBounds.min.x) / (navigationBounds.max.x - navigationBounds.min.x),
                (worldPosition.z - navigationBounds.min.z) / (navigationBounds.max.z - navigationBounds.min.z)
            );
            
            Debug.Log($"VFS: Normalized positions - method1: {normalizedPos}, method2: {altNormalizedPos}, diff: {normalizedPos - altNormalizedPos}");
            Debug.Log($"VFS: Bounds - min: {navigationBounds.min}, max: {navigationBounds.max}, size: {navigationBounds.size}");
            
            // Convert to texture coordinates
            float texX = normalizedPos.x * (resolutionWidths[level] - 1);
            float texY = normalizedPos.y * (resolutionHeights[level] - 1);
            
            Debug.Log($"VFS: Texture coords at level {level}: ({texX}, {texY}) in {resolutionWidths[level]}x{resolutionHeights[level]} texture");
            
            // Bilinear interpolation
            int x0 = Mathf.FloorToInt(texX);
            int y0 = Mathf.FloorToInt(texY);
            int x1 = Mathf.Min(x0 + 1, resolutionWidths[level] - 1);
            int y1 = Mathf.Min(y0 + 1, resolutionHeights[level] - 1);
            
            float tx = texX - x0;
            float ty = texY - y0;
            
            // Clamp indices to valid range
            x0 = Mathf.Clamp(x0, 0, resolutionWidths[level] - 1);
            y0 = Mathf.Clamp(y0, 0, resolutionHeights[level] - 1);
            
            // Sample corners
            Vector2 v00 = cpuCaches[level][x0, y0];
            Vector2 v01 = cpuCaches[level][x0, y1];
            Vector2 v10 = cpuCaches[level][x1, y0];
            Vector2 v11 = cpuCaches[level][x1, y1];
            
            Debug.Log($"VFS: Sampled vectors at level {level}:");
            Debug.Log($"VFS:   v00 at ({x0},{y0}): {v00} (mag: {v00.magnitude})");
            Debug.Log($"VFS:   v01 at ({x0},{y1}): {v01} (mag: {v01.magnitude})");
            Debug.Log($"VFS:   v10 at ({x1},{y0}): {v10} (mag: {v10.magnitude})");
            Debug.Log($"VFS:   v11 at ({x1},{y1}): {v11} (mag: {v11.magnitude})");
            
            // Bilinear interpolation
            Vector2 vx0 = Vector2.Lerp(v00, v10, tx);
            Vector2 vx1 = Vector2.Lerp(v01, v11, tx);
            Vector2 result = Vector2.Lerp(vx0, vx1, ty);
            
            Debug.Log($"VFS: Interpolated result: {result} (mag: {result.magnitude})");
            
            // Ensure normalized result
            if (result.sqrMagnitude > 0.0001f)
            {
                return result.normalized;
            }
            else
            {
                return Vector2.zero;
            }
        }

        /// <summary>
        /// Directly samples the GPU vector field at the specified world position.
        /// This is less efficient but works for all resolution levels.
        /// </summary>
        /// <param name="worldPosition">The world-space position to sample</param>
        /// <param name="level">The resolution level to sample</param>
        /// <returns>A normalized direction vector</returns>
        private Vector2 SampleGPUVectorField(Vector3 worldPosition, int level)
        {
            // Check if initialized
            if (!initialized)
            {
                Debug.LogError("VFS: Cannot sample GPU vector field - not initialized");
                return Vector2.zero;
            }
            
            // Check if level is valid
            if (level < 0 || level >= vectorFieldTextures.Length)
            {
                Debug.LogError($"VFS: Cannot sample GPU vector field - level {level} out of range (max: {vectorFieldTextures.Length - 1})");
                return Vector2.zero;
            }
            
            // Check if the texture for this level exists
            if (vectorFieldTextures[level] == null)
            {
                Debug.LogError($"VFS: Cannot sample GPU vector field - texture for level {level} is null");
                return Vector2.zero;
            }
            
            // Check if resolutionWidths and resolutionHeights are valid
            if (resolutionWidths == null || resolutionHeights == null || 
                level >= resolutionWidths.Length || level >= resolutionHeights.Length)
            {
                Debug.LogError($"VFS: Cannot sample GPU vector field - resolution data is invalid for level {level}");
                return Vector2.zero;
            }
            
            // This would typically be implemented using a shader
            // For now, we'll use a simplified approach
            
            // In a real implementation, you'd use a compute shader or Graphics.Blit
            // with a custom shader to sample the texture directly on the GPU
            
            // For this implementation, we'll create a temporary CPU cache and sample from it
            try
            {
                // Check if cpuCaches is null
                if (cpuCaches == null)
                {
                    Debug.LogError("VFS: Cannot sample GPU vector field - cpuCaches array is null");
                    return Vector2.zero;
                }
                
                // Create cache if it doesn't exist
                if (cpuCaches[level] == null)
                {
                    Debug.Log($"VFS: Creating temporary CPU cache for level {level}");
                    cpuCaches[level] = new Vector2[resolutionWidths[level], resolutionHeights[level]];
                    UpdateCPUCacheImmediate(level);
                }
                
                return SampleCPUCache(worldPosition, level);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VFS: Error sampling GPU vector field: {e.Message}");
                return Vector2.zero;
            }
        }

        /// <summary>
        /// Marks a resolution level's CPU cache as dirty, triggering an update.
        /// </summary>
        /// <param name="level">The resolution level to mark as dirty</param>
        public void MarkCacheDirty(int level)
        {
            if (!initialized || level >= cpuCacheDirty.Length)
                return;
                
            cpuCacheDirty[level] = true;
        }

        /// <summary>
        /// Marks all CPU caches as dirty, triggering updates.
        /// </summary>
        public void MarkAllCachesDirty()
        {
            if (!initialized)
                return;
                
            for (int level = 0; level < cpuCacheDirty.Length; level++)
            {
                cpuCacheDirty[level] = true;
            }
        }

        /// <summary>
        /// Gets the texture for a specific resolution level.
        /// </summary>
        /// <param name="level">The resolution level (0 = highest resolution)</param>
        /// <returns>The RenderTexture for the specified level</returns>
        public RenderTexture GetVectorFieldTexture(int level)
        {
            if (!initialized || level < 0 || level >= vectorFieldTextures.Length)
                return null;
                
            return vectorFieldTextures[level];
        }

        /// <summary>
        /// Gets the number of resolution levels available.
        /// </summary>
        /// <returns>The number of resolution levels</returns>
        public int GetResolutionLevelCount()
        {
            if (!initialized)
                return 0;
                
            return vectorFieldTextures.Length;
        }

        /// <summary>
        /// Gets the approximate memory usage of the vector field storage in MB.
        /// </summary>
        /// <returns>Memory usage in MB</returns>
        public float GetMemoryUsageMB()
        {
            if (!initialized)
                return 0f;
                
            float gpuMemory = 0f;
            float cpuMemory = 0f;
            
            // Calculate GPU memory usage
            for (int level = 0; level < vectorFieldTextures.Length; level++)
            {
                if (vectorFieldTextures[level] != null)
                {
                    // RG8 or RG16 texture format - 2 components
                    float textureMB = (resolutionWidths[level] * resolutionHeights[level] * 2) / (1024f * 1024f);
                    gpuMemory += textureMB;
                }
            }
            
            // Calculate CPU memory usage
            for (int level = 0; level < cpuCaches.Length; level++)
            {
                if (cpuCaches[level] != null)
                {
                    // Vector2 = 8 bytes (2 floats)
                    float cacheMB = (resolutionWidths[level] * resolutionHeights[level] * 8) / (1024f * 1024f);
                    cpuMemory += cacheMB;
                }
            }
            
            return gpuMemory + cpuMemory;
        }

        /// <summary>
        /// Sets whether CPU caching is enabled.
        /// </summary>
        /// <param name="enabled">Whether to enable CPU caching</param>
        public void SetCPUCacheEnabled(bool enabled)
        {
            if (enableCPUCache == enabled)
                return;
                
            enableCPUCache = enabled;
            
            if (enabled && initialized)
            {
                // Initialize CPU caches if they were disabled
                UpdateCPUCacheImmediate(0);
                
                if (cacheAllResolutionLevels)
                {
                    for (int level = 1; level < vectorFieldTextures.Length; level++)
                    {
                        UpdateCPUCacheImmediate(level);
                    }
                }
            }
        }

        /// <summary>
        /// Sets whether to cache all resolution levels or just the base level.
        /// </summary>
        /// <param name="cacheAll">Whether to cache all levels</param>
        public void SetCacheAllLevels(bool cacheAll)
        {
            if (cacheAllResolutionLevels == cacheAll)
                return;
                
            cacheAllResolutionLevels = cacheAll;
            
            if (enableCPUCache && initialized && cacheAll)
            {
                // Initialize CPU caches for additional levels
                for (int level = 1; level < vectorFieldTextures.Length; level++)
                {
                    if (cpuCaches[level] == null)
                    {
                        cpuCaches[level] = new Vector2[resolutionWidths[level], resolutionHeights[level]];
                    }
                    UpdateCPUCacheImmediate(level);
                }
            }
        }
        
        /// <summary>
        /// Force caching of all resolution levels and dump their contents
        /// </summary>
        public void ForceUpdateAndDumpAllCaches()
        {
            Debug.Log("VFS: Forcing update of all CPU caches...");
            
            // Check if initialized, and if not, try to initialize
            if (!initialized)
            {
                Debug.LogWarning("VFS: Not initialized yet, attempting to initialize first...");
                Initialize();
                
                // If still not initialized after trying, return safely
                if (!initialized)
                {
                    Debug.LogError("VFS: Cannot force update caches - initialization failed!");
                    return;
                }
            }
            
            // Check if vectorFieldTextures is null
            if (vectorFieldTextures == null)
            {
                Debug.LogError("VFS: Cannot force update caches - vectorFieldTextures is null!");
                return;
            }
            
            // Enable caching all levels
            bool originalCacheAllSetting = cacheAllResolutionLevels;
            cacheAllResolutionLevels = true;
            
            // Update all caches
            for (int level = 0; level < vectorFieldTextures.Length; level++)
            {
                Debug.Log($"VFS: Forcing update of level {level}...");
                
                // Check if the texture for this level exists
                if (vectorFieldTextures[level] == null)
                {
                    Debug.LogWarning($"VFS: Texture for level {level} is null, skipping...");
                    continue;
                }
                
                // Create cache if it doesn't exist
                if (cpuCaches[level] == null)
                {
                    cpuCaches[level] = new Vector2[resolutionWidths[level], resolutionHeights[level]];
                }
                
                // Force update
                UpdateCPUCacheImmediate(level);
                
                // Dump cache contents
                DebugDumpCPUCache(level);
            }
            
            // Restore original setting
            cacheAllResolutionLevels = originalCacheAllSetting;
            
            Debug.Log("VFS: Finished forcing update of all CPU caches");
        }
        
        /// <summary>
        /// Debug method to dump information about the CPU cache contents
        /// </summary>
        /// <param name="level">The resolution level to check</param>
        public void DebugDumpCPUCache(int level = 0)
        {
            // Check if initialized
            if (!initialized)
            {
                Debug.LogError($"VFS: Cannot dump CPU cache for level {level} - not initialized");
                return;
            }
            
            // Check if cpuCaches is null
            if (cpuCaches == null)
            {
                Debug.LogError($"VFS: Cannot dump CPU cache for level {level} - cpuCaches array is null");
                return;
            }
            
            // Check if level is valid
            if (level >= cpuCaches.Length)
            {
                Debug.LogError($"VFS: Cannot dump CPU cache for level {level} - level out of range (max: {cpuCaches.Length - 1})");
                return;
            }
            
            // Check if the cache for this level exists
            if (cpuCaches[level] == null)
            {
                Debug.LogError($"VFS: Cannot dump CPU cache for level {level} - cache is null");
                return;
            }
            
            // Check if resolutionWidths and resolutionHeights are valid
            if (resolutionWidths == null || resolutionHeights == null || 
                level >= resolutionWidths.Length || level >= resolutionHeights.Length)
            {
                Debug.LogError($"VFS: Cannot dump CPU cache for level {level} - resolution data is invalid");
                return;
            }
            
            int nonZeroCount = 0;
            Vector2 strongestVector = Vector2.zero;
            float strongestMagnitude = 0;
            Vector2Int strongestPos = Vector2Int.zero;
            
            // Check entire cache for data
            for (int y = 0; y < resolutionHeights[level]; y++)
            {
                for (int x = 0; x < resolutionWidths[level]; x++)
                {
                    Vector2 v = cpuCaches[level][x, y];
                    float mag = v.magnitude;
                    if (mag > 0.01f)
                    {
                        nonZeroCount++;
                        if (mag > strongestMagnitude)
                        {
                            strongestMagnitude = mag;
                            strongestVector = v;
                            strongestPos = new Vector2Int(x, y);
                        }
                    }
                }
            }
            
            Debug.Log($"VFS: CPU Cache level {level} ({resolutionWidths[level]}x{resolutionHeights[level]}) scan complete");
            
            if (nonZeroCount > 0)
            {
                // Convert strongest position back to world space for testing
                Vector2 normalizedPos = new Vector2(
                    (float)strongestPos.x / (resolutionWidths[level] - 1),
                    (float)strongestPos.y / (resolutionHeights[level] - 1)
                );
                
                Vector3 worldPos = new Vector3(
                    navigationBounds.min.x + normalizedPos.x * navigationBounds.size.x,
                    0,
                    navigationBounds.min.z + normalizedPos.y * navigationBounds.size.z
                );
                
                Debug.Log($"VFS: CPU Cache level {level}: {nonZeroCount} non-zero vectors found!");
                Debug.Log($"VFS: Strongest vector: {strongestVector} (mag: {strongestMagnitude}) at texture pos {strongestPos}");
                Debug.Log($"VFS: This corresponds to world position: {worldPos}");
                
                // Also check a few random non-zero positions
                int samplesChecked = 0;
                Debug.Log($"VFS: Sampling a few random non-zero vectors:");
                
                for (int y = 0; y < resolutionHeights[level] && samplesChecked < 5; y += resolutionHeights[level] / 10)
                {
                    for (int x = 0; x < resolutionWidths[level] && samplesChecked < 5; x += resolutionWidths[level] / 10)
                    {
                        Vector2 v = cpuCaches[level][x, y];
                        if (v.magnitude > 0.01f)
                        {
                            // Convert to world position
                            Vector2 sampleNormPos = new Vector2(
                                (float)x / (resolutionWidths[level] - 1),
                                (float)y / (resolutionHeights[level] - 1)
                            );
                            
                            Vector3 sampleWorldPos = new Vector3(
                                navigationBounds.min.x + sampleNormPos.x * navigationBounds.size.x,
                                0,
                                navigationBounds.min.z + sampleNormPos.y * navigationBounds.size.z
                            );
                            
                            Debug.Log($"VFS: Sample {samplesChecked+1}: Vector {v} at texture pos ({x},{y}), world pos {sampleWorldPos}");
                            samplesChecked++;
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning($"VFS: CPU Cache level {level}: NO DATA FOUND in the entire cache!");
                
                // Check if the texture has data even if the cache doesn't
                if (vectorFieldTextures[level] != null && vectorFieldTextures[level].IsCreated())
                {
                    Debug.Log($"VFS: Checking if texture has data even though cache is empty...");
                    
                    // Create temporary texture for readback
                    RenderTexture currentRT = RenderTexture.active;
                    RenderTexture.active = vectorFieldTextures[level];
                    
                    // Just check a small portion to save memory
                    int checkSize = Mathf.Min(512, Mathf.Min(resolutionWidths[level], resolutionHeights[level]));
                    int startX = (resolutionWidths[level] - checkSize) / 2;
                    int startY = (resolutionHeights[level] - checkSize) / 2;
                    
                    Texture2D tempTexture = new Texture2D(checkSize, checkSize, TextureFormat.RGFloat, false);
                    tempTexture.ReadPixels(new Rect(startX, startY, checkSize, checkSize), 0, 0);
                    tempTexture.Apply();
                    
                    RenderTexture.active = currentRT;
                    
                    // Check for non-zero data
                    Color[] pixels = tempTexture.GetPixels();
                    bool hasData = false;
                    
                    foreach (Color pixel in pixels)
                    {
                        Vector2 v = new Vector2(pixel.r, pixel.g);
                        if (v.magnitude > 0.01f)
                        {
                            hasData = true;
                            Debug.Log($"VFS: Found non-zero vector in texture: {v}");
                            break;
                        }
                    }
                    
                    if (!hasData)
                    {
                        Debug.LogError($"VFS: Texture also has NO DATA in checked region!");
                    }
                    else
                    {
                        Debug.LogWarning($"VFS: Texture has data but cache is empty! Cache update may have failed.");
                    }
                    
                    // Clean up
                    Destroy(tempTexture);
                }
            }
        }
    }
}
