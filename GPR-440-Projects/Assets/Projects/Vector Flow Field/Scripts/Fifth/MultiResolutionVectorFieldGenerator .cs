using UnityEngine;
using System.Collections.Generic;

namespace MipmapPathfinding
{
    public class MultiResolutionVectorFieldGenerator : MonoBehaviour
    {
        [Header("Required References")]
        [SerializeField] private MipmapGenerator mipmapGenerator;
        [SerializeField] private ResolutionBiasController biasController;

        [Header("Algorithm Parameters")]
        [SerializeField] private ComputeShader propagationShader;
        [SerializeField] private int propagationStagesPerLevel = 2;
        [SerializeField] private float falloffRate = 0.95f;
        [SerializeField] private float targetRadius = 1.0f;

        [Header("Resolution Transition Settings")]
        [SerializeField] private bool interpolateBetweenLevels = true;
        [SerializeField] private float interpolationBlendFactor = 0.5f;

        // Vector field output textures (one per resolution level)
        private RenderTexture[] vectorFieldTextures;

        // Internal state
        private Bounds navigationBounds;
        private bool isInitialized = false;

        // Buffer for currently processing chunks
        private List<ChunkData> activeChunks = new List<ChunkData>();
        
        private void Awake()
        {
            // Auto-initialize in Start() to ensure MipmapGenerator and ResolutionBiasController are initialized first
        }
        
        private void Start()
        {
            // Initialize after other components have had a chance to initialize
            Initialize();
        }

        /// <summary>
        /// Initializes the vector field generator and creates textures for each resolution level.
        /// </summary>
        public void Initialize()
        {
            Debug.Log("MultiResolutionVectorFieldGenerator: Initializing...");
            
            // Check if already initialized
            if (isInitialized)
            {
                Debug.Log("MultiResolutionVectorFieldGenerator: Already initialized, skipping initialization");
                return;
            }
            
            if (mipmapGenerator == null || biasController == null)
            {
                Debug.LogError("MultiResolutionVectorFieldGenerator: Missing required references!");
                return;
            }

            // Ensure MipmapGenerator is initialized first
            RenderTexture baseLevel = mipmapGenerator.GetMipmapLevel(0);
            if (baseLevel == null)
            {
                Debug.Log("MultiResolutionVectorFieldGenerator: Initializing MipmapGenerator first");
                mipmapGenerator.Initialize();
                
                // Check again after initialization
                baseLevel = mipmapGenerator.GetMipmapLevel(0);
                if (baseLevel == null)
                {
                    Debug.LogError("MultiResolutionVectorFieldGenerator: MipmapGenerator failed to initialize properly!");
                    return;
                }
            }
            
            // Ensure ResolutionBiasController is initialized
            if (biasController.BiasTexture == null)
            {
                Debug.Log("MultiResolutionVectorFieldGenerator: Initializing ResolutionBiasController");
                biasController.Initialize();
                
                // Check again after initialization
                if (biasController.BiasTexture == null)
                {
                    Debug.LogError("MultiResolutionVectorFieldGenerator: ResolutionBiasController failed to initialize properly!");
                    return;
                }
            }

            // Initialize vector field textures (one per resolution level)
            int levelCount = mipmapGenerator.GetMipmapLevelCount();
            vectorFieldTextures = new RenderTexture[levelCount];

            navigationBounds = mipmapGenerator.GetNavigationBounds();
            Debug.Log($"MultiResolutionVectorFieldGenerator: Using navigation bounds: {navigationBounds.min} to {navigationBounds.max}");

            // Create vector field textures for each resolution level
            for (int level = 0; level < levelCount; level++)
            {
                // Get dimensions for this level
                int width = mipmapGenerator.GetBaseWidth() >> level;
                int height = mipmapGenerator.GetBaseHeight() >> level;

                // Create vector field texture (RG16 format for 2D direction vectors)
                vectorFieldTextures[level] = new RenderTexture(width, height, 0, RenderTextureFormat.RG16);
                vectorFieldTextures[level].name = $"VectorField_Level{level}";
                vectorFieldTextures[level].enableRandomWrite = true;
                vectorFieldTextures[level].Create();

                Debug.Log($"MultiResolutionVectorFieldGenerator: Created vector field texture for level {level}: {width}x{height}");
                
                // Verify navigation texture content for this level
                mipmapGenerator.VerifyNavigationTextureContent(level);

                // Clear to default values (no direction)
                ClearVectorField(level);
            }

            isInitialized = true;
            Debug.Log("MultiResolutionVectorFieldGenerator initialized with " + levelCount + " resolution levels");
        }

        /// <summary>
        /// Clears a vector field texture to default values (no direction).
        /// </summary>
        private void ClearVectorField(int level)
        {
            if (propagationShader == null)
            {
                Debug.LogError("MultiResolutionVectorFieldGenerator: Missing propagation shader!");
                return;
            }

            // Set up shader for clearing the texture
            int kernelIndex = propagationShader.FindKernel("ClearVectorField");
            propagationShader.SetTexture(kernelIndex, "_VectorFieldTexture", vectorFieldTextures[level]);

            // Get dimensions for this level
            int width = vectorFieldTextures[level].width;
            int height = vectorFieldTextures[level].height;

            // Dispatch shader (8×8 thread groups)
            int threadGroupsX = Mathf.CeilToInt(width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(height / 8.0f);
            propagationShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);
        }

        /// <summary>
        /// Sets the target positions that agents will navigate towards.
        /// </summary>
        /// <param name="targetPositions">Array of world-space positions representing navigation targets</param>
        public void SetTargets(Vector3[] targetPositions)
        {
            Debug.Log($"Setting {targetPositions.Length} targets. First target: {(targetPositions.Length > 0 ? targetPositions[0].ToString() : "none")}");

            if (!isInitialized)
            {
                Debug.LogWarning("MultiResolutionVectorFieldGenerator: Not initialized!");
                return;
            }

            if (targetPositions == null || targetPositions.Length == 0)
            {
                Debug.LogWarning("MultiResolutionVectorFieldGenerator: No targets provided!");
                return;
            }

            // Clear all vector fields first
            for (int level = 0; level < vectorFieldTextures.Length; level++)
            {
                ClearVectorField(level);
            }

            // Set targets at each resolution level
            for (int level = 0; level < vectorFieldTextures.Length; level++)
            {
                SetTargetsForLevel(targetPositions, level);
            }

            Debug.Log("Set " + targetPositions.Length + " targets for vector field generation");

            for (int level = 0; level < vectorFieldTextures.Length; level++)
            {
                VerifyVectorFieldContent(level);
            }

            Debug.Log("===== VECTOR FIELD AFTER TARGET SETTING =====");
            for (int level = 0; level < vectorFieldTextures.Length; level++)
            {
                // Choose middle of texture to sample (or where your dummy agent is)
                int width = vectorFieldTextures[level].width;
                int height = vectorFieldTextures[level].height;
                int centerX = width / 2;
                int centerY = height / 2;

                // Could also use your dummy agent position
                // Vector3 agentPos = [your dummy agent position];
                // Vector2 uv = new Vector2(
                //    (agentPos.x - navigationBounds.min.x) / (navigationBounds.max.x - navigationBounds.min.x),
                //    (agentPos.z - navigationBounds.min.z) / (navigationBounds.max.z - navigationBounds.min.z)
                // );
                // int centerX = Mathf.FloorToInt(uv.x * width);
                // int centerY = Mathf.FloorToInt(uv.y * height);

                RenderTexture.active = vectorFieldTextures[level];
                Texture2D readback = new Texture2D(10, 10, TextureFormat.RGFloat, false);
                readback.ReadPixels(new Rect(centerX - 5, centerY - 5, 10, 10), 0, 0);
                readback.Apply();

                Color[] pixels = readback.GetPixels();
                bool hasData = false;
                foreach (Color c in pixels)
                {
                    if (Mathf.Abs(c.r) > 0.01f || Mathf.Abs(c.g) > 0.01f)
                    {
                        Debug.Log($"Level {level} has vector data ({c.r}, {c.g})");
                        hasData = true;
                        break;
                    }
                }

                if (!hasData)
                    Debug.LogError($"Level {level} has NO VECTOR DATA after target setting!");

                Destroy(readback);
                RenderTexture.active = null;
            }

        }

        private void VerifyVectorFieldContent(int level)
        {
            RenderTexture vfTexture = vectorFieldTextures[level];
            RenderTexture.active = vfTexture;
            Texture2D readback = new Texture2D(4, 4, TextureFormat.RGFloat, false);

            // Sample center of texture
            int centerX = vfTexture.width / 2;
            int centerY = vfTexture.height / 2;
            readback.ReadPixels(new Rect(centerX - 2, centerY - 2, 4, 4), 0, 0);
            readback.Apply();

            // Check for non-zero vectors
            Color[] pixels = readback.GetPixels();
            bool hasData = false;
            foreach (Color c in pixels)
            {
                if (Mathf.Abs(c.r) > 0.01f || Mathf.Abs(c.g) > 0.01f)
                {
                    Debug.Log($"Vector field at level {level} has data: ({c.r}, {c.g})");
                    hasData = true;
                    break;
                }
            }

            if (!hasData)
                Debug.LogWarning($"Vector field at level {level} has NO DATA in center region");

            Destroy(readback);
            RenderTexture.active = null;
        }


        /// <summary>
        /// Sets targets for a specific resolution level.
        /// </summary>
        private void SetTargetsForLevel(Vector3[] targetPositions, int level)
        {
            Debug.Log($"===== SETTING TARGETS FOR LEVEL {level} =====");
            
            // Check target positions and radius in pixel space
            foreach (var pos in targetPositions)
            {
                // Convert to UV space
                float u = (pos.x - navigationBounds.min.x) / (navigationBounds.max.x - navigationBounds.min.x);
                float v = (pos.z - navigationBounds.min.z) / (navigationBounds.max.z - navigationBounds.min.z);
                
                // Convert to pixel coordinates for this level
                int pixelX = Mathf.FloorToInt(u * vectorFieldTextures[level].width);
                int pixelY = Mathf.FloorToInt(v * vectorFieldTextures[level].height);
                
                // Calculate radius in pixels
                float radiusInPixels = targetRadius / (navigationBounds.max.x - navigationBounds.min.x) * 
                                      vectorFieldTextures[level].width;
                
                Debug.Log($"Target at world ({pos.x}, {pos.z}) maps to pixel ({pixelX}, {pixelY}) " +
                         $"with radius {radiusInPixels} pixels at level {level}");
                
                // If radius is very small (< 1 pixel), the target won't affect anything
                if (radiusInPixels < 1)
                    Debug.LogError($"TARGET RADIUS TOO SMALL: {radiusInPixels} pixels at level {level}");
            }
            
            // Create a temporary compute buffer for the target positions
            ComputeBuffer targetBuffer = new ComputeBuffer(targetPositions.Length, sizeof(float) * 3);
            targetBuffer.SetData(targetPositions);

            try
            {
                // Set up shader for target setting
                int kernelIndex = propagationShader.FindKernel("SetTargets");
                Debug.Log($"SetTargets kernel index: {kernelIndex}");
                
                // Try getting kernel indices for all shaders to verify they exist
                try {
                    int clearKernel = propagationShader.FindKernel("ClearVectorField");
                    int setTargetsKernel = propagationShader.FindKernel("SetTargets");
                    int propagateKernel = propagationShader.FindKernel("PropagateVectorField");
                    int transitionsKernel = propagationShader.FindKernel("ApplyResolutionTransitions");
                    
                    Debug.Log($"Kernel indices - Clear: {clearKernel}, SetTargets: {setTargetsKernel}, " +
                             $"Propagate: {propagateKernel}, Transitions: {transitionsKernel}");
                }
                catch (System.Exception e) {
                    Debug.LogError($"Error finding kernels: {e.Message}");
                }
                
                propagationShader.SetBuffer(kernelIndex, "_TargetPositions", targetBuffer);
                propagationShader.SetInt("_TargetCount", targetPositions.Length);
                propagationShader.SetTexture(kernelIndex, "_VectorFieldTexture", vectorFieldTextures[level]);
                
                // Add debug logging for vector field texture
                Debug.Log($"Vector field texture format: {vectorFieldTextures[level].format}, " +
                         $"size: {vectorFieldTextures[level].width}x{vectorFieldTextures[level].height}, " +
                         $"IsCreated: {vectorFieldTextures[level].IsCreated()}");
                
                // Get and set the navigation texture
                RenderTexture navTexture = mipmapGenerator.GetMipmapLevel(level);
                if (navTexture != null)
                {
                    Debug.Log($"Navigation texture at level {level}: format={navTexture.format}, " +
                             $"size={navTexture.width}x{navTexture.height}, IsCreated={navTexture.IsCreated()}");
                    
                    // Test world<->texture coordinate conversion by checking corners
                    Vector3[] testPositions = new Vector3[]
                    {
                        new Vector3(navigationBounds.min.x, 0, navigationBounds.min.z),  // Bottom-left
                        new Vector3(navigationBounds.max.x, 0, navigationBounds.min.z),  // Bottom-right 
                        new Vector3(navigationBounds.min.x, 0, navigationBounds.max.z),  // Top-left
                        new Vector3(navigationBounds.max.x, 0, navigationBounds.max.z),  // Top-right
                        new Vector3(navigationBounds.center.x, 0, navigationBounds.center.z)  // Center
                    };

                    string[] posNames = { "Bottom-left", "Bottom-right", "Top-left", "Top-right", "Center" };

                    for (int i = 0; i < testPositions.Length; i++)
                    {
                        Vector3 worldPos = testPositions[i];
                        float u = (worldPos.x - navigationBounds.min.x) / (navigationBounds.max.x - navigationBounds.min.x);
                        float v = (worldPos.z - navigationBounds.min.z) / (navigationBounds.max.z - navigationBounds.min.z);
                        
                        int texX = Mathf.FloorToInt(u * vectorFieldTextures[level].width);
                        int texY = Mathf.FloorToInt(v * vectorFieldTextures[level].height);
                        
                        Debug.Log($"{posNames[i]} world ({worldPos.x}, {worldPos.z}) maps to texture ({texX}, {texY})");
                    }
                    
                    // Verify navigation texture content
                    mipmapGenerator.VerifyNavigationTextureContent(level);
                    
                    propagationShader.SetTexture(kernelIndex, "_NavigationTexture", navTexture);
                }
                else
                {
                    Debug.LogError($"Navigation texture at level {level} is null!");
                    return;
                }
                
                // Try a much larger target radius temporarily
                float originalRadius = targetRadius;
                float tempRadius = 50.0f; // Try a much larger value temporarily
                Debug.Log($"Using INCREASED target radius: {tempRadius} (was {originalRadius})");
                propagationShader.SetFloat("_TargetRadius", tempRadius);
                
                // Don't override the increased radius!
                // propagationShader.SetFloat("_TargetRadius", targetRadius);

                // Set bounds parameters
                propagationShader.SetVector("_BoundsMin", new Vector4(navigationBounds.min.x, navigationBounds.min.y, navigationBounds.min.z, 0));
                propagationShader.SetVector("_BoundsMax", new Vector4(navigationBounds.max.x, navigationBounds.max.y, navigationBounds.max.z, 0));

                // Get dimensions for this level
                int width = vectorFieldTextures[level].width;
                int height = vectorFieldTextures[level].height;

                // In SetTargetsForLevel just before dispatch
                Debug.Log($"Setting targets for level {level}. Target radius: {targetRadius}");
                Debug.Log($"BoundsMin: {navigationBounds.min}, BoundsMax: {navigationBounds.max}");
                Debug.Log($"First target position: {(targetPositions.Length > 0 ? targetPositions[0].ToString() : "none")}");

                // Dispatch shader (8×8 thread groups)
                int threadGroupsX = Mathf.CeilToInt(width / 8.0f);
                int threadGroupsY = Mathf.CeilToInt(height / 8.0f);
                propagationShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);

                // Add at the end of SetTargetsForLevel, after the dispatch
                Debug.Log($"SetTargets kernel executed for level {level}");

                // Verify if any targets were actually set
                RenderTexture vfTexture = vectorFieldTextures[level];
                bool foundAnyVector = false;

                // Check a wide radius around each target
                foreach (var pos in targetPositions)
                {
                    // Convert to texture coordinates
                    Vector2 uv = new Vector2(
                        (pos.x - navigationBounds.min.x) / (navigationBounds.max.x - navigationBounds.min.x),
                        (pos.z - navigationBounds.min.z) / (navigationBounds.max.z - navigationBounds.min.z)
                    );

                    // Convert UV to pixel coordinates
                    int centerX = Mathf.FloorToInt(uv.x * vfTexture.width);
                    int centerY = Mathf.FloorToInt(uv.y * vfTexture.height);

                    // Check in target radius
                    int pixelRadius = Mathf.CeilToInt(targetRadius * vfTexture.width /
                                                      (navigationBounds.max.x - navigationBounds.min.x));

                    RenderTexture.active = vfTexture;
                    Texture2D readback = new Texture2D(pixelRadius * 2 + 1, pixelRadius * 2 + 1, TextureFormat.RGFloat, false);
                    readback.ReadPixels(new Rect(centerX - pixelRadius, centerY - pixelRadius,
                                                 pixelRadius * 2 + 1, pixelRadius * 2 + 1), 0, 0);
                    readback.Apply();

                    // Check if any vectors exist
                    Color[] pixels = readback.GetPixels();
                    foreach (Color c in pixels)
                    {
                        if (Mathf.Abs(c.r) > 0.01f || Mathf.Abs(c.g) > 0.01f)
                        {
                            foundAnyVector = true;
                            Debug.Log($"Found vector near target: ({c.r}, {c.g})");
                            break;
                        }
                    }

                    Destroy(readback);

                    if (foundAnyVector)
                        break;
                }

                RenderTexture.active = null;
                if (!foundAnyVector)
                    Debug.LogError($"NO VECTORS found around ANY targets in level {level}!");
            }
            finally
            {
                // Release the buffer
                targetBuffer.Release();
            }
        }

        /// <summary>
        /// Processes vector field generation for a specific chunk of the navigation space.
        /// </summary>
        /// <param name="chunk">A ChunkData object representing the chunk to process</param>
        public void ProcessChunk(ChunkData chunk)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("MultiResolutionVectorFieldGenerator: Not initialized!");
                return;
            }

            // Add to active chunks if not already processing
            if (!activeChunks.Contains(chunk))
            {
                activeChunks.Add(chunk);
            }

            // Process the chunk using a coarse-to-fine approach
            ProcessChunkCoarseToFine(chunk);
        }

        /// <summary>
        /// Performs coarse-to-fine multi-resolution processing for a chunk.
        /// </summary>
        private void ProcessChunkCoarseToFine(ChunkData chunk)
        {
            // Determine the lowest resolution level to start with
            int startLevel = DetermineStartLevel(chunk);

            // Process from lowest resolution (highest level number) to highest resolution
            for (int level = startLevel; level >= 0; level--)
            {
                // Skip levels based on bias (if the area doesn't need this resolution)
                if (ShouldProcessLevelForChunk(chunk, level))
                {
                    // Execute propagation stages for this level
                    for (int stage = 0; stage < propagationStagesPerLevel; stage++)
                    {
                        ExecutePropagationStage(chunk, level, stage);
                    }
                }
            }

            // Handle resolution transitions for smooth blending
            if (interpolateBetweenLevels)
            {
                ApplyResolutionTransitions(chunk);
            }

            // Add verification for the chunk's area
            Debug.Log($"Verifying data for chunk at {chunk.WorldCenter}");
            for (int level = 0; level < vectorFieldTextures.Length; level++)
            {
                Debug.Log($"chunk at {chunk.WorldCenter}, at level {level}");
                // Get center coordinates of this chunk
                Vector2Int chunkMin = chunk.GetTextureMin(level);
                Vector2Int chunkSize = chunk.GetTextureMax(level) - chunkMin;
                Vector2Int chunkCenter = chunkMin + chunkSize / 2;

                // Verify this specific location
                VerifyVectorFieldLocation(level, chunkCenter.x, chunkCenter.y);
            }

            // Remove from active chunks when done
            activeChunks.Remove(chunk);
        }

        private void VerifyVectorFieldLocation(int level, int centerX, int centerY)
        {
            RenderTexture vfTexture = vectorFieldTextures[level];
            RenderTexture.active = vfTexture;
            Texture2D readback = new Texture2D(1, 1, TextureFormat.RGFloat, false);

            // Check exact position
            readback.ReadPixels(new Rect(centerX, centerY, 1, 1), 0, 0);
            readback.Apply();

            Color pixel = readback.GetPixel(0, 0);
            Vector2 direction = new Vector2(pixel.r, pixel.g);

            Debug.Log($"Vector at level {level}, position ({centerX},{centerY}): {direction}");

            Destroy(readback);
            RenderTexture.active = null;
        }

        /// <summary>
        /// Determines the appropriate starting resolution level based on chunk properties.
        /// </summary>
        private int DetermineStartLevel(ChunkData chunk)
        {
            // Sample bias at chunk center
            Vector3 chunkCenter = chunk.WorldCenter;
            float bias = biasController.SampleBias(chunkCenter);

            // Convert bias to level (inverse relationship - higher bias means lower starting level)
            int levelCount = mipmapGenerator.GetMipmapLevelCount();
            int startLevel = Mathf.Clamp(levelCount - 1 - Mathf.FloorToInt(bias), 0, levelCount - 1);

            return startLevel;
        }

        /// <summary>
        /// Checks if a specific resolution level should be processed for this chunk.
        /// </summary>
        private bool ShouldProcessLevelForChunk(ChunkData chunk, int level)
        {
            //// Sample bias at chunk center
            //Vector3 chunkCenter = chunk.WorldCenter;
            //float bias = biasController.SampleBias(chunkCenter);

            //// Calculate bias threshold for this level
            //float biasThreshold = (float)(mipmapGenerator.GetMipmapLevelCount() - 1 - level);

            //// Process this level if bias is greater than threshold
            //return bias >= biasThreshold - 0.5f;
            return true;
        }

        /// <summary>
        /// Executes a single propagation stage for the specified chunk and level.
        /// </summary>
        private void ExecutePropagationStage(ChunkData chunk, int level, int stage)
        {
            // Find the kernel index
            int kernelIndex = propagationShader.FindKernel("PropagateVectorField");
            Debug.Log($"PropagateVectorField kernel index: {kernelIndex} for level {level}, stage {stage}");

            // Get the navigation texture
            RenderTexture navTexture = mipmapGenerator.GetMipmapLevel(level);
            if (navTexture == null)
            {
                Debug.LogError($"Navigation texture at level {level} is null in ExecutePropagationStage!");
                return;
            }
            
            Debug.Log($"Navigation texture in PropagateVectorField: format={navTexture.format}, " +
                     $"size={navTexture.width}x{navTexture.height}, IsCreated={navTexture.IsCreated()}");

            // Set up parameters for propagation shader
            propagationShader.SetTexture(kernelIndex, "_NavigationTexture", navTexture);
            propagationShader.SetTexture(kernelIndex, "_VectorFieldTexture", vectorFieldTextures[level]);
            propagationShader.SetFloat("_FalloffRate", falloffRate);
            propagationShader.SetInt("_PropagationStage", stage);

            // IMPORTANT CHANGE: Process the entire texture instead of just the chunk
            // Get dimensions for this level
            int width = vectorFieldTextures[level].width;
            int height = vectorFieldTextures[level].height;
            
            // Set chunk parameters to cover the entire texture
            Vector2Int fullMin = new Vector2Int(0, 0);
            Vector2Int fullSize = new Vector2Int(width, height);
            
            propagationShader.SetInts("_ChunkMin", new int[] { fullMin.x, fullMin.y });
            propagationShader.SetInts("_ChunkSize", new int[] { fullSize.x, fullSize.y });
            
            Debug.Log($"Processing ENTIRE TEXTURE: Size={fullSize}, Level={level}");

            // Set navigation bounds
            propagationShader.SetVector("_BoundsMin", new Vector4(navigationBounds.min.x, navigationBounds.min.y, navigationBounds.min.z, 0));
            propagationShader.SetVector("_BoundsMax", new Vector4(navigationBounds.max.x, navigationBounds.max.y, navigationBounds.max.z, 0));

            // Dispatch shader (8×8 thread groups) for the entire texture
            int threadGroupsX = Mathf.CeilToInt(width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(height / 8.0f);
            
            Debug.Log($"Dispatching PropagateVectorField with thread groups: {threadGroupsX}x{threadGroupsY} (FULL TEXTURE)");
            propagationShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);
            
            // Add debug verification for the last propagation stage
            if (stage == propagationStagesPerLevel - 1)
            {
                // Sample a few pixels to check if we're getting non-zero data
                RenderTexture.active = vectorFieldTextures[level];
                Texture2D debugReadback = new Texture2D(4, 4, TextureFormat.RGFloat, false);
                
                // Read from the center of the texture
                int centerX = width / 2;
                int centerY = height / 2;
                
                debugReadback.ReadPixels(new Rect(centerX - 2, centerY - 2, 4, 4), 0, 0);
                debugReadback.Apply();
                
                Color[] pixels = debugReadback.GetPixels();
                bool hasData = false;
                foreach (Color c in pixels)
                {
                    if (Mathf.Abs(c.r) > 0.01f || Mathf.Abs(c.g) > 0.01f)
                    {
                        hasData = true;
                        break;
                    }
                }
                
                Debug.Log($"Vector field at level {level}, center: {(hasData ? "HAS DATA" : "NO DATA")}");
                Destroy(debugReadback);
                
                // Also check multiple points across the texture
                int numSamples = 5;
                for (int i = 0; i < numSamples; i++)
                {
                    for (int j = 0; j < numSamples; j++)
                    {
                        int sampleX = (i * width) / numSamples;
                        int sampleY = (j * height) / numSamples;
                        
                        RenderTexture.active = vectorFieldTextures[level];
                        Texture2D sampleReadback = new Texture2D(1, 1, TextureFormat.RGFloat, false);
                        sampleReadback.ReadPixels(new Rect(sampleX, sampleY, 1, 1), 0, 0);
                        sampleReadback.Apply();
                        
                        Color samplePixel = sampleReadback.GetPixel(0, 0);
                        Vector2 direction = new Vector2(samplePixel.r, samplePixel.g);
                        
                        if (direction.magnitude > 0.01f)
                        {
                            Debug.Log($"Found vector at ({sampleX}, {sampleY}): {direction}");
                        }
                        
                        Destroy(sampleReadback);
                    }
                }
            }
        }

        /// <summary>
        /// Applies smooth transitions between resolution levels.
        /// </summary>
        private void ApplyResolutionTransitions(ChunkData chunk)
        {
            // Find the kernel index
            int kernelIndex = propagationShader.FindKernel("ApplyResolutionTransitions");
            Debug.Log($"ApplyResolutionTransitions kernel index: {kernelIndex}");

            for (int level = 0; level < vectorFieldTextures.Length - 1; level++)
            {
                // Skip if we don't need this level of detail for this chunk
                if (!ShouldProcessLevelForChunk(chunk, level))
                {
                    Debug.Log($"Skipping level {level} for chunk at {chunk.WorldCenter} (bias threshold not met)");
                    continue;
                }

                Debug.Log($"Applying resolution transitions for level {level}");

                // Set up shader parameters
                propagationShader.SetTexture(kernelIndex, "_VectorFieldCurrent", vectorFieldTextures[level]);
                propagationShader.SetTexture(kernelIndex, "_VectorFieldLower", vectorFieldTextures[level + 1]);
                
                // Debug vector field textures
                Debug.Log($"Vector field current (level {level}): format={vectorFieldTextures[level].format}, " +
                         $"size={vectorFieldTextures[level].width}x{vectorFieldTextures[level].height}");
                Debug.Log($"Vector field lower (level {level+1}): format={vectorFieldTextures[level+1].format}, " +
                         $"size={vectorFieldTextures[level+1].width}x{vectorFieldTextures[level+1].height}");
                
                // Set bias texture
                RenderTexture biasTexture = biasController.BiasTexture;
                if (biasTexture != null)
                {
                    Debug.Log($"Bias texture: format={biasTexture.format}, " +
                             $"size={biasTexture.width}x{biasTexture.height}, IsCreated={biasTexture.IsCreated()}");
                    propagationShader.SetTexture(kernelIndex, "_BiasTexture", biasTexture);
                }
                else
                {
                    Debug.LogError("Bias texture is null!");
                    continue;
                }
                
                // Add the navigation texture
                RenderTexture navTexture = mipmapGenerator.GetMipmapLevel(level);
                if (navTexture != null)
                {
                    Debug.Log($"Navigation texture at level {level}: format={navTexture.format}, " +
                             $"size={navTexture.width}x{navTexture.height}, IsCreated={navTexture.IsCreated()}");
                    propagationShader.SetTexture(kernelIndex, "_NavigationTexture", navTexture);
                }
                else
                {
                    Debug.LogError($"Navigation texture at level {level} is null in ApplyResolutionTransitions!");
                    continue;
                }
                
                propagationShader.SetFloat("_BlendFactor", interpolationBlendFactor);
                Debug.Log($"Blend factor: {interpolationBlendFactor}");

                // IMPORTANT CHANGE: Process the entire texture instead of just the chunk
                // Get dimensions for this level
                int width = vectorFieldTextures[level].width;
                int height = vectorFieldTextures[level].height;
                
                // Set chunk parameters to cover the entire texture
                Vector2Int fullMin = new Vector2Int(0, 0);
                Vector2Int fullSize = new Vector2Int(width, height);
                
                propagationShader.SetInts("_ChunkMin", new int[] { fullMin.x, fullMin.y });
                propagationShader.SetInts("_ChunkSize", new int[] { fullSize.x, fullSize.y });
                
                Debug.Log($"Processing ENTIRE TEXTURE for transitions: Size={fullSize}, Level={level}");

                // Set navigation bounds
                propagationShader.SetVector("_BoundsMin", new Vector4(navigationBounds.min.x, navigationBounds.min.y, navigationBounds.min.z, 0));
                propagationShader.SetVector("_BoundsMax", new Vector4(navigationBounds.max.x, navigationBounds.max.y, navigationBounds.max.z, 0));

                // Dispatch shader (8×8 thread groups) for the entire texture
                int threadGroupsX = Mathf.CeilToInt(width / 8.0f);
                int threadGroupsY = Mathf.CeilToInt(height / 8.0f);
                
                Debug.Log($"Dispatching ApplyResolutionTransitions with thread groups: {threadGroupsX}x{threadGroupsY} (FULL TEXTURE)");
                propagationShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);
                
                // Verify results
                RenderTexture.active = vectorFieldTextures[level];
                Texture2D debugReadback = new Texture2D(4, 4, TextureFormat.RGFloat, false);
                
                // Read from the center of the texture
                int centerX = width / 2;
                int centerY = height / 2;
                
                debugReadback.ReadPixels(new Rect(centerX - 2, centerY - 2, 4, 4), 0, 0);
                debugReadback.Apply();
                
                Color[] pixels = debugReadback.GetPixels();
                bool hasData = false;
                foreach (Color c in pixels)
                {
                    if (Mathf.Abs(c.r) > 0.01f || Mathf.Abs(c.g) > 0.01f)
                    {
                        hasData = true;
                        break;
                    }
                }
                
                Debug.Log($"After transitions, vector field at level {level}, center: {(hasData ? "HAS DATA" : "NO DATA")}");
                Destroy(debugReadback);
                
                // Also check multiple points across the texture
                int numSamples = 5;
                for (int i = 0; i < numSamples; i++)
                {
                    for (int j = 0; j < numSamples; j++)
                    {
                        int sampleX = (i * width) / numSamples;
                        int sampleY = (j * height) / numSamples;
                        
                        RenderTexture.active = vectorFieldTextures[level];
                        Texture2D sampleReadback = new Texture2D(1, 1, TextureFormat.RGFloat, false);
                        sampleReadback.ReadPixels(new Rect(sampleX, sampleY, 1, 1), 0, 0);
                        sampleReadback.Apply();
                        
                        Color samplePixel = sampleReadback.GetPixel(0, 0);
                        Vector2 direction = new Vector2(samplePixel.r, samplePixel.g);
                        
                        if (direction.magnitude > 0.01f)
                        {
                            Debug.Log($"After transitions, found vector at ({sampleX}, {sampleY}): {direction}");
                        }
                        
                        Destroy(sampleReadback);
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves the vector field texture for a specific resolution level.
        /// </summary>
        /// <param name="level">The resolution level to retrieve (0 = highest resolution)</param>
        /// <returns>The RenderTexture containing the vector field for the specified level</returns>
        public RenderTexture GetVectorFieldTexture(int level)
        {
            if (!isInitialized || level < 0 || level >= vectorFieldTextures.Length)
            {
                return null;
            }

            return vectorFieldTextures[level];
        }

        /// <summary>
        /// Returns the number of resolution levels available.
        /// </summary>
        /// <returns>The number of resolution levels</returns>
        public int GetResolutionLevelCount()
        {
            return vectorFieldTextures?.Length ?? 0;
        }

        /// <summary>
        /// Samples the vector field at a specific world position.
        /// </summary>
        /// <param name="worldPosition">The world-space position to sample</param>
        /// <param name="useHighestResolution">If true, always uses the highest resolution; otherwise uses bias to determine level</param>
        /// <returns>A normalized Vector2 representing the flow direction</returns>
        public Vector2 SampleVectorField(Vector3 worldPosition, bool useHighestResolution = false)
        {
            if (!isInitialized)
                return Vector2.zero;

            Debug.Log($"===== SAMPLING VECTOR FIELD AT {worldPosition} =====");
            
            // Try all levels from highest to lowest resolution
            for (int testLevel = 0; testLevel < vectorFieldTextures.Length; testLevel++)
            {
                // Determine which resolution level to use based on bias
                int level = testLevel;
                
                if (!useHighestResolution && testLevel == 0)
                {
                    float bias = biasController.SampleBias(worldPosition);
                    level = Mathf.Clamp(
                        Mathf.FloorToInt(vectorFieldTextures.Length - 1 - bias),
                        0,
                        vectorFieldTextures.Length - 1
                    );
                    Debug.Log($"Bias at position: {bias}, selected level: {level}");
                }
                else if (testLevel > 0)
                {
                    // When testing additional levels, use the current test level
                    level = testLevel;
                    Debug.Log($"Testing additional level: {level}");
                }

                // Convert world position to texture coordinates
                Vector2 normalizedPos = new Vector2(
                    (worldPosition.x - navigationBounds.min.x) / (navigationBounds.max.x - navigationBounds.min.x),
                    (worldPosition.z - navigationBounds.min.z) / (navigationBounds.max.z - navigationBounds.min.z)
                );
                
                // Clamp to valid range
                normalizedPos.x = Mathf.Clamp01(normalizedPos.x);
                normalizedPos.y = Mathf.Clamp01(normalizedPos.y);
                
                // Get the vector field texture for this level
                RenderTexture vectorFieldTexture = vectorFieldTextures[level];
                if (vectorFieldTexture == null)
                {
                    Debug.LogError($"Vector field texture at level {level} is null!");
                    continue;
                }
                
                // Calculate pixel coordinates
                int pixelX = Mathf.FloorToInt(normalizedPos.x * (vectorFieldTexture.width - 1));
                int pixelY = Mathf.FloorToInt(normalizedPos.y * (vectorFieldTexture.height - 1));
                
                Debug.Log($"Level {level}: Normalized position: {normalizedPos}, Pixel coordinates: ({pixelX}, {pixelY})");
                
                // Sample a 3x3 area around the target pixel
                int sampleRadius = 1;
                RenderTexture.active = vectorFieldTexture;
                Texture2D tempTexture = new Texture2D(sampleRadius * 2 + 1, sampleRadius * 2 + 1, TextureFormat.RGFloat, false);
                
                // Ensure we don't read outside the texture bounds
                int readX = Mathf.Max(0, pixelX - sampleRadius);
                int readY = Mathf.Max(0, pixelY - sampleRadius);
                int readWidth = Mathf.Min(vectorFieldTexture.width - readX, sampleRadius * 2 + 1);
                int readHeight = Mathf.Min(vectorFieldTexture.height - readY, sampleRadius * 2 + 1);
                
                tempTexture.ReadPixels(new Rect(readX, readY, readWidth, readHeight), 0, 0);
                tempTexture.Apply();
                
                // Find the strongest vector in the sampled area
                Vector2 strongestDirection = Vector2.zero;
                float strongestMagnitude = 0.01f; // Minimum threshold
                
                for (int y = 0; y < readHeight; y++)
                {
                    for (int x = 0; x < readWidth; x++)
                    {
                        Color pixel = tempTexture.GetPixel(x, y);
                        Vector2 direction = new Vector2(pixel.r, pixel.g);
                        float magnitude = direction.magnitude;
                        
                        if (magnitude > strongestMagnitude)
                        {
                            strongestDirection = direction;
                            strongestMagnitude = magnitude;
                            Debug.Log($"Found stronger vector at offset ({x}, {y}): {direction} (mag: {magnitude})");
                        }
                    }
                }
                
                // Clean up
                Destroy(tempTexture);
                RenderTexture.active = null;
                
                // If we found a valid vector, return it
                if (strongestMagnitude > 0.01f)
                {
                    Debug.Log($"Returning vector: {strongestDirection.normalized} from level {level}");
                    return strongestDirection.normalized;
                }
                
                Debug.Log($"No valid vector found at level {level}, trying next level...");
            }
            
            Debug.LogWarning($"No valid vector found at ANY level for position {worldPosition}!");
            return Vector2.zero;
        }

        /// <summary>
        /// Diagnostic method to check if there's any data in the vector field textures
        /// </summary>
        public void DiagnoseVectorFieldTextures()
        {
            if (!isInitialized || vectorFieldTextures == null)
            {
                Debug.LogError("MultiResolutionVectorFieldGenerator: Not initialized!");
                return;
            }
            
            Debug.Log("===== VECTOR FIELD TEXTURE DIAGNOSIS =====");
            
            for (int level = 0; level < vectorFieldTextures.Length; level++)
            {
                RenderTexture texture = vectorFieldTextures[level];
                
                if (texture == null || !texture.IsCreated())
                {
                    Debug.LogError($"Level {level}: Texture is null or not created!");
                    continue;
                }
                
                Debug.Log($"Level {level}: Texture size = {texture.width}x{texture.height}, format = {texture.format}");
                
                // Create a temporary texture for readback
                RenderTexture.active = texture;
                
                // Sample multiple regions of the texture
                int[] regionsToCheck = { 10, 25, 50, 75, 90 }; // Percentages
                bool foundAnyData = false;
                
                foreach (int percent in regionsToCheck)
                {
                    int x = texture.width * percent / 100;
                    int y = texture.height * percent / 100;
                    
                    // Sample a small region around this point
                    int sampleSize = 10;
                    Texture2D readback = new Texture2D(sampleSize, sampleSize, TextureFormat.RGFloat, false);
                    readback.ReadPixels(new Rect(x - sampleSize/2, y - sampleSize/2, sampleSize, sampleSize), 0, 0);
                    readback.Apply();
                    
                    // Check for non-zero vectors
                    Color[] pixels = readback.GetPixels();
                    bool hasData = false;
                    Vector2 strongestVector = Vector2.zero;
                    float strongestMagnitude = 0;
                    
                    foreach (Color pixel in pixels)
                    {
                        Vector2 v = new Vector2(pixel.r, pixel.g);
                        float mag = v.magnitude;
                        
                        if (mag > 0.01f)
                        {
                            hasData = true;
                            foundAnyData = true;
                            
                            if (mag > strongestMagnitude)
                            {
                                strongestMagnitude = mag;
                                strongestVector = v;
                            }
                        }
                    }
                    
                    if (hasData)
                    {
                        Debug.Log($"Level {level}, Region {percent}%: Found data! Strongest vector: {strongestVector} (mag: {strongestMagnitude})");
                        
                        // Convert to world position for testing
                        Vector2 normalizedPos = new Vector2(
                            (float)x / texture.width,
                            (float)y / texture.height
                        );
                        
                        Vector3 worldPos = new Vector3(
                            navigationBounds.min.x + normalizedPos.x * (navigationBounds.max.x - navigationBounds.min.x),
                            0,
                            navigationBounds.min.z + normalizedPos.y * (navigationBounds.max.z - navigationBounds.min.z)
                        );
                        
                        Debug.Log($"  This corresponds to approximately world position: {worldPos}");
                    }
                    else
                    {
                        Debug.Log($"Level {level}, Region {percent}%: No data found");
                    }
                    
                    Destroy(readback);
                }
                
                if (!foundAnyData)
                {
                    Debug.LogWarning($"Level {level}: NO DATA FOUND in any sampled region!");
                }
            }
            
            RenderTexture.active = null;
            Debug.Log("===== END OF VECTOR FIELD TEXTURE DIAGNOSIS =====");
        }
        
        /// <summary>
        /// Cleans up resources when the component is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            if (vectorFieldTextures != null)
            {
                foreach (var texture in vectorFieldTextures)
                {
                    if (texture != null)
                    {
                        texture.Release();
                    }
                }
            }
        }
    }

    // VectorFieldChunkData struct removed as it's no longer needed
    // The system now uses ChunkData from ChunkedProcessingSystem.cs
}
