using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

namespace VFF
{
    /// <summary>
    /// Handles the computational aspects of the Vector Flow Field simulation using Navier-Stokes equations.
    /// </summary>
    public class NavierStokesSolver
    {
        private ComputeShader computeShader;
        private int setupFieldKernel;
        private int generateBoundaryInfoKernel;
        private int advectKernel;
        private int diffuseBatchKernel;
        private int computeDivergenceKernel;
        private int solvePressureBatchKernel;
        private int propagateGlobalPressureKernel;
        private int projectKernel;
        private int applySinkSourceKernel;
        private int extrapolateVelocityKernel;
        private int fillZeroVelocityKernel;
        private int addVorticityKernel;
        private int samplePointKernel;

        private RenderTexture velocityTexture;
        private RenderTexture velocityTempTexture;
        private RenderTexture pressureTexture;
        private RenderTexture pressureTempTexture;
        private RenderTexture globalPressureTexture;
        private RenderTexture divergenceTexture;
        private RenderTexture boundaryInfoTexture;
        private ComputeBuffer sampleResultBuffer;

        private ComputeBuffer velocityBuffer;
        private ComputeBuffer velocityTempBuffer;
        private ComputeBuffer pressureBuffer;
        private ComputeBuffer pressureTempBuffer;
        private ComputeBuffer divergenceBuffer;
        private ComputeBuffer boundaryInfoBuffer;

        private Texture2D inputTexture;

        private Vector2Int resolution;
        private float viscosity;
        private int pressureIterations;
        private int diffusionIterations;
        private float sinkStrength;
        private float sourceStrength;
        private float globalPressureStrength;
        private int globalPressureIterations;

        private const int DIFFUSION_BATCH_SIZE = 5;
        private const int PRESSURE_BATCH_SIZE = 10;
        private const int GLOBAL_PRESSURE_ITERATIONS = 20;

        private bool isInitialized;
        private bool debugMode = true; // Enable debug logging

        /// <summary>
        /// Gets the velocity texture containing the raw vector field data.
        /// </summary>
        public RenderTexture VelocityTexture => velocityTexture;

        /// <summary>
        /// Initializes a new instance of the NavierStokesSolver class.
        /// </summary>
        /// <param name="computeShader">The compute shader to use for the simulation.</param>
        /// <param name="inputTexture">The input texture defining the field space, sinks, and sources.</param>
        /// <param name="resolution">The resolution of the simulation grid.</param>
        /// <param name="viscosity">The viscosity of the fluid.</param>
        /// <param name="pressureIterations">The number of pressure solver iterations.</param>
        /// <param name="diffusionIterations">The number of diffusion solver iterations.</param>
        /// <param name="sinkStrength">The strength of sink forces.</param>
        /// <param name="sourceStrength">The strength of source forces.</param>
        /// <param name="globalPressureStrength">The strength of global pressure influence (default is 0.5).</param>
        /// <param name="globalPressureIterations">The number of global pressure iterations (default is 20).</param>
        public NavierStokesSolver(ComputeShader computeShader, Texture2D inputTexture, Vector2Int resolution,
                                 float viscosity, int pressureIterations, int diffusionIterations,
                                 float sinkStrength, float sourceStrength,
                                 float globalPressureStrength = 0.5f, int globalPressureIterations = 20)
        {
            this.computeShader = computeShader;
            this.inputTexture = inputTexture;
            this.resolution = resolution;
            this.viscosity = viscosity;
            this.pressureIterations = pressureIterations;
            this.diffusionIterations = diffusionIterations;
            this.sinkStrength = sinkStrength;
            this.sourceStrength = sourceStrength;
            this.globalPressureStrength = globalPressureStrength;
            this.globalPressureIterations = globalPressureIterations;

            Initialize();
        }

        /// <summary>
        /// Initializes the solver by creating textures and finding kernel indices.
        /// </summary>
        private void Initialize()
        {
            if (computeShader == null || inputTexture == null)
            {
                Debug.LogError("NavierStokesSolver: Compute shader or input texture is null.");
                return;
            }

            if (debugMode)
            {
                ValidateInputTexture();
            }

            setupFieldKernel = computeShader.FindKernel("SetupField");
            generateBoundaryInfoKernel = computeShader.FindKernel("GenerateBoundaryInfo");
            advectKernel = computeShader.FindKernel("Advect");
            diffuseBatchKernel = computeShader.FindKernel("DiffuseBatch");
            computeDivergenceKernel = computeShader.FindKernel("ComputeDivergence");
            solvePressureBatchKernel = computeShader.FindKernel("SolvePressureBatch");
            propagateGlobalPressureKernel = computeShader.FindKernel("PropagateGlobalPressure");
            projectKernel = computeShader.FindKernel("Project");
            applySinkSourceKernel = computeShader.FindKernel("ApplySinkSource");
            extrapolateVelocityKernel = computeShader.FindKernel("ExtrapolateVelocity");
            fillZeroVelocityKernel = computeShader.FindKernel("FillZeroVelocity");
            addVorticityKernel = computeShader.FindKernel("AddVorticity");
            samplePointKernel = computeShader.FindKernel("SamplePoint");

            CreateTextures();

            // Create the sample result buffer
            sampleResultBuffer = new ComputeBuffer(1, sizeof(float) * 4); // Changed from 2 to 4 for ARGB format
            computeShader.SetBuffer(samplePointKernel, "SampleResult", sampleResultBuffer);

            computeShader.SetTexture(setupFieldKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(generateBoundaryInfoKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(advectKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(computeDivergenceKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(solvePressureBatchKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(propagateGlobalPressureKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(projectKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(applySinkSourceKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(extrapolateVelocityKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(fillZeroVelocityKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(addVorticityKernel, "InputTexture", inputTexture);

            Vector2 texelSize = new Vector2(1.0f / resolution.x, 1.0f / resolution.y);
            computeShader.SetVector("TexelSize", texelSize);
            computeShader.SetInts("Resolution", new int[] { resolution.x, resolution.y });

            computeShader.SetFloat("Viscosity", viscosity);
            computeShader.SetFloat("SinkStrength", sinkStrength);
            computeShader.SetFloat("SourceStrength", sourceStrength);
            computeShader.SetFloat("GlobalPressureStrength", globalPressureStrength);
            computeShader.SetInt("DiffusionBatchSize", DIFFUSION_BATCH_SIZE);
            computeShader.SetInt("PressureBatchSize", PRESSURE_BATCH_SIZE);
            computeShader.SetInt("GlobalPressureIterations", globalPressureIterations);

            if (debugMode) Debug.Log("NavierStokesSolver: Dispatching SetupField kernel");
            DispatchKernel(setupFieldKernel);

            if (debugMode) Debug.Log("NavierStokesSolver: Dispatching GenerateBoundaryInfo kernel");
            DispatchKernel(generateBoundaryInfoKernel);

            if (debugMode)
            {
                Debug.Log("NavierStokesSolver: Initialization complete");
                VerifyBoundaryInfo();
            }

            isInitialized = true;
        }

        /// <summary>
        /// Validates the input texture to ensure it has the expected format.
        /// </summary>
        private void ValidateInputTexture()
        {
            // Make the texture readable
            bool wasReadable = inputTexture.isReadable;
            if (!wasReadable)
            {
                Debug.LogWarning("NavierStokesSolver: Input texture is not readable. Some validation will be skipped.");
                return;
            }

            // Check if the texture has the expected format
            Color[] pixels = inputTexture.GetPixels();
            bool hasValidSpace = false;
            bool hasSinks = false;
            bool hasSources = false;
            int validSpaceCount = 0;
            int sinkCount = 0;
            int sourceCount = 0;

            foreach (Color pixel in pixels)
            {
                if (pixel.r > 0.5f && pixel.g > 0.5f && pixel.b > 0.5f)
                {
                    hasValidSpace = true;
                    validSpaceCount++;
                }
                if (pixel.r > 0.5f && pixel.g < 0.5f && pixel.b < 0.5f)
                {
                    hasSinks = true;
                    sinkCount++;
                }
                if (pixel.r < 0.5f && pixel.g > 0.5f && pixel.b < 0.5f)
                {
                    hasSources = true;
                    sourceCount++;
                }
            }

            Debug.Log($"Input texture validation: Valid space: {hasValidSpace} ({validSpaceCount} pixels), " +
                      $"Sinks: {hasSinks} ({sinkCount} pixels), Sources: {hasSources} ({sourceCount} pixels)");

            if (!hasValidSpace)
            {
                Debug.LogWarning("NavierStokesSolver: Input texture has no valid field space (white pixels).");
            }
        }

        /// <summary>
        /// Verifies that the boundary info texture is properly generated.
        /// </summary>
        private void VerifyBoundaryInfo()
        {
            if (boundaryInfoTexture == null)
            {
                Debug.LogError("NavierStokesSolver: BoundaryInfo texture is null.");
                return;
            }

            // Create a temporary texture to read from the boundary info texture
            Texture2D tempTexture = new Texture2D(resolution.x, resolution.y, TextureFormat.R8, false);
            RenderTexture prevRT = RenderTexture.active;
            RenderTexture.active = boundaryInfoTexture;
            tempTexture.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0);
            tempTexture.Apply();
            RenderTexture.active = prevRT;

            // Check if the texture has any non-zero values
            Color[] pixels = tempTexture.GetPixels();
            int nonZeroCount = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].r > 0.01f)
                {
                    nonZeroCount++;
                }
            }

            Debug.Log($"BoundaryInfo texture verification: {nonZeroCount} non-zero pixels out of {pixels.Length}");

            if (nonZeroCount == 0)
            {
                Debug.LogWarning("NavierStokesSolver: BoundaryInfo texture is all zeros. This will prevent pressure propagation.");
            }

            // Clean up
            Object.Destroy(tempTexture);
        }

        /// <summary>
        /// Converts a Vector2 to ARGB format where:
        /// A: -x (negative x component)
        /// R: +x (positive x component)
        /// G: -y (negative y component)
        /// B: +y (positive y component)
        /// </summary>
        private Color Vector2ToARGB(Vector2 vec)
        {
            return new Color(
                Mathf.Max(0, vec.x),   // R: +x (if positive)
                Mathf.Max(0, -vec.y),  // G: -y (if negative)
                Mathf.Max(0, vec.y),   // B: +y (if positive)
                Mathf.Max(0, -vec.x)   // A: -x (if negative)
            );
        }

        /// <summary>
        /// Converts an ARGB color to Vector2 where:
        /// x = R - A (+x - (-x))
        /// y = B - G (+y - (-y))
        /// </summary>
        private Vector2 ARGBToVector2(Color color)
        {
            return new Vector2(
                color.r - color.a,  // +x - (-x)
                color.b - color.g   // +y - (-y)
            );
        }

        /// <summary>
        /// Creates the textures used in the simulation.
        /// </summary>
        private void CreateTextures()
        {
            // Changed from RGFloat to ARGB32 for better handling of negative components
            velocityTexture = CreateRenderTexture(resolution.x, resolution.y, RenderTextureFormat.ARGB32);
            velocityTempTexture = CreateRenderTexture(resolution.x, resolution.y, RenderTextureFormat.ARGB32);

            pressureTexture = CreateRenderTexture(resolution.x, resolution.y, RenderTextureFormat.RFloat);
            pressureTempTexture = CreateRenderTexture(resolution.x, resolution.y, RenderTextureFormat.RFloat);
            globalPressureTexture = CreateRenderTexture(resolution.x, resolution.y, RenderTextureFormat.RFloat);

            divergenceTexture = CreateRenderTexture(resolution.x, resolution.y, RenderTextureFormat.RFloat);

            boundaryInfoTexture = CreateRenderTexture(resolution.x, resolution.y, RenderTextureFormat.R8);

            if (debugMode)
            {
                Debug.Log($"NavierStokesSolver: Created textures with resolution {resolution.x}x{resolution.y} using ARGB32 format for velocity");
            }

            velocityBuffer = new ComputeBuffer(resolution.x * resolution.y, sizeof(float) * 3);

            SetTexturesForKernels();
        }

        /// <summary>
        /// Creates a render texture with the specified parameters.
        /// </summary>
        private RenderTexture CreateRenderTexture(int width, int height, RenderTextureFormat format)
        {
            RenderTexture rt = new RenderTexture(width, height, 0, format);
            rt.enableRandomWrite = true;
            rt.filterMode = FilterMode.Point; // Use point filtering for precise values
            rt.wrapMode = TextureWrapMode.Clamp; // Use clamp to avoid edge artifacts
            rt.Create();

            // Clear the texture to ensure it starts with clean data
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prev;

            return rt;
        }

        /// <summary>
        /// Sets the textures for each kernel in the compute shader.
        /// </summary>
        private void SetTexturesForKernels()
        {
            computeShader.SetTexture(setupFieldKernel, "Velocity", velocityTexture);
            computeShader.SetTexture(setupFieldKernel, "Pressure", pressureTexture);
            computeShader.SetTexture(setupFieldKernel, "GlobalPressure", globalPressureTexture);
            computeShader.SetTexture(setupFieldKernel, "Divergence", divergenceTexture);

            computeShader.SetTexture(generateBoundaryInfoKernel, "BoundaryInfo", boundaryInfoTexture);

            computeShader.SetTexture(advectKernel, "Velocity", velocityTexture);
            computeShader.SetTexture(advectKernel, "VelocityTemp", velocityTempTexture);

            computeShader.SetTexture(diffuseBatchKernel, "Velocity", velocityTexture);
            computeShader.SetTexture(diffuseBatchKernel, "VelocityTemp", velocityTempTexture);

            computeShader.SetTexture(computeDivergenceKernel, "Velocity", velocityTexture);
            computeShader.SetTexture(computeDivergenceKernel, "Divergence", divergenceTexture);
            computeShader.SetTexture(computeDivergenceKernel, "BoundaryInfo", boundaryInfoTexture);

            computeShader.SetTexture(solvePressureBatchKernel, "Pressure", pressureTexture);
            computeShader.SetTexture(solvePressureBatchKernel, "PressureTemp", pressureTempTexture);
            computeShader.SetTexture(solvePressureBatchKernel, "Divergence", divergenceTexture);
            computeShader.SetTexture(solvePressureBatchKernel, "BoundaryInfo", boundaryInfoTexture);

            computeShader.SetTexture(propagateGlobalPressureKernel, "GlobalPressure", globalPressureTexture);
            computeShader.SetTexture(propagateGlobalPressureKernel, "BoundaryInfo", boundaryInfoTexture);

            computeShader.SetTexture(projectKernel, "Velocity", velocityTexture);
            computeShader.SetTexture(projectKernel, "Pressure", pressureTexture);
            computeShader.SetTexture(projectKernel, "GlobalPressure", globalPressureTexture);
            computeShader.SetTexture(projectKernel, "BoundaryInfo", boundaryInfoTexture);

            computeShader.SetTexture(applySinkSourceKernel, "Velocity", velocityTexture);

            computeShader.SetTexture(extrapolateVelocityKernel, "Velocity", velocityTexture);
            computeShader.SetTexture(extrapolateVelocityKernel, "GlobalPressure", globalPressureTexture);
            computeShader.SetTexture(extrapolateVelocityKernel, "InputTexture", inputTexture);

            // Set textures for the new kernels
            computeShader.SetTexture(fillZeroVelocityKernel, "Velocity", velocityTexture);
            computeShader.SetTexture(fillZeroVelocityKernel, "GlobalPressure", globalPressureTexture);
            computeShader.SetTexture(fillZeroVelocityKernel, "InputTexture", inputTexture);

            computeShader.SetTexture(addVorticityKernel, "Velocity", velocityTexture);
            computeShader.SetTexture(addVorticityKernel, "InputTexture", inputTexture);
        }

        /// <summary>
        /// Updates the simulation by performing a single simulation step.
        /// </summary>
        /// <param name="deltaTime">The time step for the simulation.</param>
        public void Update(float deltaTime)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("NavierStokesSolver: Solver is not initialized.");
                return;
            }

            computeShader.SetFloat("DeltaTime", deltaTime);

            if (debugMode) Debug.Log("NavierStokesSolver: Advection step");
            DispatchKernel(advectKernel);

            if (diffusionIterations > 0)
            {
                if (debugMode) Debug.Log($"NavierStokesSolver: Diffusion step with {diffusionIterations} iterations");
                int batchCount = Mathf.CeilToInt(diffusionIterations / (float)DIFFUSION_BATCH_SIZE);

                for (int batch = 0; batch < batchCount; batch++)
                {
                    int remainingIterations = diffusionIterations - batch * DIFFUSION_BATCH_SIZE;
                    int batchSize = Mathf.Min(DIFFUSION_BATCH_SIZE, remainingIterations);

                    computeShader.SetInt("DiffusionBatchSize", batchSize);

                    DispatchKernel(diffuseBatchKernel);
                }
            }

            if (debugMode) Debug.Log("NavierStokesSolver: Apply sink/source forces");
            DispatchKernel(applySinkSourceKernel);

            if (debugMode) Debug.Log("NavierStokesSolver: Compute divergence");
            DispatchKernel(computeDivergenceKernel);

            if (pressureIterations > 0)
            {
                if (debugMode) Debug.Log($"NavierStokesSolver: Solve pressure with {pressureIterations} iterations");
                int batchCount = Mathf.CeilToInt(pressureIterations / (float)PRESSURE_BATCH_SIZE);

                for (int batch = 0; batch < batchCount; batch++)
                {
                    int remainingIterations = pressureIterations - batch * PRESSURE_BATCH_SIZE;
                    int batchSize = Mathf.Min(PRESSURE_BATCH_SIZE, remainingIterations);

                    computeShader.SetInt("PressureBatchSize", batchSize);

                    DispatchKernel(solvePressureBatchKernel);
                }
            }

            // Propagate global pressure from sinks and sources
            if (globalPressureIterations > 0 && globalPressureStrength > 0)
            {
                if (debugMode) Debug.Log($"NavierStokesSolver: Global pressure propagation with {globalPressureIterations} iterations");

                // First, initialize global pressure from sinks and sources
                DispatchKernel(propagateGlobalPressureKernel);

                // Then propagate it across the field with multiple iterations
                for (int i = 0; i < globalPressureIterations; i++)
                {
                    DispatchKernel(propagateGlobalPressureKernel);
                }
            }

            if (debugMode) Debug.Log("NavierStokesSolver: Project velocity");
            DispatchKernel(projectKernel);

            // Extrapolate velocity from valid cells to invalid cells
            if (debugMode) Debug.Log("NavierStokesSolver: Extrapolate velocity");
            DispatchKernel(extrapolateVelocityKernel);

            // Fill zero-velocity regions within the field
            if (debugMode) Debug.Log("NavierStokesSolver: Fill zero-velocity regions");
            DispatchKernel(fillZeroVelocityKernel);

            // Add vorticity confinement to enhance rotational flow
            if (debugMode) Debug.Log("NavierStokesSolver: Add vorticity confinement");
            DispatchKernel(addVorticityKernel);
        }

        /// <summary>
        /// Dispatches a kernel with the appropriate thread group size.
        /// </summary>
        /// <param name="kernelIndex">The index of the kernel to dispatch.</param>
        private void DispatchKernel(int kernelIndex)
        {
            uint threadGroupSizeX, threadGroupSizeY, threadGroupSizeZ;
            computeShader.GetKernelThreadGroupSizes(kernelIndex, out threadGroupSizeX, out threadGroupSizeY, out threadGroupSizeZ);

            int groupsX = Mathf.CeilToInt(resolution.x / (float)threadGroupSizeX);
            int groupsY = Mathf.CeilToInt(resolution.y / (float)threadGroupSizeY);

            computeShader.Dispatch(kernelIndex, groupsX, groupsY, 1);
        }


        /// <summary>
        /// Updates the input texture used for the simulation.
        /// </summary>
        /// <param name="newInputTexture">The new input texture.</param>
        public void UpdateInputTexture(Texture2D newInputTexture)
        {
            if (newInputTexture == null)
            {
                Debug.LogError("NavierStokesSolver: New input texture is null.");
                return;
            }

            inputTexture = newInputTexture;

            if (debugMode)
            {
                ValidateInputTexture();
            }

            computeShader.SetTexture(setupFieldKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(generateBoundaryInfoKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(advectKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(computeDivergenceKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(solvePressureBatchKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(propagateGlobalPressureKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(projectKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(applySinkSourceKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(extrapolateVelocityKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(fillZeroVelocityKernel, "InputTexture", inputTexture);
            computeShader.SetTexture(addVorticityKernel, "InputTexture", inputTexture);

            if (debugMode) Debug.Log("NavierStokesSolver: Dispatching SetupField kernel after input texture update");
            DispatchKernel(setupFieldKernel);

            if (debugMode) Debug.Log("NavierStokesSolver: Dispatching GenerateBoundaryInfo kernel after input texture update");
            DispatchKernel(generateBoundaryInfoKernel);

            if (debugMode)
            {
                VerifyBoundaryInfo();
            }
        }

        /// <summary>
        /// Updates the simulation parameters.
        /// </summary>
        /// <param name="viscosity">The viscosity of the fluid.</param>
        /// <param name="pressureIterations">The number of pressure solver iterations.</param>
        /// <param name="diffusionIterations">The number of diffusion solver iterations.</param>
        /// <param name="sinkStrength">The strength of sink forces.</param>
        /// <param name="sourceStrength">The strength of source forces.</param>
        /// <param name="globalPressureStrength">The strength of global pressure influence (default is 0.5).</param>
        /// <param name="globalPressureIterations">The number of global pressure iterations (default is 20).</param>
        public void UpdateParameters(float viscosity, int pressureIterations, int diffusionIterations,
                                    float sinkStrength, float sourceStrength,
                                    float globalPressureStrength = 0.5f, int globalPressureIterations = 20)
        {
            this.viscosity = viscosity;
            this.pressureIterations = pressureIterations;
            this.diffusionIterations = diffusionIterations;
            this.sinkStrength = sinkStrength;
            this.sourceStrength = sourceStrength;
            this.globalPressureStrength = globalPressureStrength;
            this.globalPressureIterations = globalPressureIterations;

            computeShader.SetFloat("Viscosity", viscosity);
            computeShader.SetFloat("SinkStrength", sinkStrength);
            computeShader.SetFloat("SourceStrength", sourceStrength);
            computeShader.SetFloat("GlobalPressureStrength", globalPressureStrength);
            computeShader.SetInt("GlobalPressureIterations", globalPressureIterations);

            if (debugMode)
            {
                Debug.Log($"NavierStokesSolver: Updated parameters - " +
                          $"Viscosity: {viscosity}, " +
                          $"PressureIterations: {pressureIterations}, " +
                          $"DiffusionIterations: {diffusionIterations}, " +
                          $"SinkStrength: {sinkStrength}, " +
                          $"SourceStrength: {sourceStrength}, " +
                          $"GlobalPressureStrength: {globalPressureStrength}, " +
                          $"GlobalPressureIterations: {globalPressureIterations}");
            }
        }

        /// <summary>
        /// Resizes the simulation to a new resolution.
        /// </summary>
        /// <param name="newResolution">The new resolution.</param>
        public void Resize(Vector2Int newResolution)
        {
            if (newResolution.x <= 0 || newResolution.y <= 0)
            {
                Debug.LogError("NavierStokesSolver: Invalid resolution.");
                return;
            }

            resolution = newResolution;

            ReleaseTextures();

            CreateTextures();

            Vector2 texelSize = new Vector2(1.0f / resolution.x, 1.0f / resolution.y);
            computeShader.SetVector("TexelSize", texelSize);
            computeShader.SetInts("Resolution", new int[] { resolution.x, resolution.y });

            if (debugMode) Debug.Log($"NavierStokesSolver: Resized to {resolution.x}x{resolution.y}");
            DispatchKernel(setupFieldKernel);

            DispatchKernel(generateBoundaryInfoKernel);

            if (debugMode)
            {
                VerifyBoundaryInfo();
            }
        }

        /// <summary>
        /// Releases the textures used in the simulation.
        /// </summary>
        private void ReleaseTextures()
        {
            if (velocityTexture != null) velocityTexture.Release();
            if (velocityTempTexture != null) velocityTempTexture.Release();
            if (pressureTexture != null) pressureTexture.Release();
            if (pressureTempTexture != null) pressureTempTexture.Release();
            if (globalPressureTexture != null) globalPressureTexture.Release();
            if (divergenceTexture != null) divergenceTexture.Release();
            if (boundaryInfoTexture != null) boundaryInfoTexture.Release();
        }

        /// <summary>
        /// Releases resources used by the solver.
        /// </summary>
        public void Dispose()
        {
            ReleaseTextures();
            if (sampleResultBuffer != null) sampleResultBuffer.Release();
            isInitialized = false;
        }

        public Vector2 SampleField(Vector2 position)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("NavierStokesSolver: Solver is not initialized.");
                return Vector2.zero;
            }

            // Set the sample position
            computeShader.SetVector("SamplePosition", position);

            // Clear the result buffer
            Color[] clearData = new Color[] { Color.clear };
            sampleResultBuffer.SetData(clearData);

            // Dispatch the sample kernel
            computeShader.Dispatch(samplePointKernel, 1, 1, 1);

            // Read the result
            Color[] result = new Color[1];
            sampleResultBuffer.GetData(result);

            // Convert from ARGB to Vector2
            return ARGBToVector2(result[0]);
        }

        public Vector2 SampleField(Vector3 worldPosition, Bounds worldBounds)
        {
            Vector2 normalizedPosition = WorldToNormalizedPosition(worldPosition, worldBounds);

            return SampleField(normalizedPosition);
        }

        private Vector2 WorldToNormalizedPosition(Vector3 worldPosition, Bounds worldBounds)
        {
            float x = Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, worldPosition.x);
            float z = Mathf.InverseLerp(worldBounds.min.z, worldBounds.max.z, worldPosition.z);
            return new Vector2(x, z);
        }

        public RenderTexture GetVelocityTexture() => velocityTexture;
        public RenderTexture GetVelocityTempTexture() => velocityTempTexture;
        public RenderTexture GetPressureTexture() => pressureTexture;
        public RenderTexture GetPressureTempTexture() => pressureTempTexture;
        public RenderTexture GetGlobalPressureTexture() => globalPressureTexture;
        public RenderTexture GetDivergenceTexture() => divergenceTexture;
        public RenderTexture GetBoundaryInfoTexture() => boundaryInfoTexture;
    }
}

