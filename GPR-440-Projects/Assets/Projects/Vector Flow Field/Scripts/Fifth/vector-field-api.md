# Multi-Resolution Vector Field Generator API

The `MultiResolutionVectorFieldGenerator` is a key component of the Mipmap Pathfinding System. It executes the light propagation algorithm at multiple resolution levels to efficiently generate vector fields that agents can follow to navigate toward targets.

## Setup Instructions

### 1. Required Components

Before setting up the MultiResolutionVectorFieldGenerator, ensure you have:
- A configured `MipmapGenerator` component
- A configured `ResolutionBiasController` component
- The VectorFieldPropagation compute shader in your project's Resources folder

### 2. Component Installation

1. **Add the component to your pathfinding manager GameObject**:
   ```csharp
   var vectorFieldGenerator = pathfindingManager.AddComponent<MultiResolutionVectorFieldGenerator>();
   ```

2. **Assign dependencies**:
   ```csharp
   vectorFieldGenerator.mipmapGenerator = GetComponent<MipmapGenerator>();
   vectorFieldGenerator.biasController = GetComponent<ResolutionBiasController>();
   vectorFieldGenerator.propagationShader = Resources.Load<ComputeShader>("VectorFieldPropagation");
   ```

3. **Configure parameters** (or adjust in the Inspector):
   ```csharp
   vectorFieldGenerator.propagationStagesPerLevel = 2;
   vectorFieldGenerator.falloffRate = 0.95f;
   vectorFieldGenerator.targetRadius = 1.0f;
   vectorFieldGenerator.interpolateBetweenLevels = true;
   vectorFieldGenerator.interpolationBlendFactor = 0.5f;
   ```

### 3. Initialization Order

Always initialize components in the correct order:
1. MipmapGenerator
2. ResolutionBiasController
3. MultiResolutionVectorFieldGenerator

Example:
```csharp
void Start()
{
    // Initialize in correct order
    mipmapGenerator.Initialize();
    biasController.Initialize();
    vectorFieldGenerator.Initialize();
    
    // Set initial targets
    Vector3[] initialTargets = { target1.position, target2.position };
    vectorFieldGenerator.SetTargets(initialTargets);
}
```

### 4. Verify Setup

Check if initialization was successful:
```csharp
if (vectorFieldGenerator.GetResolutionLevelCount() > 0)
{
    Debug.Log("Vector field generator initialized successfully with " + 
              vectorFieldGenerator.GetResolutionLevelCount() + " resolution levels");
}
else
{
    Debug.LogError("Vector field generator initialization failed!");
}
```

## Public API Reference

### Initialize

```csharp
public void Initialize()
```

**Purpose:** Initializes the vector field generator and creates textures for each resolution level.

**Usage:**
- Call this method after the MipmapGenerator and ResolutionBiasController have been initialized
- Creates vector field textures for each resolution level
- Sets up internal data structures and state

**Requirements:**
- The `mipmapGenerator` and `biasController` references must be assigned
- The `propagationShader` compute shader must be assigned

**Example:**
```csharp
MultiResolutionVectorFieldGenerator vectorFieldGenerator = GetComponent<MultiResolutionVectorFieldGenerator>();
vectorFieldGenerator.Initialize();
```

### SetTargets

```csharp
public void SetTargets(Vector3[] targetPositions)
```

**Purpose:** Sets the target positions that agents will navigate towards.

**Parameters:**
- `targetPositions`: Array of world-space positions representing navigation targets

**Usage:**
- Call this when targets change
- Clears existing vector fields and sets new targets at each resolution level
- Initializes the propagation algorithm starting points

**Example:**
```csharp
// Set active targets
Vector3[] targetPositions = { target1.position, target2.position };
vectorFieldGenerator.SetTargets(targetPositions);
```

### ProcessChunk

```csharp
public void ProcessChunk(ChunkData chunk)
```

**Purpose:** Processes vector field generation for a specific chunk of the navigation space.

**Parameters:**
- `chunk`: A ChunkData object representing the chunk to process

**Usage:**
- Called by the chunked processing system to distribute work across frames
- Uses a coarse-to-fine approach starting at the lowest resolution
- Processes each level based on the resolution bias

**Example:**
```csharp
// In the chunked processing system
ChunkData chunk = new ChunkData(pixelMin, pixelSize, worldCenter);
vectorFieldGenerator.ProcessChunk(chunk);
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
- Returns `null` if the level is invalid or the generator is not initialized

**Usage:**
- Used by the vector field storage component and for visualization
- Level 0 is the highest resolution, higher numbers are lower resolutions

**Example:**
```csharp
// Get the quarter resolution vector field (Level 2)
RenderTexture vectorField = vectorFieldGenerator.GetVectorFieldTexture(2);
```

### GetResolutionLevelCount

```csharp
public int GetResolutionLevelCount()
```

**Purpose:** Returns the number of resolution levels available.

**Returns:**
- The number of resolution levels generated (typically 5)
- Returns 0 if the generator is not initialized

**Example:**
```csharp
int levelCount = vectorFieldGenerator.GetResolutionLevelCount();
Debug.Log($"Available resolution levels: {levelCount}");
```

### SampleVectorField

```csharp
public Vector2 SampleVectorField(Vector3 worldPosition, bool useHighestResolution = false)
```

**Purpose:** Samples the vector field at a specific world position.

**Parameters:**
- `worldPosition`: The world-space position to sample
- `useHighestResolution`: If true, always uses the highest resolution; otherwise uses bias to determine level

**Returns:**
- A normalized Vector2 representing the flow direction
- Returns Vector2.zero if the generator is not initialized

**Usage:**
- Used by agents to determine which direction to move
- Typically called by the Agent Query System

**Example:**
```csharp
// In the agent movement controller
Vector3 agentPosition = transform.position;
Vector2 flowDirection = vectorFieldGenerator.SampleVectorField(agentPosition);
Vector3 moveDirection = new Vector3(flowDirection.x, 0, flowDirection.y);
```

The MultiResolutionVectorFieldGenerator uses your existing ChunkData class to define regions of the navigation space for processing. It expects the ChunkData class to have the following properties and methods:

- `WorldCenter`: A Vector3 property representing the world-space center of the chunk
- `GetTextureMin(int level)`: Returns the minimum texture coordinates at a specific resolution level
- `GetTextureMax(int level)`: Returns the maximum texture coordinates at a specific resolution level

The ChunkData class that you provided includes all of these required methods and properties, so it will work seamlessly with the MultiResolutionVectorFieldGenerator.

## Integration with Other Components

The MultiResolutionVectorFieldGenerator depends on:
- `MipmapGenerator`: For accessing the navigation textures at different resolutions
- `ResolutionBiasController`: For determining the appropriate resolution levels to process

The MultiResolutionVectorFieldGenerator is used by:
- `ChunkedProcessingSystem`: To distribute work across frames
- `VectorFieldStorage`: To store and provide access to the vector field data
- `AgentQuerySystem`: For providing navigation directions to agents
