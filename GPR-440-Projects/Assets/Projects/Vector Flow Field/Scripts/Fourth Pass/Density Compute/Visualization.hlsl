#include "Constants.hlsl"
#include "Helpers.hlsl"
// Visualization.hlsl

void VisualizationStep(uint3 id, RWTexture2D<float4> VelocityTexture,
                      RWTexture2D<float> PressureTexture,
                      RWTexture2D<float> DensityTexture,
                      RWTexture2D<float4> VisualizationTexture,
                      float2 resolution, float baseDensity)
{
    if (!WithinAbsoluteBounds(id.xy, resolution)) {
        return;
    }
    
    // Normalize velocity for visualization
    float2 vel = VelocityTexture[id.xy].xy;
    float speed = length(vel);
    // Use a reasonable max velocity for normalization
    float2 normalizedVel = (speed > EPSILON) ? vel / 5.0 : float2(0, 0);
    
    // Normalize pressure for visualization
    float pressure = saturate(PressureTexture[id.xy] / (pressureCoeff * 2.0));
    
    // Normalize density for visualization
    float density = (DensityTexture[id.xy] - baseDensity) / (baseDensity * 2.0);
    density = saturate(density); // Clamp to [0,1]
    
    // Color coding: 
    // Red channel for pressure
    // Green channel for density
    // Blue/Alpha for velocity direction/magnitude
    VisualizationTexture[id.xy] = float4(
        pressure,
        density,
        normalizedVel.x * 0.5 + 0.5, // Remap from [-1,1] to [0,1]
        normalizedVel.y * 0.5 + 0.5
    );
}
