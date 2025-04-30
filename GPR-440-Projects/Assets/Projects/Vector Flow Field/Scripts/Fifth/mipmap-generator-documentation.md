# MipmapGenerator API Documentation

The `MipmapGenerator` class is responsible for creating a multi-resolution representation of the Unity NavMesh. It generates multiple resolution levels that can be used for efficient hierarchical pathfinding.

## Setup Instructions

1. **Create the Shader Asset**:
   - Create a new shader asset named `ConservativeDownsample.shader` in your project's `Assets/Shaders` folder
   - Copy the contents of the provided shader into this file

2. **Create the Script**:
   - Create a new C# script named `MipmapGenerator.cs` in your project's `Assets/Scripts/MipmapPathfinding` folder
   - Copy the contents of the provided script into this file

3. **Assign the Component**:
   - Add the `MipmapGenerator` component to a GameObject in your scene (ideally your Pathfinding Manager object)
   - Assign the `BoundsCalculator_Optimized` compute shader to the `boundsCalculatorShader` field
   - Configure the resolution settings as needed

4. **Use in your Project**:
   - Call the `Initialize()` method after NavMesh baking is complete
   - Access mipmap levels through the provided public methods

## Class Location

```
MipmapPathfinding.MipmapGenerator
```

## Public Methods

### Initialize

```csharp
public void Initialize()
```

**Purpose:** Initializes and generates the complete mipmap chain.

**Usage:**
- Call this method after the NavMesh has been baked or when you need to regenerate the mipmap levels.
- All internal data structures and textures will be created and populated.

**Requirements:**
- The `boundsCalculatorShader` must be assigned before calling this method.
- A valid NavMesh must exist in the scene.

**Example:**
```csharp
MipmapGenerator mipmapGenerator = GetComponent<MipmapGenerator>();
mipmapGenerator.Initialize();
```

### GetMipmapLevel

```csharp
public RenderTexture GetMipmapLevel(int level)
```

**Purpose:** Retrieves a RenderTexture for a specific resolution level.

**Parameters:**
- `level`: The resolution level to retrieve (0 = highest resolution, higher numbers = lower resolutions)

**Returns:** 
- The RenderTexture containing the requested mipmap level.
- Returns `null` if the level is invalid.

**Notes:**
- Level 0 is the base (highest) resolution (12,000 × 6,000 by default)
- Each subsequent level is half the resolution of the previous level
- When using a RenderTexture with mipmap chain, temporary textures are created for levels > 0

**Example:**
```csharp
// Get the quarter resolution texture (Level 2)
RenderTexture quarterResolution = mipmapGenerator.GetMipmapLevel(2);
```

### GetMipmapLevelCount

```csharp
public int GetMipmapLevelCount()
```

**Purpose:** Returns the total number of mipmap levels available.

**Returns:** The number of mipmap levels generated (typically 5).

**Example:**
```csharp
int levelCount = mipmapGenerator.GetMipmapLevelCount();
Debug.Log($"Available mipmap levels: {levelCount}");
```

### GetNavigationBounds

```csharp
public Bounds GetNavigationBounds()
```

**Purpose:** Retrieves the world-space bounds of the navigation area.

**Returns:** A `Bounds` struct representing the navigation area covered by the mipmap textures.

**Example:**
```csharp
Bounds navBounds = mipmapGenerator.GetNavigationBounds();
Debug.Log($"Navigation area spans from {navBounds.min} to {navBounds.max}");
```

### GetBaseWidth

```csharp
public int GetBaseWidth()
```

**Purpose:** Returns the width of the base (highest resolution) level texture.

**Returns:** The width in pixels of the level 0 texture (default: 12,000).

**Example:**
```csharp
int baseWidth = mipmapGenerator.GetBaseWidth();
```

### GetBaseHeight

```csharp
public int GetBaseHeight()
```

**Purpose:** Returns the height of the base (highest resolution) level texture.

**Returns:** The height in pixels of the level 0 texture (default: 6,000).

**Example:**
```csharp
int baseHeight = mipmapGenerator.GetBaseHeight();
```

## Inspector Properties

| Property | Type | Description |
|----------|------|-------------|
| `boundsCalculatorShader` | ComputeShader | The compute shader that generates the base level texture from the NavMesh. |
| `useRenderTextureArray` | bool | If true, uses a single RenderTexture with mipmap chain; if false, uses separate textures for each level. |
| `baseWidth` | int | Width of the base (highest resolution) level in pixels. |
| `baseHeight` | int | Height of the base (highest resolution) level in pixels. |
| `mipmapLevels` | int | Total number of resolution levels to generate. |
| `navMeshSampleDistance` | float | Sample distance for NavMesh queries. |
| `navMeshAreaMask` | int | Mask defining which NavMesh areas to include. |
| Debug textures | Texture2D | Debug visualizations for each mipmap level (read-only) |

## Integration with Other Components

The MipmapGenerator is typically used by:

1. **Resolution Bias Controller** - For determining resolution levels for different areas
2. **Multi-Resolution Vector Field Generator** - For executing pathfinding algorithms
3. **Vector Field Storage** - For maintaining the generated data
4. **Agent Query System** - For providing navigation information to agents

## Usage Example

```csharp
// Setup
MipmapGenerator mipmapGenerator = gameObject.AddComponent<MipmapGenerator>();
mipmapGenerator.boundsCalculatorShader = Resources.Load<ComputeShader>("BoundsCalculator_Optimized");

// Initialize when NavMesh is ready
mipmapGenerator.Initialize();

// Access for other systems
RenderTexture baseLevel = mipmapGenerator.GetMipmapLevel(0);
RenderTexture lowResLevel = mipmapGenerator.GetMipmapLevel(3);

// Get metadata
Bounds navBounds = mipmapGenerator.GetNavigationBounds();
int levelCount = mipmapGenerator.GetMipmapLevelCount();
```

## Notes

- The MipmapGenerator uses conservative downsampling to preserve path connectivity across resolution levels.
- The generated textures use the R8 format (single channel, 8-bit per pixel).
- Debug textures are automatically generated and can be viewed in the inspector.
- The component handles cleanup of all resources when destroyed.
