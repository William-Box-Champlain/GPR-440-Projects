# Global Pressure Propagation for Vector Flow Fields

This update adds global pressure propagation to the Vector Flow Field system. This ensures that pressure effects from sinks and sources propagate throughout the entire vector space, rather than being limited to a local area.

## Key Changes

1. **NavierStokesCompute.compute**:
   - Added a new `PropagateGlobalPressure` kernel that propagates pressure from sinks and sources across the entire field
   - Modified the `Project` kernel to incorporate both local and global pressure gradients
   - Simplified the pressure propagation approach to be more robust

2. **NavierStokesSolver.cs**:
   - Added a new `globalPressureTexture` to store the global pressure field
   - Added parameters for `globalPressureStrength` and `globalPressureIterations`
   - Modified the `Update` method to call the global pressure propagation kernel multiple times
   - Added debugging capabilities to verify texture creation and boundary info generation

3. **VectorFieldParameters.cs**:
   - Added new parameters for global pressure strength and iterations
   - Increased the maximum range for pressure iterations

4. **VectorFieldManager.cs**:
   - Added properties to expose the pressure, global pressure, divergence, and boundary info textures
   - Updated the initialization to pass global pressure parameters to the solver

5. **GlobalPressureVisualizer.cs**:
   - Added a new component to visualize the global pressure field, local pressure field, boundary info, or divergence field
   - Useful for debugging and understanding how pressure propagates through the field

## How to Use

1. **Configure Global Pressure Parameters**:
   - In the VectorFieldParameters asset, adjust the following settings:
     - **Global Pressure Strength**: Controls how strongly the global pressure affects the velocity field (0.0-2.0)
     - **Global Pressure Iterations**: Controls how many iterations to use for global pressure propagation (1-50)

2. **Visualize the Global Pressure Field**:
   - Add a plane to your scene
   - Add the `GlobalPressureVisualizer` component to the plane
   - Assign your VectorFieldManager to the component
   - Choose which field to visualize (global pressure, local pressure, boundary info, or divergence)
   - Adjust the visualization scale and update frequency as needed

3. **Debug Issues**:
   - If pressure isn't propagating correctly, check the console for warnings about the boundary info texture
   - Ensure your input texture has valid field space (white pixels), sinks (red pixels), and sources (green pixels)
   - Try increasing the global pressure iterations if pressure isn't propagating far enough

## Technical Details

### Global Pressure Propagation

The global pressure propagation works by:

1. Initializing the global pressure field with values from sinks and sources
2. Propagating these values across the field using a simple diffusion approach
3. Combining the global pressure with the local pressure in the projection step

This approach ensures that pressure effects from sinks and sources can influence the entire field, not just their local area.

### Boundary Information

The boundary info texture is crucial for pressure propagation. It stores information about which cells have valid neighbors, which is used to determine how pressure should propagate. If this texture is all zeros, pressure won't propagate correctly.

### Performance Considerations

Global pressure propagation is more computationally expensive than local pressure solving. To balance performance and quality:

- Adjust the global pressure iterations based on your needs
- Consider reducing the global pressure strength if the effects are too strong
- For very large fields, you might need to increase the iterations to ensure pressure propagates across the entire field

## Troubleshooting

If you encounter issues with the global pressure propagation:

1. **No visible pressure effects**:
   - Check if the global pressure strength is set to a non-zero value
   - Verify that your input texture has sinks and sources
   - Use the GlobalPressureVisualizer to see if pressure is being generated

2. **Pressure not propagating far enough**:
   - Increase the global pressure iterations
   - Check if the boundary info texture is being generated correctly

3. **Performance issues**:
   - Reduce the global pressure iterations
   - Consider using a lower resolution for the simulation
