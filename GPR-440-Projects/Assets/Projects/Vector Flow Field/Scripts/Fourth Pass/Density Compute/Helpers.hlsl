#ifndef FLUID_HELPERS_INCLUDED
#define FLUID_HELPERS_INCLUDED


#include "Constants.hlsl"
// Helpers.hlsl

//bool helpers
bool IsCell(int2 cell, RWTexture2D<float4> BoundaryTexture)
{
    return BoundaryTexture[cell].a > THRESHOLD && //opaque??
        BoundaryTexture[cell].b < THRESHOLD;
}

bool IsBoundary(int2 cell, RWTexture2D<float4> BoundaryTexture)
{
    return BoundaryTexture[cell].a < THRESHOLD &&
        BoundaryTexture[cell].b > THRESHOLD;   //blue
}

bool IsSource(int2 cell, RWTexture2D<float4> BoundaryTexture)
{
    return BoundaryTexture[cell].r < THRESHOLD &&
        BoundaryTexture[cell].g > THRESHOLD && //green
        BoundaryTexture[cell].b < THRESHOLD;
}

bool IsSink(int2 cell, RWTexture2D<float4> BoundaryTexture)
{
    return BoundaryTexture[cell].r > THRESHOLD && //red is big
        BoundaryTexture[cell].g < THRESHOLD &&
        BoundaryTexture[cell].b < THRESHOLD;
}

bool WithinAbsoluteBounds(uint2 id, float2 resolution)
{
    return (id.x <= (uint)resolution.x || id.y <= (uint)resolution.y);
}

//masking helpers
float GetCellMask(int2 cell, RWTexture2D<float4> BoundaryTexture)
{
    return step(THRESHOLD, BoundaryTexture[cell].a);
}

float GetBoundaryMask(int2 cell, RWTexture2D<float4> BoundaryTexture)
{
    // Returns 1.0 if cell is a boundary, 0.0 otherwise
    return step(THRESHOLD, BoundaryTexture[cell].b);
}

float GetSourceMask(int2 cell, RWTexture2D<float4> BoundaryTexture)
{
    // Returns normalized strength of source
    return BoundaryTexture[cell].g;
}

float GetSinkMask(int2 cell, RWTexture2D<float4> BoundaryTexture)
{
    // Returns normalized strength of sink
    return BoundaryTexture[cell].r;
}

// Sample velocity at a position using bilinear interpolation
float4 SampleVelocity(float2 pos, RWTexture2D<float4> VelocityTexture, float2 resolution, RWTexture2D<float4> BoundaryTexture)
{
    float2 cellSize = 1.0 / resolution;
    
    // Find the four cells surrounding this position
    int2 cell = int2(pos);
    float2 fracPos = frac(pos);
    
    int2 cells[4] = {
        cell,
        cell + int2(1, 0),
        cell + int2(0, 1),
        cell + int2(1, 1)
    };
    
    // Get cell values and boundary masks
    float4 values[4];
    float bounds[4];
    
    for (int i = 0; i < 4; i++) {
        if (WithinAbsoluteBounds(cells[i], resolution)) {
            values[i] = VelocityTexture[cells[i]];
            bounds[i] = 1.0 - GetBoundaryMask(cells[i], BoundaryTexture);
        } else {
            values[i] = float4(0, 0, 0, 0);
            bounds[i] = 0.0;
        }
    }
    
    // Bilinear interpolation with boundary handling
    float4 result = lerp(
        lerp(values[0] * bounds[0], values[1] * bounds[1], fracPos.x),
        lerp(values[2] * bounds[2], values[3] * bounds[3], fracPos.x),
        fracPos.y
    );
    
    // Normalize if all cells were not boundaries
    float totalWeight = lerp(
        lerp(bounds[0], bounds[1], fracPos.x),
        lerp(bounds[2], bounds[3], fracPos.x),
        fracPos.y
    );
    
    if (totalWeight > EPSILON) {
        result /= totalWeight;
    }
    
    return result;
}

// Calculate divergence of a vector field
float CalculateDivergence(int2 cell, RWTexture2D<float4> VelocityTexture, float2 resolution)
{
    float2 invCellSize = resolution;
    
    // Get velocities at neighboring cells
    float4 vL = VelocityTexture[int2(cell.x - 1, cell.y)];
    float4 vR = VelocityTexture[int2(cell.x + 1, cell.y)];
    float4 vB = VelocityTexture[int2(cell.x, cell.y - 1)];
    float4 vT = VelocityTexture[int2(cell.x, cell.y + 1)];
    
    // Calculate divergence using central differences
    float divergence = 0.5 * (
        (vR.x - vL.x) * invCellSize.x +
        (vT.y - vB.y) * invCellSize.y
    );
    
    return divergence;
}

// Calculate gradient of a scalar field
float2 CalculateGradient(int2 cell, RWTexture2D<float> ScalarTexture, float2 resolution)
{
    float2 invCellSize = resolution;
    
    // Get scalar values at neighboring cells
    float vL = ScalarTexture[int2(cell.x - 1, cell.y)];
    float vR = ScalarTexture[int2(cell.x + 1, cell.y)];
    float vB = ScalarTexture[int2(cell.x, cell.y - 1)];
    float vT = ScalarTexture[int2(cell.x, cell.y + 1)];
    
    // Calculate gradient using central differences
    float2 gradient = float2(
        0.5 * (vR - vL) * invCellSize.x,
        0.5 * (vT - vB) * invCellSize.y
    );
    
    return gradient;
}
#endif // FLUID_HELPERS_INCLUDED// Helpers.hlsl
