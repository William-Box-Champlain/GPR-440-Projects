using UnityEngine;

namespace VFF
{
    /// <summary>
    /// Component for visualizing Vector Flow Fields using a color field.
    /// </summary>
    public class VectorFieldVisualizer : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [Tooltip("Whether visualization is enabled")]
        [SerializeField] private bool visualizationEnabled = true;

        [Tooltip("How often to update the visualization (in seconds)")]
        [Range(0.05f, 1.0f)]
        [SerializeField] private float updateInterval = 0.1f;

        [Header("Color Field Visualization")]
        [Tooltip("Material for the color field visualization")]
        [SerializeField] private Material colorFieldMaterial;

        [Tooltip("Intensity of the color field")]
        [Range(0.1f, 2.0f)]
        [SerializeField] private float colorIntensity = 1f;

        [Tooltip("Height offset for the visualization plane")]
        [Range(0.001f, 0.1f)]
        [SerializeField] private float heightOffset = 0.01f;

        // Private fields for implementation
        private GameObject colorFieldObject;
        private MeshRenderer colorFieldRenderer;
        private MeshFilter colorFieldMeshFilter;
        private float lastUpdateTime;
        private Bounds lastBounds;

        // Reference to the manager (will use singleton if not explicitly set)
        private SecondPassVectorFieldManager vectorFieldManager;

        /// <summary>
        /// Gets the VectorFieldManager to use for visualization.
        /// </summary>
        private SecondPassVectorFieldManager Manager
        {
            get
            {
                if (vectorFieldManager == null)
                {
                    vectorFieldManager = SecondPassVectorFieldManager.GetInstance();
                }
                return vectorFieldManager;
            }
        }

        /// <summary>
        /// Initializes the component.
        /// </summary>
        private void Awake()
        {
            // Create default material if none is assigned
            if (colorFieldMaterial == null)
            {
                colorFieldMaterial = new Material(Shader.Find("VFF/VectorFieldVisualization"));
                if (colorFieldMaterial == null)
                {
                    Debug.LogError("VectorFieldVisualizer: Could not find VFF/VectorFieldVisualization shader. Make sure it's included in your project.");
                }
            }
        }

        /// <summary>
        /// Sets up the visualizations.
        /// </summary>
        private void Start()
        {
            // Initialize visualization if enabled
            if (visualizationEnabled && Manager != null)
            {
                SetupColorFieldVisualization();
            }
        }

        /// <summary>
        /// Updates the visualizations.
        /// </summary>
        private void Update()
        {
            if (!visualizationEnabled || Manager == null)
                return;

            // Check if bounds have changed
            Bounds currentBounds = Manager.WorldBounds;
            bool boundsChanged = !lastBounds.Equals(currentBounds);

            if (boundsChanged)
            {
                // Recreate visualization if bounds have changed
                SetupColorFieldVisualization();
                lastBounds = currentBounds;
            }
            // Update visualization at the specified interval
            else if (Time.time - lastUpdateTime >= updateInterval)
            {
                lastUpdateTime = Time.time;
                UpdateColorFieldVisualization();
            }
        }

        /// <summary>
        /// Sets up the color field visualization.
        /// </summary>
        private void SetupColorFieldVisualization()
        {
            // Clean up existing visualization
            if (colorFieldObject != null)
            {
                DestroyImmediate(colorFieldObject);
            }

            // Create a child GameObject for the color field
            colorFieldObject = new GameObject("ColorField");
            colorFieldObject.transform.SetParent(transform, false);

            // Add mesh components
            colorFieldMeshFilter = colorFieldObject.AddComponent<MeshFilter>();
            colorFieldRenderer = colorFieldObject.AddComponent<MeshRenderer>();

            // Create a quad mesh covering the vector field area
            Bounds bounds = Manager.WorldBounds;
            Mesh quadMesh = new Mesh();
            quadMesh.vertices = new Vector3[]
            {
                new Vector3(bounds.min.x, heightOffset, bounds.min.z),
                new Vector3(bounds.max.x, heightOffset, bounds.min.z),
                new Vector3(bounds.max.x, heightOffset, bounds.max.z),
                new Vector3(bounds.min.x, heightOffset, bounds.max.z)
            };
            quadMesh.uv = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quadMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            quadMesh.RecalculateNormals();

            colorFieldMeshFilter.mesh = quadMesh;
            colorFieldRenderer.material = colorFieldMaterial;

            // Set initial properties
            UpdateColorFieldVisualization();
        }

        /// <summary>
        /// Updates the color field visualization.
        /// </summary>
        private void UpdateColorFieldVisualization()
        {
            if (colorFieldRenderer == null || colorFieldMaterial == null || Manager == null)
                return;

            // Get the velocity texture from the vector field manager
            RenderTexture velocityTexture = Manager.GetVelocityTexture();

            if (velocityTexture == null)
                return;

            // Update material properties
            colorFieldMaterial.SetTexture("_VelocityTex", velocityTexture);
            colorFieldMaterial.SetFloat("_ColorIntensity", colorIntensity);
        }

        /// <summary>
        /// Toggles the visualization on/off.
        /// </summary>
        public void ToggleVisualization()
        {
            visualizationEnabled = !visualizationEnabled;

            if (visualizationEnabled && Manager != null)
            {
                if (colorFieldObject == null)
                {
                    SetupColorFieldVisualization();
                }
                else
                {
                    colorFieldObject.SetActive(true);
                    UpdateColorFieldVisualization();
                }
            }
            else if (colorFieldObject != null)
            {
                colorFieldObject.SetActive(false);
            }
        }

        /// <summary>
        /// Sets the color intensity.
        /// </summary>
        public void SetColorIntensity(float intensity)
        {
            colorIntensity = Mathf.Clamp(intensity, 0.1f, 2.0f);
            if (colorFieldMaterial != null)
            {
                colorFieldMaterial.SetFloat("_ColorIntensity", colorIntensity);
            }
        }

        /// <summary>
        /// Explicitly sets the VectorFieldManager to use.
        /// </summary>
        public void SetVectorFieldManager(SecondPassVectorFieldManager manager)
        {
            if (vectorFieldManager != manager)
            {
                vectorFieldManager = manager;

                if (visualizationEnabled && manager != null)
                {
                    SetupColorFieldVisualization();
                }
            }
        }

        /// <summary>
        /// Cleans up resources when the component is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            if (colorFieldObject != null)
            {
                DestroyImmediate(colorFieldObject);
            }
        }
    }
}
