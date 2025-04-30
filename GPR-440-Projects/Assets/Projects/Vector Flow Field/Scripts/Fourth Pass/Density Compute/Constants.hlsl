// Constants.hlsl
// This file contains all constants used across the fluid simulation

#ifndef FLUID_CONSTANTS_INCLUDED
#define FLUID_CONSTANTS_INCLUDED

static float4 CELL = float4(0, 0, 0, 1);
static float4 BOUNDARY = float4(0, 0, 1, 0);
static float4 SOURCE = float4(0, 1, 0, 0);
static float4 SINK = float4(1, 0, 0, 0);
static float EPSILON = 0.01;
static float THRESHOLD = 0.01;
static float DISSIPATION = 0.8;
static float SPEED_OF_SOUND = 10.0; // Large value for fast pressure propagation
static float MAX_VELOCITY = 5.0; // Maximum allowed velocity magnitude

#endif // FLUID_CONSTANTS_INCLUDED