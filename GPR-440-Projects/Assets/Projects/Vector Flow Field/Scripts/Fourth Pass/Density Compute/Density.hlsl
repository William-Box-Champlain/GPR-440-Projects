#include "Constants.hlsl"
#include "Helpers.hlsl"
// Density.hlsl

void DensityStep(uint3 id, RWTexture2D<float4> VelocityTexture,
                RWTexture2D<float> DensityTexture,
                RWTexture2D<float4> BoundaryTexture,
                float deltaTime, float densityDissipation, float2 resolution, float baseDensity)
{
    if (!WithinAbsoluteBounds(id.xy, resolution) || IsBoundary(id.xy, BoundaryTexture)) {
        return;
    }
    
    // Density update for compressible flow
    // We already advected the density in the Advection step
    // Now we need to account for compression/expansion
    
    float divergence = CalculateDivergence(id.xy, VelocityTexture, resolution);
    
    // If divergence is negative, the fluid is being compressed
    // If divergence is positive, the fluid is expanding
    
    // Update density based on divergence
    float densityChange = -DensityTexture[id.xy] * divergence * deltaTime;
    DensityTexture[id.xy] += densityChange;
    
    float currentDensity = DensityTexture[id.xy];
    
    // Apply density dissipation
    DensityTexture[id.xy] = lerp(baseDensity, currentDensity, densityDissipation);
    
    // Ensure density doesn't go below base density
    DensityTexture[id.xy] = max(baseDensity, currentDensity);
}
