# Vector Flow Field Calculator for Unity

A complete implementation of a Vector Flow Field calculator in Unity using C# and compute shaders. This system uses the Navier-Stokes equations to calculate fluid dynamics for pathfinding applications.

## Overview

This Vector Flow Field implementation is designed for pathfinding in games, where multiple agents need to navigate toward destinations (sinks) while avoiding obstacles and potentially dangerous areas (sources). The system uses fluid dynamics principles to create smooth, natural-looking paths that automatically flow around obstacles.

## Features

- Real-time fluid simulation using Navier-Stokes equations
- GPU-accelerated computation using compute shaders
- Efficient batched diffusion with multiple iterations
- Support for multiple sinks (destinations) and sources (areas to avoid)
- Mesh-based field generation for complex navigation areas
- Customizable simulation parameters
- Easy integration with Unity's physics system
- Sample pathfinding agent implementation
- Comprehensive visualization tools for debugging and analysis

## Components

### Core Components

- **VectorFieldManager**: Main controller class that orchestrates the simulation
- **NavierStokesSolver**: Handles the computational aspects of the simulation
- **FieldTextureGenerator**: Creates and manages the input bitmap texture
- **PathfindingAgent**: Sample agent that uses the vector field for navigation
- **VectorFieldVisualizer**: Provides visualization of the vector field

### Visualization Components

- **VectorFieldVisualization.shader**: Shader for visualizing the vector field as colors
- **VectorFieldVisualizer**: Component for visualizing the vector field using different methods

### Supporting Files

- **NavierStokesCompute.compute**: Compute shader implementing the Navier-Stokes equations
- **MeshProcessor.compute**: Compute shader for processing meshes into field textures
- **VectorFieldParameters.cs**: Scriptable object for managing simulation parameters
- **VectorFieldVisualization.shader**: Shader for visualizing the vector field

## How It Works

1. The system uses a 2D bitmap texture to represent the field space:
   - **White pixels**: Valid field space
   - **Black pixels**: Outside field space (obstacles)
   - **Red pixels**: Sinks (destinations)
   - **Green pixels**: Sources (areas to avoid)

2. The field space can be defined in several ways:
   - Using simple shapes (rectangles, circles)
   - Using a mesh (e.g., a NavMesh) to define the navigable area
   - Using an existing texture

3. The Navier-Stokes solver uses this bitmap to calculate a vector field:
   - Fluid flows toward sinks (red areas)
   - Fluid flows away from sources (green areas)
   - Fluid cannot flow through obstacles (black areas)

4. Agents sample the vector field at their position to determine movement direction:
   - The vector field naturally guides agents around obstacles
   - Multiple agents can use the same field without individual path planning
   - The field provides smooth, natural-looking paths

## Setup Instructions

### 1. Import the Files

Import all the files into your Unity project. Make sure they are placed in a folder within your project's Assets directory.

### 2. Create a Vector Field Parameters Asset

1. Right-click in the Project window
2. Select Create > Vector Flow Field > Parameters
3. Configure the parameters as needed:
   - Grid Resolution: Higher values provide more detail but require more computation
   - Viscosity: Controls how quickly the flow field smooths out
   - Diffusion Iterations: Controls how smooth the velocity field becomes (higher values create more uniform flow)
   - Pressure Iterations: More iterations provide more accurate results but require more computation
   - Sink/Source Strength: Controls how strongly sinks and sources affect the flow

### 3. Set Up the Vector Field Manager

1. Create an empty GameObject in your scene
2. Add the VectorFieldManager component
3. Assign the NavierStokesCompute.compute shader
4. Assign the MeshProcessor.compute shader
5. Assign the Vector Field Parameters asset
6. Configure the world bounds to match your game world

### 4. Define Your Field Space

You can define the field space in several ways:

- **Using simple shapes**:
  ```csharp
  // Get a reference to the VectorFieldManager
  VectorFieldManager vectorField = FindObjectOfType<VectorFieldManager>();
  
  // Set a rectangular field area
  vectorField.SetFieldRect(new Vector3(0, 0, 0), new Vector3(20, 0, 20));
  
  // Add a sink (destination)
  vectorField.AddSink(new Vector3(15, 0, 15), 0.05f);
  
  // Add a source (area to avoid)
  vectorField.AddSource(new Vector3(5, 0, 5), 0.05f);
  
  // Add an obstacle
  vectorField.AddObstacle(new Vector3(10, 0, 10), 0.1f);
  ```

- **Using a mesh**:
  ```csharp
  // Get a reference to a mesh (e.g., from a NavMesh)
  Mesh navMesh = GetComponent<MeshFilter>().mesh;
  
  // Define sink locations in world space
  Vector2[] sinkLocations = new Vector2[]
  {
      new Vector2(15, 15),
      new Vector2(5, 10)
  };
  
  // Initialize the field from the mesh
  vectorField.InitializeFromMesh(navMesh, sinkLocations, 0.05f);
  
  // Add sources after initialization
  vectorField.AddSource(new Vector3(5, 0, 5), 0.05f);
  ```

- **Using a texture**:
  1. Create a texture with white, black, red, and green pixels
  2. Assign it to the Initial Field Texture field in the VectorFieldManager

### 5. Set Up Agents

1. Create a GameObject for your agent
2. Add a Rigidbody component
3. Add the PathfindingAgent component
4. Configure the agent parameters
5. Register the agent with the VectorFieldManager:
   ```csharp
   // Get references
   VectorFieldManager vectorField = FindObjectOfType<VectorFieldManager>();
   PathfindingAgent agent = GetComponent<PathfindingAgent>();
   
   // Register the agent
   vectorField.RegisterAgent(agent);
   ```

## Visualization

The system includes a powerful visualization component that can help you understand and debug your vector fields:

### Color Field Visualization

The color field visualization displays the vector field as a color-coded texture:
- **Hue (color)**: Represents the direction of the vector (0-360 degrees mapped to the color wheel)
- **Saturation and Value**: Represent the magnitude of the vector (stronger vectors appear more vibrant)
- **Alpha**: Partially based on magnitude (stronger vectors are more opaque)

This provides an intuitive way to see the flow patterns across the entire field at once. The color field is rendered as a flat quad mesh positioned slightly above the ground plane.

#### Color Field Parameters

- **Color Intensity**: Controls how much the vector magnitude affects the color brightness (0.1-2.0)
- **Show Color Legend**: Option to display a legend explaining the color mapping

### Arrow Grid Visualization

The arrow grid visualization displays a grid of arrows that point in the direction of the vector field:
- **Arrow Direction**: Represents the direction of the vector
- **Arrow Size**: Scales based on the magnitude of the vector
- **Arrow Color**: Changes based on the magnitude using a configurable gradient

This provides a more direct and intuitive representation of the vectors in the field. Arrows with near-zero magnitude are automatically hidden to reduce visual clutter.

#### Arrow Grid Parameters

- **Arrow Density**: Controls how many arrows are displayed in each dimension (5-50)
- **Arrow Scale**: Overall size multiplier for all arrows (0.1-2.0)
- **Min/Max Arrow Size**: Range for scaling arrows based on vector magnitude
- **Arrow Color Gradient**: Customizable gradient for coloring arrows based on magnitude (default: blue→green→red)
- **Update Interval**: How frequently the arrows update their orientation and size (in seconds)

### Using the Visualizer

1. Add the `VectorFieldVisualizer` component to your GameObject that has the `VectorFieldManager` component
2. Choose the visualization mode (Color Field, Arrows, Both, or None)
3. Adjust the visualization parameters as needed
4. Use the buttons in the inspector to toggle the visualization on/off or switch modes

```csharp
// Example code to set up a visualizer programmatically
VectorFieldVisualizer visualizer = gameObject.AddComponent<VectorFieldVisualizer>();
visualizer.SetMode(VectorFieldVisualizer.VisualizationMode.Both);
```

### Editor Controls

The VectorFieldVisualizer comes with a custom editor that adds convenient controls to the inspector:

- **Toggle Visualization**: Button to turn the visualization on or off
- **Color Field**: Button to switch to color field visualization mode
- **Arrows**: Button to switch to arrow grid visualization mode
- **Both**: Button to enable both visualization modes simultaneously

These controls make it easy to switch between visualization modes during runtime for debugging purposes.

### Performance Considerations

Visualization can impact performance, especially with high-density arrow grids or large field sizes:

- **Arrow Density**: Higher density provides more detail but creates more GameObjects
- **Update Interval**: Increasing this value reduces the frequency of updates, improving performance
- **Visualization Mode**: The Color Field mode is generally more performant than the Arrow Grid mode
- **Both Mode**: Using both visualization modes simultaneously has the highest performance cost

For optimal performance during gameplay, you can disable the visualizer or set its mode to None when visualization is not needed.

### Debugging with Visualization

The visualizer is particularly useful for debugging:

- **Flow Patterns**: Quickly identify areas where flow is blocked or redirected
- **Sink/Source Influence**: See how sinks and sources affect the surrounding flow
- **Obstacle Effects**: Observe how obstacles create flow patterns around them
- **Agent Behavior**: Debug agent movement by comparing their paths to the underlying flow

When using GameObject-based sinks and sources, the visualizer updates automatically as these objects move, allowing you to see the dynamic changes to the flow field in real-time.

## Advanced Usage

### Custom Field Layouts

You can create custom field layouts by:

1. Creating a texture with the appropriate color coding
2. Loading it at runtime:
   ```csharp
   // Load a texture from resources
   Texture2D fieldTexture = Resources.Load<Texture2D>("MyFieldLayout");
   
   // Create a field generator with the texture
   FieldTextureGenerator fieldGenerator = new FieldTextureGenerator(new Vector2Int(256, 256));
   fieldGenerator.LoadFromTexture(fieldTexture);
   
   // Update the vector field
   vectorField.UpdateInputTexture(fieldGenerator.FieldTexture);
   ```

### Dynamic Field Updates

You can dynamically update the field during gameplay:

```csharp
// Add a new sink when the player clicks
if (Input.GetMouseButtonDown(0))
{
    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
    if (Physics.Raycast(ray, out RaycastHit hit))
    {
        vectorField.AddSink(hit.point, 0.05f);
    }
}

// Add a new source when the player right-clicks
if (Input.GetMouseButtonDown(1))
{
    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
    if (Physics.Raycast(ray, out RaycastHit hit))
    {
        vectorField.AddSource(hit.point, 0.05f);
    }
}
```

### Custom Agent Behavior

You can extend the PathfindingAgent class to create custom agent behavior:

```csharp
public class MyCustomAgent : PathfindingAgent
{
    // Add custom behavior here
    protected override void Update()
    {
        base.Update();
        
        // Add custom logic
    }
}
```

## Performance Considerations

- The simulation is GPU-accelerated, but high resolutions can still impact performance
- For large numbers of agents, consider using instancing or ECS
- Adjust the pressure iterations based on your performance requirements
- Use the FixedUpdate option for more consistent simulation

## License

This code is provided under the MIT License. Feel free to use it in your projects, both personal and commercial.

## Credits

This implementation is based on the Navier-Stokes equations for incompressible flow and uses techniques from computational fluid dynamics.
