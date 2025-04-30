using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

public enum eParameters : int
{
    maxSinkSourceStrength,
    deltaTime,
    resolution,
    bounds,
    viscosityCoeff,
    pressureCoeff,
    iterationCount,
    inverseResolution
}
namespace fourth
{
    public class VFFInterface
    {
        private string[] kernelStrings =
            {
            "InitializePressureField",
            "Advection",
            "Diffusion",
            "ApplyForces",
            "Divergence",
            "PressureSolve",
            "PressureDelta",
            "Visualization",
            "DensityAdvection", // NEW: Kernel for density advection
            "PressureVisualization"
        };
        public string[] renderNames =
            {
            "VelocityTexture",
            "VelocityTexturePrev",
            "PressureTexture",
            "PressureTexturePrev",
            "DivergenceTexture",
            "BoundaryTexture",
            "VisualizationTexture",
            "BoundaryAndInfluenceTexture",
            "DensityTexture",       // NEW: Texture for fluid density
            "DensityTexturePrev",    // NEW: Texture for previous density state
            "PressureVisualizationTexture"
        };
        private string[] simulationParameters =
        {
        "maxSinkSourceStrength",
        "deltaTime",
        "resolution",
        "bounds",
        "viscosityCoeff",
        "pressureCoeff",
        "iterationCount",
        "inverseResolution",
        "densityDissipation",   // NEW: Density dissipation parameter
        "densityToVelocity",    // NEW: Density to velocity influence parameter
        "baseDensity"           // NEW: Base density parameter
        };

        private ComputeShader VFFCalculator;
        private Dictionary<string, RenderTexture> renderTextures;
        private Dictionary<string, int> kernels;
        private List<FlowFieldInfluence> influences;
        private List<FlowFieldInfluence> activeInfluences;
        private VFFParameters parameters;
        private Vector2 cachedResolution;
        private float MAX_VELOCITY = 10.0f;
        private float SAFETY_FACTOR = 0.9f;

        public VFFInterface(ComputeShader compute, VFFParameters parameters, List<FlowFieldInfluence> influences)
        {
            VFFCalculator = compute;
            this.parameters = parameters;
            Initialize(parameters, influences);
        }

        ~VFFInterface()
        {
            ReleaseTextures();
        }

        private void Initialize(VFFParameters simulationParameters, List<FlowFieldInfluence> influences)
        {
            parameters = simulationParameters;
            cachedResolution = Vector2.zero;
            CreateRenderTextures();
            GetKernelIDs();
            SetParameters();
            InitializeVelocityField();
            InitializeDensityField();
            BindAllTexturesToAllKernels();
            this.influences = influences;
            this.activeInfluences = new List<FlowFieldInfluence>();
        }

        private float GetSafeTimestep(float deltaTime)
        {
            float cellSizeX = parameters.bounds.size.x / parameters.resolution.x;
            float cellSizeY = parameters.bounds.size.z / parameters.resolution.y;
            float minCellSize = Mathf.Min(cellSizeX, cellSizeY);
            float maxDeltaTime = SAFETY_FACTOR * minCellSize / MAX_VELOCITY;
            float safeTimeStep = Mathf.Min(deltaTime, maxDeltaTime);
            return safeTimeStep;
        }

        public void Update(float deltaTime,VFFParameters parameters)
        {
            if (VFFCalculator == null)
            {
                Debug.LogWarning("Compute Shader not set!");
                return;
            }

            try
            {
                this.parameters = parameters;
                SetParameters();
                //CFL condition
                float time = deltaTime;
                time = GetSafeTimestep(time);
                time *= .25f;
                //Debug.Log(time);
                VFFCalculator.SetFloat("deltaTime", time);
                ApplyInfluences();
                DispatchAllKernels();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during simulation: {ex.Message}");
            }
        }

        public void UpdatePathfinding(int propagationIterations = 10)
        {
            // First generate cost field
            GenerateCostField();

            for (int i = 0; i < propagationIterations; i++)
            {
                VFFCalculator.SetInt("iterationCount", i);
                EasyDispatch(kernels["SourcePressureDiffusion"]);
            }

            // Run integration field with sequential iterations
            for (int i = 0; i < propagationIterations; i++)
            {
                VFFCalculator.SetInt("iterationCount", i);
                EasyDispatch(kernels["IntegrationField"]);
            }

            // Generate the flow field based on the integration field
            GeneratePathFlowField();
        }

        public void GenerateCostField()
        {
            EasyDispatch(kernels["CostField"]);
        }

        public void GeneratePathFlowField()
        {
            EasyDispatch(kernels["PathflowField"]);
        }

        #region Private Simulation Functions
        private void RunSimulation(float deltaTime)
        {
            if (VFFCalculator == null)
            {
                Debug.LogWarning("Compute Shader not set!");
                return;
            }
            try
            {
                VFFCalculator.SetFloat("deltaTime", deltaTime);

                ApplyInfluences();

                DispatchAllKernels();

                //RunAdvection();
                //RunDiffusion();
                //RunApplyPressure();
                //RunProjection();
                //UpdateVisualization();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during simulation: {ex.Message}");
            }
        }

        private void ApplyInfluences()
        {
            Texture2D modifiableTexture = CreateModifiableTextureFromBoundary();
            bool textureChanged = ApplyInfluencesToTexture(modifiableTexture, influences);
            if (textureChanged)
            {
                Debug.Log("Updating boundary texture, Prepropagating pressure!");
                UpdateBoundaryTexture(modifiableTexture);
                PrepropagatePressure(modifiableTexture);
            }
            UnityEngine.Object.Destroy(modifiableTexture);
        }

        private Texture2D CreateModifiableTextureFromBoundary()
        {
            RenderTexture boundaryTexture = renderTextures["BoundaryTexture"];
            RenderTexture tempTexture = RenderTexture.GetTemporary(
                boundaryTexture.width,
                boundaryTexture.height,
                0,
                RenderTextureFormat.ARGB32
                );

            Graphics.Blit(boundaryTexture, tempTexture);

            RenderTexture.active = tempTexture;
            Texture2D modifiableTexture = new Texture2D(tempTexture.width, tempTexture.height, TextureFormat.RGBA32, false);
            modifiableTexture.ReadPixels(new Rect(0, 0, tempTexture.width, tempTexture.height), 0, 0);
            modifiableTexture.Apply();
            RenderTexture.active = null;

            RenderTexture.ReleaseTemporary(tempTexture);

            return modifiableTexture;
        }

        /// <summary>
        /// Returns true if there has been a change in the active influences and the boundary texture had to be updated
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="influences"></param>
        /// <returns></returns>
        private bool ApplyInfluencesToTexture(Texture2D texture, IEnumerable<FlowFieldInfluence> influences)
        {
            bool output = false;

            //Find and cache active influences
            List<FlowFieldInfluence> newActiveInfluences = new List<FlowFieldInfluence>();

            foreach(var influence in influences)
            {
                if(influence.active && !activeInfluences.Contains(influence))
                {
                    newActiveInfluences.Add(influence);
                    output = true;
                }
            }

            foreach(var influence in activeInfluences)
            {
                if(!influence.active)
                {
                    activeInfluences.Remove(influence);
                    output = true;
                }
            }

            if(!output) //no change
            {
                return output;
            }
            else //something changed
            {
                activeInfluences.AddRange(newActiveInfluences);
                foreach (var influence in activeInfluences)
                {
                    if (influence.active)
                    {
                        Vector2Int UV = WorldToUV(influence.position, new((int)parameters.resolution.x, (int)parameters.resolution.y));

                        Color current = texture.GetPixel(UV.x, UV.y);
                        Color modified = current;
                        modified += AddInfluenceToColor(influence);

                        Debug.Log($"Modified color is: {modified.r},{modified.g},{modified.b},{modified.a}");

                        texture.SetPixel(UV.x, UV.y, modified);
                        output = true;
                        texture.Apply();
                    }
                }
                if (output) Debug.Log("influences changed!");
                return output;
            }
        }

        private void PrepropagatePressure(Texture2D boundaryTexture)
        {
            // Get the pressure textures
            RenderTexture pressureTexture = renderTextures["PressureTexture"];
            RenderTexture pressureTexturePrev = renderTextures["PressureTexturePrev"];

            // Create a temporary RenderTexture for manipulation
            RenderTexture tempRT = RenderTexture.GetTemporary(
                pressureTexture.width,
                pressureTexture.height,
                0,
                RenderTextureFormat.RFloat
            );

            // Clear the temp texture to zero
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = tempRT;
            GL.Clear(true, true, new Color(0, 0, 0, 0));

            // Create a texture to read/write pixel data
            Texture2D pressureData = new Texture2D(pressureTexture.width, pressureTexture.height, TextureFormat.RFloat, false);
            pressureData.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
            pressureData.Apply();

            // Define the radius of influence in texels
            float radiusInWorldUnits = 10.0f; // TODO: make this a parameter that can be adjusted, use fixed value for now / testing.
            int radiusInTexels = Mathf.CeilToInt(radiusInWorldUnits * Mathf.Max(parameters.inverseResolution.x, parameters.inverseResolution.y));

            // For each active influence, set pressure values in a radius
            foreach (var influence in influences)
            {
                if (!influence.active) continue;

                // Convert world position to texture coordinates
                Vector2Int center = WorldToUV(influence.position, new Vector2Int(pressureTexture.width, pressureTexture.height));

                // Normalized strength value (for sources: positive, for sinks: negative)
                float normalizedStrength = (influence.type == eInfluenceType.Source) ?
                    influence.strength : -influence.strength;

                // Set pressure in a radius around the influence
                for (int dx = -radiusInTexels; dx <= radiusInTexels; dx++)
                {
                    for (int dy = -radiusInTexels; dy <= radiusInTexels; dy++)
                    {
                        int x = center.x + dx;
                        int y = center.y + dy;

                        // Skip if out of bounds
                        if (x < 0 || x >= pressureTexture.width || y < 0 || y >= pressureTexture.height)
                            continue;

                        // Calculate distance from influence center
                        float distSq = dx * dx + dy * dy;
                        float radiusSq = radiusInTexels * radiusInTexels;

                        // Skip if outside radius
                        if (distSq > radiusSq)
                            continue;

                        // Check if this is a valid fluid cell (not a boundary)
                        Color boundaryColor = boundaryTexture.GetPixel(x, y);
                        if (boundaryColor.b > 0.01f) // If it's a boundary cell 
                            continue;

                        // Calculate pressure based on distance from center
                        float distance = Mathf.Sqrt(distSq);
                        float falloff = 1.0f - Mathf.Clamp01(distance / radiusInTexels);

                        // Apply quadratic falloff for smoother transition
                        falloff = falloff * falloff;

                        // Scale by influence strength
                        float pressureValue = normalizedStrength * falloff;

                        // For RFloat format, we only need to set the red channel
                        Color currentPressure = pressureData.GetPixel(x, y);
                        pressureData.SetPixel(x, y, new Color(currentPressure.r + pressureValue, 0, 0, 0));
                    }
                }
            }

            // Apply changes to the texture
            pressureData.Apply();

            // Upload the modified texture to both GPU textures
            Graphics.Blit(pressureData, tempRT);
            Graphics.Blit(tempRT, pressureTexture);
            Graphics.Blit(tempRT, pressureTexturePrev);

            // Rebind the pressure textures to the compute shader
            BindTextureToAllKernels("PressureTexture", pressureTexture);
            BindTextureToAllKernels("PressureTexturePrev", pressureTexturePrev);

            // Clean up
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(tempRT);
            UnityEngine.Object.Destroy(pressureData);

            Debug.Log("Pressure pre-propagation applied");
        }

        private Color AddInfluenceToColor(FlowFieldInfluence influence)
        {
            Color output = new();

            float normalizedStrength = influence.strength;

            switch (influence.type)
            {
                case eInfluenceType.Source:
                    output.g = normalizedStrength;
                    break;
                case eInfluenceType.Sink:
                    output.r = normalizedStrength;
                    break;
                default:
                    break;
            }

            return output;
        }

        private void UpdateBoundaryTexture(Texture2D modifiedTexture)
        {
            RenderTexture boundaryTexture = renderTextures["BoundaryTexture"];
            RenderTexture tempRT = RenderTexture.GetTemporary(
                boundaryTexture.width,
                boundaryTexture.height,
                0,
                boundaryTexture.format
                );
            Graphics.Blit(modifiedTexture, tempRT);
            Graphics.Blit(tempRT, boundaryTexture);

            RenderTexture.ReleaseTemporary(tempRT);

            BindTextureToAllKernels("BoundaryTexture", boundaryTexture);
        }

        private Vector2Int WorldToUV(Vector3 worldPosition, Vector2Int textureResolution)
        {
            Vector2 normalizedPosition = new Vector2(
                Mathf.InverseLerp(parameters.bounds.min.x, parameters.bounds.max.x, worldPosition.x),
                Mathf.InverseLerp(parameters.bounds.max.z, parameters.bounds.min.z, worldPosition.z)
                );

            int u = Mathf.Clamp(Mathf.FloorToInt(normalizedPosition.x * textureResolution.x), 0, textureResolution.x - 1);
            int v = Mathf.Clamp(Mathf.FloorToInt(normalizedPosition.y * textureResolution.y), 0, textureResolution.y - 1);

            return new Vector2Int(u, v);
        }
        private void DispatchAllKernels()
        {
            foreach(var kernel in kernels)
            {
                //if (VFFCalculator.HasKernel(kernel.Key)) Debug.Log($"Dispatching kernel {kernel.Key}");
                EasyDispatch(kernel.Value);
            }
        }
        private void EasyDispatch(int kernelID)
        {

            VFFCalculator.Dispatch(kernelID, Mathf.CeilToInt(parameters.resolution.x / 8.0f), Mathf.CeilToInt(parameters.resolution.y / 8.0f), 1);
        }

        private void RunAdvection()
        {
            EasyDispatch(kernels["Advection"]);
        }

        private void RunDiffusion()
        {
            EasyDispatch(kernels["Diffusion"]);
        }

        private void RunApplyPressure()
        {
            EasyDispatch(kernels["ApplyForces"]);
        }

        private void RunProjection()
        {
            EasyDispatch(kernels["Divergence"]);
            EasyDispatch(kernels["PressureSolve"]);
            EasyDispatch(kernels["PressureDelta"]);
        }

        private void UpdateVisualization()
        {
            EasyDispatch(kernels["Visualization"]);
        }
        #endregion
        #region Texture Handling Functions
        private void CreateRenderTextures()
        {
            bool createNewTextures = this.renderTextures == null;
            bool resolutionMismatch =
                !(
                    Mathf.Approximately(cachedResolution.x, parameters.resolution.x) &&
                    Mathf.Approximately(cachedResolution.y, parameters.resolution.y)
                );
            if (createNewTextures || resolutionMismatch) //have the textures been created?
            {
                if (!createNewTextures) ReleaseTextures();

                renderTextures = new Dictionary<string, RenderTexture>();
                foreach (var kernel in renderNames)
                {
                    // NEW: Modified to include density textures as RFloat format
                    RenderTextureFormat format = (kernel == "PressureTexture" ||
                                                kernel == "PressureTexturePrev" ||
                                                kernel == "DivergenceTexture" ||
                                                kernel == "DensityTexture" ||
                                                kernel == "DensityTexturePrev")
                                                ? RenderTextureFormat.RFloat
                                                : RenderTextureFormat.ARGBFloat;
                    renderTextures.Add(kernel, CreateRenderTexture(format));
                }
                cachedResolution = parameters.resolution;
            }
        }

        private void InitializeDensityField()
        {
            // Create a texture with the base density value everywhere except boundaries
            Texture2D tempTexture = new Texture2D(
                (int)parameters.resolution.x,
                (int)parameters.resolution.y,
                TextureFormat.RFloat,
                false);

            // Set base density (1.0) for all cells
            for (int x = 0; x < parameters.resolution.x; x++)
            {
                for (int y = 0; y < parameters.resolution.y; y++)
                {
                    // Read the boundary texture pixel to check for boundaries
                    Color boundary = Color.black;
                    RenderTexture.active = renderTextures["BoundaryTexture"];
                    tempTexture.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
                    boundary = tempTexture.GetPixel(0, 0);
                    RenderTexture.active = null;

                    // Set zero density at boundaries, base density elsewhere
                    float density = boundary.b > 0.01f ? 0.0f : 1.0f;
                    tempTexture.SetPixel(x, y, new Color(density, 0, 0, 0));
                }
            }
            tempTexture.Apply();

            // Copy to density textures
            Graphics.Blit(tempTexture, renderTextures["DensityTexture"]);
            Graphics.Blit(tempTexture, renderTextures["DensityTexturePrev"]);

            UnityEngine.Object.Destroy(tempTexture);
        }

        private RenderTexture CreateRenderTexture(RenderTextureFormat format)
        {
            RenderTexture newTex = new RenderTexture((int)this.parameters.resolution.x, (int)this.parameters.resolution.y, 0, format);
            newTex.enableRandomWrite = true;
            newTex.filterMode = FilterMode.Bilinear;
            newTex.wrapMode = TextureWrapMode.Clamp;
            newTex.Create();

            return newTex;
        }
        private void ClearTexture(RenderTexture texture, RenderTextureFormat format)
        {
            RenderTexture temp = RenderTexture.GetTemporary((int)parameters.resolution.x, (int)parameters.resolution.y, 0, format);
            Graphics.SetRenderTarget(temp);
            GL.Clear(true, true, Color.yellow);
            Graphics.Blit(temp, texture);

            RenderTexture.ReleaseTemporary(temp);
        }
        private void ReleaseTextures()
        {
            foreach (var texture in renderTextures)
            {
                ReleaseAndNullTexture(texture.Value);
            }
        }
        private void ReleaseAndNullTexture(RenderTexture texture)
        {
            texture.Release();
            texture = null;
        }
        private void BindAllTexturesToAllKernels()
        {
            foreach (var texture in renderTextures)
            {
                BindTextureToAllKernels(texture.Key, texture.Value);
            }
        }
        private void BindTextureToAllKernels(string textureName, RenderTexture texture)
        {
            foreach (var kernel in kernels)
            {
                VFFCalculator.SetTexture(kernel.Value, textureName, texture);
            }
        }
        #endregion
        #region Setup Functions
        private void GetKernelIDs()
        {
            kernels = new Dictionary<string, int>();
            foreach (var kernel in kernelStrings)
            {
                int kernelID = VFFCalculator.FindKernel(kernel);
                kernels.Add(kernel, kernelID);
                Debug.Log($"Kernel ID found, kernel:{kernel}, ID: {kernelID}");
            }
        }
        private void SetParameters()
        {
            VFFCalculator.SetVector("resolution", parameters.resolution);
            VFFCalculator.SetVector("bounds", parameters.bounds.size);
            VFFCalculator.SetVector("inverseResolution", parameters.inverseResolution);
            VFFCalculator.SetFloat("maxSinkSourceStrength", parameters.maxSinkSourceStrength);
            VFFCalculator.SetFloat("viscosityCoeff", parameters.viscosityCoeff);
            VFFCalculator.SetFloat("pressureCoeff", parameters.pressureCoeff);
            VFFCalculator.SetInt("iterationCount", parameters.iterationCount);

            Debug.Log($"Sink/Source Strength {parameters.maxSinkSourceStrength}");

            // NEW: Set density-related parameters (using default values if not specified)
            VFFCalculator.SetFloat("densityDissipation", 0.99f);
            VFFCalculator.SetFloat("densityToVelocity", 1.0f);
            VFFCalculator.SetFloat("baseDensity", 1.0f);
        }

        public void SetBoundaryTexture(RenderTexture texture)
        {
            if (texture == null) Debug.Log("BoundaryTexture is null!");
            renderTextures["BoundaryTexture"] = texture;
        }

        private void InitializeVelocityField()
        {
            // Create a temporary texture with the same size as boundary
            Texture2D tempTexture = new Texture2D(
                (int)parameters.resolution.x,
                (int)parameters.resolution.y,
                TextureFormat.RGBAFloat,
                false);

            // Loop through each pixel
            for (int x = 0; x < parameters.resolution.x; x++)
            {
                for (int y = 0; y < parameters.resolution.y; y++)
                {
                    Color color = Color.black;

                    // Read boundary texture to find sources/sinks
                    RenderTexture.active = renderTextures["BoundaryTexture"];
                    Color boundary = tempTexture.GetPixel(x, y);
                    RenderTexture.active = null;

                    // If source (green), create outward velocity
                    if (boundary.g > 0.5f)
                    {
                        Vector2 center = new Vector2(parameters.resolution.x / 2, parameters.resolution.y / 2);
                        Vector2 pos = new Vector2(x, y);
                        Vector2 dir = (pos - center).normalized;
                        color = new Color(dir.x, dir.y, 0, 0) * parameters.maxSinkSourceStrength;
                    }
                    // If sink (red), create inward velocity
                    else if (boundary.r > 0.5f)
                    {
                        Vector2 center = new Vector2(parameters.resolution.x / 2, parameters.resolution.y / 2);
                        Vector2 pos = new Vector2(x, y);
                        Vector2 dir = (center - pos).normalized;
                        color = new Color(dir.x, dir.y, 0, 0) * parameters.maxSinkSourceStrength;
                    }

                    tempTexture.SetPixel(x, y, color);
                }
            }
            tempTexture.Apply();

            // Copy to velocity textures
            Graphics.Blit(tempTexture, renderTextures["VelocityTexture"]);
            Graphics.Blit(tempTexture, renderTextures["VelocityTexturePrev"]);

            UnityEngine.Object.Destroy(tempTexture);
        }
        #endregion
        #region Public Data Functions 
        public void AddInfluence(FlowFieldInfluence influence)
        {
            influences.Add(influence);
        }
        public RenderTexture GetVelocityTexture()
        {
            return renderTextures["VelocityTexture"];
        }

        /// <summary>
        /// Creates and returns a Texture2D from the visualization RenderTexture
        /// </summary>
        /// <returns>A new Texture2D containing the visualization data</returns>
        public Texture2D GetVisualization()
        {
            // Get the visualization render texture
            RenderTexture visualizationTexture = renderTextures["VisualizationTexture"];

            // Create a temporary render texture to work with
            RenderTexture tempTexture = RenderTexture.GetTemporary(
                visualizationTexture.width,
                visualizationTexture.height,
                0,
                RenderTextureFormat.ARGB32
            );

            // Copy visualization texture to our temp texture
            Graphics.Blit(visualizationTexture, tempTexture);

            // Set the active render texture so we can read pixels from it
            RenderTexture.active = tempTexture;

            // Create a new Texture2D to hold the data
            Texture2D outputTexture = new Texture2D(
                tempTexture.width,
                tempTexture.height,
                TextureFormat.RGBA32,
                false
            );

            // Read the pixels from the active render texture into our Texture2D
            outputTexture.ReadPixels(
                new Rect(0, 0, tempTexture.width, tempTexture.height),
                0,
                0
            );

            // Apply the changes to make sure the texture data is updated
            outputTexture.Apply();

            // Clear the active render texture
            RenderTexture.active = null;

            // Release the temporary texture
            RenderTexture.ReleaseTemporary(tempTexture);

            return outputTexture;
        }

        public Texture2D GetPressureVisualization()
        {
            // Get the visualization render texture
            RenderTexture visualizationTexture = renderTextures["PressureVisualizationTexture"];

            // Create a temporary render texture to work with
            RenderTexture tempTexture = RenderTexture.GetTemporary(
                visualizationTexture.width,
                visualizationTexture.height,
                0,
                RenderTextureFormat.ARGB32
            );

            // Copy visualization texture to our temp texture
            Graphics.Blit(visualizationTexture, tempTexture);

            // Set the active render texture so we can read pixels from it
            RenderTexture.active = tempTexture;

            // Create a new Texture2D to hold the data
            Texture2D outputTexture = new Texture2D(
                tempTexture.width,
                tempTexture.height,
                TextureFormat.RGBA32,
                false
            );

            // Read the pixels from the active render texture into our Texture2D
            outputTexture.ReadPixels(
                new Rect(0, 0, tempTexture.width, tempTexture.height),
                0,
                0
            );

            // Apply the changes to make sure the texture data is updated
            outputTexture.Apply();

            // Clear the active render texture
            RenderTexture.active = null;

            // Release the temporary texture
            RenderTexture.ReleaseTemporary(tempTexture);

            return outputTexture;
        }
        public RenderTexture GetRenderBuffer(string bufferName)
        {
            return renderTextures[bufferName];
        }

        public Vector3 SampleVelocityField(Vector3 worldPosition)
        {
            Vector2 normalizedPosition = new Vector2(
                Mathf.InverseLerp(parameters.bounds.min.x, parameters.bounds.max.x, worldPosition.x),
                Mathf.InverseLerp(parameters.bounds.max.z, parameters.bounds.min.z, worldPosition.z)
            );

            Vector2 texCoord = new Vector2(
                normalizedPosition.x * parameters.resolution.x - 0.5f,
                normalizedPosition.y * parameters.resolution.y - 0.5f
            );

            int x0 = Mathf.Clamp(Mathf.FloorToInt(texCoord.x), 0, (int)parameters.resolution.x - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(texCoord.y), 0, (int)parameters.resolution.y - 1);
            int x1 = Mathf.Clamp(x0 + 1, 0, (int)parameters.resolution.x - 1);
            int y1 = Mathf.Clamp(y0 + 1, 0, (int)parameters.resolution.y - 1);

            float tx = texCoord.x - x0;
            float ty = texCoord.y - y0;

            RenderTexture velocityTexture = renderTextures["VelocityTexture"];
            RenderTexture.active = velocityTexture;
            Texture2D tempTexture = new Texture2D(2, 2, TextureFormat.RGBAFloat, false);

            tempTexture.ReadPixels(new Rect(x0, y0, 2, 2), 0, 0);
            tempTexture.Apply();

            Color v00 = tempTexture.GetPixel(0, 0);
            Color v10 = tempTexture.GetPixel(1, 0);
            Color v01 = tempTexture.GetPixel(0, 1);
            Color v11 = tempTexture.GetPixel(1, 1);

            UnityEngine.Object.Destroy(tempTexture);
            RenderTexture.active = null;

            Vector2 vel00 = new Vector2(v00.r, v00.g);
            Vector2 vel10 = new Vector2(v10.r, v10.g);
            Vector2 vel01 = new Vector2(v01.r, v01.g);
            Vector2 vel11 = new Vector2(v11.r, v11.g);

            Vector2 velocity = Vector2.Lerp(
                Vector2.Lerp(vel00, vel10, tx),
                Vector2.Lerp(vel01, vel11, tx),
                ty
            );

            return new Vector3(velocity.x, 0, velocity.y);
        }

        private void SwapDensityTextures()
        {
            RenderTexture temp = renderTextures["DensityTexture"];
            renderTextures["DensityTexture"] = renderTextures["DensityTexturePrev"];
            renderTextures["DensityTexturePrev"] = temp;

            // Rebind the textures
            BindTextureToAllKernels("DensityTexture", renderTextures["DensityTexture"]);
            BindTextureToAllKernels("DensityTexturePrev", renderTextures["DensityTexturePrev"]);
        }

        public Vector2Int GetUV(Vector3 worldPosition)
        {
            return WorldToUV(worldPosition, new((int)parameters.resolution.x, (int)parameters.resolution.y));
        }
        #endregion
    }

    public class VFFParameters
    {
        public ComputeShader shader { get; set; }
        public float maxSinkSourceStrength { get; set; }
        public Vector2 resolution { get; set; }
        public Bounds bounds { get; set; }
        public float viscosityCoeff { get; set; }
        public float pressureCoeff { get; set; }
        public int iterationCount { get; set; }
        public Vector2 inverseResolution { get; set; }

        public class Builder
        {
            readonly VFFParameters parameters;
            public Builder()
            {
                parameters = new VFFParameters();
            }
            public Builder WithComputeShader(ComputeShader shader)
            {
                parameters.shader = shader;
                return this;
            }
            public Builder WithMaxInfluenceStrength(float strength)
            {
                parameters.maxSinkSourceStrength = strength;
                return this;
            }
            public Builder WithResolution(Vector2 resolution)
            {
                parameters.resolution = resolution;
                return this;
            }
            public Builder WithBounds(Bounds bounds)
            {
                parameters.bounds = bounds;
                return this;
            }
            public Builder WithViscosity(float viscosity)
            {
                parameters.viscosityCoeff = viscosity;
                return this;
            }
            public Builder WithPressure(float pressure)
            {
                parameters.pressureCoeff = pressure;
                return this;
            }
            public Builder WithIterations(int iterations)
            {
                parameters.iterationCount = iterations;
                return this;
            }
            public Builder CalculateInversResolution()
            {
                if (parameters.resolution == default)
                {
                    Debug.LogWarning("VFFParameters.resolution not set, cannot calculate InverseResolution");
                    return this;
                }
                if (parameters.bounds == default)
                {
                    Debug.LogWarning("VFFParameters.bounds not set, cannot calculate InverseResolution");
                    return this;
                }
                parameters.inverseResolution = new Vector2(parameters.resolution.x / (parameters.bounds.extents.x * 2), parameters.resolution.y / (parameters.bounds.extents.y * 2));
                return this;
            }
            public VFFParameters Build()
            {
                return this.parameters;
            }
        }
    }

    public class BoundaryTextureGenerator
    {
        private ComputeShader generator;

        public BoundaryTextureGenerator(ComputeShader generator)
        {
            this.generator = generator;
        }

        public RenderTexture GenerateTexture(Mesh mesh, VFFParameters parameters)
        {
            return GenerateTexture(mesh, parameters.resolution, parameters.bounds);
        }

        private RenderTexture GenerateTexture(Mesh mesh, Vector2 resolution, Bounds bounds)
        {
            RenderTexture output = new RenderTexture(
                (int)resolution.x,
                (int)resolution.y,
                0,
                RenderTextureFormat.ARGBFloat
                );
            output.enableRandomWrite = true;
            output.filterMode = FilterMode.Bilinear;
            output.wrapMode = TextureWrapMode.Clamp;
            output.Create();

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            ComputeBuffer vertexBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
            ComputeBuffer triangleBuffer = new ComputeBuffer(triangles.Length, sizeof(int));

            vertexBuffer.SetData(vertices);
            triangleBuffer.SetData(triangles);

            try
            {
                // Set up shader parameters
                int kernelIndex = generator.FindKernel("GenerateTexture");
                generator.SetBuffer(kernelIndex, "Verticies", vertexBuffer);
                generator.SetBuffer(kernelIndex, "Triangles", triangleBuffer);
                generator.SetTexture(kernelIndex, "Output", output);
                generator.SetInt("VertexCount", vertices.Length);
                generator.SetInt("TriangleCount", triangles.Length / 3);
                generator.SetInts("TextureSize", new int[] { (int)resolution.x, (int)resolution.y });
                generator.SetFloats("boundsMin", new float[] { bounds.min.x, bounds.min.z });
                generator.SetFloats("boundsMax", new float[] { bounds.max.x, bounds.max.z });

                // Dispatch shader with thread groups matching the 8x8x1 size in the compute shader
                generator.Dispatch(kernelIndex, Mathf.CeilToInt(resolution.x / 8.0f), Mathf.CeilToInt(resolution.y / 8.0f), 1);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error generating boundary texture: {ex.Message}");
                output.Release();
                throw;
            }
            finally
            {
                // Clean up buffers
                vertexBuffer.Release();
                triangleBuffer.Release();
            }

            return output;
        }
    }

    public class FlowFieldInfluence
    {
        public bool active { get; set; }
        public Vector3 position { get; set; }
        public float strength { get; set; }
        public eInfluenceType type { get; set; }

    }

    public enum eInfluenceType
    {
        Sink,
        Source
    }
}