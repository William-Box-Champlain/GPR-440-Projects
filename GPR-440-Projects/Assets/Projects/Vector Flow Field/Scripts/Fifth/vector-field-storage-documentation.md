# Vector Field Storage API Documentation

The `VectorFieldStorage` class efficiently stores and provides access to the multi-resolution vector field data, with CPU-side caching for rapid agent queries. It manages the data transfer between GPU and CPU and optimizes memory usage while maintaining performance.

## Setup Instructions

### 1. Required Files

Ensure you have the following files in your project:
- `VectorFieldStorage.cs` - The main component implementation
- `VectorFieldStorageInspector.cs` - Custom inspector for debugging (place this in an Editor folder)

### 2. Component Dependencies

The Vector Field Storage system depends on:
- `MipmapGenerator` - For accessing navigation mesh data and resolution information
- `MultiResolutionVectorFieldGenerator` - For accessing the generated vector field textures

### 3. Component Setup

1. Add the `VectorFieldStorage` component to your pathfinding manager GameObject:
   ```csharp
   // In your initialization code
   VectorFieldStorage vectorFieldStorage = gameObject.AddComponent<VectorFieldStorage>();
   ```
   
   Or add it through the Unity Editor:
   - Select your GameObject that has the MipmapGenerator and MultiResolutionVectorFieldGenerator
   - Click "Add Component" in the Inspector
   - Type "Vector Field Storage" and select it

2. Assign required references in the Inspector:
   - **Mipmap Generator**: Drag your MipmapGenerator component here
   - **Vector Field Generator**: Drag your MultiResolutionVectorFieldGenerator component here

3. Configure CPU cache settings:
   - **Enable CPU Cache**: Toggle for faster agent queries (recommended for most scenarios)
   - **Async CPU Update**: Enable for better frame rate stability during updates
   - **CPU Cache Update Interval**: Time between cache updates (lower = more frequent updates)
   - **Cache All Resolution Levels**: Toggle to cache all levels or just the base resolution

### 4. Initialization Order

The VectorFieldStorage must be initialized after the MipmapGenerator and MultiResolutionVectorFieldGenerator:

```csharp
void Start()
{
    // Initialize components in order
    mipmapGenerator.Initialize();
    vectorFieldGenerator.Initialize();
    vectorFieldStorage.Initialize();
}
```

### 5. Integration with Agent Query System

Create a simple query system that uses the VectorFieldStorage:

```csharp
public class AgentQuerySystem : MonoBehaviour
{
    [SerializeField] private VectorFieldStorage vectorFieldStorage;
    
    public Vector3 GetDirectionForAgent(Vector3 position)
    {
        // Sample the vector field at agent position
        Vector2 direction2D = vectorFieldStorage.SampleVectorField(position);
        
        // Convert to 3D movement direction (XZ plane)
        return new Vector3(direction2D.x, 0, direction2D.y);
    }
}
```

### 6. Update Cycle

Make sure the VectorFieldStorage is updated each frame to handle CPU cache updates:

```csharp
void Update()
{
    // Update the vector field storage 
    vectorFieldStorage.Update();
}

## Class Location

```
MipmapPathfinding.VectorFieldStorage
```

## Memory Management

### Memory Optimization Techniques

The VectorFieldStorage uses several techniques to optimize memory usage:

1. **Resolution-Based Memory Allocation**:
   - Lower resolution levels use significantly less memory (1/4 per level reduction)
   - Typically uses ~35MB total for all resolution levels in a 12,000 × 6,000 navigation space

2. **Selective CPU Caching**:
   - Option to cache only the highest resolution level (level 0)
   - Reduces CPU memory usage by ~10-15% with minimal performance impact
   - Useful for memory-constrained platforms

3. **Efficient Texture Format**:
   - Uses RG8 format (2 components, 8-bit per component) for direction vectors
   - No magnitude storage since only normalized directions are needed
   - Represents normalized 2D vectors in minimal space

4. **Asynchronous Updates**:
   - Updates CPU cache asynchronously to avoid frame rate hitches
   - Spreads update cost across multiple frames
   - Only updates regions that have changed

### Memory Usage Guidelines

Based on typical configurations:
- **Full System (5 resolution levels)**:
  - GPU Memory: ~12MB for vector field textures
  - CPU Memory: ~20MB for cached data (~8MB if only caching level 0)
  - Total: ~32MB (~20MB with minimal caching)

- **Memory vs Performance Tradeoffs**:
  - Maximum Performance: Cache all resolution levels (~32MB total)
  - Balanced: Cache only level 0 (~20MB total)
  - Minimum Memory: Disable CPU cache entirely (~12MB total)

## Troubleshooting

### Common Issues

1. **System Not Initialized**:
   - Check initialization order (Mipmap → VectorField → VectorFieldStorage)
   - Verify required component references are assigned
   - Check console for initialization errors

2. **Performance Issues**:
   - Enable asynchronous CPU updates for smoother frame rates
   - Reduce the number of cached resolution levels
   - Increase the CPU cache update interval

3. **Vector Field Quality Issues**:
   - Ensure vector field generator is providing valid textures
   - Check if all levels are being generated correctly
   - Verify that the CPU cache is being updated when fields change

4. **Memory Problems**:
   - Disable caching of all resolution levels
   - Increase update interval to reduce memory pressure
   - Consider disabling CPU caching entirely if necessary

### Debug Visualization

The included VectorFieldStorageInspector provides several debugging tools:

1. **Memory Usage Monitoring**:
   - Displays total memory usage in MB
   - Shows breakdown of GPU vs CPU usage
   - Provides memory optimization toggles

2. **Performance Analysis**:
   - Measures query times for performance profiling
   - Shows average and per-frame query costs
   - Visualizes performance trends over time

3. **Vector Field Visualization**:
   - Displays vector field textures for each resolution level
   - Shows direction vectors and field strength
   - Helps identify issues with field generation

## Integration Example

Here's a complete integration example:

```csharp
using UnityEngine;
using MipmapPathfinding;

public class PathfindingManager : MonoBehaviour
{
    // Component references
    private MipmapGenerator mipmapGenerator;
    private ResolutionBiasController biasController;
    private MultiResolutionVectorFieldGenerator vectorFieldGenerator;
    private VectorFieldStorage vectorFieldStorage;
    private AgentQuerySystem agentQuerySystem;
    
    void Awake()
    {
        // Create components
        mipmapGenerator = gameObject.AddComponent<MipmapGenerator>();
        biasController = gameObject.AddComponent<ResolutionBiasController>();
        vectorFieldGenerator = gameObject.AddComponent<MultiResolutionVectorFieldGenerator>();
        vectorFieldStorage = gameObject.AddComponent<VectorFieldStorage>();
        agentQuerySystem = gameObject.AddComponent<AgentQuerySystem>();
        
        // Configure references
        biasController.mipmapGenerator = mipmapGenerator;
        vectorFieldGenerator.mipmapGenerator = mipmapGenerator;
        vectorFieldGenerator.biasController = biasController;
        vectorFieldStorage.mipmapGenerator = mipmapGenerator;
        vectorFieldStorage.vectorFieldGenerator = vectorFieldGenerator;
        agentQuerySystem.vectorFieldStorage = vectorFieldStorage;
    }
    
    void Start()
    {
        // Initialize components in correct order
        mipmapGenerator.Initialize();
        biasController.Initialize();
        vectorFieldGenerator.Initialize();
        vectorFieldStorage.Initialize();
        agentQuerySystem.Initialize();
        
        // Set initial targets
        Vector3[] targets = new Vector3[] { new Vector3(10, 0, 10) };
        vectorFieldGenerator.SetTargets(targets);
    }
    
    void Update()
    {
        // Update vector field storage (handles CPU cache updates)
        vectorFieldStorage.Update();
        
        // Update agent query system
        agentQuerySystem.Update();
    }
}

### Initialize

```csharp
public void Initialize()
```

**Purpose:** Initializes the vector field storage with data from the vector field generator.

**Usage:**
- Call this method after the MipmapGenerator and MultiResolutionVectorFieldGenerator have been initialized
- Sets up internal data structures for storing vector field data
- Creates CPU-side caches for efficient agent queries
- Should be called before any other methods

**Requirements:**
- The `mipmapGenerator` and `vectorFieldGenerator` references must be assigned

**Example:**
```csharp
VectorFieldStorage vectorFieldStorage = GetComponent<VectorFieldStorage>();
vectorFieldStorage.Initialize();
```

### SampleVectorField

```csharp
public Vector2 SampleVectorField(Vector3 worldPosition, int resolutionLevel = 0)
```

**Purpose:** Samples the vector field at a specific world position to get a direction vector.

**Parameters:**
- `worldPosition`: The world-space position to sample
- `resolutionLevel`: The resolution level to sample (0 = highest resolution)

**Returns:**
- A normalized Vector2 representing the flow direction at the specified position
- Returns Vector2.zero if the system is not initialized or the position is invalid

**Usage:**
- This is the primary method that agent query systems will call
- Uses bilinear interpolation for smooth direction values
- Automatically uses CPU cache when available for better performance

**Example:**
```csharp
// In the agent query system
Vector3 agentPosition = agent.transform.position;
Vector2 flowDirection = vectorFieldStorage.SampleVectorField(agentPosition);
Vector3 moveDirection = new Vector3(flowDirection.x, 0, flowDirection.y);
```

### UpdateCPUCacheImmediate

```csharp
public void UpdateCPUCacheImmediate(int level)
```

**Purpose:** Forces an immediate update of the CPU-side cache for a specific resolution level.

**Parameters:**
- `level`: The resolution level to update (0 = highest resolution)

**Usage:**
- Call this when vector fields have changed significantly
- Updates the CPU cache with the latest data from the GPU texture
- May cause a brief performance spike on large textures

**Example:**
```csharp
// When targets have changed significantly
vectorFieldStorage.UpdateCPUCacheImmediate(0); // Update base resolution level
```

### UpdateCPUCacheAsync

```csharp
public async void UpdateCPUCacheAsync(int level)
```

**Purpose:** Starts an asynchronous update of the CPU cache for a specific resolution level.

**Parameters:**
- `level`: The resolution level to update (0 = highest resolution)

**Usage:**
- Use this instead of `UpdateCPUCacheImmediate` to avoid performance spikes
- Spreads CPU cache updating across multiple frames
- Less likely to cause framerate issues than immediate updates

**Example:**
```csharp
// When vector fields have changed but immediate update is not critical
vectorFieldStorage.UpdateCPUCacheAsync(0);
```

### MarkCacheDirty

```csharp
public void MarkCacheDirty(int level)
```

**Purpose:** Marks a resolution level's CPU cache as dirty, triggering an update during the next Update cycle.

**Parameters:**
- `level`: The resolution level to mark as dirty (0 = highest resolution)

**Usage:**
- Call this when vector fields have been modified
- Updates will occur based on the cache update interval
- Less immediate than `UpdateCPUCache` methods but more efficient overall

**Example:**
```csharp
// After vector field generator has processed a chunk
vectorFieldStorage.MarkCacheDirty(affectedLevel);
```

### MarkAllCachesDirty

```csharp
public void MarkAllCachesDirty()
```

**Purpose:** Marks all CPU caches as dirty, triggering updates for all resolution levels.

**Usage:**
- Call this when major changes affect all vector fields
- Updates will occur gradually based on the cache update interval
- Use when targets change or when the entire vector field is regenerated

**Example:**
```csharp
// When targets have changed significantly
vectorFieldStorage.MarkAllCachesDirty();
```

### GetVectorFieldTexture

```csharp
public RenderTexture GetVectorFieldTexture(int level)
```

**Purpose:** Retrieves the vector field texture for a specific resolution level.

**Parameters:**
- `level`: The resolution level to retrieve (0 = highest resolution)

**Returns:**
- The RenderTexture containing the vector field for the specified level
- Returns `null` if the level is invalid or the system is not initialized

**Usage:**
- Used for visualization or custom GPU-based sampling
- Can be used to implement shader-based agent movement

**Example:**
```csharp
// For vector field visualization
RenderTexture vectorFieldTexture = vectorFieldStorage.GetVectorFieldTexture(0);
visualizationMaterial.SetTexture("_VectorField", vectorFieldTexture);
```

### GetResolutionLevelCount

```csharp
public int GetResolutionLevelCount()
```

**Purpose:** Returns the number of resolution levels available.

**Returns:**
- The number of resolution levels in the vector field storage
- Returns 0 if the system is not initialized

**Example:**
```csharp
int levelCount = vectorFieldStorage.GetResolutionLevelCount();
Debug.Log($"Available resolution levels: {levelCount}");
```

### GetMemoryUsageMB

```csharp
public float GetMemoryUsageMB()
```

**Purpose:** Gets the approximate memory usage of the vector field storage in megabytes.

**Returns:**
- Combined GPU and CPU memory usage in MB
- Returns 0 if the system is not initialized

**Usage:**
- Use for memory profiling and optimization
- Helps tracking memory usage in performance-critical applications

**Example:**
```csharp
float memoryUsage = vectorFieldStorage.GetMemoryUsageMB();
Debug.Log($"Vector field storage using {memoryUsage:F2} MB of memory");
```

### SetCPUCacheEnabled

```csharp
public void SetCPUCacheEnabled(bool enabled)
```

**Purpose:** Sets whether CPU caching is enabled.

**Parameters:**
- `enabled`: Whether to enable CPU caching

**Usage:**
- Can be toggled at runtime to adjust memory usage vs. performance
- When disabled, vector field queries will use direct GPU sampling (slower)
- When enabled, queries can use the faster CPU cache

**Example:**
```csharp
// On low-memory devices
vectorFieldStorage.SetCPUCacheEnabled(false);

// On high-performance devices
vectorFieldStorage.SetCPUCacheEnabled(true);
```

### SetCacheAllLevels

```csharp
public void SetCacheAllLevels(bool cacheAll)
```

**Purpose:** Sets whether to cache all resolution levels or just the base level.

**Parameters:**
- `cacheAll`: Whether to cache all levels

**Usage:**
- When true, all resolution levels are cached in CPU memory (higher memory usage)
- When false, only the base (highest resolution) level is cached (lower memory usage)
- Can be adjusted at runtime to balance memory usage vs. performance

**Example:**
```csharp
// For memory-constrained scenarios
vectorFieldStorage.SetCacheAllLevels(false);

// For performance-critical scenarios
vectorFieldStorage.SetCacheAllLevels(true);
```

## Inspector Properties

| Property | Type | Description |
|----------|------|-------------|
| `mipmapGenerator` | MipmapGenerator | Reference to the MipmapGenerator component |
| `vectorFieldGenerator` | MultiResolutionVectorFieldGenerator | Reference to the MultiResolutionVectorFieldGenerator component |
| `enableCPUCache` | bool | Whether to enable CPU-side caching for faster agent queries |
| `asyncCPUUpdate` | bool | Whether to update CPU caches asynchronously to avoid performance spikes |
| `cpuCacheUpdateInterval` | float | Minimum time in seconds between CPU cache updates |
| `cacheAllResolutionLevels` | bool | If true, caches all resolution levels; if false, only caches the base level |

## Integration with Other Components

The VectorFieldStorage depends on:
- `MipmapGenerator`: For accessing navigation bounds and resolution information
- `MultiResolutionVectorFieldGenerator`: For accessing the generated vector field textures

The VectorFieldStorage is used by:
- `AgentQuerySystem`: For providing efficient vector field sampling to agents
- Visualization systems: For debugging and displaying vector fields

## Usage Example

```csharp
// Setup and initialization
MipmapGenerator mipmapGenerator = GetComponent<MipmapGenerator>();
MultiResolutionVectorFieldGenerator vectorFieldGenerator = GetComponent<MultiResolutionVectorFieldGenerator>();
VectorFieldStorage vectorFieldStorage = GetComponent<VectorFieldStorage>();

// Initialize components in correct order
mipmapGenerator.Initialize();
vectorFieldGenerator.Initialize();
vectorFieldStorage.Initialize();

// In the update loop
void Update()
{
    // Standard update to handle automatic cache updates
    vectorFieldStorage.Update();
    
    // When vector fields change significantly
    if (vectorFieldsRegenerated)
    {
        vectorFieldStorage.MarkAllCachesDirty();
    }
    
    // For agent queries in the agent query system
    foreach (var agent in agents)
    {
        Vector3 agentPosition = agent.transform.position;
        Vector2 flowDirection = vectorFieldStorage.SampleVectorField(agentPosition);
        agent.SetMoveDirection(new Vector3(flowDirection.x, 0, flowDirection.y));
    }
}
```

## Memory Optimization Notes

- The VectorFieldStorage uses the RG8 or RG16 texture format (2 components for direction)
- CPU caching can be selectively enabled/disabled for memory-constrained platforms
- Caching only the base resolution level reduces memory usage at slight performance cost
- Asynchronous cache updates help maintain smooth framerates
- Total memory usage is approximately (35MB for typical 5-level configuration):
  - GPU: ~12MB for all resolution levels (RG8 format)
  - CPU: ~23MB when caching all levels, ~20MB when caching only base level
