using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class VectorFieldManager : MonoBehaviour
{
    //Singleton instance
    private static VectorFieldManager _instance;

    /// <summary>
    /// Gets the singleton instance of the VectorFieldManager.
    /// </summary>
    public static VectorFieldManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<VectorFieldManager>();

                if (_instance == null)
                {
                    Debug.LogError("No VectorFieldManager found in the scene. Ensure one exists.");
                }
            }
            return _instance;
        }
    }

    // Simulation parameters
    [SerializeField] private Vector2Int gridDimensions = new Vector2Int(64, 64); // Grid dimensions (width, height)
    [SerializeField] private ComputeShader fluidShader;
    [SerializeField] private Texture2D boundaryTexture; // Texture defining boundaries and sources/sinks
    private bool boundaryTextureNeedsUpdate = true;
    [Range(0.1f, 50.0f)]
    [SerializeField] private float sourceStrength = 5.0f; // How strong source pressure is (positive value)
    [Range(1.0f, 100.0f)]
    [SerializeField] private float sourceRadius = 1.0f;
    [Range(-0.1f, -50.0f)]
    [SerializeField] private float sinkStrength = 5.0f; // How strong sink pressure is (positive value, applied as negative)
    [Range(1.0f, 100.0f)]
    [SerializeField] private float sinkRadius = 1.0f;
    [SerializeField] private int pressureSolveIterations = 20; // Number of pressure solve iterations
    [SerializeField] private bool useMultiIterationPressureSolve = true; // Whether to use optimized multi-iteration pressure solve
    
    // World space bounds
    [Header("World Space Mapping")]
    [SerializeField] private Transform minBounds; // Transform representing minimum bounds in world space
    [SerializeField] private Transform maxBounds; // Transform representing maximum bounds in world space
    [SerializeField] private bool drawGizmos = true; // Whether to draw gizmos for the bounds
    
    // Dynamic sources and sinks
    [Header("Dynamic Sources & Sinks")]
    [SerializeField] private List<GameObject> sourceObjects = new List<GameObject>();
    [SerializeField] private List<GameObject> sinkObjects = new List<GameObject>();
    [SerializeField] private bool useSourcesFromTexture = true; // Whether to use sources defined in the boundary texture
    [SerializeField] private bool useSinksFromTexture = true; // Whether to use sinks defined in the boundary texture
    
    // Visualization
    [Header("Visualization")]
    [SerializeField] private RenderTexture visualizationTexture; // For displaying the vector field
    [SerializeField] private bool visualizeFlow = true; // Whether to update the visualization each frame
    [SerializeField] private float spacing = 4.0f;
    [SerializeField] private float visualizationScalar = 1.0f;
    [SerializeField] private float sphereScalar = .5f;
    [SerializeField] private bool emergencyExit = true;

    // Compute buffers
    private ComputeBuffer velocityBuffer; // Stores velocity vectors
    private ComputeBuffer pressureBuffer; // For pressure calculations
    private ComputeBuffer pressureBufferAlt; // Second buffer for ping-pong technique in multi-iteration pressure solve
    private ComputeBuffer divergenceBuffer; // For incompressibility constraint
    
    // For sampling the velocity field in CPU code
    private Vector3[] velocityArray;
    private bool velocityArrayNeedsUpdate = true;
    
    // Kernel IDs
    private int advectionKernelId;
    private int divergenceKernelId;
    private int pressureSolveKernelId;
    private int pressureSolveMultiKernelId;
    private int pressureGradientKernelId;
    private int applyBoundariesKernelId;
    private int applySourcesAndSinksKernelId;
    private int visualizeVectorFieldKernelId;

    private float accumulatedTime = 0.0f;
    
    void Start()
    {
        // Ensure we have a boundary texture before initializing
        if (boundaryTexture == null)
        {
            Debug.LogError("Boundary texture is not assigned! Please assign it in the inspector.");
            // Create a default texture if none is provided
            CreateDefaultBoundaryTexture();
        }

        InitializeBuffers();
        InitializeKernels();

        // Force update the boundary texture immediately before any simulation steps
        UpdateBoundaryTexture(true);
    }
    void Update()
    {
        // Check if transforms exist
        if (minBounds == null || maxBounds == null)
        {
            Debug.LogWarning("Min or Max bounds transform not set. World space mapping will not work correctly.");
        }

        // Check if boundary texture exists
        if (boundaryTexture == null)
        {
            Debug.LogWarning("Boundary texture not assigned. Simulation may not work correctly.");
        }

        // Update the boundary texture in the compute shader if needed
        if (boundaryTextureNeedsUpdate)
        {
            UpdateBoundaryTexture();
        }

        // DEBUG: Inject test values directly into velocity buffer
        if (Time.frameCount == 2) // Do this only once after startup
        {
            Debug.Log("Injecting test velocity values");
            InjectTestVelocityValues();

            // Verify injection worked
            Vector3[] testVelocities = new Vector3[gridDimensions.x * gridDimensions.y];
            velocityBuffer.GetData(testVelocities);

            // Check if data was written correctly
            bool hasData = false;
            for (int i = 0; i < testVelocities.Length; i++)
            {
                if (testVelocities[i].magnitude > 0.001f)
                {
                    hasData = true;
                    break;
                }
            }

            Debug.Log($"Velocity buffer has non-zero values: {hasData}");
        }

        try
        {
            // Apply sources and sinks first to ensure they're always active
            ApplySourcesAndSinks();

            // Run the simulation steps
            RunAdvection();
            RunDivergence();
            RunPressureSolve();
            RunPressureGradient();
            ApplyBoundaries();

        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error during simulation update: {e.Message}");
        }

        // Mark that the CPU-side array needs updating
        velocityArrayNeedsUpdate = true;

        // Visualize the vector field if requested
        if (visualizeFlow)
        {
            UpdateVectorFieldVisualization();
        }

        VerifyVelocity();

        // Debug: Check pressure and velocity values periodically
        if (Time.frameCount % 60 == 0) // Every second at 60fps
        {
            DebugCheckSimulationState();
        }
        int frameRate = (int)(1f / Time.unscaledDeltaTime);
        accumulatedTime += Time.unscaledDeltaTime;
        if (frameRate <= 10 && accumulatedTime >= 5.0f && emergencyExit)
        {
            //EditorApplication.ExitPlaymode();
        }
    }
    // Create a simple boundary texture if none is provided
    void CreateDefaultBoundaryTexture()
    {
        Debug.Log("Creating default boundary texture");
        boundaryTexture = new Texture2D(gridDimensions.x, gridDimensions.y, TextureFormat.RGBA32, false);

        // Fill with white (all fluid region)
        Color[] pixels = new Color[gridDimensions.x * gridDimensions.y];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        // Add a simple source and sink
        int centerX = gridDimensions.x / 2;
        int centerY = gridDimensions.y / 2;

        // Source at top center
        if (gridDimensions.y > 10)
        {
            pixels[centerX + (gridDimensions.y - 5) * gridDimensions.x] = Color.green;
        }

        // Sink at bottom center
        if (gridDimensions.y > 10)
        {
            pixels[centerX + 5 * gridDimensions.x] = Color.red;
        }

        boundaryTexture.SetPixels(pixels);
        boundaryTexture.Apply();
    }

    void InitializeBuffers()
    {
        int totalCells = gridDimensions.x * gridDimensions.y; // Total cells in the grid
        
        // For velocity, we need 3 floats per cell (vx, vy, vz)
        int velocityStride = sizeof(float) * 3;
        velocityBuffer = new ComputeBuffer(totalCells, velocityStride);
        
        // Initialize with zero velocities
        Vector3[] initialVelocities = new Vector3[totalCells];
        for (int i = 0; i < totalCells; i++)
        {
            initialVelocities[i] = Vector3.zero;
        }
        velocityBuffer.SetData(initialVelocities);
        
        // Initialize the array for CPU-side sampling
        velocityArray = new Vector3[totalCells];
        
        // Other buffers needed for Navier-Stokes
        pressureBuffer = new ComputeBuffer(totalCells, sizeof(float));
        pressureBufferAlt = new ComputeBuffer(totalCells, sizeof(float)); // Second buffer for ping-pong technique
        divergenceBuffer = new ComputeBuffer(totalCells, sizeof(float));
        
        // Fill with zeros initially
        float[] zeroData = new float[totalCells];
        pressureBuffer.SetData(zeroData);
        pressureBufferAlt.SetData(zeroData);
        divergenceBuffer.SetData(zeroData);
        
        // Create the visualization texture if needed
        if (visualizationTexture == null || 
            visualizationTexture.width != gridDimensions.x || 
            visualizationTexture.height != gridDimensions.y)
        {
            if (visualizationTexture != null)
                visualizationTexture.Release();
                
            visualizationTexture = new RenderTexture(gridDimensions.x, gridDimensions.y, 0, RenderTextureFormat.ARGB32);
            visualizationTexture.enableRandomWrite = true;
            visualizationTexture.Create();
        }
    }

    void InitializeKernels()
    {
        try
        {
            // Get kernel IDs for the simulation steps
            advectionKernelId = fluidShader.FindKernel("Advection");
            divergenceKernelId = fluidShader.FindKernel("Divergence");
            pressureSolveKernelId = fluidShader.FindKernel("PressureSolve");
            pressureSolveMultiKernelId = fluidShader.FindKernel("PressureSolveMulti");
            pressureGradientKernelId = fluidShader.FindKernel("PressureGradient");
            applyBoundariesKernelId = fluidShader.FindKernel("ApplyBoundaries");
            applySourcesAndSinksKernelId = fluidShader.FindKernel("ApplySourcesAndSinks");
            visualizeVectorFieldKernelId = fluidShader.FindKernel("VisualizeVectorField");

            // Verify all kernels were found
            Debug.Log($"Kernels found - Advection: {advectionKernelId}, " +
                      $"Divergence: {divergenceKernelId}, " +
                      $"PressureSolve: {pressureSolveKernelId}, " +
                      $"PressureSolveMulti: {pressureSolveMultiKernelId}, " +
                      $"PressureGradient: {pressureGradientKernelId}, " +
                      $"ApplyBoundaries: {applyBoundariesKernelId}, " +
                      $"ApplySourcesAndSinks: {applySourcesAndSinksKernelId}, " +
                      $"VisualizeVectorField: {visualizeVectorFieldKernelId}");

            // Set shared parameters
            fluidShader.SetVector("GridDimensions", new Vector4(gridDimensions.x, gridDimensions.y, 0, 0));
            fluidShader.SetFloat("SourceStrength", sourceStrength);
            fluidShader.SetFloat("SinkStrength", sinkStrength);

            // Boundary texture is set separately in UpdateBoundaryTexture()

            // Set the visualization texture
            fluidShader.SetTexture(visualizeVectorFieldKernelId, "VisualizationTexture", visualizationTexture);

            // Bind buffers to all kernels
            BindBuffersToKernel(advectionKernelId);
            BindBuffersToKernel(divergenceKernelId);
            BindBuffersToKernel(pressureSolveKernelId);
            BindBuffersToKernel(pressureSolveMultiKernelId);
            BindBuffersToKernel(pressureGradientKernelId);
            BindBuffersToKernel(applyBoundariesKernelId);
            BindBuffersToKernel(applySourcesAndSinksKernelId);
            BindBuffersToKernel(visualizeVectorFieldKernelId);

            Debug.Log("Successfully initialized all compute shader kernels");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error finding kernels: {e.Message}\n{e.StackTrace}");

            // Additional debug info to help diagnose the issue
            if (fluidShader == null)
            {
                Debug.LogError("Fluid shader is null! Please assign it in the inspector.");
            }
            else
            {
                Debug.Log($"Shader asset name: {fluidShader.name}");
                // List kernel count and names if possible
                try
                {
                    Debug.Log($"Shader has kernels: {fluidShader.FindKernel("Advection")}");
                }
                catch
                {
                    Debug.Log("Could not find any kernels in the shader.");
                }
            }
        }
    }

    void BindBuffersToKernel(int kernelId)
    {
        fluidShader.SetBuffer(kernelId, "VelocityBuffer", velocityBuffer);
        fluidShader.SetBuffer(kernelId, "PressureBuffer", pressureBuffer);
        fluidShader.SetBuffer(kernelId, "PressureBufferAlt", pressureBufferAlt);
        fluidShader.SetBuffer(kernelId, "DivergenceBuffer", divergenceBuffer);
    }

    void UpdateBoundaryTexture(bool forceUpdate = false)
    {
        if (boundaryTexture == null)
        {
            Debug.LogError("Cannot update boundary texture: Texture is null");
            return;
        }

        // Set the boundary texture for all kernels
        SetBoundaryTextureForAllKernels();

        Debug.Log($"Boundary texture updated: {boundaryTexture.width}x{boundaryTexture.height}");

        // Reset the flag
        boundaryTextureNeedsUpdate = false;
    }

    /// <summary>
    /// Sets the boundary texture for all kernels in the compute shader
    /// </summary>
    void SetBoundaryTextureForAllKernels()
    {
        try
        {
            // Set the texture for each kernel
            fluidShader.SetTexture(advectionKernelId, "BoundaryTexture", boundaryTexture);
            fluidShader.SetTexture(divergenceKernelId, "BoundaryTexture", boundaryTexture);
            fluidShader.SetTexture(pressureSolveKernelId, "BoundaryTexture", boundaryTexture);
            fluidShader.SetTexture(pressureSolveMultiKernelId, "BoundaryTexture", boundaryTexture);
            fluidShader.SetTexture(pressureGradientKernelId, "BoundaryTexture", boundaryTexture);
            fluidShader.SetTexture(applyBoundariesKernelId, "BoundaryTexture", boundaryTexture);
            fluidShader.SetTexture(applySourcesAndSinksKernelId, "BoundaryTexture", boundaryTexture);
            fluidShader.SetTexture(visualizeVectorFieldKernelId, "BoundaryTexture", boundaryTexture);

            Debug.Log("Successfully set boundary texture for all kernels");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error setting boundary texture: {e.Message}");
        }
    }

    void UpdateVectorFieldVisualization()
    {
        // Run the visualization kernel
        fluidShader.Dispatch(visualizeVectorFieldKernelId, Mathf.CeilToInt(gridDimensions.x / 8.0f), Mathf.CeilToInt(gridDimensions.y / 8.0f), 1);
    }
    
    void RunAdvection()
    {
        fluidShader.SetFloat("DeltaTime", Time.deltaTime);
        fluidShader.Dispatch(advectionKernelId, Mathf.CeilToInt(gridDimensions.x / 8.0f), Mathf.CeilToInt(gridDimensions.y / 8.0f), 1);
    }
    
    void ApplySourcesAndSinks()
    {
        // First apply sources and sinks from the texture
        if (useSourcesFromTexture || useSinksFromTexture)
        {
            fluidShader.Dispatch(applySourcesAndSinksKernelId, Mathf.CeilToInt(gridDimensions.x / 8.0f), Mathf.CeilToInt(gridDimensions.y / 8.0f), 1);
        }
        
        // Then apply sources and sinks from the dynamic objects
        ApplyDynamicSourcesAndSinks();
    }
    
    void ApplyDynamicSourcesAndSinks()
    {
        // Skip if there are no dynamic sources or sinks
        if (sourceObjects.Count == 0 && sinkObjects.Count == 0)
            return;
        
        // Make sure we have valid bounds
        if (minBounds == null || maxBounds == null)
        {
            Debug.LogWarning("Cannot apply dynamic sources/sinks: Min or Max bounds transform not set.");
            return;
        }
        
        // Get local copy of pressure buffer data
        float[] pressureData = new float[gridDimensions.x * gridDimensions.y];
        pressureBuffer.GetData(pressureData);
        
        // Apply dynamic sources
        foreach (var source in sourceObjects)
        {
            if (source == null) continue;
            
            // Convert world position to grid coordinates
            Vector2 gridPos = WorldToGridPosition(source.transform.position);
            
            // Convert the world-space radius to grid space
            Vector3 worldSize = maxBounds.position - minBounds.position;
            float gridRadius = sourceRadius / Mathf.Max(worldSize.x, worldSize.y) * Mathf.Max(gridDimensions.x, gridDimensions.y);
            
            // Apply source pressure to cells within radius
            ApplyInfluence(gridPos, gridRadius, sourceStrength, pressureData);
        }
        
        // Apply dynamic sinks
        foreach (var sink in sinkObjects)
        {
            if (sink == null) continue;
            
            // Convert world position to grid coordinates
            Vector2 gridPos = WorldToGridPosition(sink.transform.position);
            
            // Convert the world-space radius to grid space
            Vector3 worldSize = maxBounds.position - minBounds.position;
            float gridRadius = sinkRadius / Mathf.Max(worldSize.x, worldSize.y) * Mathf.Max(gridDimensions.x, gridDimensions.y);
            
            // Apply sink pressure to cells within radius (negative strength)
            ApplyInfluence(gridPos, gridRadius, sinkStrength, pressureData);
        }
        
        // Update the pressure buffer with our modifications
        pressureBuffer.SetData(pressureData);
        
        // Also update the alternate pressure buffer for multi-step pressure solving
        pressureBufferAlt.SetData(pressureData);
    }
    
    void ApplyInfluence(Vector2 gridPos, float radius, float strength, float[] pressureData)
    {
        // Calculate the grid cell range that might be affected
        int minX = Mathf.Max(0, Mathf.FloorToInt(gridPos.x - radius));
        int maxX = Mathf.Min(gridDimensions.x - 1, Mathf.CeilToInt(gridPos.x + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(gridPos.y - radius));
        int maxY = Mathf.Min(gridDimensions.y - 1, Mathf.CeilToInt(gridPos.y + radius));
        
        float sqrRadius = radius * radius;
        
        // Apply influence to cells within radius
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                // Calculate squared distance to the center
                float dx = x - gridPos.x;
                float dy = y - gridPos.y;
                float sqrDist = dx * dx + dy * dy;
                
                // Skip cells outside the radius
                if (sqrDist > sqrRadius)
                    continue;
                
                // Calculate index in the grid
                int index = y * gridDimensions.x + x;
                
                // Calculate influence strength based on distance (linear falloff)
                float influence = 1.0f - Mathf.Sqrt(sqrDist) / radius;
                
                // Apply the influence
                pressureData[index] = strength * influence;
            }
        }
    }
    
    void RunDivergence()
    {
        fluidShader.Dispatch(divergenceKernelId, Mathf.CeilToInt(gridDimensions.x / 8.0f), Mathf.CeilToInt(gridDimensions.y / 8.0f), 1);
    }
    
    void RunPressureSolve()
    {
        if (useMultiIterationPressureSolve)
        {
            // Set the iteration count parameter for the multi-iteration kernel
            fluidShader.SetInt("IterationCount", pressureSolveIterations);
            
            // Single dispatch that performs all iterations internally
            fluidShader.Dispatch(pressureSolveMultiKernelId, Mathf.CeilToInt(gridDimensions.x / 8.0f), Mathf.CeilToInt(gridDimensions.y / 8.0f), 1);
        }
        else
        {
            // Traditional approach with multiple dispatches (one per iteration)
            for (int i = 0; i < pressureSolveIterations; i++)
            {
                fluidShader.Dispatch(pressureSolveKernelId, Mathf.CeilToInt(gridDimensions.x / 8.0f), Mathf.CeilToInt(gridDimensions.y / 8.0f), 1);
            }
        }
    }
    
    void RunPressureGradient()
    {
        int num = pressureGradientKernelId;
        fluidShader.Dispatch(pressureGradientKernelId, Mathf.CeilToInt(gridDimensions.x / 8.0f), Mathf.CeilToInt(gridDimensions.y / 8.0f), 1);
    }
    
    void ApplyBoundaries()
    {
        fluidShader.Dispatch(applyBoundariesKernelId, Mathf.CeilToInt(gridDimensions.x / 8.0f), Mathf.CeilToInt(gridDimensions.y / 8.0f), 1);
    }
    
    void OnDestroy()
    {
        // Release all buffers to prevent memory leaks
        velocityBuffer?.Release();
        pressureBuffer?.Release();
        pressureBufferAlt?.Release();
        divergenceBuffer?.Release();
        
        // Release the visualization texture
        if (visualizationTexture != null)
        {
            visualizationTexture.Release();
            visualizationTexture = null;
        }
    }
    
    // This method allows other scripts to get the visualization texture
    public RenderTexture GetVisualizationTexture()
    {
        return visualizationTexture;
    }
    
    /// <summary>
    /// Gets the velocity vector at a specified world position by sampling the fluid simulation.
    /// </summary>
    /// <param name="worldPosition">Position in world space to sample</param>
    /// <returns>Velocity vector at the specified position (returns zero if position is outside bounds)</returns>
    public bool GetVelocityAtPosition(Vector3 worldPosition, out Vector3 sampledVelocity)
    {
        // Check if we have valid bounds
        if (minBounds == null || maxBounds == null)
        {
            Debug.LogWarning("Cannot sample velocity: Min or Max bounds transform not set.");
            sampledVelocity = Vector3.zero;
            return false;
        }
        
        // Convert world position to normalized grid position
        Vector2 gridPos = WorldToGridPosition(worldPosition);
        
        // Check if position is within bounds and report the issue
        if (gridPos.x < 0 || gridPos.x > 1 || gridPos.y < 0 || gridPos.y > 1)
        {
            Debug.LogWarning($"Position {worldPosition} is outside the grid bounds. Normalized position: {gridPos}");
            sampledVelocity = Vector3.zero;
            return false;
        }
        
        // Debug: output position info
        Debug.Log($"Sampling at world pos: {worldPosition}, grid pos: {gridPos}");

        // Sample the velocity at this position
        sampledVelocity = SampleVelocityField(gridPos);
        return true;
    }
    
    /// <summary>
    /// Converts a world space position to normalized grid coordinates (0-1)
    /// </summary>
    private Vector2 WorldToGridPosition(Vector3 worldPosition)
    {
        // Get the bounds in world space
        Vector3 min = minBounds.position;
        Vector3 max = maxBounds.position;

        // Calculate normalized position within bounds (0 to 1)
        float normalizedX = Normalize(min.x, max.x, worldPosition.x);
        float normalizedZ = Normalize(min.z, max.z, worldPosition.z);

        return new Vector2(normalizedX, normalizedZ);
    }
    
    private float Normalize(float min, float max, float value)
    {
        return (value - min) / (max - min);
    }

    /// <summary>
    /// Samples the velocity field at the given normalized position (0-1, 0-1)
    /// Uses bilinear interpolation between grid cells
    /// </summary>
    private Vector3 SampleVelocityField(Vector2 normalizedPosition)
    {
        // Ensure our CPU-side array is up to date
        UpdateVelocityArray();
        
        // Convert normalized position to grid position
        float gridX = normalizedPosition.x * (gridDimensions.x - 1);
        float gridY = normalizedPosition.y * (gridDimensions.y - 1);
        
        // Get the four nearest grid points
        int x0 = Mathf.FloorToInt(gridX);
        int y0 = Mathf.FloorToInt(gridY);
        int x1 = Mathf.Min(x0 + 1, gridDimensions.x - 1);
        int y1 = Mathf.Min(y0 + 1, gridDimensions.y - 1);
        
        // Calculate interpolation factors
        float tx = gridX - x0;
        float ty = gridY - y0;
        
        // Get velocity values at the four corners
        Vector3 v00 = velocityArray[y0 * gridDimensions.x + x0];
        Vector3 v10 = velocityArray[y0 * gridDimensions.x + x1];
        Vector3 v01 = velocityArray[y1 * gridDimensions.x + x0];
        Vector3 v11 = velocityArray[y1 * gridDimensions.x + x1];
        
        // Debug: print the sampled values
        //Debug.Log($"Sampling velocities: v00={v00}, v10={v10}, v01={v01}, v11={v11}");
        
        // Perform bilinear interpolation
        Vector3 vx0 = Vector3.Lerp(v00, v10, tx);
        Vector3 vx1 = Vector3.Lerp(v01, v11, tx);
        Vector3 result = Vector3.Lerp(vx0, vx1, ty);
        
        //Debug.Log($"Interpolated result: {result}");
        
        return result;
    }
    
    /// <summary>
    /// Updates the CPU-side velocity array from the GPU buffer
    /// </summary>
    private void UpdateVelocityArray()
    {
        if (velocityArrayNeedsUpdate)
        {
            // Copy data from GPU to CPU
            velocityBuffer.GetData(velocityArray);
            velocityArrayNeedsUpdate = false;
            
            // Debug: Check if we received non-zero data
            bool hasNonZeroValues = false;
            for (int i = 0; i < velocityArray.Length; i++)
            {
                if (velocityArray[i].sqrMagnitude > 0.00001f)
                {
                    hasNonZeroValues = true;
                    break;
                }
            }
            
            if (!hasNonZeroValues)
            {
                Debug.LogWarning("UpdateVelocityArray: Velocity buffer contains only zero values!");
            }
            else
            {
                Debug.Log("UpdateVelocityArray: Successfully retrieved non-zero velocity data");
            }
        }
    }
    
    public void UpdateSinksAndSources()
    {
        boundaryTextureNeedsUpdate = true;
    }

    public void ClearSinks()
    {
        sinkObjects.Clear();
        UpdateSinksAndSources();
    }

    public void AddSink(GameObject sink)
    {
        sinkObjects.Add(sink);
        UpdateSinksAndSources();
    }

    public void RemoveSink(GameObject sink)
    {
        sinkObjects.Remove(sink);
        UpdateSinksAndSources();
    }

    public void ClearSources()
    {
        sourceObjects.Clear();
        UpdateSinksAndSources();
    }

    public void AddSource(GameObject source)
    {
        sourceObjects.Add(source);
        UpdateSinksAndSources();
    }

    public void RemoveSource(GameObject source)
    {
        sourceObjects.Remove(source);
        UpdateSinksAndSources();
    }

    /// <summary>
    /// Draws gizmos to visualize the bounds of the vector field
    /// </summary>
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !drawGizmos || minBounds == null || maxBounds == null)
            return;

        // Draw the bounds as a box for reference
        Gizmos.color = Color.white;
        Vector3 size = maxBounds.position - minBounds.position;
        Vector3 center = minBounds.position + size * 0.5f;
        Gizmos.DrawWireCube(center, size);

        // Skip velocity visualization if array isn't initialized
        if (velocityArray == null)
            return;       

        // Draw velocity vectors at sample points
        Gizmos.color = Color.cyan;
        float maxVelMagnitude = 0.01f; // Prevent division by zero

        // First pass: determine max velocity for proper scaling
        for (int z = 0; z < spacing; z++)
        {
            for (int x = 0; x < spacing; x++)
            {
                // Calculate normalized position (0.5 offset centers in cells)
                float normX = (x + 0.5f) / spacing;
                float normZ = (z + 0.5f) / spacing;

                // Convert to world position - IMPORTANT: Y stays constant, normZ maps to world Z
                Vector3 worldPos = new Vector3(
                    Mathf.Lerp(minBounds.position.x, maxBounds.position.x, normX),
                    minBounds.position.y, // Use a constant Y value at ground level
                    Mathf.Lerp(minBounds.position.z, maxBounds.position.z, normZ)
                );

                // Sample velocity from our 2D field
                Vector3 vel = new();
                GetVelocityAtPosition(worldPos, out vel);

                // Convert 2D velocity (xy in simulation) to 3D world space (xz for ground movement)
                Vector3 worldVel = new Vector3(vel.x, 0, vel.y);

                // Track max velocity magnitude for scaling
                maxVelMagnitude = Mathf.Max(maxVelMagnitude, worldVel.magnitude);
            }
        }

        // Calculate a scale factor for good visibility
        float cellSizeX = size.x / spacing;
        float cellSizeZ = size.z / spacing;
        float scaleFactor = Mathf.Min(cellSizeX, cellSizeZ);
        float velocityScale = scaleFactor / maxVelMagnitude ;

        // Actual drawing pass
        for (int z = 0; z < spacing; z++)
        {
            for (int x = 0; x < spacing; x++)
            {
                // Calculate normalized position (0.5 offset centers in cells)
                float normX = (x + 0.5f) / spacing;
                float normZ = (z + 0.5f) / spacing;

                // Convert to world position
                Vector3 worldPos = new Vector3(
                    Mathf.Lerp(minBounds.position.x, maxBounds.position.x, normX),
                    minBounds.position.y, // Stay at ground level
                    Mathf.Lerp(minBounds.position.z, maxBounds.position.z, normZ)
                );

                // Sample velocity
                Vector3 vel = new();
                GetVelocityAtPosition(worldPos, out vel);

                // Convert 2D velocity to 3D world space (map Y component to Z)
                Vector3 worldVel = new Vector3(vel.x, 0, vel.y);

                // Draw the vector
                //Gizmos.DrawRay(worldPos, worldVel * velocityScale);
                Debug.DrawRay(worldPos, worldVel * velocityScale * visualizationScalar, Color.red);

                // Draw a small sphere at the start point
                Gizmos.DrawSphere(worldPos, scaleFactor * sphereScalar);
            }
        }
    }

    void VerifyVelocity()
    {
        Vector3[] testVel = new Vector3[velocityArray.Length];
        velocityBuffer.GetData(testVel);

        float maxMag = 0;
        for (int i = 0; i < testVel.Length; i++)
        {
            maxMag = Mathf.Max(maxMag, testVel[i].magnitude);
        }

        //Debug.Log($"Max velocity magnitude: {maxMag}");
    }
    
    // Debug method to inject test values directly into velocity buffer
    void InjectTestVelocityValues()
    {
        int totalCells = gridDimensions.x * gridDimensions.y;
        Vector3[] testVelocities = new Vector3[totalCells];
        
        // Create a simple test pattern - velocity flowing outward from center
        int centerX = gridDimensions.x / 2;
        int centerY = gridDimensions.y / 2;
        
        for (int y = 0; y < gridDimensions.y; y++)
        {
            for (int x = 0; x < gridDimensions.x; x++)
            {
                int index = y * gridDimensions.x + x;
                float dx = x - centerX;
                float dy = y - centerY;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                
                // Avoid division by zero at center
                if (distance < 0.001f)
                {
                    testVelocities[index] = Vector3.zero;
                }
                else
                {
                    // Normalize direction vector and scale
                    float invDist = 1.0f / distance;
                    testVelocities[index] = new Vector3(dx * invDist, dy * invDist, 0) * 1.0f;
                }
            }
        }
        
        // Upload the test pattern to the buffer
        velocityBuffer.SetData(testVelocities);
    }
    
    // Debug method to check overall simulation state
    void DebugCheckSimulationState()
    {
        // Check pressure values
        float[] pressureValues = new float[gridDimensions.x * gridDimensions.y];
        pressureBuffer.GetData(pressureValues);
        
        float minPressure = float.MaxValue;
        float maxPressure = float.MinValue;
        float avgPressure = 0;
        
        for (int i = 0; i < pressureValues.Length; i++)
        {
            minPressure = Mathf.Min(minPressure, pressureValues[i]);
            maxPressure = Mathf.Max(maxPressure, pressureValues[i]);
            avgPressure += pressureValues[i];
        }
        
        avgPressure /= pressureValues.Length;
        
        // Check velocity values
        Vector3[] velocityValues = new Vector3[gridDimensions.x * gridDimensions.y];
        velocityBuffer.GetData(velocityValues);
        
        float maxVelocityMagnitude = 0;
        float avgVelocityMagnitude = 0;
        float minVelocityMagnitude = 0;
        
        for (int i = 0; i < velocityValues.Length; i++)
        {
            float magnitude = velocityValues[i].magnitude;
            
            minVelocityMagnitude = Mathf.Min(minVelocityMagnitude, magnitude);
            maxVelocityMagnitude = Mathf.Max(maxVelocityMagnitude, magnitude);
            avgVelocityMagnitude += magnitude;
        }
        
        avgVelocityMagnitude /= velocityValues.Length;

        //if (avgVelocityMagnitude != float.NaN)
        //{
        //    frameCount++;
        //}

        Debug.Log($"Simulation State - Pressure: min={minPressure:F3}, max={maxPressure:F3}, avg={avgPressure:F3} | " + $"Velocity: min={minVelocityMagnitude:F3}, max={maxVelocityMagnitude:F3}, avg={avgVelocityMagnitude:F3}");
        //if(avgVelocityMagnitude == float.NaN)
        //{
        //    Debug.Log("FrameCount: " + frameCount);
        //}
    }
    
    void OnGUI()
    {
        if (minBounds != null && maxBounds != null)
        {
            GUI.Label(new Rect(10, 10, 300, 20), 
                $"Bounds: Min({minBounds.position}), Max({maxBounds.position})");
        }
    }
}
