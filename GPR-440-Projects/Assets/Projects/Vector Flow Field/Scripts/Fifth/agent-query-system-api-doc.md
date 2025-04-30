# Agent Query System API Documentation

The `AgentQuerySystem` class provides a simple and efficient interface for agents to access the vector field data for navigation. It optimizes queries for performance and supports a large number of concurrent agents.

## Setup Guide

### Required Components

* `MipmapGenerator`: Provides the navigation boundaries and textures
* `VectorFieldStorage`: Stores the vector field data
* `ResolutionBiasController` (optional): Determines the appropriate resolution levels

### Initialization

The Agent Query System needs to be initialized after all other components. During initialization, it verifies that the required components are present and sets up internal data structures.

```csharp
// First, initialize all required components
mipmapGenerator.Initialize();
biasController.Initialize();
vectorFieldGenerator.Initialize();
vectorFieldStorage.Initialize();

// Then, initialize the agent query system
querySystem.Initialize();
```

If the dependencies are not properly set up or initialization fails, the system will log errors and will not function properly.

### Installation Steps

1. **Create the script**: 
   - Create a new C# script named `AgentQuerySystem.cs` in your project
   - Place it in the same namespace as your other Mipmap Pathfinding components

2. **Add to your scene**:
   - Add the AgentQuerySystem component to the same GameObject that contains your other pathfinding components
   - Alternatively, create a dedicated GameObject for it named "AgentQuerySystem"

3. **Set up references**:
   - In the Inspector, assign references to the required components:
     - MipmapGenerator
     - ResolutionBiasController
     - VectorFieldStorage

4. **Configure settings**:
   - Adjust the Query Settings based on your needs:
     - Enable/disable batch queries for performance optimization
     - Enable/disable resolution bias for adaptive resolution
     - Enable/disable result caching for performance

5. **Initialize in the correct order**:
   - Ensure all components are initialized in the correct sequence:
     1. MipmapGenerator.Initialize()
     2. ResolutionBiasController.Initialize()
     3. MultiResolutionVectorFieldGenerator.Initialize()
     4. VectorFieldStorage.Initialize()
     5. AgentQuerySystem.Initialize()

### Agent Integration

To use the AgentQuerySystem with your agents:

1. **Implement the IPathfindingAgent interface** in your agent controller class:
   ```csharp
   public class AgentController : MonoBehaviour, IPathfindingAgent
   {
       // Required interface implementations
       public Vector3 GetPosition() => transform.position;
       public float GetImportance() => importance;
   }
   ```

2. **Register agents with the system**:
   ```csharp
   // In your agent's Start method
   agentQuerySystem.RegisterAgent(this);
   
   // In your agent's OnDestroy method
   agentQuerySystem.UnregisterAgent(this);
   ```

3. **Query for movement directions**:
   ```csharp
   // In your agent's Update method
   Vector3 moveDirection = agentQuerySystem.GetFlowDirection(transform.position, importance);
   
   // Apply movement
   transform.position += moveDirection * speed * Time.deltaTime;
   ```

### Performance Optimization

For optimal performance with large numbers of agents:

1. Use batch queries when managing groups of agents:
   ```csharp
   agentQuerySystem.GetFlowDirectionBatch(positions, results, importanceValues);
   ```

2. Enable result caching to reduce redundant calculations

3. Set appropriate agent importance values (0.0-1.0) to control resolution usage

4. Distribute agent updates across multiple frames when dealing with 500+ agents

## Class Location

```
MipmapPathfinding.AgentQuerySystem
```

## Public Methods

### Initialize

```csharp
public void Initialize()
```

**Purpose:** Initializes the agent query system and its connections to other system components.

**Usage:**
- Call this method after all required components (MipmapGenerator, ResolutionBiasController, VectorFieldStorage) have been initialized
- Sets up internal data structures for efficient agent queries
- Establishes connections to the vector field data sources

**Requirements:**
- The `mipmapGenerator`, `biasController`, and `vectorFieldStorage` references must be assigned

**Example:**
```csharp
AgentQuerySystem querySystem = GetComponent<AgentQuerySystem>();
querySystem.Initialize();
```

### GetFlowDirection

```csharp
public Vector3 GetFlowDirection(Vector3 worldPosition, float agentImportance = 1.0f)
```

**Purpose:** Returns the optimal movement direction for an agent at the specified position.

**Parameters:**
- `worldPosition`: The world-space position of the agent
- `agentImportance`: A value from 0.0 to 1.0 indicating the agent's importance (higher values may use higher resolution data)

**Returns:**
- A normalized Vector3 representing the optimal movement direction (XZ plane, Y = 0)
- Returns Vector3.zero if the system is not initialized or the position is invalid

**Usage:**
- The primary method agents will call to determine their movement direction
- Can be called every frame for each agent (optimized for performance)
- Considers resolution bias and agent importance when selecting data resolution

**Example:**
```csharp
// In the agent movement controller
Vector3 agentPosition = transform.position;
float importance = agent.GetImportance(); // 0.0 to 1.0
Vector3 moveDirection = querySystem.GetFlowDirection(agentPosition, importance);
agent.SetMoveDirection(moveDirection);
```

### GetFlowDirectionBatch

```csharp
public void GetFlowDirectionBatch(Vector3[] positions, Vector3[] results, float[] importanceValues = null)
```

**Purpose:** Efficiently retrieves movement directions for multiple agents in a single call.

**Parameters:**
- `positions`: Array of world-space positions for the agents
- `results`: Pre-allocated array that will be filled with the resulting direction vectors
- `importanceValues`: Optional array of importance values (0.0 to 1.0) for each agent

**Usage:**
- Use for significant performance gains when managing many agents
- More efficient than calling GetFlowDirection individually for each agent
- Optimizes memory access patterns for better cache utilization

**Notes:**
- The results array must be pre-allocated and the same length as the positions array
- If importanceValues is null, all agents use the default importance of 1.0

**Example:**
```csharp
// For a group of agents
Vector3[] agentPositions = new Vector3[agents.Length];
Vector3[] moveDirections = new Vector3[agents.Length];
float[] importanceValues = new float[agents.Length];

// Fill position and importance arrays
for (int i = 0; i < agents.Length; i++)
{
    agentPositions[i] = agents[i].transform.position;
    importanceValues[i] = agents[i].GetImportance();
}

// Get all directions in a single call
querySystem.GetFlowDirectionBatch(agentPositions, moveDirections, importanceValues);

// Apply the results
for (int i = 0; i < agents.Length; i++)
{
    agents[i].SetMoveDirection(moveDirections[i]);
}
```

### IsPositionNavigable

```csharp
public bool IsPositionNavigable(Vector3 worldPosition)
```

**Purpose:** Checks if a position is within the navigable area.

**Parameters:**
- `worldPosition`: The world-space position to check

**Returns:**
- `true` if the position is within the navigable area
- `false` if the position is outside the navigable area or the system is not initialized

**Usage:**
- Use to verify if a destination or path point is valid before attempting navigation
- Can be used for agent placement or path planning

**Example:**
```csharp
// Check if a position is valid before setting it as a destination
Vector3 targetPosition = GetRandomPosition();
if (querySystem.IsPositionNavigable(targetPosition))
{
    agent.SetDestination(targetPosition);
}
else
{
    // Find a valid position instead
    targetPosition = FindNearestNavigablePosition(targetPosition);
}
```

### GetNearestNavigablePosition

```csharp
public Vector3 GetNearestNavigablePosition(Vector3 worldPosition, float maxSearchDistance = 10.0f)
```

**Purpose:** Finds the nearest navigable position to the specified world position.

**Parameters:**
- `worldPosition`: The world-space position to start the search from
- `maxSearchDistance`: The maximum distance to search for a navigable position

**Returns:**
- The nearest navigable position if found within the maxSearchDistance
- The original position if no navigable position is found or the system is not initialized

**Usage:**
- Use to find valid positions near obstacles or boundaries
- Helpful for agent spawning or target positioning
- Can be used to "snap" invalid positions to the navigation mesh

**Example:**
```csharp
// When spawning an agent
Vector3 spawnPosition = spawnPoint.position;
if (!querySystem.IsPositionNavigable(spawnPosition))
{
    // Find a valid spawn position nearby
    spawnPosition = querySystem.GetNearestNavigablePosition(spawnPosition);
}
agent.transform.position = spawnPosition;
```

### GetApproximateDistanceToTarget

```csharp
public float GetApproximateDistanceToTarget(Vector3 worldPosition, float maxDistance = 1000.0f)
```

**Purpose:** Provides an estimate of the distance to the nearest target following the vector field.

**Parameters:**
- `worldPosition`: The world-space position of the agent
- `maxDistance`: The maximum search distance (to prevent infinite loops)

**Returns:**
- An approximate distance value following the flow field to the nearest target
- Returns -1.0f if the system is not initialized or the position is invalid

**Usage:**
- Use for priority decisions or behavior changes based on distance to targets
- Can help agents decide whether to follow the vector field or take other actions
- Not as accurate as true pathfinding distance but much faster to compute

**Notes:**
- This is an approximation, not an exact path distance
- Performance impact is higher than basic direction queries

**Example:**
```csharp
// For agent decision making
float distanceToTarget = querySystem.GetApproximateDistanceToTarget(transform.position);
if (distanceToTarget < 10.0f)
{
    // Switch to more precise navigation when close to target
    agent.SetPreciseNavigationMode(true);
}
else if (distanceToTarget > 100.0f)
{
    // Use faster movement for longer distances
    agent.SetSpeedMultiplier(1.5f);
}
```

### GetResolutionLevelAtPosition

```csharp
public int GetResolutionLevelAtPosition(Vector3 worldPosition, float agentImportance = 1.0f)
```

**Purpose:** Returns the resolution level that would be used for a query at the specified position.

**Parameters:**
- `worldPosition`: The world-space position to check
- `agentImportance`: A value from 0.0 to 1.0 indicating the agent's importance

**Returns:**
- The resolution level that would be used (0 = highest resolution, higher numbers = lower resolution)
- Returns -1 if the system is not initialized or the position is invalid

**Usage:**
- Primarily for debugging and visualization
- Can be used to understand the system's resolution behavior
- Helpful when optimizing the resolution bias settings

**Example:**
```csharp
// For debugging
int resolutionLevel = querySystem.GetResolutionLevelAtPosition(transform.position);
Debug.Log($"Current resolution level: {resolutionLevel}");
```

### RegisterAgent

```csharp
public void RegisterAgent(IPathfindingAgent agent)
```

**Purpose:** Registers an agent with the query system for tracking and optimization.

**Parameters:**
- `agent`: An object implementing the IPathfindingAgent interface

**Usage:**
- Call when agents are created or activated
- Allows the system to optimize based on agent distribution
- Can provide data for chunk prioritization

**Example:**
```csharp
// When a new agent is created
AgentController agent = Instantiate(agentPrefab).GetComponent<AgentController>();
querySystem.RegisterAgent(agent);
```

### UnregisterAgent

```csharp
public void UnregisterAgent(IPathfindingAgent agent)
```

**Purpose:** Removes an agent from the query system's tracking.

**Parameters:**
- `agent`: The agent to remove

**Usage:**
- Call when agents are destroyed or deactivated
- Ensures the system doesn't maintain references to inactive agents
- Keeps agent tracking accurate for optimization purposes

**Example:**
```csharp
// When an agent is destroyed
querySystem.UnregisterAgent(agent);
Destroy(agent.gameObject);
```

## Properties

### IsInitialized

```csharp
public bool IsInitialized { get; }
```

**Purpose:** Indicates whether the agent query system has been initialized.

**Type:** Boolean

**Usage:**
- Use to check if the system is ready before calling other methods
- Return value of true indicates the system is fully operational

**Example:**
```csharp
if (querySystem.IsInitialized)
{
    // Safe to use other methods
}
```

### AgentCount

```csharp
public int AgentCount { get; }
```

**Purpose:** Returns the number of currently registered agents.

**Type:** Integer

**Usage:**
- Use for debugging or UI display
- Can help with performance monitoring
- Useful for systems that scale behavior based on agent count

**Example:**
```csharp
// Display agent count in UI
agentCountText.text = $"Active Agents: {querySystem.AgentCount}";
```

## Inspector Properties

| Property | Type | Description |
|----------|------|-------------|
| `mipmapGenerator` | MipmapGenerator | Reference to the MipmapGenerator component |
| `biasController` | ResolutionBiasController | Reference to the ResolutionBiasController component |
| `vectorFieldStorage` | VectorFieldStorage | Reference to the VectorFieldStorage component |
| `enableBatchQueries` | bool | Whether to use batch processing for multiple agent queries |
| `useResolutionBias` | bool | Whether to use resolution bias when selecting data sources |
| `cacheSamplingResults` | bool | Whether to cache query results briefly for better performance |
| `debugQueryVisualization` | bool | Whether to display debug visualizations of agent queries |

## Integration with Other Components

The AgentQuerySystem depends on:
- `MipmapGenerator`: For accessing navigation bounds and mapping between world and texture space
- `ResolutionBiasController`: For determining the appropriate resolution level for each query
- `VectorFieldStorage`: For accessing the vector field data at different resolution levels

The AgentQuerySystem is used by:
- Agent controllers that need to know which direction to move
- Navigation systems that need to check position validity
- Any system that needs to make pathfinding queries

## Usage Example

```csharp
// Setup and initialization
MipmapGenerator mipmapGenerator = GetComponent<MipmapGenerator>();
ResolutionBiasController biasController = GetComponent<ResolutionBiasController>();
MultiResolutionVectorFieldGenerator vectorFieldGenerator = GetComponent<MultiResolutionVectorFieldGenerator>();
VectorFieldStorage vectorFieldStorage = GetComponent<VectorFieldStorage>();
AgentQuerySystem querySystem = GetComponent<AgentQuerySystem>();

// Initialize components in the correct order
mipmapGenerator.Initialize();
biasController.Initialize();
vectorFieldGenerator.Initialize();
vectorFieldStorage.Initialize();
querySystem.Initialize();

// Agent class implementation
public class AgentController : MonoBehaviour, IPathfindingAgent
{
    private AgentQuerySystem querySystem;
    private float importance = 1.0f;
    
    public void Initialize(AgentQuerySystem querySystem)
    {
        this.querySystem = querySystem;
        querySystem.RegisterAgent(this);
    }
    
    // Implement IPathfindingAgent interface
    public Vector3 GetPosition() => transform.position;
    public float GetImportance() => importance;
    
    private void Update()
    {
        // Get movement direction from query system
        Vector3 moveDirection = querySystem.GetFlowDirection(transform.position, importance);
        
        // Apply movement
        if (moveDirection != Vector3.zero)
        {
            transform.position += moveDirection * speed * Time.deltaTime;
            transform.forward = moveDirection;
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

## Notes

- The AgentQuerySystem is optimized for efficient queries with minimal per-agent cost
- Using batch queries can significantly improve performance for large agent counts
- Resolution bias is used to focus computational resources where they matter most
- The system provides a simple, consistent API regardless of the underlying vector field implementation
- For optimal performance, register and unregister agents properly
