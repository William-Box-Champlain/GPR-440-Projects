using UnityEngine;

namespace VFF
{
    /// <summary>
    /// Visualizes the global pressure field for debugging purposes.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class GlobalPressureVisualizer : MonoBehaviour
    {
        [Tooltip("The Vector Field Manager to visualize")]
        [SerializeField] private VectorFieldManager vectorFieldManager;

        [Tooltip("Whether to visualize the global pressure field")]
        [SerializeField] private bool visualizeGlobalPressure = true;

        [Tooltip("Whether to visualize the local pressure field")]
        [SerializeField] private bool visualizeLocalPressure = false;

        [Tooltip("Whether to visualize the boundary info")]
        [SerializeField] private bool visualizeBoundaryInfo = false;

        [Tooltip("Whether to visualize the divergence field")]
        [SerializeField] private bool visualizeDivergence = false;

        [Tooltip("Scale factor for the visualization")]
        [Range(0.1f, 10f)]
        [SerializeField] private float visualizationScale = 1f;

        [Tooltip("Update frequency in seconds")]
        [Range(0.01f, 1f)]
        [SerializeField] private float updateFrequency = 0.1f;

        private MeshRenderer meshRenderer;
        private Material material;
        private float updateTimer;

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            
            // Create a new material based on the Unlit/Texture shader
            material = new Material(Shader.Find("Unlit/Texture"));
            meshRenderer.material = material;
        }

        private void Start()
        {
            // If no vector field manager is assigned, try to find one
            if (vectorFieldManager == null)
            {
                vectorFieldManager = FindObjectOfType<VectorFieldManager>();
                if (vectorFieldManager == null)
                {
                    Debug.LogError("GlobalPressureVisualizer: No VectorFieldManager found in the scene.");
                    enabled = false;
                    return;
                }
            }

            // Set the initial texture
            UpdateVisualization();
        }

        private void Update()
        {
            updateTimer += Time.deltaTime;
            if (updateTimer >= updateFrequency)
            {
                updateTimer = 0f;
                UpdateVisualization();
            }
        }

        /// <summary>
        /// Updates the visualization texture.
        /// </summary>
        private void UpdateVisualization()
        {
            if (vectorFieldManager == null)
                return;

            RenderTexture sourceTexture = null;

            // Choose which texture to visualize
            if (visualizeGlobalPressure)
            {
                sourceTexture = vectorFieldManager.GlobalPressureTexture;
            }
            else if (visualizeLocalPressure)
            {
                sourceTexture = vectorFieldManager.PressureTexture;
            }
            else if (visualizeBoundaryInfo)
            {
                sourceTexture = vectorFieldManager.BoundaryInfoTexture;
            }
            else if (visualizeDivergence)
            {
                sourceTexture = vectorFieldManager.DivergenceTexture;
            }

            if (sourceTexture == null)
            {
                Debug.LogWarning("GlobalPressureVisualizer: Source texture is null.");
                return;
            }

            // Create a temporary texture to read from the source texture
            Texture2D tempTexture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
            RenderTexture prevRT = RenderTexture.active;
            RenderTexture.active = sourceTexture;
            tempTexture.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0);
            tempTexture.Apply();
            RenderTexture.active = prevRT;

            // Process the pixels to create a visualization
            Color[] pixels = tempTexture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float value = pixels[i].r;
                
                // Scale the value for better visualization
                value = value * visualizationScale;
                
                // Map the value to a color
                if (visualizeGlobalPressure || visualizeLocalPressure || visualizeDivergence)
                {
                    // Use a blue-white-red gradient for pressure and divergence
                    // Negative values are blue, positive values are red, zero is white
                    if (value < 0)
                    {
                        // Blue for negative values
                        pixels[i] = new Color(0, 0, Mathf.Clamp01(-value), 1);
                    }
                    else
                    {
                        // Red for positive values
                        pixels[i] = new Color(Mathf.Clamp01(value), 0, 0, 1);
                    }
                }
                else if (visualizeBoundaryInfo)
                {
                    // Use grayscale for boundary info
                    pixels[i] = new Color(value, value, value, 1);
                }
            }

            // Create a new texture for the visualization
            Texture2D visualizationTexture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
            visualizationTexture.SetPixels(pixels);
            visualizationTexture.Apply();

            // Set the texture on the material
            material.mainTexture = visualizationTexture;

            // Clean up
            Destroy(tempTexture);
        }

        private void OnDestroy()
        {
            // Clean up
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}
