#include "Constants.hlsl"
#include "Helpers.hlsl"
// Forces.hlsl

void ApplyForcesStep(uint3 id, RWTexture2D<float4> VelocityTexture,
                    RWTexture2D<float> PressureTexture,
                    RWTexture2D<float> DensityTexture,
                    RWTexture2D<float4> BoundaryTexture,
                    float maxSinkSourceStrength, float deltaTime,
                    float viscosityCoeff, float2 resolution,
                    float densityToVelocity, float baseDensity)
{
    // Create a single mask for valid cells (not a boundary and within bounds)
    float validCellMask = (float)WithinAbsoluteBounds(id.xy, resolution) * (1.0 - GetBoundaryMask(id.xy, BoundaryTexture));
    
    // Only process if mask is non-zero (this single branch is often unavoidable for early exit)
    if (validCellMask < EPSILON) {
        return;
    }
    
    // Apply viscosity using a mask instead of a branch
    float viscosityMask = step(EPSILON, viscosityCoeff);
    
    float4 vL = VelocityTexture[int2(id.x - 1, id.y)];
    float4 vR = VelocityTexture[int2(id.x + 1, id.y)];
    float4 vB = VelocityTexture[int2(id.x, id.y - 1)];
    float4 vT = VelocityTexture[int2(id.x, id.y + 1)];
    
    float4 vLaplacian = vL + vR + vB + vT - 4.0 * VelocityTexture[id.xy];
    VelocityTexture[id.xy] += viscosityCoeff * deltaTime * vLaplacian * viscosityMask;
    
    // Get source and sink masks
    float sourceMask = GetSourceMask(id.xy, BoundaryTexture);
    float sinkMask = GetSinkMask(id.xy, BoundaryTexture);
    
    // Create activation masks instead of branches
    float sourceActiveMask = step(EPSILON, sourceMask);
    float sinkActiveMask = step(EPSILON, sinkMask);
    
    // Source effects - compute for all cells but only apply based on mask
    float2 centerOfCell = float2(id.xy) + 0.5;
    float2 sourceForceDir = normalize(float2(id.xy) - centerOfCell + float2(0.01, 0.01));
    float2 sourceForce = sourceForceDir * sourceMask * maxSinkSourceStrength * deltaTime * sourceActiveMask;
    
    // Sink effects - compute for all cells but only apply based on mask
    float2 sinkForceDir = normalize(centerOfCell - float2(id.xy) + float2(0.01, 0.01));
    float2 sinkForce = sinkForceDir * sinkMask * maxSinkSourceStrength * deltaTime * sinkActiveMask;
    
    // Apply combined forces
    VelocityTexture[id.xy].xy += sourceForce + sinkForce;
    
    // Update density and pressure based on source and sink - use lerp to avoid branches
    float sourceDensity = baseDensity * (1.0 + sourceMask);
    float textureDensity = DensityTexture[id.xy];
    float texturePressure = PressureTexture[id.xy];
    DensityTexture[id.xy] = lerp(textureDensity, max(textureDensity, sourceDensity), sourceActiveMask);
    PressureTexture[id.xy] = lerp(texturePressure, max(texturePressure, sourceMask * pressureCoeff), sourceActiveMask);
    
    // Apply sink effects on density and pressure
    float sinkDensityFactor = max(0.0, 1.0 - sinkMask * deltaTime);
    DensityTexture[id.xy] *= lerp(1.0, sinkDensityFactor, sinkActiveMask);
    PressureTexture[id.xy] *= lerp(1.0, sinkDensityFactor, sinkActiveMask);
}
