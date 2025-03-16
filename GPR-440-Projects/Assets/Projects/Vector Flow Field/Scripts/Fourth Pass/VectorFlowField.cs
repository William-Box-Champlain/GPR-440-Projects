using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Schema;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using VectorFlowField;

namespace VectorFlowField
{
    public interface IVectorFlowFieldAgent
    {
        UnityEngine.GameObject go {  get; }
        private Vector3 GetFieldDirection()
        {
            //return VectorFlowField.VectorFlowFieldManager.SampleFieldDirection(go.transform.position);
            return default;
        }
    }

    public class VectorFlowFieldManager
    {
        private static VectorFlowFieldManager _instance;

        public static VectorFlowFieldManager GetInstance()
        {
            if(_instance == null ) _instance = new VectorFlowFieldManager();
            return _instance;
        }

        //Subsystems
        public NavMeshAdapter navMeshAdapter { get; private set; }
        public ComputeShader adapterCompute { get; private set; }
        public FluidSimulator simulator { get; private set; }
        public ComputeShader simulatorCompute {  get; private set; }
        public SourceSinkManager influenceManager { get; private set; }

        private VectorFlowFieldManager() { }

        /// <summary>
        /// Instantiates the various sub-components using the given config-data
        /// </summary>
        public void Initialize(SimulationParameters parameters)
        {
            this.navMeshAdapter = new NavMeshAdapter(parameters.gridResolution, parameters.bounds, parameters.AdapterShader);
            this.simulator = new FluidSimulator();
            this.influenceManager = new SourceSinkManager(parameters.defaultSourceStrength, parameters.defaultSinkStrength);
        }

        public void Update(float dt)
        {
            simulator.Update(dt);
        }
    }
    public class SimulationParameters
    {
        public Vector2Int gridResolution { get; private set; } = default;
        public int iterations { get; private set; } = default;
        public Bounds bounds { get; private set; } = default;
        public float defaultSourceStrength { get; private set; } = default;
        public float defaultSinkStrength { get; private set; } = default;
        public ComputeShader SimulationShader { get; private set; } = default;
        public ComputeShader AdapterShader { get; private set; } = default;
        public float ViscosityCoeffecient { get; private set; } = default;
        public float PressureCoeffecient { get; private set; } = default;
        public float MaxInfluenceStrength { get; private set; } = 1.0f;

        public class Builder
        {
            readonly SimulationParameters simulationParameters;
            public Builder()
            {
                simulationParameters = new SimulationParameters();
            }

            public Builder WithResolution(Vector2Int resolution)
            {
                simulationParameters.gridResolution = resolution;
                return this;
            }
            public Builder WithIterations(int iterations)
            {
                simulationParameters.iterations = iterations;
                return this;
            }
            public Builder WithBounds(Bounds bounds)
            {
                simulationParameters.bounds = bounds;
                return this;
            }
            public Builder WithSourceStrength(float strength)
            {
                simulationParameters.defaultSourceStrength = strength;
                return this;
            }
            public Builder WithSinkStrength(float strength)
            {
                simulationParameters.defaultSinkStrength = strength;
                return this;
            }
            public Builder WithViscosity(float viscosity)
            {
                simulationParameters.ViscosityCoeffecient = viscosity;
                return this;
            }
            public Builder WithPressure(float pressure)
            {
                simulationParameters.PressureCoeffecient = pressure;
                return this;
            }
            public Builder WithMaxInfluenceStrength(float maxInfluenceStrength)
            {
                simulationParameters.MaxInfluenceStrength = maxInfluenceStrength;
                return this;
            }
            public Builder WithSimulation(ComputeShader simulation)
            {
                simulationParameters.SimulationShader = simulation;
                return this;
            }
            public Builder WithAdapter(ComputeShader adapter)
            {
                simulationParameters.AdapterShader = adapter;
                return this;
            }
            public SimulationParameters Build()
            {
                return simulationParameters;
            }
        }
    }
    public class NavMeshAdapter
    {
        private Vector2Int resolution;
        private Bounds bounds;
        private Texture2D boundryTexture;
        private bool shouldUpdateTexture;
        private ComputeShader shader;

        public NavMeshAdapter(Vector2Int resolution, Bounds bounds, ComputeShader shader)
        {
            this.resolution = resolution;
            this.bounds = bounds;
            this.shader = shader;
        }

        public Texture2D GenerateBoundaryTexture()
        {
            if (boundryTexture && !shouldUpdateTexture)
            {
                return boundryTexture;
            }
            Texture2D output = default;

            Mesh mesh = GenerateFlattenedMesh();
            shouldUpdateTexture = GenerateTextureFromMesh(mesh, out output);

            return output;
        }

        private Mesh GenerateFlattenedMesh()
        {
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            Mesh output = new Mesh();

            Vector3[] verts3d = triangulation.vertices;
            Vector3[] verts2d = new Vector3[verts3d.Length];

            for(int i = 0; i < verts3d.Length; i++)
            {
                verts2d[i] = new Vector3(verts3d[i].x, 0, verts3d[i].z);
            }

            output.vertices = verts2d;
            output.triangles = triangulation.indices;

            return output;
        }

        private bool GenerateTextureFromMesh(Mesh flattenedMesh, out Texture2D output)
        {
            output = default;

            ComputeBuffer vertexBuffer = new ComputeBuffer(flattenedMesh.vertices.Length, sizeof(float) * 3);
            vertexBuffer.SetData(flattenedMesh.vertices);

            ComputeBuffer triangleBuffer = new ComputeBuffer(flattenedMesh.triangles.Length, sizeof(int));
            triangleBuffer.SetData(flattenedMesh.triangles);

            RenderTexture computeTexture = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.ARGB32);
            computeTexture.enableRandomWrite = true;
            computeTexture.Create();

            int kernel = shader.FindKernel("GenerateTexture");

            shader.SetBuffer(kernel, "Verticies", vertexBuffer);
            shader.SetBuffer(kernel, "Triangles", triangleBuffer);
            shader.SetTexture(kernel, "Output", computeTexture);
            shader.SetInt("VertexCount", flattenedMesh.vertices.Length);
            shader.SetInt("TriangleCount", flattenedMesh.triangles.Length/3);
            shader.SetInts("TextureSize", resolution.x, resolution.y);
            shader.SetFloats("boundsMin", bounds.min.x, bounds.min.y);
            shader.SetFloats("boundsMax", bounds.max.x, bounds.max.y);

            int threadsX = (int)MathF.Ceiling(bounds.min.x / 8.0f);
            int threadsY = (int)MathF.Ceiling(bounds.max.y / 8.0f);
            shader.Dispatch(kernel,threadsX, threadsY,1);

            output = new Texture2D(resolution.x, resolution.y);
            RenderTexture.active = computeTexture;
            output.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0);
            output.Apply();
            RenderTexture.active = default;

            vertexBuffer.Release();
            triangleBuffer.Release();
            computeTexture.Release();

            boundryTexture = output;

            return true;
        }

        public Vector2 WorldToGridPosition(Vector3 position)
        {
            float normX = (position.x - bounds.min.x) / (bounds.max.x - bounds.min.x);
            float normY = (position.y - bounds.min.y) / (bounds.max.y - bounds.min.y);
            return new Vector2(normX, normY);
        }

        public Vector3 GridToWorldPosition(Vector2 position) 
        {
            float worldX = Mathf.Lerp(bounds.min.x, bounds.max.x, position.x);
            float worldY = UnityEngine.Terrain.activeTerrain.SampleHeight(new Vector3(position.x,0,position.y));
            float worldZ = Mathf.Lerp(bounds.min.y,bounds.max.y,position.y);
            return new Vector3(worldX,worldY,worldZ);
        }
    }
    public class FluidSimulator
    {
        private ComputeShader simulationShader;
        //textures
        private RenderTexture velocityTexture;
        private RenderTexture velocityTexturePrev;
        private RenderTexture pressureTexture;
        private RenderTexture pressureTexturePrev;
        private RenderTexture divergenceTexture;
        private RenderTexture boundaryTexture;
        private RenderTexture visualizationTexture;
        //kernels
        private int advectionKernelId;
        private int diffusionKernelId;
        private int divergenceKernelId;
        private int pressureGradientKernelId;
        private int pressureSolveKernelId;
        private int applyBoundariesKernelId;
        private int applyInfluencesKernelId;
        private int visualizationKernelId;
        //Simulation parameters
        private bool parametersSet = false;
        private Vector2Int resolution;
        private Bounds bounds;
        private float viscosityCoeffecient; // Matching SimulationParameters spelling
        private float pressureCoeffecient; // Matching SimulationParameters spelling
        private int iterationCount;
        private Vector2 inverseGridScale;
        private float maxInfluenceIntensity;
        private float defaultSourceStrength;
        private float defaultSinkStrength;
        private float defaultInfluenceRadius = 2.0f;

        // Struct to match the InfluencePoint structure in the shader
        private struct ShaderInfluencePoint
        {
            public Vector3 position;
            public float strength;
            public float radius;
            public int type;
            public Vector3 direction;
        }

        // Influence buffer for passing dynamic influences to the shader
        private ComputeBuffer influenceBuffer;
        private const int MAX_INFLUENCES = 32; // Maximum number of influences to support

        public FluidSimulator()
        {
            InitializeSimulator();
        }

        private void InitializeSimulator()
        {
            // We'll use the simulation shader from SimulationParameters instead of loading it directly

            // Create the influence buffer
            influenceBuffer = new ComputeBuffer(MAX_INFLUENCES,
                sizeof(float) * 3 + // position (Vector3)
                sizeof(float) +     // strength
                sizeof(float) +     // radius
                sizeof(int) +       // type
                sizeof(float) * 3); // direction (Vector3)
        }

        public void SetBounds(Bounds bounds)
        {
            this.bounds = bounds;

            // Update shader parameters if textures are already created
            if (parametersSet)
            {
                simulationShader.SetVector("WorldBounds", new Vector4(
                    bounds.min.x, bounds.min.z,  // Using X and Z for a 2D simulation
                    bounds.max.x, bounds.max.z
                ));
            }
        }

        public void SetBoundaryTexture(Texture2D texture)
        {
            if (texture == null || boundaryTexture == null) return;
            Graphics.Blit(texture, boundaryTexture);
            BindTextureToAllKernels("BoundaryTexture", boundaryTexture);
        }

        public void SetSimulationParameters(SimulationParameters parameters)
        {
            // Get simulation shader from the parameters
            simulationShader = parameters.SimulationShader;

            if (simulationShader == null)
            {
                Debug.LogError("Simulation shader is not assigned in parameters!");
                return;
            }

            // Initialize kernel IDs after setting the shader
            try
            {
                advectionKernelId = simulationShader.FindKernel("Advection");
                diffusionKernelId = simulationShader.FindKernel("Diffusion");
                divergenceKernelId = simulationShader.FindKernel("Divergence");
                pressureSolveKernelId = simulationShader.FindKernel("PressureSolve");
                pressureGradientKernelId = simulationShader.FindKernel("PressureGradient");
                applyBoundariesKernelId = simulationShader.FindKernel("ApplyBoundaries");
                applyInfluencesKernelId = simulationShader.FindKernel("ApplyInfluences");
                visualizationKernelId = simulationShader.FindKernel("Visualization");

                Debug.Log("Successfully found all compute shader kernels");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error finding compute shader kernels: {e.Message}");
                return;
            }

            resolution = parameters.gridResolution;
            bounds = parameters.bounds;
            viscosityCoeffecient = parameters.ViscosityCoeffecient; // Using consistent spelling
            pressureCoeffecient = parameters.PressureCoeffecient; // Using consistent spelling
            iterationCount = parameters.iterations;
            maxInfluenceIntensity = parameters.MaxInfluenceStrength;
            defaultSourceStrength = parameters.defaultSourceStrength;
            defaultSinkStrength = parameters.defaultSinkStrength;

            inverseGridScale = new Vector2(
                resolution.x / (bounds.max.x - bounds.min.x),
                resolution.y / (bounds.max.z - bounds.min.z)
            );

            // Initialize textures if not already created or if resolution changed
            InitializeTextures();

            // Set shader parameters
            simulationShader.SetVector("GridSize", new Vector2(resolution.x, resolution.y));
            simulationShader.SetVector("WorldBounds", new Vector4(
                bounds.min.x, bounds.min.z,
                bounds.max.x, bounds.max.z
            ));
            simulationShader.SetVector("InverseGridScale", inverseGridScale);
            simulationShader.SetFloat("ViscosityCoefficient", viscosityCoeffecient);
            simulationShader.SetFloat("PressureCoefficient", pressureCoeffecient);
            simulationShader.SetInt("IterationCount", iterationCount);

            // Bind textures to kernels
            BindTexturesToKernels();

            parametersSet = true;
        }

        private void InitializeTextures()
        {
            // Helper function to create or recreate a RenderTexture
            RenderTexture CreateOrUpdateTexture(RenderTexture current, RenderTextureFormat format)
            {
                if (current != null)
                {
                    // Only recreate if resolution changed
                    if (current.width == (int)resolution.x &&
                        current.height == (int)resolution.y)
                    {
                        return current;
                    }

                    current.Release();
                }

                // Create new texture
                RenderTexture texture = new RenderTexture(
                    (int)resolution.x,
                    (int)resolution.y,
                    0,
                    format,
                    RenderTextureReadWrite.Linear
                );
                texture.enableRandomWrite = true;
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.Create();

                return texture;
            }

            // Create or update all textures
            velocityTexture = CreateOrUpdateTexture(velocityTexture, RenderTextureFormat.ARGBFloat);
            velocityTexturePrev = CreateOrUpdateTexture(velocityTexturePrev, RenderTextureFormat.ARGBFloat);
            pressureTexture = CreateOrUpdateTexture(pressureTexture, RenderTextureFormat.RFloat);
            pressureTexturePrev = CreateOrUpdateTexture(pressureTexturePrev, RenderTextureFormat.RFloat);
            divergenceTexture = CreateOrUpdateTexture(divergenceTexture, RenderTextureFormat.RFloat);
            boundaryTexture = CreateOrUpdateTexture(boundaryTexture, RenderTextureFormat.ARGB32);
            visualizationTexture = CreateOrUpdateTexture(visualizationTexture, RenderTextureFormat.ARGB32);

            // Clear textures
            ClearTextures();
        }

        private void ClearTextures()
        {
            // Create temporary RenderTexture for clearing
            RenderTexture tempRT = RenderTexture.GetTemporary(
                (int)resolution.x,
                (int)resolution.y,
                0,
                RenderTextureFormat.ARGBFloat
            );

            // Clear to zero
            Graphics.SetRenderTarget(tempRT);
            GL.Clear(true, true, Color.clear);

            // Copy cleared texture to all simulation textures
            Graphics.Blit(tempRT, velocityTexture);
            Graphics.Blit(tempRT, velocityTexturePrev);
            Graphics.Blit(tempRT, pressureTexture);
            Graphics.Blit(tempRT, pressureTexturePrev);
            Graphics.Blit(tempRT, divergenceTexture);

            // For visualization we want a more visible default
            Graphics.SetRenderTarget(visualizationTexture);
            GL.Clear(true, true, Color.black);

            // Release temporary texture
            RenderTexture.ReleaseTemporary(tempRT);
        }

        public void Update(float dt)
        {
            if (!parametersSet || simulationShader == null)
            {
                Debug.LogWarning("Cannot update fluid simulation: parameters not set or shader not loaded");
                return;
            }

            try
            {
                // Set time step for the simulation
                simulationShader.SetFloat("DeltaTime", dt);

                // Step 0: Apply cached influences (sources and sinks) at the beginning of each update
                ApplyInfluencesToTexture();

                // Step 1: Advection - move quantities along the velocity field
                RunAdvection();

                // Step 2: Diffusion - spread velocity to neighboring cells (if viscosity is significant)
                RunDiffusion();

                // Step 3: Projection - make the velocity field divergence-free
                RunProjection();

                // Step 4: Apply boundary conditions
                ApplyBoundaries();

                // Step 5: Update visualization
                UpdateVisualization();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error during fluid simulation update: {e.Message}");
            }
        }

        private void RunAdvection()
        {
            // Dispatch advection kernel
            simulationShader.Dispatch(
                advectionKernelId,
                Mathf.CeilToInt(resolution.x / 8.0f),
                Mathf.CeilToInt(resolution.y / 8.0f),
                1
            );
        }

        private void RunDiffusion()
        {
            // Only run diffusion if viscosity is significant
            if (viscosityCoeffecient > 0.0001f)
            {
                simulationShader.Dispatch(
                    diffusionKernelId,
                    Mathf.CeilToInt(resolution.x / 8.0f),
                    Mathf.CeilToInt(resolution.y / 8.0f),
                    1
                );
            }
        }

        private void RunProjection()
        {
            // Step 3a: Calculate divergence
            simulationShader.Dispatch(
                divergenceKernelId,
                Mathf.CeilToInt(resolution.x / 8.0f),
                Mathf.CeilToInt(resolution.y / 8.0f),
                1
            );

            // Step 3b: Solve pressure equation
            simulationShader.Dispatch(
                pressureSolveKernelId,
                Mathf.CeilToInt(resolution.x / 8.0f),
                Mathf.CeilToInt(resolution.y / 8.0f),
                1
            );

            // Step 3c: Subtract pressure gradient
            simulationShader.Dispatch(
                pressureGradientKernelId,
                Mathf.CeilToInt(resolution.x / 8.0f),
                Mathf.CeilToInt(resolution.y / 8.0f),
                1
            );
        }

        private void ApplyBoundaries()
        {
            // Apply boundaries
            simulationShader.Dispatch(
                applyBoundariesKernelId,
                Mathf.CeilToInt(resolution.x / 8.0f),
                Mathf.CeilToInt(resolution.y / 8.0f),
                1
            );
        }

        private void UpdateVisualization()
        {
            // Dispatch the visualization kernel
            simulationShader.Dispatch(
                visualizationKernelId,
                Mathf.CeilToInt(resolution.x / 8.0f),
                Mathf.CeilToInt(resolution.y / 8.0f),
                1
            );
        }

        // Collection to cache influences
        private List<FlowFieldInfluence> cachedInfluences = new List<FlowFieldInfluence>();

        internal void ApplyInfluences(IEnumerable<FlowFieldInfluence> flowFieldInfluences)
        {
            // First, update our cached collection of influences for use in the Update method
            cachedInfluences.Clear();
            foreach (var pair in VectorFlowFieldManager.GetInstance().influenceManager.GetInfluences())
            {
                if (pair.Value != null && pair.Value.active)
                {
                    cachedInfluences.Add(pair.Value);
                }
            }

            ApplyInfluencesToTexture();
        }
            private void ApplyInfluencesToTexture()
            {
                if (!parametersSet || simulationShader == null || boundaryTexture == null || cachedInfluences.Count == 0)
                {
                    return;
                }

                try
                {
                    // Create a temporary texture to modify
                    RenderTexture tempTexture = RenderTexture.GetTemporary(
                        boundaryTexture.width,
                        boundaryTexture.height,
                        0,
                        RenderTextureFormat.ARGB32
                    );

                    // Copy the current boundary texture as a starting point
                    Graphics.Blit(boundaryTexture, tempTexture);

                    // Create a temporary readable texture for modifications
                    RenderTexture.active = tempTexture;
                    Texture2D modifiableTexture = new Texture2D(tempTexture.width, tempTexture.height, TextureFormat.RGBA32, false);
                    modifiableTexture.ReadPixels(new Rect(0, 0, tempTexture.width, tempTexture.height), 0, 0);
                    modifiableTexture.Apply();
                    RenderTexture.active = null;

                    // Keep track of whether any modifications were made
                    bool textureModified = false;

                    // Collect all influences by position first, so we can handle multiple influences
                    // at the same location (a cell can be both source and sink)
                    Dictionary<Vector2Int, List<FlowFieldInfluence>> influencesByPosition = new Dictionary<Vector2Int, List<FlowFieldInfluence>>();

                    foreach (var influence in cachedInfluences)
                    {
                        // Skip invalid or inactive influences
                        if (influence == null || !influence.active) continue;

                        // Convert world position to normalized grid coordinates (0-1)
                        Vector2 normalizedPos = new Vector2(
                            Mathf.InverseLerp(bounds.min.x, bounds.max.x, influence.position.x),
                            Mathf.InverseLerp(bounds.min.z, bounds.max.z, influence.position.z)
                        );

                        // Convert to texture coordinates
                        int texX = Mathf.Clamp(Mathf.FloorToInt(normalizedPos.x * modifiableTexture.width), 0, modifiableTexture.width - 1);
                        int texY = Mathf.Clamp(Mathf.FloorToInt(normalizedPos.y * modifiableTexture.height), 0, modifiableTexture.height - 1);
                        Vector2Int texPos = new Vector2Int(texX, texY);

                        // Add influence to the collection
                        if (!influencesByPosition.ContainsKey(texPos))
                        {
                            influencesByPosition[texPos] = new List<FlowFieldInfluence>();
                        }
                        influencesByPosition[texPos].Add(influence);
                    }

                    // Now process each position that has influences
                    foreach (var kvp in influencesByPosition)
                    {
                        Vector2Int texPos = kvp.Key;
                        List<FlowFieldInfluence> positionInfluences = kvp.Value;

                        // Get current color at position - we'll keep the blue channel
                        // as it might represent boundaries
                        Color currentColor = modifiableTexture.GetPixel(texPos.x, texPos.y);
                        float boundaryValue = currentColor.b; // Preserve boundary information

                        // Start with a clean color that preserves boundary information
                        Color newColor = new Color(0, 0, boundaryValue, 0);

                        // Process all influences at this position
                        float sourceStrength = 0;
                        float sinkStrength = 0;

                        foreach (var influence in positionInfluences)
                        {
                            // Determine strength based on influence type
                            float strength = influence.strength;

                            // Apply strength based on influence type
                            if (influence.type == InfluenceType.Source)
                            {
                                if (strength == 0) strength = defaultSourceStrength;
                                sourceStrength += strength;
                            }
                            else if (influence.type == InfluenceType.Sink)
                            {
                                if (strength == 0) strength = defaultSinkStrength;
                                sinkStrength += Mathf.Abs(strength); // Ensure positive for red channel
                            }
                        }

                        // Normalize the strengths
                        float normalizedSourceStrength = Mathf.InverseLerp(0, maxInfluenceIntensity, sourceStrength);
                        float normalizedSinkStrength = Mathf.InverseLerp(0, maxInfluenceIntensity, sinkStrength);

                        // Set the color channels
                        newColor.g = normalizedSourceStrength;
                        newColor.r = normalizedSinkStrength;

                        // A cell is a fluid cell only if it's not a boundary, source, or sink
                        // So alpha remains 0 for source/sink cells

                        // Update the pixel in the texture
                        modifiableTexture.SetPixel(texPos.x, texPos.y, newColor);
                        textureModified = true;
                    }

                    // If no modifications were made, skip the update
                    if (!textureModified)
                    {
                        RenderTexture.ReleaseTemporary(tempTexture);
                        UnityEngine.Object.Destroy(modifiableTexture);
                        return;
                    }

                    // Apply all texture modifications
                    modifiableTexture.Apply();

                    // Copy back to the boundary texture
                    Graphics.Blit(modifiableTexture, boundaryTexture);

                    // Clean up temporary resources
                    RenderTexture.ReleaseTemporary(tempTexture);
                    UnityEngine.Object.Destroy(modifiableTexture);

                    // Since the boundary texture has been updated, rebind it to all kernels
                    BindTextureToAllKernels("BoundaryTexture", boundaryTexture);

                    // Apply sources and sinks (no need to pass the buffer since it uses the texture)
                    simulationShader.Dispatch(
                        applyInfluencesKernelId,
                        Mathf.CeilToInt(resolution.x / 8.0f),
                        Mathf.CeilToInt(resolution.y / 8.0f),
                        1
                    );
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error applying influences: {e.Message}");
                }
            }

            public RenderTexture GetVisualizationTexture()
            {
                return visualizationTexture;
            }

            public Vector3 SampleVelocityField(Vector3 worldPosition)
            {
                if (!parametersSet || velocityTexture == null)
                {
                    return Vector3.zero;
                }

                // Convert world position to normalized grid coordinates (0-1)
                Vector2 normalizedPos = new Vector2(
                    Mathf.InverseLerp(bounds.min.x, bounds.max.x, worldPosition.x),
                    Mathf.InverseLerp(bounds.min.z, bounds.max.z, worldPosition.z)
                );

                // Check if position is within bounds
                if (normalizedPos.x < 0 || normalizedPos.x > 1 ||
                    normalizedPos.y < 0 || normalizedPos.y > 1)
                {
                    return Vector3.zero;
                }

                // If the AdapterShader is available in the parameters, we can use it to sample the texture
                // But for compatibility with the code without requiring another shader, we'll keep the
                // direct sampling approach

                // Create a 1x1 temporary texture for reading the pixel value
                RenderTexture tempRT = RenderTexture.GetTemporary(1, 1, 0, RenderTextureFormat.ARGBFloat);

                // Create a temporary material for sampling with bilinear filtering
                Material samplingMaterial = new Material(Shader.Find("Unlit/Texture"));

                // Set up sampling coordinates
                samplingMaterial.mainTexture = velocityTexture;
                samplingMaterial.mainTextureOffset = normalizedPos;
                samplingMaterial.mainTextureScale = Vector2.zero; // Sample a single point

                // Render to the temporary texture
                Graphics.Blit(null, tempRT, samplingMaterial);

                // Read back the pixel value
                RenderTexture.active = tempRT;
                Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
                tex.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                tex.Apply();
                Color pixelColor = tex.GetPixel(0, 0);
                RenderTexture.active = null;

                // Clean up
                RenderTexture.ReleaseTemporary(tempRT);
                UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(samplingMaterial);

                // Extract velocity (stored in R and G channels)
                // For a 2D simulation, we map to X and Z in world space
                return new Vector3(pixelColor.r, 0, pixelColor.g);
            }

            public void Release()
            {
                // Release all textures
                if (velocityTexture != null) velocityTexture.Release();
                if (velocityTexturePrev != null) velocityTexturePrev.Release();
                if (pressureTexture != null) pressureTexture.Release();
                if (pressureTexturePrev != null) pressureTexturePrev.Release();
                if (divergenceTexture != null) divergenceTexture.Release();
                if (boundaryTexture != null) boundaryTexture.Release();
                if (visualizationTexture != null) visualizationTexture.Release();

                // Release compute buffer
                if (influenceBuffer != null) influenceBuffer.Release();

                // Clear references
                velocityTexture = null;
                velocityTexturePrev = null;
                pressureTexture = null;
                pressureTexturePrev = null;
                divergenceTexture = null;
                boundaryTexture = null;
                visualizationTexture = null;
                influenceBuffer = null;

                parametersSet = false;
            }

            //Helper Functions
            /// <summary>
            /// Binds all textures to their respective compute shader kernels.
            /// </summary>
            private void BindTexturesToKernels()
            {
                // Get all kernel IDs
                int[] kernels = new int[]
                {
                    advectionKernelId,
                    diffusionKernelId,
                    divergenceKernelId,
                    pressureSolveKernelId,
                    pressureGradientKernelId,
                    applyBoundariesKernelId,
                    applyInfluencesKernelId,
                    visualizationKernelId
                };

                // Bind each texture to all relevant kernels
                foreach (int kernel in kernels)
                {
                    simulationShader.SetTexture(kernel, "VelocityTexture", velocityTexture);
                    simulationShader.SetTexture(kernel, "VelocityTexturePrev", velocityTexturePrev);
                    simulationShader.SetTexture(kernel, "PressureTexture", pressureTexture);
                    simulationShader.SetTexture(kernel, "PressureTexturePrev", pressureTexturePrev);
                    simulationShader.SetTexture(kernel, "DivergenceTexture", divergenceTexture);
                    simulationShader.SetTexture(kernel, "BoundaryTexture", boundaryTexture);
                }

                // Bind visualization texture to visualization kernel
                simulationShader.SetTexture(visualizationKernelId, "VisualizationTexture", visualizationTexture);
            }

            /// <summary>
            /// Binds a specific texture to all kernels in the compute shader.
            /// </summary>
            private void BindTextureToAllKernels(string textureName, RenderTexture texture)
            {
                int[] kernels = new int[]
                {
                    advectionKernelId,
                    diffusionKernelId,
                    divergenceKernelId,
                    pressureSolveKernelId,
                    pressureGradientKernelId,
                    applyBoundariesKernelId,
                    applyInfluencesKernelId,
                    visualizationKernelId
                };

                foreach (int kernel in kernels)
                {
                    simulationShader.SetTexture(kernel, textureName, texture);
                }
            }
        }
    public class SourceSinkManager
    {
        public float defaultSourceStrength { get; private set; }
        public float defaultSinkStrength { get; private set; }

        private Dictionary<int,FlowFieldInfluence> influencesMap = new Dictionary<int,FlowFieldInfluence>();

        public SourceSinkManager(float defaultSourceStrength, float defaultSinkStrength)
        {
            this.defaultSourceStrength = defaultSourceStrength;
            this.defaultSinkStrength = defaultSinkStrength;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="go">GameObject representing the influence object</param>
        /// <param name="type">Is the object supposed to be a sink or source?</param>
        /// <param name="strength">How powerful is this objects attraction / repulsion? (should always be positive)</param>
        /// <returns></returns>
        public bool TryAddInfluence(GameObject go, InfluenceType type, float strength = default)
        {
            float effectiveStrength = strength;

            if(effectiveStrength == default)
            {
                switch (type)
                {
                    case InfluenceType.Source:
                        effectiveStrength = defaultSourceStrength;
                        break;
                    case InfluenceType.Sink:
                        effectiveStrength = defaultSinkStrength;
                        break;
                }
            }

            return influencesMap.TryAdd
                (
                    go.GetInstanceID(),
                    new FlowFieldInfluence.Builder()
                    .WithID(go)
                    .OfType(type)
                    .AtPosition(go.transform.position)
                    .WithStrength(effectiveStrength)
                    .IsActive(true)
                    .Build()
                );
        }
        public bool RemoveInfluence(GameObject go)
        {
            if(influencesMap.ContainsKey(go.GetInstanceID()))
            {
                influencesMap.Remove(go.GetInstanceID());
                return true;
            }
            return false;
        }
        public bool UpdateInfluencePosition(GameObject go,Vector3 position)
        {
            if(influencesMap.TryGetValue(go.GetInstanceID(),out var influence))
            {
                influence.position = position;
                return true;
            }
            return false;
        }
        public bool ChangeInfluenceState(GameObject go, bool IsActive)
        {
            if (influencesMap.TryGetValue(go.GetInstanceID(), out var influence))
            {
                influence.active = IsActive;
                return true;
            }
            return false;
        }
        internal Dictionary<int,FlowFieldInfluence> GetInfluences()
        {
            return influencesMap;
        }
    }

    internal struct Influences
    {
        public List<string> ids;
        public List<Vector3> positions;
        public List<float> strengths;
        public List<InfluenceType> types;
        public List<bool> active;
    }

    internal class FlowFieldInfluence
    {
        public GameObject id;
        public Vector3 position;
        public float strength;
        public bool active;
        public InfluenceType type;
        public class Builder
        {
            FlowFieldInfluence influence;

            public Builder WithID(GameObject id)
            {
                influence.id = id;
                return this;
            }
            public Builder AtPosition(Vector3 position)
            {
                influence.position = position; 
                return this;
            }
            public Builder WithStrength(float strength)
            {
                influence.strength = strength;
                return this;
            }
            public Builder OfType(InfluenceType type)
            {
                influence.type = type; 
                return this;
            }
            public Builder IsActive(bool active)
            {
                influence.active = active; 
                return this;
            }
            public FlowFieldInfluence Build()
            {
                return influence;
            }
        }
    }

    public enum InfluenceType
    {
        Source,
        Sink,
        Directional,
    }
}