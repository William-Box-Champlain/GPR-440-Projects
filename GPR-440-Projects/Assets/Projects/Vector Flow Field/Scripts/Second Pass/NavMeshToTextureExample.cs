using UnityEngine;
using UnityEngine.AI;

namespace VFF
{
    /// <summary>
    /// Example component that demonstrates how to convert a NavMesh to a texture for use with Vector Flow Fields.
    /// </summary>
    public class NavMeshToTextureExample : MonoBehaviour
    {
        [Header("NavMesh Conversion Settings")]
        [Tooltip("The compute shader used for mesh processing")]
        public ComputeShader meshProcessor;

        [Tooltip("The axis to project along (0 = X, 1 = Y, 2 = Z)")]
        public int projectionAxis = 1;

        [Tooltip("The resolution of the generated texture")]
        public Vector2Int textureResolution = new Vector2Int(512, 512);

        [Tooltip("The area mask for sink areas (0 = none)")]
        public int sinkAreaMask = 0;

        [Tooltip("The radius of sink points in world units")]
        public float sinkRadius = 1.0f;

        [Header("Debug Settings")]
        [Tooltip("Whether to save the generated texture to disk")]
        public bool saveTextureToDisk = false;

        [Tooltip("The path to save the texture to (relative to project folder)")]
        public string savePath = "Assets/NavMeshTexture.png";

        [Tooltip("Whether to display the generated texture")]
        public bool displayTexture = true;

        [Tooltip("The material to display the texture on")]
        public Material displayMaterial;

        // Reference to the VectorFieldManager
        private SecondPassVectorFieldManager vectorFieldManager;

        // The generated texture
        private Texture2D generatedTexture;

        private void Awake()
        {
            vectorFieldManager = GetComponent<SecondPassVectorFieldManager>();

            if (meshProcessor == null)
            {
                Debug.LogError("NavMeshToTextureExample: Mesh processor compute shader is not assigned.");
                return;
            }
        }

        private void Start()
        {
            // Generate the texture from the NavMesh
            GenerateTextureFromNavMesh();

            // Apply the texture to the VectorFieldManager
            if (generatedTexture != null && vectorFieldManager != null)
            {
                vectorFieldManager.UpdateInputTexture(generatedTexture);
            }

            // Display the texture if requested
            if (displayTexture && generatedTexture != null && displayMaterial != null)
            {
                displayMaterial.mainTexture = generatedTexture;
            }
        }

        /// <summary>
        /// Generates a texture from the current NavMesh.
        /// </summary>
        public void GenerateTextureFromNavMesh()
        {
            // Check if NavMesh exists
            NavMeshTriangulation navMeshData = NavMesh.CalculateTriangulation();
            if (navMeshData.vertices.Length == 0 || navMeshData.indices.Length == 0)
            {
                Debug.LogError("NavMeshToTextureExample: No NavMesh data found. Make sure a NavMesh is baked in the scene.");
                return;
            }

            // Create the texture generator
            MeshTextureGenerator textureGenerator = new MeshTextureGenerator(meshProcessor);
            textureGenerator.SetTextureSize(textureResolution.x, textureResolution.y);
            textureGenerator.SetSinkRadius(sinkRadius);

            // Generate the texture
            generatedTexture = textureGenerator.GenerateTextureFromNavMesh(sinkAreaMask, projectionAxis);

            if (generatedTexture == null)
            {
                Debug.LogError("NavMeshToTextureExample: Failed to generate texture from NavMesh.");
                return;
            }

            Debug.Log($"NavMeshToTextureExample: Successfully generated texture from NavMesh ({textureResolution.x}x{textureResolution.y}).");

            // Save the texture if requested
            if (saveTextureToDisk)
            {
                SaveTextureToDisk();
            }
        }

        /// <summary>
        /// Saves the generated texture to disk.
        /// </summary>
        private void SaveTextureToDisk()
        {
            if (generatedTexture == null)
            {
                Debug.LogError("NavMeshToTextureExample: No texture to save.");
                return;
            }

            try
            {
                // Convert the texture to PNG
                byte[] bytes = generatedTexture.EncodeToPNG();

                // Save the texture
                System.IO.File.WriteAllBytes(savePath, bytes);

                Debug.Log($"NavMeshToTextureExample: Texture saved to {savePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"NavMeshToTextureExample: Failed to save texture: {e.Message}");
            }
        }

        /// <summary>
        /// Visualizes the NavMesh and the generated texture.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !displayTexture || generatedTexture == null)
                return;

            // Draw the NavMesh bounds
            NavMeshTriangulation navMeshData = NavMesh.CalculateTriangulation();
            if (navMeshData.vertices.Length == 0)
                return;

            // Calculate bounds
            Vector3 min = navMeshData.vertices[0];
            Vector3 max = navMeshData.vertices[0];

            foreach (Vector3 vertex in navMeshData.vertices)
            {
                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }

            // Draw bounds
            Gizmos.color = Color.cyan;

            // Adjust bounds based on projection axis
            Vector3 size = max - min;
            Vector3 center = (min + max) * 0.5f;

            switch (projectionAxis)
            {
                case 0: // X-axis projection
                    size.x = 0.1f;
                    break;
                case 1: // Y-axis projection
                    size.y = 0.1f;
                    break;
                case 2: // Z-axis projection
                    size.z = 0.1f;
                    break;
            }

            Gizmos.DrawWireCube(center, size);

            // Draw sink areas if any
            if (sinkAreaMask != 0)
            {
                Gizmos.color = Color.red;

                for (int i = 0; i < navMeshData.areas.Length; i++)
                {
                    if ((navMeshData.areas[i] & sinkAreaMask) != 0)
                    {
                        // Draw the triangles in this area
                        int triIndex = i * 3;
                        if (triIndex + 2 < navMeshData.indices.Length)
                        {
                            Vector3 v1 = navMeshData.vertices[navMeshData.indices[triIndex]];
                            Vector3 v2 = navMeshData.vertices[navMeshData.indices[triIndex + 1]];
                            Vector3 v3 = navMeshData.vertices[navMeshData.indices[triIndex + 2]];

                            Gizmos.DrawLine(v1, v2);
                            Gizmos.DrawLine(v2, v3);
                            Gizmos.DrawLine(v3, v1);
                        }
                    }
                }
            }
        }
    }
}
