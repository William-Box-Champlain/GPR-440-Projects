// Advection.hlsl
#include "Constants.hlsl"
#include "Helpers.hlsl"
void AdvectionStep(uint3 id, RWTexture2D<float4> VelocityTexture, 
                  RWTexture2D<float4> VelocityTexturePrev,
                  RWTexture2D<float> DensityTexture,
                  RWTexture2D<float> DensityTexturePrev,
                  RWTexture2D<float4> BoundaryTexture,
                  float deltaTime, float2 resolution, float baseDensity)
{
    // Skip if outside bounds or if this is a boundary cell (keep this early-return as requested)
    if (!WithinAbsoluteBounds(id.xy, resolution) || IsBoundary(id.xy, BoundaryTexture)) {
        return;
    }
    
    // Save current state
    VelocityTexturePrev[id.xy] = VelocityTexture[id.xy];
    DensityTexturePrev[id.xy] = DensityTexture[id.xy];
    
    // Semi-Lagrangian advection for velocity
    float2 pos = float2(id.xy) - deltaTime * VelocityTexture[id.xy].xy;
    float4 advectedVel = SampleVelocity(pos, VelocityTexturePrev, resolution, BoundaryTexture);
    
    // Apply boundary conditions
    float cellMask = GetCellMask(id.xy, BoundaryTexture);
    VelocityTexture[id.xy] = advectedVel * cellMask;
    
    // Also advect density
    float advectedDensity = 0.0;
    
    // Use a mask instead of branch for position check
    float posInBoundsMask = (float)WithinAbsoluteBounds(uint2(pos), resolution);
    float sampledDensity = DensityTexturePrev[int2(pos)];
    advectedDensity = sampledDensity * posInBoundsMask;
    
    DensityTexture[id.xy] = advectedDensity * cellMask;
    
    // Get source and sink masks
    float sourceMask = GetSourceMask(id.xy, BoundaryTexture);
    float sinkMask = GetSinkMask(id.xy, BoundaryTexture);
    
    // Create activation masks instead of branches
    float sourceActiveMask = step(EPSILON, sourceMask);
    float sinkActiveMask = step(EPSILON, sinkMask);
    
    // Apply source density with mask instead of branch
    float sourceDensity = baseDensity * sourceMask;
    float currentDensity = DensityTexture[id.xy];
    DensityTexture[id.xy] = lerp(currentDensity, max(currentDensity, sourceDensity), sourceActiveMask);
    
    // Apply sink with mask instead of branch
    float sinkFactor = 1.0 - sinkMask * deltaTime;
    DensityTexture[id.xy] *= lerp(1.0, sinkFactor, sinkActiveMask);
}
