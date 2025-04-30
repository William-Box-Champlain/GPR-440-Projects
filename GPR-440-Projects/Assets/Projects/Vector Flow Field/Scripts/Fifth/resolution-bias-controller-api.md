# Resolution Bias Controller API Documentation

The `ResolutionBiasController` is a critical component of the Mipmap Pathfinding System. Its purpose is to determine appropriate resolution levels for different areas of the navigation space, focusing computational resources where they matter most. This ensures high detail in important navigation areas while using lower resolutions in less critical spaces.

## Setup Instructions

### Required Assets
1. **Compute Shaders**: Place these in your project's Resources folder
   - `BiasGenerator.compute` - Generates the bias texture
   - `JunctionDetector.compute` - Detects junctions in navigation texture

### Component Setup
1. Add the `ResolutionBiasController` component to the same GameObject as the `MipmapGenerator`
2. Configure inspector properties:
   - Junction Detection parameters
   - Target Bias parameters
   - Bias Settings (radius, strength, falloff curve)
3. If not using auto-reference, assign the `MipmapGenerator` reference

### Initialization Order
Always initialize components in the correct order:
1. First: MipmapGenerator
2. Second: ResolutionBiasController

### Code Setup Example
```csharp
// First initialize the MipmapGenerator
MipmapGenerator mipmapGenerator = GetComponent<MipmapGenerator>();
mipmapGenerator.Initialize();

// Then initialize the ResolutionBiasController
ResolutionBiasController biasController = GetComponent<ResolutionBiasController>();
biasController.Initialize();

// Set your initial targets
Transform[] targets = new Transform[] { target1, target2 };
biasController.SetTargets(targets);
```

### Integration with Other Components
When implementing Vector Field Generator or other components that need bias information:
```csharp
// Sample the bias at a world position
float bias = biasController.SampleBias(worldPosition);

// Convert bias to resolution level (0-4)
// Higher bias = lower resolution level (more detail)
int resolutionLevel = Mathf.Clamp(Mathf.FloorToInt(4.0f - bias), 0, 4);

// Use resolution level to determine processing detail
```

## Public Methods

### Initialize

```csharp
public void Initialize()
```

**Purpose:** Initializes and generates the resolution bias texture.

**Usage:**
- Call this method after the MipmapGenerator has been initialized
- Creates a bias texture at half the resolution of base mipmap level
- Automatically detects junctions if `autoDetectJunctions` is true
- Generates the initial bias texture

**Requirements:**
- The `mipmapGenerator` reference must be assigned
- The `BiasGenerator.compute` compute shader must be available in Resources

**Example:**
```csharp
ResolutionBiasController biasController = GetComponent<ResolutionBiasController>();
biasController.Initialize();
```

### DetectJunctions

```csharp
public void DetectJunctions()
```

**Purpose:** Analyzes the navigation texture to find junction points where multiple paths connect.

**Usage:**
- Call this method when the navigation mesh changes
- Analyzes the navigation texture using a compute shader to detect junctions
- Identifies important pathway intersections using pattern recognition and connected components analysis
- Ranks junctions by importance and selects the top ones (up to maxJunctions)
- Marks the bias texture as dirty to trigger regeneration

**Technical Implementation:**
- Uses a compute shader (JunctionDetector.compute) to analyze the navigation texture
- Detects T-junctions, 4-way intersections, and complex path connections
- Assigns importance scores based on junction complexity (more complex = higher score)
- Filters out closely spaced junctions by finding local maxima
- Converts texture coordinates to world-space positions for bias application

**Requirements:**
- Requires the JunctionDetector compute shader in Resources folder
- The navigation texture must be readable by the compute shader

**Example:**
```csharp
// After changing the navigation mesh
biasController.DetectJunctions();
```

### SetTargets

```csharp
public void SetTargets(Transform[] newTargets)
```

**Purpose:** Sets the array of target transforms that will influence the resolution bias.

**Parameters:**
- `newTargets`: Array of Transform objects representing navigation targets

**Usage:**
- Call this when targets are first established or when the set of targets changes
- Marks the bias texture as dirty to trigger regeneration
- Only active targets (active GameObjects) will influence the bias

**Example:**
```csharp
// Set initial targets
Transform[] targets = { target1, target2, target3 };
biasController.SetTargets(targets);
```

### ActivateTarget

```csharp
public void ActivateTarget(Transform target)
```

**Purpose:** Marks a specific target as active in the system.

**Parameters:**
- `target`: The Transform of the target to activate

**Usage:**
- Call this when a target becomes active in the outside-in sequence
- Supports the outside-in activation pattern specified in the project goals
- Marks the bias texture as dirty to trigger regeneration

**Example:**
```csharp
// When a target becomes active in sequence
biasController.ActivateTarget(targetTransform);
```

### GenerateBiasTexture

```csharp
public void GenerateBiasTexture()
```

**Purpose:** Generates the resolution bias texture using the compute shader.

**Usage:**
- Call this to manually trigger a regeneration of the bias texture
- Automatically called when the bias data is marked as dirty
- Uses the BiasGenerator compute shader to efficiently process data

**Technical Implementation:**
- Creates compute buffers for junctions and targets
- Sets up shader parameters including navigation bounds and bias settings
- Converts the falloff AnimationCurve to a texture for GPU access
- Dispatches the compute shader to generate the texture
- Properly releases all compute buffers to avoid memory leaks

**Example:**
```csharp
// When you need to force a refresh
biasController.GenerateBiasTexture();
```

### SampleBias

```csharp
public float SampleBias(Vector3 worldPosition)
```

**Purpose:** Samples the bias value at a specific world position.

**Parameters:**
- `worldPosition`: The world-space position to sample

**Returns:**
- A float value representing the resolution bias at the specified position
- Range is 0 to maxBiasStrength (typically 0-4)
- Higher values indicate areas that should use higher resolution

**Usage:**
- Used by the vector field generator to determine the appropriate resolution level
- Called for each position where resolution bias is needed
- Convert the bias value to a resolution level: `level = 4 - bias`

**Technical Note:**
- The current implementation reads back the texture data to the CPU
- For performance-critical applications, consider GPU-side sampling

**Example:**
```csharp
// In the vector field generator
float bias = biasController.SampleBias(agentPosition);
int resolutionLevel = Mathf.FloorToInt(4.0f - bias); // Convert to level (0-4)
```

## Properties

### BiasTexture

```csharp
public RenderTexture BiasTexture { get; }
```

**Purpose:** Provides access to the underlying bias texture.

**Type:** RenderTexture in R8 format (single channel, 8-bit per pixel)

**Usage:**
- Used for visualization and debugging
- Can be referenced by other systems for direct GPU-side sampling

**Example:**
```csharp
// For debugging or visualization
RenderTexture biasTexture = biasController.BiasTexture;
```

## Inspector Properties

| Property | Type | Description |
|----------|------|-------------|
| `mipmapGenerator` | MipmapGenerator | Reference to the MipmapGenerator component |
| `autoDetectJunctions` | bool | If true, automatically detects junctions during initialization |
| `junctionDetectionThreshold` | float | Threshold for junction detection sensitivity (0-1) |
| `maxJunctions` | int | Maximum number of junctions to detect (project spec: ~50) |
| `targets` | Transform[] | Array of target transforms that influence the bias |
| `targetBiasStrength` | float | Strength of bias applied around active targets |
| `biasRadius` | float | World-space radius of influence around junctions/targets |
| `maxBiasStrength` | float | Maximum bias strength value (0-4) |
| `biasRadiusFalloff` | AnimationCurve | Defines how bias strength falls off with distance |

## Performance Considerations

1. **Memory Usage:**
   - The bias texture uses R8 format (1 byte per pixel)
   - Typically half the resolution of the navigation texture
   - For 12,000 × 6,000 nav texture, bias is 6,000 × 3,000 = ~17MB

2. **Compute Shader Performance:**
   - Junction detection is compute-intensive but only runs when the navigation mesh changes
   - Bias texture generation runs when targets change or junctions are re-detected
   - Uses 8×8 thread groups for efficient GPU utilization

3. **CPU-GPU Synchronization:**
   - The current SampleBias implementation involves a GPU readback
   - For high-performance applications, consider using a compute buffer
   - Or sample the bias in the same compute shader that uses the bias value

## Debug and Visualization

The included `ResolutionBiasControllerEditor` provides debug tools in the inspector:

1. Buttons for manual junction detection and texture regeneration
2. Visualization of the current bias texture
3. Real-time updates when bias parameters change

For runtime visualization:
```csharp
// Draw gizmos showing bias values
void OnDrawGizmos()
{
    if (biasController == null) return;
    
    // Sample a grid of points
    for (float x = minX; x <= maxX; x += step)
    {
        for (float z = minZ; z <= maxZ; z += step)
        {
            Vector3 pos = new Vector3(x, 0, z);
            float bias = biasController.SampleBias(pos);
            
            // Visualize using color gradient (blue to red)
            Gizmos.color = Color.Lerp(Color.blue, Color.red, bias / 4.0f);
            Gizmos.DrawSphere(pos, 0.2f);
        }
    }
}
```

## Integration with Pathfinding System

The Resolution Bias Controller is the second component in the Mipmap Pathfinding System:

1. **MipmapGenerator** - Creates multi-resolution navigation textures
2. **ResolutionBiasController** - Determines resolution importance
3. **Multi-Resolution Vector Field Generator** - Uses the bias to process at appropriate detail levels
4. **Chunked Processing System** - Uses bias for prioritization
5. **Vector Field Storage** - Stores final vector field data
6. **Agent Query System** - Provides navigation directions to agents

When configuring the system, ensure the components are initialized in the correct order and that computation is focused on the ~50 junction points and 3-5 dynamic targets as specified in the project goals.