# Chunked Processing System API Documentation

The `ChunkedProcessingSystem` class distributes vector field computation across multiple frames for consistent performance. It manages work distribution by dividing the navigation space into chunks, prioritizing them based on various metrics, and processing them within a configurable frame budget.

## Setup Guide

### 1. Add the Components

Create a GameObject in your scene that will host the pathfinding system components. Add these components in order:

1. MipmapGenerator
2. ResolutionBiasController
3. MultiResolutionVectorFieldGenerator
4. ChunkedProcessingSystem
5. PathfindingManager (optional, but recommended for easy coordination)

### 2. Configure References

For each component, set up the necessary references:

- MipmapGenerator:
  - Assign the `boundsCalculatorShader` to your BoundsCalculator_Optimized compute shader
  - Configure base width/height (default: 12,000 × 6,000)

- ResolutionBiasController:
  - Set reference to the MipmapGenerator
  - Configure bias parameters (bias radius, strength, junction detection settings)

- MultiResolutionVectorFieldGenerator:
  - Set references to MipmapGenerator and ResolutionBiasController
  - Configure propagation settings

- ChunkedProcessingSystem:
  - Set references to MipmapGenerator, ResolutionBiasController, and MultiResolutionVectorFieldGenerator
  - Configure chunk size (recommended: 256)
  - Set frame budget (recommended: 8ms for 60 FPS)
  - Adjust priority weights as needed

### 3. Initialization

Initialize the components in your game startup sequence (for example, in the Start method of PathfindingManager):

```csharp
// Initialize in correct order
mipmapGenerator.Initialize();
yield return null; // Optional: Wait a frame to prevent UI freezing

biasController.Initialize();
yield return null;

vectorFieldGenerator.Initialize();
yield return null;

chunkProcessor.Initialize();
```

### 4. Set Up Targets

Define the target positions that agents will navigate toward:

```csharp
// Create array of target positions
Vector3[] targetPositions = { target1.position, target2.position };

// Set targets in the chunked processing system
chunkProcessor.SetTargets(targetPositions);
```

### 5. Update Loop

Call the ChunkedProcessingSystem's Update method in your main update loop:

```csharp
void Update()
{
    // Process chunks within frame budget
    chunkProcessor.Update();
    
    // Optional: Update UI progress
    progressBar.value = chunkProcessor.GetChunkProgress();
}
```

### 6. Agent Integration

For each agent that will use the pathfinding system:

1. Implement the IPathfindingAgent interface
2. Register the agent with the chunked processing system
3. Sample the vector field for movement directions

```csharp
// Register agent
chunkProcessor.RegisterAgent(agent);

// In agent update loop
Vector2 flowDirection = vectorFieldGenerator.SampleVectorField(transform.position);
Vector3 moveDirection = new Vector3(flowDirection.x, 0, flowDirection.y);
```

### 7. Dynamic Obstacle Handling

When obstacles move or change, mark affected areas as dirty:

```csharp
// When an obstacle moves
Vector3 obstaclePosition = obstacle.transform.position;
float obstacleRadius = obstacle.GetComponent<Collider>().bounds.extents.magnitude;
chunkProcessor.MarkChunkDirty(obstaclePosition, obstacleRadius);
```

### Performance Tips

- **Chunk Size**: Larger chunks mean fewer total chunks but more work per chunk. Find the right balance for your scene.
- **Frame Budget**: Lower values (4-8ms) prioritize frame rate, higher values prioritize faster updates.
- **Adaptive Processing**: Enable this option to automatically adjust chunk processing based on performance.
- **Resolution Bias**: Configure the bias controller to focus computational resources on critical areas.
- **Agent Density**: If you have many agents in specific areas, adjust priority weights to prioritize those areas.

## Class Location

```
MipmapPathfinding.ChunkedProcessingSystem
```

## Public Methods

### Initialize

```csharp
public void Initialize()
```

**Purpose:** Initializes the chunked processing system and sets up the initial chunk distribution.

**Usage:**
- Call this method after the MipmapGenerator, ResolutionBiasController, and MultiResolutionVectorFieldGenerator have been initialized
- Divides the navigation space into chunks based on the configured chunk size
- Sets up the priority queue and frame budget tracking

**Requirements:**
- The `mipmapGenerator`, `biasController`, and `vectorFieldGenerator` references must be assigned
- The system should be initialized before the Update loop begins

**Example:**
```csharp
ChunkedProcessingSystem chunkProcessor = GetComponent<ChunkedProcessingSystem>();
chunkProcessor.Initialize();
```

### SetTargets

```csharp
public void SetTargets(Vector3[] targetPositions)
```

**Purpose:** Sets the target positions that agents will navigate towards and marks affected chunks as dirty.

**Parameters:**
- `targetPositions`: Array of world-space positions representing navigation targets

**Usage:**
- Call this when targets change to update the vector field generator
- Marks chunks near targets as high priority for processing
- Propagates the target updates to the vector field generator

**Example:**
```csharp
// Set active targets
Vector3[] targetPositions = { target1.position, target2.position };
chunkProcessor.SetTargets(targetPositions);
```

### MarkChunkDirty

```csharp
public void MarkChunkDirty(Vector3 worldPosition, float radius)
```

**Purpose:** Marks chunks within the specified radius as dirty and needing reprocessing.

**Parameters:**
- `worldPosition`: The world-space position center of the affected area
- `radius`: World-space radius of the affected area

**Usage:**
- Call this when dynamic obstacles change or when other events affect pathfinding
- Efficiently flags only the necessary chunks for reprocessing
- Adds dirty chunks to the priority queue

**Example:**
```csharp
// When a dynamic obstacle moves
Vector3 obstaclePosition = obstacle.transform.position;
float obstacleRadius = obstacle.GetComponent<Collider>().bounds.extents.magnitude;
chunkProcessor.MarkChunkDirty(obstaclePosition, obstacleRadius);
```

### Update

```csharp
public void Update()
```

**Purpose:** Processes chunks within the frame budget during the update loop.

**Usage:**
- Call this method from the MonoBehaviour Update method
- Processes as many chunks as possible within the frame budget
- Prioritizes chunks based on agent density, target proximity, and time since update

**Notes:**
- Automatically adapts the number of chunks processed based on available frame time
- Monitors performance to maintain the target frame rate

**Example:**
```csharp
void Update()
{
    chunkProcessor.Update();
}
```

### GetChunkProgress

```csharp
public float GetChunkProgress()
```

**Purpose:** Returns the overall completion percentage of chunk processing.

**Returns:**
- A float between 0 and 1 representing the progress (1 = all chunks processed)

**Usage:**
- Used for UI feedback or debugging
- Can be used to determine if initial processing is complete

**Example:**
```csharp
float progress = chunkProcessor.GetChunkProgress();
progressBar.value = progress;
```

### GetAgentDensity

```csharp
public float GetAgentDensity(Vector3 worldPosition)
```

**Purpose:** Returns the agent density at a specific world position.

**Parameters:**
- `worldPosition`: The world-space position to check

**Returns:**
- A float representing the local agent density

**Usage:**
- Used internally for chunk prioritization
- Can be called to visualize agent density

**Example:**
```csharp
float density = chunkProcessor.GetAgentDensity(transform.position);
```

### SetFrameBudget

```csharp
public void SetFrameBudget(float milliseconds)
```

**Purpose:** Sets the target frame budget for chunk processing.

**Parameters:**
- `milliseconds`: Maximum time in milliseconds to spend on chunk processing per frame

**Usage:**
- Call to adjust performance characteristics at runtime
- Lower values prioritize frame rate, higher values prioritize processing speed

**Example:**
```csharp
// Adjust for performance
chunkProcessor.SetFrameBudget(8.0f); // 8ms (half of 16.7ms for 60 FPS)
```

### RegisterAgent

```csharp
public void RegisterAgent(IPathfindingAgent agent)
```

**Purpose:** Registers an agent with the chunked processing system for density tracking.

**Parameters:**
- `agent`: An object implementing the IPathfindingAgent interface

**Usage:**
- Called when agents are created or activated
- Used to track agent density for chunk prioritization

**Example:**
```csharp
// When a new agent is created
AgentController agent = Instantiate(agentPrefab).GetComponent<AgentController>();
chunkProcessor.RegisterAgent(agent);
```

### UnregisterAgent

```csharp
public void UnregisterAgent(IPathfindingAgent agent)
```

**Purpose:** Removes an agent from the density tracking system.

**Parameters:**
- `agent`: The agent to remove

**Usage:**
- Called when agents are destroyed or deactivated
- Ensures accurate agent density tracking

**Example:**
```csharp
// When an agent is destroyed
chunkProcessor.UnregisterAgent(agent);
Destroy(agent.gameObject);
```

## Properties

### IsInitialized

```csharp
public bool IsInitialized { get; }
```

**Purpose:** Indicates whether the chunked processing system has been initialized.

**Type:** Boolean

**Usage:**
- Use to check if the system is ready before calling other methods
- Return value of true indicates the system is fully operational

**Example:**
```csharp
if (chunkProcessor.IsInitialized)
{
    // Safe to use other methods
}
```

### IsBusy

```csharp
public bool IsBusy { get; }
```

**Purpose:** Indicates whether the system is currently processing chunks.

**Type:** Boolean

**Usage:**
- Use to check if the system is actively working
- Can be used to determine if initial processing is complete

**Example:**
```csharp
// Wait until initial processing is complete
if (!chunkProcessor.IsBusy)
{
    // Safe to spawn agents
    SpawnAgents();
}
```

## Inspector Properties

| Property | Type | Description |
|----------|------|-------------|
| `mipmapGenerator` | MipmapGenerator | Reference to the MipmapGenerator component |
| `biasController` | ResolutionBiasController | Reference to the ResolutionBiasController component |
| `vectorFieldGenerator` | MultiResolutionVectorFieldGenerator | Reference to the MultiResolutionVectorFieldGenerator component |
| `chunkSize` | int | Size of each chunk in pixels at the base resolution |
| `frameBudgetMs` | float | Maximum time in milliseconds to spend on chunk processing per frame |
| `adaptiveProcessing` | bool | If true, automatically adjusts the number of chunks processed per frame |
| `visualizeChunks` | bool | If true, displays debug visualization of chunks and their priorities |
| `agentDensityWeight` | float | Weight of agent density in the priority calculation |
| `targetProximityWeight` | float | Weight of target proximity in the priority calculation |
| `timeSinceUpdateWeight` | float | Weight of time since last update in the priority calculation |

## Integration with Other Components

The ChunkedProcessingSystem depends on:
- `MipmapGenerator`: For accessing the navigation space dimensions
- `ResolutionBiasController`: For determining the appropriate resolution levels to process
- `MultiResolutionVectorFieldGenerator`: For executing the actual vector field generation

The ChunkedProcessingSystem is used by:
- Agent controllers that implement the IPathfindingAgent interface
- Other systems that need to trigger pathfinding updates (e.g., obstacle managers)

## Usage Example

```csharp
// Setup and initialization
MipmapGenerator mipmapGenerator = GetComponent<MipmapGenerator>();
ResolutionBiasController biasController = GetComponent<ResolutionBiasController>();
MultiResolutionVectorFieldGenerator vectorFieldGenerator = GetComponent<MultiResolutionVectorFieldGenerator>();

mipmapGenerator.Initialize();
biasController.Initialize();
vectorFieldGenerator.Initialize();

ChunkedProcessingSystem chunkProcessor = GetComponent<ChunkedProcessingSystem>();
chunkProcessor.Initialize();

// Set initial targets
Vector3[] targetPositions = { target1.position, target2.position };
chunkProcessor.SetTargets(targetPositions);

// In the update loop
void Update()
{
    // Process chunks within frame budget
    chunkProcessor.Update();
    
    // Update UI progress indicator
    progressBar.value = chunkProcessor.GetChunkProgress();
    
    // When a dynamic obstacle moves
    if (obstacleHasMoved)
    {
        chunkProcessor.MarkChunkDirty(obstacle.position, obstacle.radius);
    }
    
    // When a new target becomes active
    if (newTargetActivated)
    {
        Vector3[] updatedTargets = GetActiveTargets();
        chunkProcessor.SetTargets(updatedTargets);
    }
}
```

## Notes

- The ChunkedProcessingSystem is designed to balance performance and responsiveness by distributing work across frames.
- Chunks are prioritized based on a combination of agent density, target proximity, and time since last update.
- The system adapts to performance conditions by adjusting the number of chunks processed per frame.
- Optimal chunk size depends on navigation space complexity and target hardware performance.
- To disable the system temporarily, set frameBudgetMs to 0.
