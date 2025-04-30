using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace MipmapPathfinding
{
    /// <summary>
    /// Generates multiple resolution levels of navigation textures from Unity's NavMesh
    /// </summary>
    public class MipmapGenerator : MonoBehaviour
    {
        [Header("Input Settings")]
        [SerializeField] private ComputeShader boundsCalculatorShader;
        
        [Header("Output Configuration")]
        [SerializeField] private bool useRenderTextureArray = true;
        [Tooltip("If false, will create separate textures for each level")]
        
        [Header("Resolution Settings")]
        [SerializeField] private int baseWidth = 12000; 
        [SerializeField] private int baseHeight = 6000;
        [SerializeField] private int mipmapLevels = 5;
        
        [Header("NavMesh Settings")]
        [SerializeField] private float navMeshSampleDistance = 0.1f;
        [SerializeField] private int navMeshAreaMask = NavMesh.AllAreas;
        
        [Header("Debug")]
        [Tooltip("Full resolution base texture")]
        [SerializeField] private Texture2D debugBaseTexture;
        [Tooltip("Half resolution (Level 1)")]
        [SerializeField] private Texture2D debugLevel1Texture;
        [Tooltip("Quarter resolution (Level 2)")]
        [SerializeField] private Texture2D debugLevel2Texture;
        [Tooltip("Eighth resolution (Level 3)")]
        [SerializeField] private Texture2D debugLevel3Texture;
        [Tooltip("Sixteenth resolution (Level 4)")]
        [SerializeField] private Texture2D debugLevel4Texture;
        
        // Flag to track initialization state
        private bool isInitialized = false;
        
        private void Awake()
        {
            // Auto-initialize on Awake
            Initialize();
        }
        
    // Output storage
    private RenderTexture[] mipmapTextures;
    private RenderTexture mipmapArray;
    
    // Cache for mipmap levels to prevent recreation
    private Dictionary<int, RenderTexture> cachedMipmapLevels = new Dictionary<int, RenderTexture>();
    
    // Cached data for shader parameters
    private ComputeBuffer vertexBuffer;
    private ComputeBuffer triangleBuffer;
    private int generateTextureKernel;
    private Bounds navigationBounds;
        
        /// <summary>
        /// Initialize and generate the mipmap chain
        /// </summary>
        public void Initialize()
        {
            Debug.Log("MipmapGenerator: Initializing...");
            
            // Check if already initialized
            if (isInitialized)
            {
                Debug.Log("MipmapGenerator: Already initialized, skipping initialization");
                return;
            }
            
            if (boundsCalculatorShader == null)
            {
                Debug.LogError("MipmapGenerator: BoundsCalculator shader not assigned!");
                return;
            }
            
            // Extract NavMesh data
            if (!ExtractNavMeshData())
            {
                Debug.LogError("MipmapGenerator: Failed to extract NavMesh data!");
                return;
            }
            
            // Create storage for mipmaps
            CreateMipmapStorage();
            
            // Generate all mipmap levels
            GenerateAllLevels();
            
            // Create debug textures
            CreateDebugTextures();
            
            // Verify that mipmap level 0 is valid
            RenderTexture level0 = GetMipmapLevel(0);
            if (level0 == null || !level0.IsCreated())
            {
                Debug.LogError("MipmapGenerator: Failed to create base mipmap level!");
                return;
            }
            
            // Mark as initialized
            isInitialized = true;
            
            Debug.Log($"MipmapGenerator: Successfully generated {mipmapLevels} mipmap levels");
        }

        /// <summary>
        /// Extract triangle data from Unity's NavMesh
        /// </summary>
        private bool ExtractNavMeshData()
        {
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

            // In ExtractNavMeshData, after getting triangulation:
            Debug.Log($"NavMesh triangulation: {triangulation.vertices.Length} vertices, {triangulation.indices.Length} indices");
            // Log a sample of the vertices to verify they're valid
            if (triangulation.vertices.Length > 0)
            {
                Debug.Log($"Sample vertex: {triangulation.vertices[0]}");
            }

            if (triangulation.vertices == null || triangulation.indices == null || 
                triangulation.vertices.Length == 0 || triangulation.indices.Length == 0)
            {
                Debug.LogError("MipmapGenerator: NavMesh triangulation data is empty!");
                return false;
            }
            
            // Create and fill vertex buffer
            vertexBuffer = new ComputeBuffer(triangulation.vertices.Length, sizeof(float) * 3);
            Vector3[] vertices = triangulation.vertices;
            vertexBuffer.SetData(vertices);
            
            // Create and fill triangle buffer
            triangleBuffer = new ComputeBuffer(triangulation.indices.Length, sizeof(int));
            triangleBuffer.SetData(triangulation.indices);
            
            // Calculate bounds
            CalculateNavMeshBounds(vertices);
            
            // Get the kernel ID
            generateTextureKernel = boundsCalculatorShader.FindKernel("GenerateTexture");
            
            return true;
        }
        
        /// <summary>
        /// Calculate the bounds of the NavMesh
        /// </summary>
        private void CalculateNavMeshBounds(Vector3[] vertices)
        {
            if (vertices.Length == 0)
            {
                navigationBounds = new Bounds(Vector3.zero, new Vector3(10, 10, 10));
                return;
            }
            
            Vector3 min = vertices[0];
            Vector3 max = vertices[0];
            
            for (int i = 1; i < vertices.Length; i++)
            {
                // Update min bounds
                if (vertices[i].x < min.x) min.x = vertices[i].x;
                if (vertices[i].y < min.y) min.y = vertices[i].y;
                if (vertices[i].z < min.z) min.z = vertices[i].z;
                
                // Update max bounds
                if (vertices[i].x > max.x) max.x = vertices[i].x;
                if (vertices[i].y > max.y) max.y = vertices[i].y;
                if (vertices[i].z > max.z) max.z = vertices[i].z;
            }
            
            // Add a small padding to avoid edge cases
            Vector3 padding = new Vector3(0.5f, 0.5f, 0.5f);
            min -= padding;
            max += padding;
            
            // Create bounds
            navigationBounds = new Bounds();
            navigationBounds.SetMinMax(min, max);
            
            Debug.Log($"NavMesh bounds: {navigationBounds.min} to {navigationBounds.max}, size: {navigationBounds.size}");
        }
        
        /// <summary>
        /// Create storage for all mipmap levels
        /// </summary>
        private void CreateMipmapStorage()
        {
            // Cleanup existing textures if they exist
            CleanupTextures();
            
            if (useRenderTextureArray)
            {
                // Create a single RenderTexture with mipmap chain
                mipmapArray = new RenderTexture(baseWidth, baseHeight, 0, RenderTextureFormat.R8);
                mipmapArray.name = "Navigation_MipmapArray";
                mipmapArray.enableRandomWrite = true;
                mipmapArray.useMipMap = true;
                mipmapArray.autoGenerateMips = false; // We'll generate them ourselves
                mipmapArray.Create();
            }
            else
            {
                // Create separate textures for each level
                mipmapTextures = new RenderTexture[mipmapLevels];
                int width = baseWidth;
                int height = baseHeight;
                
                for (int i = 0; i < mipmapLevels; i++)
                {
                    mipmapTextures[i] = new RenderTexture(width, height, 0, RenderTextureFormat.R8);
                    mipmapTextures[i].name = $"Navigation_Level{i}";
                    mipmapTextures[i].enableRandomWrite = true;
                    mipmapTextures[i].Create();
                    
                    width /= 2;
                    height /= 2;
                }
            }
        }
        
        /// <summary>
        /// Generate all mipmap levels
        /// </summary>
        private void GenerateAllLevels()
        {
            // Generate base level
            GenerateBaseLevel();
            
            // Generate remaining levels using conservative downsampling
            GenerateDownsampledLevels();
            
            // Clean up temporary buffers
            CleanupShaderData();
        }
        
        /// <summary>
        /// Generate the base (highest resolution) level
        /// </summary>
        private void GenerateBaseLevel()
        {
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            
            // Set shader parameters
            boundsCalculatorShader.SetBuffer(generateTextureKernel, "Verticies", vertexBuffer);
            boundsCalculatorShader.SetBuffer(generateTextureKernel, "Triangles", triangleBuffer);
            boundsCalculatorShader.SetInt("VertexCount", triangulation.vertices.Length);
            boundsCalculatorShader.SetInt("TriangleCount", triangulation.indices.Length / 3);
            boundsCalculatorShader.SetVector("boundsMin", new Vector4(navigationBounds.min.x, navigationBounds.min.z, 0, 0));
            boundsCalculatorShader.SetVector("boundsMax", new Vector4(navigationBounds.max.x, navigationBounds.max.z, 0, 0));
            boundsCalculatorShader.SetInts("TextureSize", new int[] { baseWidth, baseHeight });
            
            // Set target texture
            RenderTexture targetTexture = useRenderTextureArray ? mipmapArray : mipmapTextures[0];
            boundsCalculatorShader.SetTexture(generateTextureKernel, "Output", targetTexture);
            
            // Calculate dispatch size
            int threadGroupsX = Mathf.CeilToInt(baseWidth / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(baseHeight / 8.0f);

            // Add before dispatching the shader:
            Debug.Log($"Dispatching with bounds: min={navigationBounds.min}, max={navigationBounds.max}");
            Debug.Log($"Triangle count: {triangulation.indices.Length / 3}");

            // Dispatch the compute shader
            boundsCalculatorShader.Dispatch(generateTextureKernel, threadGroupsX, threadGroupsY, 1);
            
            Debug.Log($"Generated base level: {baseWidth}x{baseHeight}");

            // Add a test pattern to the navigation texture
            AddTestPatternToNavigationTexture();

            // After GenerateBaseLevel:
            RenderTexture.active = targetTexture;
            // Create a test pattern at the center
            Texture2D tempTex = new Texture2D(100, 100, TextureFormat.R8, false);
            for (int i = 0; i < 100; i++)
            {
                for (int j = 0; j < 100; j++)
                {
                    tempTex.SetPixel(i, j, Color.white);
                }
            }
            tempTex.Apply();
            Graphics.Blit(tempTex, targetTexture);
            Debug.Log("Added test pattern to navigation texture");
        }

        /// <summary>
        /// Generate all downsampled mipmap levels using conservative downsampling
        /// </summary>
        private void GenerateDownsampledLevels()
        {
            if (useRenderTextureArray)
            {
                // Using built-in mipmap chain
                GenerateMipmapChain();
            }
            else
            {
                // Using separate textures
                GenerateSeparateTextures();
            }
        }
        
        /// <summary>
        /// Generate mipmaps for the mipmap chain texture
        /// </summary>
        private void GenerateMipmapChain()
        {
            // Create a material for conservative downsampling
            Material conservativeDownsample = new Material(Shader.Find("Hidden/ConservativeDownsample"));
            
            // Create a temporary render texture
            int width = baseWidth / 2;
            int height = baseHeight / 2;
            
            for (int level = 1; level < mipmapLevels; level++)
            {
                RenderTexture tempRT = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.R8);
                tempRT.enableRandomWrite = true;
                tempRT.Create();
                
                // Set the source level
                conservativeDownsample.SetTexture("_MainTex", mipmapArray);
                conservativeDownsample.SetFloat("_Level", level - 1);
                
                // Execute conservative downsampling
                Graphics.Blit(null, tempRT, conservativeDownsample);
                
                // Copy to appropriate mip level
                Graphics.CopyTexture(tempRT, 0, 0, mipmapArray, 0, level);
                
                // Release temp texture
                RenderTexture.ReleaseTemporary(tempRT);
                
                width /= 2;
                height /= 2;
                
                Debug.Log($"Generated mipmap level {level}: {width*2}x{height*2}");
            }
            
            // Destroy the material
            Destroy(conservativeDownsample);
        }
        
        /// <summary>
        /// Generate separate textures for each mipmap level
        /// </summary>
        private void GenerateSeparateTextures()
        {
            // Create a material for conservative downsampling
            Material conservativeDownsample = new Material(Shader.Find("Hidden/ConservativeDownsample"));
            
            for (int level = 1; level < mipmapLevels; level++)
            {
                // Set the source texture (previous level)
                conservativeDownsample.SetTexture("_MainTex", mipmapTextures[level-1]);
                
                // Execute conservative downsampling
                Graphics.Blit(null, mipmapTextures[level], conservativeDownsample);
                
                Debug.Log($"Generated separate texture level {level}: {mipmapTextures[level].width}x{mipmapTextures[level].height}");
            }
            
            // Destroy the material
            Destroy(conservativeDownsample);
        }
        
        /// <summary>
        /// Create debug textures for inspector visualization
        /// </summary>
        private void CreateDebugTextures()
        {
            // Clean up any existing debug textures
            CleanupDebugTextures();
            
            // Create readable textures for visualization
            // Note: For large textures, consider creating smaller visualization versions
            
            // Determine the size to use for debug textures (scaled down from full size)
            int debugWidth = Mathf.Min(baseWidth, 1024);
            int debugHeight = Mathf.Min(baseHeight, 1024);
            float scaleRatio = (float)debugWidth / baseWidth;
            
            // Create a temporary RT for readback
            RenderTexture tempRT = RenderTexture.GetTemporary(debugWidth, debugHeight, 0, RenderTextureFormat.R8);
            
            // Create debug textures for each level (or up to 5 levels)
            int numLevelsToDebug = Mathf.Min(mipmapLevels, 5);
            
            for (int level = 0; level < numLevelsToDebug; level++)
            {
                // Get the texture for this level
                RenderTexture levelTexture = GetMipmapLevel(level);
                
                // Calculate size for this level's debug texture
                int levelDebugWidth = Mathf.Max(8, Mathf.RoundToInt(debugWidth / Mathf.Pow(2, level)));
                int levelDebugHeight = Mathf.Max(8, Mathf.RoundToInt(debugHeight / Mathf.Pow(2, level)));
                
                // Create debug texture
                Texture2D debugTexture = new Texture2D(levelDebugWidth, levelDebugHeight, TextureFormat.R8, false);
                debugTexture.name = $"Debug_NavMesh_Level{level}";
                
                // Blit the level texture to temp RT
                Graphics.Blit(levelTexture, tempRT);
                
                // Activate tempRT for reading
                RenderTexture prevRT = RenderTexture.active;
                RenderTexture.active = tempRT;
                
                // Read pixels
                debugTexture.ReadPixels(new Rect(0, 0, levelDebugWidth, levelDebugHeight), 0, 0);
                debugTexture.Apply();
                
                // Restore previous RT
                RenderTexture.active = prevRT;
                
                // Assign to appropriate debug field
                switch (level)
                {
                    case 0: debugBaseTexture = debugTexture; break;
                    case 1: debugLevel1Texture = debugTexture; break;
                    case 2: debugLevel2Texture = debugTexture; break;
                    case 3: debugLevel3Texture = debugTexture; break;
                    case 4: debugLevel4Texture = debugTexture; break;
                }
                
                // Release temp level texture if needed
                if (level > 0 && useRenderTextureArray)
                {
                    RenderTexture.ReleaseTemporary(levelTexture);
                }
            }
            
            // Release temp RT
            RenderTexture.ReleaseTemporary(tempRT);
        }
        
        /// <summary>
        /// Get a specific mipmap level texture
        /// </summary>
        public RenderTexture GetMipmapLevel(int level)
        {
            if (level < 0 || level >= mipmapLevels)
            {
                Debug.LogError($"MipmapGenerator: Invalid mipmap level requested: {level}");
                return null;
            }

            // Auto-initialize if needed
            if ((useRenderTextureArray && mipmapArray == null) ||
                (!useRenderTextureArray && (mipmapTextures == null || mipmapTextures[0] == null)))
            {
                Debug.LogWarning("MipmapGenerator: GetMipmapLevel called before initialization. Auto-initializing now.");
                Initialize();

                // Check again after initialization
                if ((useRenderTextureArray && mipmapArray == null) ||
                    (!useRenderTextureArray && (mipmapTextures == null || mipmapTextures[0] == null)))
                {
                    Debug.LogError("MipmapGenerator: Failed to initialize textures!");
                    return null;
                }
            }


            // For level 0 or when using separate textures, return directly
            if (level == 0 || !useRenderTextureArray)
            {
                return useRenderTextureArray ? mipmapArray : mipmapTextures[level];
            }
            
            // For other levels with mipmap array, check cache first
            if (cachedMipmapLevels.TryGetValue(level, out RenderTexture cachedTexture))
            {
                if (cachedTexture != null && cachedTexture.IsCreated())
                {
                    Debug.Log($"Using cached mipmap level {level}");
                    return cachedTexture;
                }
                else
                {
                    // Remove invalid cache entry
                    cachedMipmapLevels.Remove(level);
                    Debug.LogWarning($"Removed invalid cached texture for level {level}");
                }
            }
            
            // Create a new texture for this level
            int width = baseWidth >> level;
            int height = baseHeight >> level;
            RenderTexture levelTexture = new RenderTexture(width, height, 0, RenderTextureFormat.R8);
            levelTexture.name = $"Navigation_Level{level}_Cached";
            levelTexture.enableRandomWrite = true;
            levelTexture.Create();
            
            Debug.Log($"Created new cached texture for level {level}: {width}x{height}");
            
            // Copy from the appropriate mip level
            Graphics.CopyTexture(mipmapArray, 0, level, levelTexture, 0, 0);
            
            // Cache the texture
            cachedMipmapLevels[level] = levelTexture;
            
            return levelTexture;
        }
        
        /// <summary>
        /// Get the number of mipmap levels available
        /// </summary>
        public int GetMipmapLevelCount()
        {
            return mipmapLevels;
        }
        
        /// <summary>
        /// Get the navigation bounds used for the mipmap generation
        /// </summary>
        public Bounds GetNavigationBounds()
        {
            return navigationBounds;
        }
        
        /// <summary>
        /// Get the base width of the full resolution texture
        /// </summary>
        public int GetBaseWidth()
        {
            return baseWidth;
        }
        
        /// <summary>
        /// Get the base height of the full resolution texture
        /// </summary>
        public int GetBaseHeight()
        {
            return baseHeight;
        }
        
        /// <summary>
        /// Check if the MipmapGenerator has been initialized
        /// </summary>
        /// <returns>True if initialized, false otherwise</returns>
        public bool IsInitialized()
        {
            return isInitialized;
        }
        
        /// <summary>
        /// Clean up all created textures
        /// </summary>
        private void CleanupTextures()
        {
            if (mipmapArray != null)
            {
                mipmapArray.Release();
                Destroy(mipmapArray);
                mipmapArray = null;
            }
            
            if (mipmapTextures != null)
            {
                foreach (var texture in mipmapTextures)
                {
                    if (texture != null)
                    {
                        texture.Release();
                        Destroy(texture);
                    }
                }
                mipmapTextures = null;
            }
        }
        
        /// <summary>
        /// Clean up compute buffers
        /// </summary>
        private void CleanupShaderData()
        {
            if (vertexBuffer != null)
            {
                vertexBuffer.Release();
                vertexBuffer = null;
            }
            
            if (triangleBuffer != null)
            {
                triangleBuffer.Release();
                triangleBuffer = null;
            }
        }
        
        /// <summary>
        /// Clean up debug textures
        /// </summary>
        private void CleanupDebugTextures()
        {
            if (debugBaseTexture != null) Destroy(debugBaseTexture);
            if (debugLevel1Texture != null) Destroy(debugLevel1Texture);
            if (debugLevel2Texture != null) Destroy(debugLevel2Texture);
            if (debugLevel3Texture != null) Destroy(debugLevel3Texture);
            if (debugLevel4Texture != null) Destroy(debugLevel4Texture);
            
            debugBaseTexture = null;
            debugLevel1Texture = null;
            debugLevel2Texture = null;
            debugLevel3Texture = null;
            debugLevel4Texture = null;
        }
        
        /// <summary>
        /// Verifies the content of a navigation texture at the specified level
        /// </summary>
        public void VerifyNavigationTextureContent(int level)
        {
            RenderTexture navTexture = GetMipmapLevel(level);
            if (navTexture == null)
            {
                Debug.LogError($"Navigation texture at level {level} is null!");
                return;
            }
            
            // Create a temporary texture to read pixels
            Texture2D readback = new Texture2D(navTexture.width, navTexture.height, TextureFormat.R8, false);
            
            // Set active render texture and read pixels
            RenderTexture prevRT = RenderTexture.active;
            RenderTexture.active = navTexture;
            
            readback.ReadPixels(new Rect(0, 0, navTexture.width, navTexture.height), 0, 0);
            readback.Apply();
            
            // Restore active render texture
            RenderTexture.active = prevRT;
            
            // Check pixel values
            Color[] pixels = readback.GetPixels();
            int zeroCount = 0;
            int oneCount = 0;
            
            foreach (Color c in pixels)
            {
                if (c.r < 0.1f) zeroCount++;
                if (c.r > 0.9f) oneCount++;
            }
            
            Debug.Log($"Navigation texture level {level} content: " +
                     $"Size={navTexture.width}x{navTexture.height}, " +
                     $"Zero values={zeroCount}, One values={oneCount}, Total pixels={pixels.Length}");
            
            // Clean up
            Destroy(readback);
        }

        /// <summary>
        /// Adds a test pattern to the navigation texture to help diagnose issues
        /// </summary>
        private void AddTestPatternToNavigationTexture()
        {
            Debug.Log("Adding test pattern to navigation texture...");

            // Get the target texture
            RenderTexture targetTexture = useRenderTextureArray ? mipmapArray : mipmapTextures[0];

            // Create a temporary texture for the test pattern
            int patternSize = 100;
            Texture2D testPattern = new Texture2D(patternSize, patternSize, TextureFormat.R8, false);

            // Fill with a checkerboard pattern
            for (int y = 0; y < patternSize; y++)
            {
                for (int x = 0; x < patternSize; x++)
                {
                    // Create a checkerboard pattern
                    bool isWhite = (x / 10 + y / 10) % 2 == 0;
                    testPattern.SetPixel(x, y, isWhite ? Color.white : Color.black);
                }
            }

            testPattern.Apply();

            // Calculate center position to place the test pattern
            int centerX = baseWidth / 2 - patternSize / 2;
            int centerY = baseHeight / 2 - patternSize / 2;

            // Set the active render texture
            RenderTexture prevRT = RenderTexture.active;
            RenderTexture.active = targetTexture;

            // Draw the test pattern
            Graphics.Blit(testPattern, targetTexture);
            // Restore previous render texture
            RenderTexture.active = prevRT;

            // Clean up
            Destroy(testPattern);

            Debug.Log($"Added test pattern to navigation texture at position ({centerX}, {centerY}) with size {patternSize}x{patternSize}");
        }

        private void OnDestroy()
        {
            CleanupTextures();
            CleanupShaderData();
            CleanupDebugTextures();
            
            // Clean up cached mipmap levels
            foreach (var texture in cachedMipmapLevels.Values)
            {
                if (texture != null)
                {
                    texture.Release();
                    Destroy(texture);
                }
            }
            cachedMipmapLevels.Clear();
        }
    }
}
