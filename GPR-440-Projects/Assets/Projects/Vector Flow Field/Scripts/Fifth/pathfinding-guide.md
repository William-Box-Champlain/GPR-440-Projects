# Mipmap Pathfinding System: Setup and Usage Guide

## Overview

The Mipmap Pathfinding System is a high-performance GPU-based pathfinding solution designed for large navigation spaces. It uses a multi-resolution approach to efficiently generate vector fields for multiple dynamic targets, supporting up to 1000 agents navigating to 3-5 activation targets that activate in an outside-in sequence.

## Key Features

- **Multi-Resolution Processing**: Uses a mipmap hierarchy with 5 resolution levels to efficiently process navigation data
- **Resolution Bias**: Focuses computational resources on ~50 junction points and active targets
- **Frame-Distributed Computing**: Spreads computation across multiple frames to maintain 60 FPS
- **Efficient Agent Queries**: Optimized CPU-side caching for rapid direction lookups
- **Scalable Design**: Supports up to 1000 simultaneous agents navigating a 12,000 × 6,000 space
- **Minimal Memory Usage**: Optimized storage formats keep total memory usage under 50MB

## System Architecture

The system consists of six main components that work together in a clear pipeline:

1. **Mipmap Generator**: Creates multiple resolution levels of the navigation texture (from 12,000 × 6,000 down to 750 × 375)
2. **Resolution Bias Controller**: Determines which areas need high or low resolution processing based on junctions and targets
3. **Multi-Resolution Vector Field Generator**: Executes the light propagation algorithm at each resolution level to generate vector fields
4. **Chunked Processing System**: Divides the work into chunks and distributes computation across multiple frames
5. **Vector Field Storage**: Efficiently stores vector field data with CPU-side caching for rapid access
6. **Agent Query System**: Provides a simple position-based interface for agents to query the vector field

## Setup Guide

### 1. Required Assets

Ensure you have the following compute shaders and custom shaders in your project's Resources folder:

- **Compute Shaders**:
  - `BoundsCalculator_Optimized.compute` - Generates the base navigation texture from Unity's NavMesh
  - `BiasGenerator.compute` - Generates the resolution bias texture based on junctions and targets
  - `JunctionDetector.compute` - Automatically detects ~50 junction points in the navigation network
  - `VectorFieldPropagation.compute` - Executes the light propagation algorithm at multiple resolutions

- **Custom Shaders**:
  - `ConservativeDownsample.shader` - Special downsampling shader that preserves path connectivity

### 2. Component Setup

Create a GameObject in your scene to host the pathfinding system:

```csharp
// Create GameObject
GameObject pathfindingManager = new GameObject("PathfindingManager");

// Add required components
MipmapGenerator mipmapGenerator = pathfindingManager.AddComponent<MipmapGenerator>();
ResolutionBiasController biasController = pathfindingManager.AddComponent<ResolutionBiasController>();
MultiResolutionVectorFieldGenerator vectorFieldGenerator = pathfindingManager.AddComponent<MultiResolutionVectorFieldGenerator>();
ChunkedProcessingSystem chunkProcessor = pathfindingManager.AddComponent<ChunkedProcessingSystem>();
VectorFieldStorage vectorFieldStorage = pathfindingManager.AddComponent<VectorFieldStorage>();
AgentQuerySystem querySystem = pathfindingManager.AddComponent<AgentQuerySystem>();
```

### 3. Configure Components

Set up the components in the Inspector or via code:

#### MipmapGenerator Configuration

```csharp
// Configure MipmapGenerator
mipmapGenerator.boundsCalculatorShader = Resources.Load<ComputeShader>("BoundsCalculator_Optimized");
mipmapGenerator.baseWidth = 12000;  // Default for large environments
mipmapGenerator.baseHeight = 6000;  // Default for large environments
mipmapGenerator.mipmapLevels = 5;   // Default level count
```

#### ResolutionBiasController Configuration

```csharp
// Configure ResolutionBiasController
biasController.mipmapGenerator = mipmapGenerator;
biasController.autoDetectJunctions = true;
biasController.maxJunctions = 50;  // Project goal specifies ~50 junctions
biasController.biasRadius = 5.0f;  // World-space radius, adjust based on scale
biasController.maxBiasStrength = 4.0f;  // One per resolution level
```

#### MultiResolutionVectorFieldGenerator Configuration

```csharp
// Configure VectorFieldGenerator
vectorFieldGenerator.mipmapGenerator = mipmapGenerator;
vectorFieldGenerator.biasController = biasController;
vectorFieldGenerator.propagationShader = Resources.Load<ComputeShader>("VectorFieldPropagation");
vectorFieldGenerator.propagationStagesPerLevel = 2;
vectorFieldGenerator.interpolateBetweenLevels = true;
```

#### ChunkedProcessingSystem Configuration

```csharp
// Configure ChunkedProcessingSystem
chunkProcessor.mipmapGenerator = mipmapGenerator;
chunkProcessor.biasController = biasController;
chunkProcessor.vectorFieldGenerator = vectorFieldGenerator;
chunkProcessor.chunkSize = 256;  // Default for good performance balance
chunkProcessor.frameBudgetMs = 8.0f;  // Half of 16.7ms (60 FPS)
```

#### VectorFieldStorage Configuration

```csharp
// Configure VectorFieldStorage
vectorFieldStorage.mipmapGenerator = mipmapGenerator;
vectorFieldStorage.vectorFieldGenerator = vectorFieldGenerator;
vectorFieldStorage.enableCPUCache = true;
vectorFieldStorage.asyncCPUUpdate = true;
```

#### AgentQuerySystem Configuration

```csharp
// Configure AgentQuerySystem
querySystem.mipmapGenerator = mipmapGenerator;
querySystem.biasController = biasController;
querySystem.vectorFieldStorage = vectorFieldStorage;
querySystem.enableBatchQueries = true;
```

### 4. Initialization

Components must be initialized in the correct order. Use a coroutine to distribute initialization across frames to prevent freezing:

```csharp
private IEnumerator InitializeSystem()
{
    // Step 1: Initialize MipmapGenerator
    // Creates the multi-resolution navigation textures
    Debug.Log("Initializing MipmapGenerator...");
    mipmapGenerator.Initialize();
    yield return null;  // Wait a frame to prevent UI freezing
    
    // Step 2: Initialize ResolutionBiasController
    // Detects junctions and sets up bias system
    Debug.Log("Initializing ResolutionBiasController...");
    biasController.Initialize();
    yield return null;
    
    // Step 3: Initialize MultiResolutionVectorFieldGenerator
    // Sets up vector field textures for each resolution level
    Debug.Log("Initializing VectorFieldGenerator...");
    vectorFieldGenerator.Initialize();
    yield return null;
    
    // Step 4: Initialize VectorFieldStorage
    // Creates CPU-side caches for efficient queries
    Debug.Log("Initializing VectorFieldStorage...");
    vectorFieldStorage.Initialize();
    yield return null;
    
    // Step 5: Initialize ChunkedProcessingSystem
    // Sets up chunk division and priority system
    Debug.Log("Initializing ChunkedProcessingSystem...");
    chunkProcessor.Initialize();
    yield return null;
    
    // Step 6: Initialize AgentQuerySystem
    // Sets up the interface for agent queries
    Debug.Log("Initializing AgentQuerySystem...");
    querySystem.Initialize();
    yield return null;
    
    // Step 7: Set initial targets (if any)
    if (initialTargets.Length > 0)
    {
        Debug.Log("Setting initial targets...");
        Vector3[] targetPositions = new Vector3[initialTargets.Length];
        for (int i = 0; i < initialTargets.Length; i++)
        {
            targetPositions[i] = initialTargets[i].position;
        }
        SetTargets(targetPositions);
    }
    
    Debug.Log("Pathfinding system initialized successfully!");
}

// Helper method for target management - use this when targets change
public void SetTargets(Vector3[] targetPositions)
{
    // The ChunkedProcessingSystem acts as the main interface for target updates
    // It will propagate the targets to other components as needed
    chunkProcessor.SetTargets(targetPositions);
    
    // For more direct control, you can update individual components:
    // vectorFieldGenerator.SetTargets(targetPositions);
    // biasController.SetTargets(targetTransforms);
}
```

### 5. Update Loop

Implement the update loop to process chunks within the frame budget:

```csharp
private void Update()
{
    // Process chunks within frame budget
    chunkProcessor.Update();
    
    // Optional: Update vector field storage if not handled internally
    vectorFieldStorage.Update();
    
    // Optional: Update UI progress
    if (progressBar != null)
    {
        progressBar.value = chunkProcessor.GetChunkProgress();
    }
}
```

## Agent Integration

### 1. Create Agent Interface

Implement the `IPathfindingAgent` interface in your agent controller:

```csharp
public class AgentController : MonoBehaviour, IPathfindingAgent
{
    [SerializeField] private float speed = 5.0f;
    [SerializeField] private float importance = 1.0f;
    
    private AgentQuerySystem querySystem;
    private Vector3 currentDirection;
    
    // Required interface implementations
    public Vector3 GetPosition() => transform.position;
    public float GetImportance() => importance;
    
    public void Initialize(AgentQuerySystem querySystem)
    {
        this.querySystem = querySystem;
        querySystem.RegisterAgent(this);
    }
    
    private void Update()
    {
        if (querySystem != null && querySystem.IsInitialized)
        {
            // Get movement direction from query system
            currentDirection = querySystem.GetFlowDirection(transform.position, importance);
            
            // Apply movement
            if (currentDirection != Vector3.zero)
            {
                transform.position += currentDirection * speed * Time.deltaTime;
                transform.forward = currentDirection;
            }
        }
    }
    
    private void OnDestroy()
    {
        if (querySystem != null)
        {
            querySystem.UnregisterAgent(this);
        }
    }
}
```

### 2. Agent Manager

Create an agent manager to handle mass spawning and efficient batch querying for up to 1000 agents:

```csharp
public class AgentManager : MonoBehaviour
{
    // Inspector settings
    [SerializeField] private AgentController agentPrefab;
    [SerializeField] private int agentCount = 1000;
    [SerializeField] private float spawnRadius = 50.0f;
    [SerializeField] private Transform spawnCenter;
    [SerializeField] private bool useBatchProcessing = true;
    
    // System references
    private AgentQuerySystem querySystem;
    private List<AgentController> agents = new List<AgentController>();
    
    // Batch processing arrays
    private Vector3[] positions;
    private Vector3[] results;
    private float[] importanceValues;
    
    public void Initialize(AgentQuerySystem querySystem)
    {
        this.querySystem = querySystem;
        
        // Pre-allocate arrays for batch processing
        if (useBatchProcessing)
        {
            positions = new Vector3[agentCount];
            results = new Vector3[agentCount];
            importanceValues = new float[agentCount];
        }
    }
    
    public void SpawnAgents()
    {
        int spawnedCount = 0;
        int attempts = 0;
        int maxAttempts = agentCount * 2; // Avoid infinite loops
        
        while (spawnedCount < agentCount && attempts < maxAttempts)
        {
            attempts++;
            
            // Generate random position within spawn radius
            Vector2 randomPos = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPos = spawnCenter.position + new Vector3(randomPos.x, 0, randomPos.y);
            
            // Ensure position is navigable
            if (querySystem.IsPositionNavigable(spawnPos))
            {
                AgentController agent = Instantiate(agentPrefab, spawnPos, Quaternion.identity);
                agent.transform.parent = transform; // Parent to this object for hierarchy organization
                agent.Initialize(querySystem);
                agents.Add(agent);
                spawnedCount++;
            }
        }
        
        Debug.Log($"Spawned {agents.Count} agents (requested {agentCount}, attempted {attempts} positions)");
    }
    
    // Batch update for better performance with many agents
    private void Update()
    {
        if (!useBatchProcessing || agents.Count == 0 || querySystem == null || !querySystem.IsInitialized)
            return;
        
        // Fill arrays with agent data
        for (int i = 0; i < agents.Count; i++)
        {
            positions[i] = agents[i].transform.position;
            importanceValues[i] = agents[i].GetImportance();
        }
        
        // Get all directions in a single batch query
        querySystem.GetFlowDirectionBatch(positions, results, importanceValues);
        
        // Apply results to all agents
        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i] != null)
            {
                agents[i].SetMoveDirection(results[i]);
            }
        }
    }
    
    // Helper method to get the current agent count
    public int GetAgentCount()
    {
        return agents.Count;
    }
}
```

## Target Management

Implement a target manager to handle the outside-in activation sequence as specified in the project goals:

```csharp
public class TargetManager : MonoBehaviour
{
    // Target configuration
    [SerializeField] private Transform[] targetTransforms; // Arrange these from inner to outer positions
    [SerializeField] private float activationInterval = 10.0f; // Time between target activations
    [SerializeField] private bool activateOnStart = false; // Should the first target activate immediately?
    [SerializeField] private bool visualizeTargets = true; // Show gizmos for targets
    
    // System references
    private ChunkedProcessingSystem chunkProcessor;
    private ResolutionBiasController biasController;
    
    // Runtime state
    private List<Transform> activeTargets = new List<Transform>();
    private float nextActivationTime;
    
    // Target visual properties
    private Color activeTargetColor = Color.green;
    private Color inactiveTargetColor = Color.gray;
    private float targetGizmoSize = 1.5f;
    
    public void Initialize(ChunkedProcessingSystem chunkProcessor, ResolutionBiasController biasController)
    {
        this.chunkProcessor = chunkProcessor;
        this.biasController = biasController;
        
        // Start with no active targets
        activeTargets.Clear();
        
        // Set first activation time
        nextActivationTime = Time.time + (activateOnStart ? 0.1f : activationInterval);
        
        Debug.Log($"Target Manager initialized with {targetTransforms.Length} targets in outside-in sequence");
    }
    
    private void Update()
    {
        // Check if it's time to activate the next target
        if (Time.time >= nextActivationTime && activeTargets.Count < targetTransforms.Length)
        {
            // Activate next target (outside-in sequence)
            // The project specifies outside-in activation, so we start from the furthest target (end of array)
            // and move inward (toward start of array)
            int nextIndex = targetTransforms.Length - 1 - activeTargets.Count;
            Transform nextTarget = targetTransforms[nextIndex];
            
            // Add to active targets
            activeTargets.Add(nextTarget);
            
            // Update biasController to apply resolution bias around the new target
            biasController.ActivateTarget(nextTarget);
            
            // Update chunked processing system with all active target positions
            Vector3[] targetPositions = activeTargets.ConvertAll(t => t.position).ToArray();
            chunkProcessor.SetTargets(targetPositions);
            
            // Schedule next activation
            nextActivationTime = Time.time + activationInterval;
            
            Debug.Log($"Activated target {nextIndex} at {nextTarget.position}. Active targets: {activeTargets.Count}/{targetTransforms.Length}");
        }
    }
    
    // Visual debugging
    private void OnDrawGizmos()
    {
        if (!visualizeTargets || targetTransforms == null)
            return;
            
        for (int i = 0; i < targetTransforms.Length; i++)
        {
            if (targetTransforms[i] == null)
                continue;
                
            // Determine if this target is active
            bool isActive = false;
            if (Application.isPlaying && activeTargets != null)
            {
                isActive = activeTargets.Contains(targetTransforms[i]);
            }
            
            // Draw sphere at target position
            Gizmos.color = isActive ? activeTargetColor : inactiveTargetColor;
            Gizmos.DrawSphere(targetTransforms[i].position, targetGizmoSize);
            
            // Label with index
            // Note: This requires the Unity.TextMeshPro package
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(targetTransforms[i].position + Vector3.up * 2, 
                $"Target {i} ({(isActive ? "Active" : "Inactive")})");
            #endif
        }
    }
    
    // Allows external systems to check if all targets are active
    public bool AreAllTargetsActive()
    {
        return activeTargets.Count >= targetTransforms.Length;
    }
    
    // Force activation of a specific target (useful for testing)
    public void ForceActivateTarget(int index)
    {
        if (index < 0 || index >= targetTransforms.Length)
            return;
            
        Transform target = targetTransforms[index];
        
        // Skip if already active
        if (activeTargets.Contains(target))
            return;
            
        // Add to active list
        activeTargets.Add(target);
        
        // Update bias and processing
        biasController.ActivateTarget(target);
        Vector3[] targetPositions = activeTargets.ConvertAll(t => t.position).ToArray();
        chunkProcessor.SetTargets(targetPositions);
        
        Debug.Log($"Forced activation of target {index}. Active targets: {activeTargets.Count}/{targetTransforms.Length}");
    }
}
```

## Dynamic Obstacle Integration

For handling dynamic obstacles that affect pathfinding:

```csharp
public class DynamicObstacle : MonoBehaviour
{
    [SerializeField] private float obstacleRadius = 5.0f;
    
    private ChunkedProcessingSystem chunkProcessor;
    private Vector3 lastPosition;
    
    public void Initialize(ChunkedProcessingSystem chunkProcessor)
    {
        this.chunkProcessor = chunkProcessor;
        lastPosition = transform.position;
    }
    
    private void Update()
    {
        // Check if obstacle has moved significantly
        if (Vector3.Distance(transform.position, lastPosition) > 0.5f)
        {
            // Mark affected chunks as dirty
            chunkProcessor.MarkChunkDirty(transform.position, obstacleRadius);
            lastPosition = transform.position;
        }
    }
}
```

## PathfindingManager: Putting It All Together

Create a master manager component that ties everything together:

```csharp
public class PathfindingManager : MonoBehaviour
{
    // Components
    private MipmapGenerator mipmapGenerator;
    private ResolutionBiasController biasController;
    private MultiResolutionVectorFieldGenerator vectorFieldGenerator;
    private ChunkedProcessingSystem chunkProcessor;
    private VectorFieldStorage vectorFieldStorage;
    private AgentQuerySystem querySystem;
    
    // Managers
    [SerializeField] private AgentManager agentManager;
    [SerializeField] private TargetManager targetManager;
    [SerializeField] private DynamicObstacle[] dynamicObstacles;
    
    // UI
    [SerializeField] private Slider progressBar;
    
    private void Start()
    {
        // Get or add components
        mipmapGenerator = GetComponent<MipmapGenerator>();
        biasController = GetComponent<ResolutionBiasController>();
        vectorFieldGenerator = GetComponent<MultiResolutionVectorFieldGenerator>();
        chunkProcessor = GetComponent<ChunkedProcessingSystem>();
        vectorFieldStorage = GetComponent<VectorFieldStorage>();
        querySystem = GetComponent<AgentQuerySystem>();
        
        // Initialize system
        StartCoroutine(InitializeSystem());
    }
    
    private IEnumerator InitializeSystem()
    {
        // Initialize components in order
        mipmapGenerator.Initialize();
        yield return null;
        
        biasController.Initialize();
        yield return null;
        
        vectorFieldGenerator.Initialize();
        yield return null;
        
        vectorFieldStorage.Initialize();
        yield return null;
        
        chunkProcessor.Initialize();
        yield return null;
        
        querySystem.Initialize();
        yield return null;
        
        // Initialize managers
        agentManager.Initialize(querySystem);
        targetManager.Initialize(chunkProcessor, biasController);
        
        foreach (var obstacle in dynamicObstacles)
        {
            obstacle.Initialize(chunkProcessor);
        }
        
        // Wait for initial processing
        while (chunkProcessor.GetChunkProgress() < 0.5f)
        {
            progressBar.value = chunkProcessor.GetChunkProgress();
            yield return null;
        }
        
        // Spawn agents when system is ready
        agentManager.SpawnAgents();
        
        Debug.Log("Pathfinding system fully initialized!");
    }
    
    private void Update()
    {
        // Update progress bar
        if (progressBar != null)
        {
            progressBar.value = chunkProcessor.GetChunkProgress();
        }
    }
    
    // Public API for setting targets externally
    public void SetTargets(Vector3[] targetPositions)
    {
        chunkProcessor.SetTargets(targetPositions);
    }
    
    // Public API for changing frame budget during runtime
    public void SetFrameBudget(float milliseconds)
    {
        chunkProcessor.SetFrameBudget(milliseconds);
    }
}
```

## Performance Optimization Tips

### 1. Resolution and Chunk Size

- **Navigation Resolution**: The default 12,000 × 6,000 resolution provides good precision for large environments
  - For smaller environments, consider reducing this to 6,000 × 3,000
  - Each lower resolution level is exactly half the previous level (down to 750 × 375 at level 4)
  
- **Chunk Size Tuning**: The default 256×256 pixel chunks offer a good balance
  - Larger chunks (512×512): Fewer chunks to process but more work per chunk
  - Smaller chunks (128×128): More granular updating but higher overhead
  - For busy scenes, smaller chunks help distribute work more evenly

- **Mobile Optimization**: For mobile platforms:
  - Reduce mipmap levels from 5 to 3-4
  - Use smaller base resolution (6,000 × 3,000)
  - Increase chunk size to reduce overall chunk count

### 2. Frame Budget Management

- **Default Setting**: Start with 8ms frame budget (half of 16.7ms for 60 FPS)
  - Leaves approximately 8ms for rendering and other game systems
  
- **Adaptive Settings**:
  - GPU-bound games: Increase to 10-12ms since CPU has available time
  - CPU-heavy games: Reduce to 4-6ms to leave room for other processing
  - VR applications: Consider 4ms to maintain 90+ FPS
  
- **Monitoring Performance**:
  - Track `chunkProcessor.GetChunkProgress()` to see overall completion rate
  - If progress stays low, consider increasing frame budget temporarily
  - Enable `vectorFieldStorage.asyncCPUUpdate` to prevent CPU readback hitches

### 3. Agent Optimization

- **Batch Processing**: Essential for 500+ agents
  - Pre-allocate position and result arrays to avoid GC
  - Process all agents in a single `GetFlowDirectionBatch()` call
  
- **Agent Importance Levels**: Use the importance parameter to control resolution
  - Critical agents (player, enemies): 1.0 importance
  - Background agents (crowds, ambience): 0.25-0.5 importance
  - Lower importance uses lower resolution sampling for better performance
  
- **Update Frequency**: Not all agents need updates every frame
  - Distant agents: Update every 3-5 frames
  - Use time-slicing to distribute agent updates evenly
  - Implement agent LOD system based on visibility and importance

### 4. Memory Optimization

- **Texture Formats**:
  - Navigation textures: R8 format (1 byte per pixel)
  - Vector fields: RG8 format (2 bytes per pixel)
  - Total should be under 50MB for full system
  
- **CPU Cache Settings**:
  - Memory-constrained platforms: Set `vectorFieldStorage.cacheAllResolutionLevels = false`
  - This reduces memory by ~30% with minimal performance impact
  - For extreme cases, disable CPU cache entirely (but expect slower queries)
  
- **Monitoring**:
  - Check `vectorFieldStorage.GetMemoryUsageMB()` to track memory usage
  - Memory usage should be ~35MB for all resolution levels
  - Consider reducing base resolution if memory is critical

## Debugging and Visualization

Add visualization helpers for debugging:

```csharp
public class PathfindingDebugVisualizer : MonoBehaviour
{
    [SerializeField] private MipmapGenerator mipmapGenerator;
    [SerializeField] private ResolutionBiasController biasController;
    [SerializeField] private MultiResolutionVectorFieldGenerator vectorFieldGenerator;
    [SerializeField] private ChunkedProcessingSystem chunkProcessor;
    
    [SerializeField] private RawImage navigationTextureImage;
    [SerializeField] private RawImage biasTextureImage;
    [SerializeField] private RawImage vectorFieldImage;
    [SerializeField] private Dropdown resolutionDropdown;
    
    [SerializeField] private bool visualizeChunks = false;
    [SerializeField] private bool visualizeAgentDensity = false;
    
    private int currentLevel = 0;
    
    private void Start()
    {
        // Setup resolution dropdown
        if (resolutionDropdown != null)
        {
            resolutionDropdown.ClearOptions();
            
            List<string> options = new List<string>();
            for (int i = 0; i < mipmapGenerator.GetMipmapLevelCount(); i++)
            {
                options.Add($"Level {i} ({mipmapGenerator.GetBaseWidth() >> i}x{mipmapGenerator.GetBaseHeight() >> i})");
            }
            
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        }
    }
    
    private void OnResolutionChanged(int level)
    {
        currentLevel = level;
        UpdateVisualizations();
    }
    
    private void Update()
    {
        UpdateVisualizations();
        
        if (visualizeChunks)
        {
            VisualizeChunks();
        }
        
        if (visualizeAgentDensity)
        {
            VisualizeAgentDensity();
        }
    }
    
    private void UpdateVisualizations()
    {
        if (navigationTextureImage != null)
        {
            navigationTextureImage.texture = mipmapGenerator.GetMipmapLevel(currentLevel);
        }
        
        if (biasTextureImage != null)
        {
            biasTextureImage.texture = biasController.BiasTexture;
        }
        
        if (vectorFieldImage != null)
        {
            vectorFieldImage.texture = vectorFieldGenerator.GetVectorFieldTexture(currentLevel);
        }
    }
    
    private void VisualizeChunks()
    {
        // Draw chunk boundaries and priorities using Debug.DrawLine
        // Implementation depends on chunk data exposure from ChunkedProcessingSystem
    }
    
    private void VisualizeAgentDensity()
    {
        // Draw agent density heatmap using Debug.DrawLine or particle system
        // Implementation depends on density data exposure from ChunkedProcessingSystem
    }
}
```

## Common Issues and Solutions

### Problem: Agents Getting Stuck at Corners or Narrow Passages

**Solution**:
- Increase the `navMeshSampleDistance` in MipmapGenerator (default: 0.1)
- Modify the conservative downsampling logic in `ConservativeDownsample.shader`
- Enable `preserveJunctions` option in MipmapGenerator to maintain critical path connectivity
- In extreme cases, manually add bias points at problematic junctions using `biasController.AddJunctionPoint()`

### Problem: Inconsistent Frame Rate or Stuttering

**Solution**:
- Reduce the frame budget in ChunkedProcessingSystem (try 4-6ms instead of 8ms)
- Enable `adaptiveProcessing` in ChunkedProcessingSystem
- Set `vectorFieldStorage.asyncCPUUpdate = true` to prevent synchronous readbacks
- Check for other systems running CPU-intensive tasks in the same frames
- Use the Unity Profiler to identify frame spikes - they may not be from the pathfinding system

### Problem: High Memory Usage

**Solution**:
- Check current memory usage with `vectorFieldStorage.GetMemoryUsageMB()`
- Reduce memory with these steps (in order of impact):
  1. Set `vectorFieldStorage.cacheAllResolutionLevels = false` (~30% reduction)
  2. Reduce base resolution in MipmapGenerator (12,000 → 8,000 or 6,000)
  3. Reduce mipmap levels from 5 to 4 or 3
  4. Use R8 format instead of RG16 for vector field storage by setting `vectorFieldGenerator.useCompressedFormat = true`

### Problem: Slow Initial Processing / Long Startup Time

**Solution**:
- Implement a loading screen during initialization
- Temporarily increase frame budget during initialization with `chunkProcessor.SetFrameBudget(12.0f)`
- Prioritize initial processing with `chunkProcessor.SetInitialProcessingPriority(true)`
- Show progress with `float progress = chunkProcessor.GetChunkProgress()`
- For development: use a smaller test navigation area until ready for production

### Problem: Agents Not Following Optimal Paths

**Solution**:
- Increase `propagationStagesPerLevel` in MultiResolutionVectorFieldGenerator (2→3 or 4)
- Adjust `falloffRate` to be closer to 1.0 (try 0.98 instead of 0.95)
- Verify resolution bias is correctly applied at junctions with `biasController.VisualizeJunctions = true`
- Check that navigation mesh has accurate NavMesh Agent settings for your characters
- Add more target points to create a more granular guidance system

### Problem: Poor Performance with Many Agents

**Solution**:
- Implement batch queries with `querySystem.GetFlowDirectionBatch()`
- Stagger agent updates (not all agents need direction every frame)
- Use `AgentQuerySystem.GetFlowDirectionBatch()` with pre-allocated arrays
- Lower the importance value for background agents (0.5 or less)
- Implement a simple agent LOD system based on distance or visibility

## Extending the System

### Custom Agent Behaviors

Extend the basic agent controller with more advanced behaviors:

```csharp
public class AdvancedAgentController : AgentController
{
    [SerializeField] private float avoidanceRadius = 2.0f;
    [SerializeField] private float avoidanceWeight = 0.5f;
    
    protected override Vector3 GetMovementDirection()
    {
        // Get base direction from pathfinding
        Vector3 pathDirection = base.GetMovementDirection();
        
        // Add agent avoidance
        Vector3 avoidanceDirection = GetAvoidanceDirection();
        
        // Combine directions
        return Vector3.Normalize(pathDirection + avoidanceDirection * avoidanceWeight);
    }
    
    private Vector3 GetAvoidanceDirection()
    {
        // Implement simple agent avoidance logic
        Vector3 avoidance = Vector3.zero;
        
        Collider[] nearbyAgents = Physics.OverlapSphere(transform.position, avoidanceRadius, LayerMask.GetMask("Agent"));
        
        foreach (var agentCollider in nearbyAgents)
        {
            if (agentCollider.gameObject != gameObject)
            {
                Vector3 awayDirection = transform.position - agentCollider.transform.position;
                float distance = awayDirection.magnitude;
                
                // Closer agents have more influence
                float weight = 1.0f - (distance / avoidanceRadius);
                avoidance += awayDirection.normalized * weight;
            }
        }
        
        return avoidance.normalized;
    }
}
```

### Custom Resolution Bias

Implement a custom bias provider to add gameplay-driven bias factors:

```csharp
public class GameplayBiasProvider : MonoBehaviour
{
    [SerializeField] private ResolutionBiasController biasController;
    [SerializeField] private Transform player;
    [SerializeField] private float playerBiasRadius = 10.0f;
    [SerializeField] private float playerBiasStrength = 2.0f;
    
    private void Update()
    {
        // Add custom bias around player
        if (player != null && biasController != null)
        {
            // Custom implementation to add bias around player
            // This would require exposing an AddCustomBias method in ResolutionBiasController
        }
    }
}
```

## Conclusion

The Mipmap Pathfinding System provides a scalable, high-performance solution for navigation in large environments with many agents. By using a multi-resolution approach and distributing computation across frames, it maintains consistent performance while delivering high-quality pathfinding results.

For optimal results:
1. Configure the system based on your specific environment size and agent count
2. Adjust resolution bias to focus computational resources where they matter most
3. Fine-tune the frame budget to balance pathfinding quality with overall performance
4. Use batch operations for large agent counts
5. Monitor and optimize memory usage for your target platforms
