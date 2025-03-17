using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace VFF
{
    /// <summary>
    /// Main controller class for the Vector Flow Field simulation.
    /// Implemented as a singleton to allow easy access from other components.
    /// </summary>
    public class VectorFieldManager : MonoBehaviour
    {
        // Singleton instance
        private static VectorFieldManager _instance;

        /// <summary>
        /// Gets the singleton instance of the VectorFieldManager.
        /// </summary>
        public static VectorFieldManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<VectorFieldManager>();

                    if (_instance == null)
                    {
                        Debug.LogError("No VectorFieldManager found in the scene. Ensure one exists.");
                    }
                }
                return _instance;
            }
        }
        [Header("References")]
        [Tooltip("The compute shader for the Navier-Stokes simulation")]
        [SerializeField] private ComputeShader navierStokesComputeShader;

        [Tooltip("The compute shader for processing meshes")]
        [SerializeField] private ComputeShader meshProcessorComputeShader;

        [Tooltip("Parameters for the simulation")]
        [SerializeField] private VectorFieldParameters parameters;

        [Header("Field Settings")]
        [Tooltip("GameObject defining the minimum extent (bottom-left corner) of the vector field")]
        [SerializeField] private GameObject minExtentObject;

        [Tooltip("GameObject defining the maximum extent (top-right corner) of the vector field")]
        [SerializeField] private GameObject maxExtentObject;

        [Tooltip("Default bounds to use if extent objects are not assigned")]
        [SerializeField] private Bounds defaultBounds = new Bounds(Vector3.zero, new Vector3(20f, 0f, 20f));

        // Property to calculate bounds dynamically based on extent GameObjects
        public Bounds WorldBounds
        {
            get
            {
                if (minExtentObject == null || maxExtentObject == null)
                    return defaultBounds;

                Vector3 min = minExtentObject.transform.position;
                Vector3 max = maxExtentObject.transform.position;

                // Ensure min is actually less than max
                min = new Vector3(
                    Mathf.Min(min.x, max.x),
                    min.y,
                    Mathf.Min(min.z, max.z)
                );

                max = new Vector3(
                    Mathf.Max(min.x, max.x),
                    max.y,
                    Mathf.Max(min.z, max.z)
                );

                return new Bounds(
                    (min + max) * 0.5f,  // Center
                    max - min            // Size
                );
            }
        }

        [Tooltip("Optional texture to initialize the field layout")]
        [SerializeField] private Texture2D initialFieldTexture;

        // Internal components
        private FieldTextureGenerator fieldGenerator;
        private NavierStokesSolver navierStokesSolver;

        // Agents using this vector field
        private List<PathfindingAgent> agents = new List<PathfindingAgent>();

        // Sinks and sources
        [Header("Dynamic Sinks and Sources")]
        [Tooltip("GameObjects with VectorFieldSink components to use as sinks")]
        [SerializeField] private List<VectorFieldSink> sinks = new List<VectorFieldSink>();

        [Tooltip("GameObjects with VectorFieldSource components to use as sources")]
        [SerializeField] private List<VectorFieldSource> sources = new List<VectorFieldSource>();

        [Tooltip("Whether to automatically update when sinks/sources change position")]
        [SerializeField] private bool autoUpdateOnPositionChange = true;

        // Track previous positions for auto-update
        private Dictionary<VectorFieldSink, Vector3> previousSinkPositions = new Dictionary<VectorFieldSink, Vector3>();
        private Dictionary<VectorFieldSource, Vector3> previousSourcePositions = new Dictionary<VectorFieldSource, Vector3>();

        // Cached vector field bitmap
        private Texture2D cachedVectorFieldBitmap;
        private float lastCacheUpdateTime;
        private const float CACHE_UPDATE_INTERVAL = 0.1f;


        [SerializeField] public RenderTexture VelocityTexture;
        [SerializeField] public RenderTexture PressureTexture;
        [SerializeField] public RenderTexture GlobalPressureTexture;
        [SerializeField] public RenderTexture DivergenceTexture;
        [SerializeField] public RenderTexture BoundaryInfoTexture;
        [SerializeField] public Texture2D FieldTexture;
        [SerializeField] public Texture2D CachedVectorFieldBitmap;

        /// <summary>
        /// Gets the velocity texture for visualization purposes.
        /// </summary>
        /// <returns>The velocity texture containing the raw vector field data.</returns>
        public RenderTexture GetVelocityTexture()
        {
            return navierStokesSolver?.VelocityTexture;
        }

        /// <summary>
        /// Updates the cached vector field bitmap.
        /// </summary>
        public void UpdateCachedVectorFieldBitmap()
        {
            if (navierStokesSolver == null || navierStokesSolver.VelocityTexture == null)
                return;

            RenderTexture velocityTexture = navierStokesSolver.VelocityTexture;

            // Create the cached bitmap if it doesn't exist or if the resolution changed
            if (cachedVectorFieldBitmap == null ||
                cachedVectorFieldBitmap.width != velocityTexture.width ||
                cachedVectorFieldBitmap.height != velocityTexture.height)
            {
                if (cachedVectorFieldBitmap != null)
                    Destroy(cachedVectorFieldBitmap);

                cachedVectorFieldBitmap = new Texture2D(
                    velocityTexture.width,
                    velocityTexture.height,
                    TextureFormat.RGBA32,
                    false
                );
            }

            // Set up a temporary render texture to read from
            RenderTexture tempRT = RenderTexture.GetTemporary(
                velocityTexture.width,
                velocityTexture.height,
                0,
                RenderTextureFormat.ARGBFloat
            );

            // Copy the velocity texture to the temporary RT
            Graphics.Blit(velocityTexture, tempRT);

            // Read the pixels
            RenderTexture.active = tempRT;
            cachedVectorFieldBitmap.ReadPixels(
                new Rect(0, 0, tempRT.width, tempRT.height),
                0, 0
            );

            // Process the pixels to create a visualization
            Color[] pixels = cachedVectorFieldBitmap.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                // Extract vector components
                float vx = pixels[i].r;
                float vy = pixels[i].g;

                // Calculate magnitude and direction
                float magnitude = Mathf.Sqrt(vx * vx + vy * vy);
                float direction = Mathf.Atan2(vy, vx) / (2f * Mathf.PI) + 0.5f;

                // Direction as hue, magnitude as saturation and value
                pixels[i] = Color.HSVToRGB(
                    direction,
                    Mathf.Clamp01(magnitude * 0.5f + 0.5f),
                    Mathf.Clamp01(magnitude * 0.7f + 0.3f)
                );
            }

            cachedVectorFieldBitmap.SetPixels(pixels);
            cachedVectorFieldBitmap.Apply();

            // Clean up
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(tempRT);
        }

        /// <summary>
        /// Samples the vector field from the cached texture using a world-space position.
        /// This provides a faster alternative to the compute shader sampling method.
        /// </summary>
        /// <param name="worldPosition">The world position of the agent.</param>
        /// <returns>A direction vector (normalized) for the agent to follow.</returns>
        public Vector3 SampleFieldFromCachedTexture(Vector3 worldPosition)
        {
            if (cachedVectorFieldBitmap == null)
            {
                // If the cached texture doesn't exist yet, update it
                UpdateCachedVectorFieldBitmap();

                // If it still doesn't exist, fall back to the regular sampling method
                if (cachedVectorFieldBitmap == null)
                    return SampleField(WorldToNormalizedPosition(worldPosition));
            }

            // Convert world position to normalized coordinates (0-1)
            Vector2 normalizedPosition = WorldToNormalizedPosition(worldPosition);

            // Convert normalized position to texture coordinates with bilinear interpolation
            float texX = normalizedPosition.x * (cachedVectorFieldBitmap.width - 1);
            float texY = normalizedPosition.y * (cachedVectorFieldBitmap.height - 1);

            // Get the four surrounding pixels for bilinear interpolation
            int x0 = Mathf.FloorToInt(texX);
            int y0 = Mathf.FloorToInt(texY);
            int x1 = Mathf.Min(x0 + 1, cachedVectorFieldBitmap.width - 1);
            int y1 = Mathf.Min(y0 + 1, cachedVectorFieldBitmap.height - 1);

            // Calculate interpolation factors
            float tx = texX - x0;
            float ty = texY - y0;

            // Sample the four surrounding pixels
            Color c00 = cachedVectorFieldBitmap.GetPixel(x0, y0);
            Color c10 = cachedVectorFieldBitmap.GetPixel(x1, y0);
            Color c01 = cachedVectorFieldBitmap.GetPixel(x0, y1);
            Color c11 = cachedVectorFieldBitmap.GetPixel(x1, y1);

            // Convert each color to a direction vector
            Vector2 dir00 = ColorToDirection(c00);
            Vector2 dir10 = ColorToDirection(c10);
            Vector2 dir01 = ColorToDirection(c01);
            Vector2 dir11 = ColorToDirection(c11);

            // Perform bilinear interpolation on the directions
            Vector2 dirX0 = Vector2.Lerp(dir00, dir10, tx);
            Vector2 dirX1 = Vector2.Lerp(dir01, dir11, tx);
            Vector2 finalDir = Vector2.Lerp(dirX0, dirX1, ty);

            // Check if the result is too small (effectively zero)
            if (finalDir.sqrMagnitude < 0.001f)
            {
                // Try to find a non-zero vector in the vicinity using a spiral search
                finalDir = FindNearestNonZeroVectorFromTexture(normalizedPosition);
            }

            // Convert to a 3D vector (XZ plane)
            return new Vector3(finalDir.x, 0, finalDir.y);
        }

        /// <summary>
        /// Converts a color from the cached texture to a direction vector.
        /// </summary>
        private Vector2 ColorToDirection(Color color)
        {
            // Convert RGB to HSV
            float h, s, v;
            Color.RGBToHSV(color, out h, out s, out v);

            // If saturation or value is too low, this is effectively a zero vector
            if (s < 0.1f || v < 0.1f)
                return Vector2.zero;

            // Convert hue to angle (hue of 0.5 = 0 radians, ranges from 0-1)
            float angle = (h - 0.5f) * 2f * Mathf.PI;

            // Create normalized direction vector
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>
        /// Finds the nearest non-zero vector in the cached texture using a spiral search pattern.
        /// </summary>
        private Vector2 FindNearestNonZeroVectorFromTexture(Vector2 normalizedPosition)
        {
            if (cachedVectorFieldBitmap == null)
                return Vector2.zero;

            // Define search parameters
            const int maxSearchSteps = 10;
            const float searchStepSize = 0.02f;

            // Try sampling in a spiral pattern
            Vector2 bestVector = Vector2.zero;
            float bestMagnitude = 0f;

            for (int step = 1; step <= maxSearchSteps; step++)
            {
                // Calculate angle based on step (golden angle for better distribution)
                float angle = step * 2.4f;
                float radius = step * searchStepSize;

                // Calculate offset
                Vector2 offset = new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius
                );

                // Calculate sample position
                Vector2 samplePos = normalizedPosition + offset;

                // Clamp to valid range
                samplePos.x = Mathf.Clamp01(samplePos.x);
                samplePos.y = Mathf.Clamp01(samplePos.y);

                // Convert to texture coordinates
                int x = Mathf.FloorToInt(samplePos.x * (cachedVectorFieldBitmap.width - 1));
                int y = Mathf.FloorToInt(samplePos.y * (cachedVectorFieldBitmap.height - 1));

                // Sample color and convert to direction
                Color color = cachedVectorFieldBitmap.GetPixel(x, y);
                Vector2 direction = ColorToDirection(color);

                float magnitude = direction.sqrMagnitude;

                // If this is better than our current best, update
                if (magnitude > bestMagnitude)
                {
                    bestVector = direction;
                    bestMagnitude = magnitude;

                    // If it's good enough, early exit
                    if (magnitude > 0.01f)
                        break;
                }
            }

            // If we found a good vector, return it
            if (bestMagnitude > 0.001f)
                return bestVector;

            // Otherwise, use a fallback based on position
            // Direct toward the center of the field as a last resort
            Vector2 dirToCenter = new Vector2(0.5f, 0.5f) - normalizedPosition;

            if (dirToCenter.sqrMagnitude > 0.001f)
                return dirToCenter.normalized * 0.1f;

            // If at center, use a small upward vector
            return new Vector2(0, 0.1f);
        }

        /// <summary>
        /// Initializes the vector field manager.
        /// </summary>
        private void Awake()
        {
            // Singleton enforcement
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("Multiple VectorFieldManager instances detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            _instance = this;

            // Original initialization
            if (parameters == null)
            {
                Debug.LogError("VectorFieldManager: Parameters not assigned.");
                return;
            }

            if (navierStokesComputeShader == null)
            {
                Debug.LogError("VectorFieldManager: Compute shader not assigned.");
                return;
            }

            fieldGenerator = new FieldTextureGenerator(parameters.GridResolution);

            if (initialFieldTexture != null)
            {
                fieldGenerator.LoadFromTexture(initialFieldTexture);
            }
            else
            {
                fieldGenerator.SetFullField();
            }

            navierStokesSolver = new NavierStokesSolver(
                navierStokesComputeShader,
                fieldGenerator.FieldTexture,
                parameters.GridResolution,
                parameters.Viscosity,
                parameters.PressureIterations,
                parameters.DiffusionIterations,
                parameters.SinkStrength,
                parameters.SourceStrength,
                parameters.GlobalPressureStrength,
                parameters.GlobalPressureIterations
            );
        }

        /// <summary>
        /// Updates the simulation.
        /// </summary>
        private void Update()
        {
            if (navierStokesSolver == null || !parameters.AutoUpdate)
                return;

            if (!parameters.UseFixedUpdate)
            {
                UpdateSimulation(Time.deltaTime * parameters.TimeStepMultiplier);
            }

            // Check if any sinks or sources have moved
            if (autoUpdateOnPositionChange)
            {
                bool needsUpdate = false;

                // Check sinks
                foreach (VectorFieldSink sink in sinks)
                {
                    if (sink != null && sink.isActive && previousSinkPositions.ContainsKey(sink))
                    {
                        if (sink.transform.position != previousSinkPositions[sink])
                        {
                            previousSinkPositions[sink] = sink.transform.position;
                            needsUpdate = true;
                        }
                    }
                }

                // Check sources
                foreach (VectorFieldSource source in sources)
                {
                    if (source != null && source.isActive && previousSourcePositions.ContainsKey(source))
                    {
                        if (source.transform.position != previousSourcePositions[source])
                        {
                            previousSourcePositions[source] = source.transform.position;
                            needsUpdate = true;
                        }
                    }
                }

                // Update if needed
                if (needsUpdate)
                {
                    UpdateSinksAndSources();
                    // Update the cached bitmap when sinks or sources move
                    UpdateCachedVectorFieldBitmap();
                }
            }

            // Periodically update the cached bitmap for accurate sampling
            if (Time.time - lastCacheUpdateTime > CACHE_UPDATE_INTERVAL)
            {
                UpdateCachedVectorFieldBitmap();
                lastCacheUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// Updates the simulation in FixedUpdate if configured to do so.
        /// </summary>
        private void FixedUpdate()
        {
            if (navierStokesSolver == null || !parameters.AutoUpdate)
                return;

            if (parameters.UseFixedUpdate)
            {
                UpdateSimulation(Time.fixedDeltaTime * parameters.TimeStepMultiplier);
            }

            UpdateTextureCache();
        }

        /// <summary>
        /// Updates the simulation with the specified time step.
        /// </summary>
        /// <param name="deltaTime">The time step for the simulation.</param>
        public void UpdateSimulation(float deltaTime)
        {
            navierStokesSolver.Update(deltaTime);
        }


        /// <summary>
        /// Converts a world position to normalized coordinates (0-1) for field sampling.
        /// </summary>
        /// <param name="worldPosition">The world position to convert.</param>
        /// <returns>Normalized coordinates (0-1).</returns>
        public Vector2 WorldToNormalizedPosition(Vector3 worldPosition)
        {
            Bounds bounds = WorldBounds;
            float x = Mathf.InverseLerp(bounds.min.x, bounds.max.x, worldPosition.x);
            float z = Mathf.InverseLerp(bounds.min.z, bounds.max.z, worldPosition.z);
            return new Vector2(x, z);
        }

        /// <summary>
        /// Converts normalized coordinates (0-1) to a world position.
        /// </summary>
        /// <param name="normalizedPosition">The normalized coordinates to convert.</param>
        /// <returns>World position.</returns>
        public Vector3 NormalizedToWorldPosition(Vector2 normalizedPosition)
        {
            Bounds bounds = WorldBounds;
            float x = Mathf.Lerp(bounds.min.x, bounds.max.x, normalizedPosition.x);
            float z = Mathf.Lerp(bounds.min.z, bounds.max.z, normalizedPosition.y);
            return new Vector3(x, bounds.center.y, z);
        }

        /// <summary>
        /// Samples the vector field at the specified normalized position.
        /// </summary>
        /// <param name="normalizedPosition">The position to sample in normalized coordinates (0-1).</param>
        /// <returns>The vector at the specified position.</returns>
        public Vector3 SampleField(Vector2 normalizedPosition)
        {
            if (navierStokesSolver == null)
                return Vector3.zero;

            Vector2 fieldVector = navierStokesSolver.SampleField(normalizedPosition);

            // Check if the sampled vector is too small (effectively zero)
            if (fieldVector.sqrMagnitude < 0.001f)
            {
                // Try to find a non-zero vector in the vicinity
                fieldVector = FindNearestNonZeroVector(normalizedPosition);
            }

            return new Vector3(fieldVector.x, 0, fieldVector.y);
        }

        /// <summary>
        /// Finds the nearest non-zero vector by sampling in a spiral pattern around the given position.
        /// </summary>
        /// <param name="normalizedPosition">The center position to search around.</param>
        /// <returns>A non-zero vector, or a fallback vector if none is found.</returns>
        private Vector2 FindNearestNonZeroVector(Vector2 normalizedPosition)
        {
            if (navierStokesSolver == null)
                return Vector2.zero;

            // Define search parameters
            const int maxSearchSteps = 10;
            const float searchStepSize = 0.02f;

            // Try sampling in a spiral pattern
            Vector2 bestVector = Vector2.zero;
            float bestMagnitude = 0f;

            for (int step = 1; step <= maxSearchSteps; step++)
            {
                // Calculate angle based on step (golden angle for better distribution)
                float angle = step * 2.4f;
                float radius = step * searchStepSize;

                // Calculate offset
                Vector2 offset = new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius
                );

                // Calculate sample position
                Vector2 samplePos = normalizedPosition + offset;

                // Clamp to valid range
                samplePos.x = Mathf.Clamp01(samplePos.x);
                samplePos.y = Mathf.Clamp01(samplePos.y);

                // Sample at this position
                Vector2 sampleVector = navierStokesSolver.SampleField(samplePos);
                float magnitude = sampleVector.sqrMagnitude;

                // If this is better than our current best, update
                if (magnitude > bestMagnitude)
                {
                    bestVector = sampleVector;
                    bestMagnitude = magnitude;

                    // If it's good enough, early exit
                    if (magnitude > 0.01f)
                        break;
                }
            }

            // If we found a good vector, return it
            if (bestMagnitude > 0.001f)
                return bestVector;

            // Otherwise, use a fallback based on position
            // Direct toward the center of the field as a last resort
            Vector2 dirToCenter = new Vector2(0.5f, 0.5f) - normalizedPosition;

            if (dirToCenter.sqrMagnitude > 0.001f)
                return dirToCenter.normalized * 0.1f;

            // If at center, use a small upward vector
            return new Vector2(0, 0.1f);
        }

        /// <summary>
        /// Adds a sink (destination) to the field.
        /// </summary>
        /// <param name="worldPosition">The world position of the sink.</param>
        /// <param name="radius">The radius of the sink in normalized coordinates (0-1).</param>
        /// <param name="createGameObject">Whether to create a GameObject with a VectorFieldSink component.</param>
        /// <returns>The created VectorFieldSink component, or null if createGameObject is false.</returns>
        public VectorFieldSink AddSink(Vector3 worldPosition, float radius, bool createGameObject = false)
        {
            if (fieldGenerator == null)
                return null;

            Vector2 normalizedPosition = WorldToNormalizedPosition(worldPosition);
            fieldGenerator.AddSink(normalizedPosition, radius);
            navierStokesSolver.UpdateInputTexture(fieldGenerator.FieldTexture);

            if (createGameObject)
            {
                GameObject sinkObject = new GameObject("Sink");
                sinkObject.transform.position = worldPosition;
                VectorFieldSink sink = sinkObject.AddComponent<VectorFieldSink>();
                Bounds bounds = WorldBounds;
                sink.radius = radius * Mathf.Max(
                    bounds.max.x - bounds.min.x,
                    bounds.max.z - bounds.min.z
                );
                RegisterSink(sink);
                return sink;
            }

            return null;
        }

        /// <summary>
        /// Adds a source (area to avoid) to the field.
        /// </summary>
        /// <param name="worldPosition">The world position of the source.</param>
        /// <param name="radius">The radius of the source in normalized coordinates (0-1).</param>
        /// <param name="createGameObject">Whether to create a GameObject with a VectorFieldSource component.</param>
        /// <returns>The created VectorFieldSource component, or null if createGameObject is false.</returns>
        public VectorFieldSource AddSource(Vector3 worldPosition, float radius, bool createGameObject = false)
        {
            if (fieldGenerator == null)
                return null;

            Vector2 normalizedPosition = WorldToNormalizedPosition(worldPosition);
            fieldGenerator.AddSource(normalizedPosition, radius);
            navierStokesSolver.UpdateInputTexture(fieldGenerator.FieldTexture);

            if (createGameObject)
            {
                GameObject sourceObject = new GameObject("Source");
                sourceObject.transform.position = worldPosition;
                VectorFieldSource source = sourceObject.AddComponent<VectorFieldSource>();
                Bounds bounds = WorldBounds;
                source.radius = radius * Mathf.Max(
                    bounds.max.x - bounds.min.x,
                    bounds.max.z - bounds.min.z
                );
                RegisterSource(source);
                return source;
            }

            return null;
        }

        /// <summary>
        /// Adds an obstacle to the field.
        /// </summary>
        /// <param name="worldPosition">The world position of the obstacle.</param>
        /// <param name="radius">The radius of the obstacle in normalized coordinates (0-1).</param>
        public void AddObstacle(Vector3 worldPosition, float radius)
        {
            if (fieldGenerator == null)
                return;

            Vector2 normalizedPosition = WorldToNormalizedPosition(worldPosition);
            fieldGenerator.AddObstacle(normalizedPosition, radius);
            navierStokesSolver.UpdateInputTexture(fieldGenerator.FieldTexture);
        }

        /// <summary>
        /// Sets a rectangular area as valid field space.
        /// </summary>
        /// <param name="center">Center of the rectangle in world coordinates.</param>
        /// <param name="size">Size of the rectangle in world coordinates.</param>
        public void SetFieldRect(Vector3 center, Vector3 size)
        {
            if (fieldGenerator == null)
                return;

            Vector2 normalizedCenter = WorldToNormalizedPosition(center);
            Bounds bounds = WorldBounds;
            Vector2 normalizedSize = new Vector2(
                size.x / (bounds.max.x - bounds.min.x),
                size.z / (bounds.max.z - bounds.min.z)
            );

            fieldGenerator.SetFieldRect(normalizedCenter, normalizedSize);
            navierStokesSolver.UpdateInputTexture(fieldGenerator.FieldTexture);
        }

        /// <summary>
        /// Sets a circular area as valid field space.
        /// </summary>
        /// <param name="center">Center of the circle in world coordinates.</param>
        /// <param name="radius">Radius of the circle in world coordinates.</param>
        public void SetFieldCircle(Vector3 center, float radius)
        {
            if (fieldGenerator == null)
                return;

            Vector2 normalizedCenter = WorldToNormalizedPosition(center);
            Bounds bounds = WorldBounds;
            float normalizedRadius = radius / Mathf.Max(
                bounds.max.x - bounds.min.x,
                bounds.max.z - bounds.min.z
            );

            fieldGenerator.SetFieldCircle(normalizedCenter, normalizedRadius);
            navierStokesSolver.UpdateInputTexture(fieldGenerator.FieldTexture);
        }

        /// <summary>
        /// Initializes the field from a mesh.
        /// </summary>
        /// <param name="mesh">The mesh to use for field generation.</param>
        /// <param name="sinkLocations">Optional array of sink locations in world space.</param>
        /// <param name="sinkRadius">Radius of the sinks in world space.</param>
        public void InitializeFromMesh(Mesh mesh, Vector2[] sinkLocations = null, float sinkRadius = 0.05f)
        {
            if (fieldGenerator == null || meshProcessorComputeShader == null || mesh == null)
            {
                Debug.LogError("VectorFieldManager: Cannot initialize from mesh. Check references.");
                return;
            }

            fieldGenerator.GenerateFromMesh(mesh, meshProcessorComputeShader, sinkLocations, sinkRadius);

            navierStokesSolver.UpdateInputTexture(fieldGenerator.FieldTexture);
        }

        /// <summary>
        /// Clears all sinks and sources from the field.
        /// </summary>
        /// <param name="destroyGameObjects">Whether to destroy the GameObjects associated with sinks and sources.</param>
        public void ClearSinksAndSources(bool destroyGameObjects = false)
        {
            if (fieldGenerator == null)
                return;

            fieldGenerator.ClearSinksAndSources();
            navierStokesSolver.UpdateInputTexture(fieldGenerator.FieldTexture);

            if (destroyGameObjects)
            {
                foreach (VectorFieldSink sink in sinks)
                {
                    if (sink != null)
                    {
                        DestroyImmediate(sink.gameObject);
                    }
                }

                foreach (VectorFieldSource source in sources)
                {
                    if (source != null)
                    {
                        DestroyImmediate(source.gameObject);
                    }
                }
            }

            sinks.Clear();
            sources.Clear();
            previousSinkPositions.Clear();
            previousSourcePositions.Clear();
        }

        /// <summary>
        /// Registers an agent with this vector field manager.
        /// </summary>
        /// <param name="agent">The agent to register.</param>
        public void RegisterAgent(PathfindingAgent agent)
        {
            if (agent != null && !agents.Contains(agent))
            {
                agents.Add(agent);
                agent.SetVectorFieldManager(this);
            }
        }

        /// <summary>
        /// Unregisters an agent from this vector field manager.
        /// </summary>
        /// <param name="agent">The agent to unregister.</param>
        public void UnregisterAgent(PathfindingAgent agent)
        {
            if (agent != null && agents.Contains(agent))
            {
                agents.Remove(agent);
            }
        }

        /// <summary>
        /// Updates the simulation parameters.
        /// </summary>
        public void UpdateParameters()
        {
            if (navierStokesSolver == null || parameters == null)
                return;

            navierStokesSolver.UpdateParameters(
                parameters.Viscosity,
                parameters.PressureIterations,
                parameters.DiffusionIterations,
                parameters.SinkStrength,
                parameters.SourceStrength,
                parameters.GlobalPressureStrength,
                parameters.GlobalPressureIterations
            );
        }

        /// <summary>
        /// Creates a standard mesh from the current NavMesh.
        /// </summary>
        /// <returns>A mesh representing the NavMesh.</returns>
        public Mesh CreateMeshFromNavMesh()
        {
            // Get the NavMesh triangulation data
            NavMeshTriangulation navMeshData = NavMesh.CalculateTriangulation();

            // Create a new mesh from the NavMesh data
            Mesh navMesh = new Mesh();
            navMesh.vertices = navMeshData.vertices;
            navMesh.triangles = navMeshData.indices;
            navMesh.RecalculateNormals();

            Debug.Log($"Created mesh from NavMesh with {navMeshData.vertices.Length} vertices and {navMeshData.indices.Length / 3} triangles");

            return navMesh;
        }

        /// <summary>
        /// Creates a 2D projection mesh from a 3D mesh by flattening it onto the XZ plane.
        /// </summary>
        /// <param name="sourceMesh">The source 3D mesh to project.</param>
        /// <param name="yValue">Optional Y value for the projected mesh (default is 0).</param>
        /// <returns>A new mesh with vertices projected onto the XZ plane.</returns>
        public Mesh Create2DProjectionMesh(Mesh sourceMesh, float yValue = 0f)
        {
            if (sourceMesh == null)
            {
                Debug.LogError("VectorFieldManager: Source mesh is null.");
                return null;
            }

            // Create a new mesh for the 2D projection
            Mesh projectedMesh = new Mesh();

            // Get the vertices from the source mesh
            Vector3[] sourceVertices = sourceMesh.vertices;
            Vector3[] projectedVertices = new Vector3[sourceVertices.Length];

            // Project each vertex onto the XZ plane (Y becomes the specified value)
            for (int i = 0; i < sourceVertices.Length; i++)
            {
                projectedVertices[i] = new Vector3(sourceVertices[i].x, yValue, sourceVertices[i].z);
            }

            // Set the vertices and triangles for the projected mesh
            projectedMesh.vertices = projectedVertices;
            projectedMesh.triangles = sourceMesh.triangles;

            // Recalculate normals (they will all point up since it's a flat mesh)
            projectedMesh.RecalculateNormals();

            Debug.Log($"Created 2D projection with {projectedVertices.Length} vertices and {sourceMesh.triangles.Length / 3} triangles");

            return projectedMesh;
        }

        /// <summary>
        /// Creates a 2D projection mesh directly from the current NavMesh.
        /// This is a convenience method that combines CreateMeshFromNavMesh and Create2DProjectionMesh.
        /// </summary>
        /// <param name="yValue">Optional Y value for the projected mesh (default is 0).</param>
        /// <returns>A 2D mesh projected from the NavMesh.</returns>
        public Mesh CreateNavMesh2DProjection(float yValue = 0f)
        {
            Mesh navMesh = CreateMeshFromNavMesh();
            if (navMesh == null)
            {
                return null;
            }

            return Create2DProjectionMesh(navMesh, yValue);
        }

        /// <summary>
        /// Sets the world bounds of the vector field based on the current NavMesh.
        /// </summary>
        /// <param name="padding">Optional padding to add around the NavMesh bounds (default is 1.0).</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool SetWorldBoundsFromNavMesh(float padding = 1.0f)
        {
            // Get the NavMesh triangulation data
            NavMeshTriangulation navMeshData = NavMesh.CalculateTriangulation();

            if (navMeshData.vertices.Length == 0)
            {
                Debug.LogError("VectorFieldManager: NavMesh has no vertices.");
                return false;
            }

            // Calculate the bounds of the NavMesh
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (Vector3 vertex in navMeshData.vertices)
            {
                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }

            // Add padding to the bounds
            min -= new Vector3(padding, 0, padding);
            max += new Vector3(padding, 0, padding);

            // Update the extent GameObjects if they exist
            if (minExtentObject != null && maxExtentObject != null)
            {
                minExtentObject.transform.position = min;
                maxExtentObject.transform.position = max;
                Debug.Log($"Set extent GameObjects from NavMesh: Min={min}, Max={max}");
            }
            else
            {
                // Fall back to setting the default bounds
                defaultBounds = new Bounds(
                    (min + max) * 0.5f,  // Center
                    max - min            // Size
                );
                Debug.Log($"Set default bounds from NavMesh: {defaultBounds}");
            }

            // If we already have a field generator, we need to reinitialize it
            if (fieldGenerator != null)
            {
                // Reinitialize the field with the new bounds
                if (initialFieldTexture != null)
                {
                    fieldGenerator.LoadFromTexture(initialFieldTexture);
                }
                else
                {
                    fieldGenerator.SetFullField();
                }

                // Update the NavierStokes solver with the new field texture
                if (navierStokesSolver != null)
                {
                    navierStokesSolver.UpdateInputTexture(fieldGenerator.FieldTexture);
                }
            }

            return true;
        }

        /// <summary>
        /// Initializes the vector field from the current NavMesh.
        /// This method automatically sets the world bounds and creates a field from the NavMesh.
        /// </summary>
        /// <param name="sinkLocations">Optional array of sink locations in world space.</param>
        /// <param name="sinkRadius">Radius of the sinks in world space.</param>
        /// <param name="padding">Optional padding to add around the NavMesh bounds.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool InitializeFromNavMesh(Vector2[] sinkLocations = null, float sinkRadius = 0.05f, float padding = 1.0f)
        {
            // Set the world bounds based on the NavMesh
            if (!SetWorldBoundsFromNavMesh(padding))
            {
                return false;
            }

            // Create a mesh from the NavMesh
            Mesh navMesh = CreateMeshFromNavMesh();
            if (navMesh == null)
            {
                return false;
            }

            // Initialize the field from the mesh
            InitializeFromMesh(navMesh, sinkLocations, sinkRadius);

            return true;
        }

        /// <summary>
        /// Registers a sink with this vector field manager.
        /// </summary>
        /// <param name="sink">The sink to register.</param>
        public void RegisterSink(VectorFieldSink sink)
        {
            if (sink != null && !sinks.Contains(sink))
            {
                sinks.Add(sink);
                sink.SetManager(this);
                previousSinkPositions[sink] = sink.transform.position;
                UpdateSinksAndSources();
            }
        }

        /// <summary>
        /// Unregisters a sink from this vector field manager.
        /// </summary>
        /// <param name="sink">The sink to unregister.</param>
        public void UnregisterSink(VectorFieldSink sink)
        {
            if (sink != null && sinks.Contains(sink))
            {
                sinks.Remove(sink);
                previousSinkPositions.Remove(sink);
                UpdateSinksAndSources();
            }
        }

        /// <summary>
        /// Registers a source with this vector field manager.
        /// </summary>
        /// <param name="source">The source to register.</param>
        public void RegisterSource(VectorFieldSource source)
        {
            if (source != null && !sources.Contains(source))
            {
                sources.Add(source);
                source.SetManager(this);
                previousSourcePositions[source] = source.transform.position;
                UpdateSinksAndSources();
            }
        }

        /// <summary>
        /// Unregisters a source from this vector field manager.
        /// </summary>
        /// <param name="source">The source to unregister.</param>
        public void UnregisterSource(VectorFieldSource source)
        {
            if (source != null && sources.Contains(source))
            {
                sources.Remove(source);
                previousSourcePositions.Remove(source);
                UpdateSinksAndSources();
            }
        }

        /// <summary>
        /// Updates the field based on the current sink and source positions.
        /// </summary>
        public void UpdateSinksAndSources()
        {
            if (fieldGenerator == null)
                return;

            // Clear existing sinks and sources
            fieldGenerator.ClearSinksAndSources();

            // Add sinks from registered GameObjects
            foreach (VectorFieldSink sink in sinks)
            {
                if (sink != null && sink.isActive)
                {
                    Vector2 normalizedPosition = WorldToNormalizedPosition(sink.transform.position);
                    Bounds bounds = WorldBounds;
                    float normalizedRadius = sink.radius / Mathf.Max(
                        bounds.max.x - bounds.min.x,
                        bounds.max.z - bounds.min.z
                    );
                    fieldGenerator.AddSink(normalizedPosition, normalizedRadius);
                }
            }

            // Add sources from registered GameObjects
            foreach (VectorFieldSource source in sources)
            {
                if (source != null && source.isActive)
                {
                    Vector2 normalizedPosition = WorldToNormalizedPosition(source.transform.position);
                    Bounds bounds = WorldBounds;
                    float normalizedRadius = source.radius / Mathf.Max(
                        bounds.max.x - bounds.min.x,
                        bounds.max.z - bounds.min.z
                    );
                    fieldGenerator.AddSource(normalizedPosition, normalizedRadius);
                }
            }

            // Update the NavierStokes solver
            navierStokesSolver.UpdateInputTexture(fieldGenerator.FieldTexture);

            // Update the cached bitmap
            UpdateCachedVectorFieldBitmap();
        }

        /// <summary>
        /// Releases resources when the object is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            // Clear singleton reference if this is the current instance
            if (_instance == this)
            {
                _instance = null;
            }

            navierStokesSolver?.Dispose();

            if (cachedVectorFieldBitmap != null)
                Destroy(cachedVectorFieldBitmap);

            // Clear references to this manager in sinks and sources
            foreach (VectorFieldSink sink in sinks)
            {
                if (sink != null)
                {
                    sink.SetManager(null);
                }
            }

            foreach (VectorFieldSource source in sources)
            {
                if (source != null)
                {
                    source.SetManager(null);
                }
            }
        }

        public void UpdateInputTexture(Texture2D texture)
        {
            navierStokesSolver.UpdateInputTexture(texture);
        }
        private void UpdateTextureCache()
        {
            VelocityTexture = navierStokesSolver?.VelocityTexture;
            PressureTexture = navierStokesSolver?.GetPressureTexture();
            GlobalPressureTexture = navierStokesSolver?.GetGlobalPressureTexture();
            DivergenceTexture = navierStokesSolver?.GetDivergenceTexture();
            BoundaryInfoTexture = navierStokesSolver?.GetBoundaryInfoTexture();
            FieldTexture = fieldGenerator?.FieldTexture;
            CachedVectorFieldBitmap = cachedVectorFieldBitmap;
        }
    }
}
