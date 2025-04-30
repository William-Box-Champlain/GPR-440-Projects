#include "Constants.hlsl"
#include "Helpers.hlsl"
// Pressure.hlsl

void PressureStep(uint3 id, RWTexture2D<float4> VelocityTexture, 
                 RWTexture2D<float> PressureTextureTarget, 
                 RWTexture2D<float> PressureTextureSource,
                 RWTexture2D<float> DensityTexture,
                 RWTexture2D<float4> BoundaryTexture, 
                 float deltaTime, float2 resolution,
                 float pressureCoeff,
                 int iteration)
{
    if (!WithinAbsoluteBounds(id.xy, resolution) || IsBoundary(id.xy, BoundaryTexture)) {
        return;
    }
    
    // For compressible flow, we use the continuity equation
    // dp/dt = -rho * c² * div(v)
    // where c is the speed of sound, and rho is the density
    
    // Calculate divergence
    float divergence = CalculateDivergence(id.xy, VelocityTexture, resolution);
    
    // Update pressure based on divergence
    float density = max((float) DensityTexture[id.xy], baseDensity);
    float pressureChange = -density * SPEED_OF_SOUND * SPEED_OF_SOUND * divergence * deltaTime;
    
    // Read from source, write to target
    PressureTextureTarget[id.xy] = PressureTextureSource[id.xy] + pressureCoeff * pressureChange;
    
    // Ensure pressure is non-negative
    float currentPressure = PressureTextureTarget[id.xy];
    PressureTextureTarget[id.xy] = max(0.0, currentPressure);
    
    // Now apply pressure forces to velocity
    // v' = v - dt * (1/rho) * grad(p)
    float2 pressureGradient = CalculateGradient(id.xy, PressureTextureTarget, resolution);
    float2 pressureForce = pressureGradient / density;
    
    VelocityTexture[id.xy].xy -= deltaTime * pressureForce;
    
    // Velocity magnitude clipping (branchless) - clearer implementation
    float currentVel = length(VelocityTexture[id.xy].xy);
    VelocityTexture[id.xy].xy = normalize(VelocityTexture[id.xy].xy) * min(currentVel, MAX_VELOCITY);
}
