using UnityEngine;
using System.Collections.Generic;

namespace VFF
{
    /// <summary>
    /// Generates textures from meshes for use with the Vector Flow Field system.
    /// </summary>
    public class MeshTextureGenerator
    {
        private ComputeShader meshProcessor;
        private int kernelIndex;
        
        // Default texture size
        private Vector2Int textureSize = new Vector2Int(512, 512);
        
        // Default sink radius
        private float sinkRadius = 1.0f;

        /// <summary>
        /// Initializes a new instance of the MeshTextureGenerator class.
        /// </summary>
        /// <param name="meshProcessor">The compute shader used for mesh processing.</param>
        public MeshTextureGenerator(ComputeShader meshProcessor)
        {
            this.meshProcessor = meshProcessor;
            kernelIndex = meshProcessor.FindKernel("CSMain");
        }

        /// <summary>
        /// Sets the texture size for generated textures.
        /// </summary>
        /// <param name="width">The width of the texture.</param>
        /// <param name="height">The height of the texture.</param>
        public void SetTextureSize(int width, int height)
        {
            textureSize = new Vector2Int(width, height);
        }

        /// <summary>
        /// Sets the radius for sink points in the generated texture.
        /// </summary>
        /// <param name="radius">The radius of sink points.</param>
        public void SetSinkRadius(float radius)
        {
            sinkRadius = Mathf.Max(0.1f, radius);
        }

        /// <summary>
        /// Generates a texture from a set of 2D triangles.
        /// </summary>
        /// <param name="triangles">The triangles to process. Each triangle consists of 3 consecutive Vector2 vertices.</param>
        /// <param name="bounds">The bounds of the triangles. Uses X and Z components for 2D mapping.</param>
        /// <param name="sinks">Optional array of sink points.</param>
        /// <returns>A texture representing the mesh, or null if generation fails.</returns>
        public Texture2D GenerateTextureFromTriangles(Vector2[] triangles, Bounds bounds, Vector2[] sinks = null)
        {
            // Validate input parameters
            if (triangles == null || triangles.Length == 0)
            {
                Debug.LogError("MeshTextureGenerator: No triangles provided.");
                return null;
            }

            if (triangles.Length % 3 != 0)
            {
                Debug.LogError($"MeshTextureGenerator: Triangle array length ({triangles.Length}) is not divisible by 3.");
                return null;
            }

            if (textureSize.x <= 0 || textureSize.y <= 0)
            {
                Debug.LogError($"MeshTextureGenerator: Invalid texture size: {textureSize}");
                return null;
            }

            if (kernelIndex < 0 || meshProcessor == null)
            {
                Debug.LogError("MeshTextureGenerator: Invalid compute shader configuration.");
                return null;
            }

            // Store variables that need to be cleaned up
            RenderTexture resultTexture = null;
            ComputeBuffer triangleBuffer = null;
            ComputeBuffer sinkBuffer = null;
            Texture2D resultTexture2D = null;
            RenderTexture previousActive = RenderTexture.active; // Store current active render texture

            try
            {
                // Create the result texture
                resultTexture = new RenderTexture(textureSize.x, textureSize.y, 0, RenderTextureFormat.ARGB32);
                resultTexture.enableRandomWrite = true;
                resultTexture.Create();

                // Create the triangle buffer
                triangleBuffer = new ComputeBuffer(triangles.Length, sizeof(float) * 2);
                triangleBuffer.SetData(triangles);

                // Create the sink buffer if needed
                if (sinks != null && sinks.Length > 0)
                {
                    sinkBuffer = new ComputeBuffer(sinks.Length, sizeof(float) * 2);
                    sinkBuffer.SetData(sinks);
                }
                else
                {
                    // Create an empty buffer with zero elements
                    sinkBuffer = new ComputeBuffer(1, sizeof(float) * 2);
                    sinkBuffer.SetData(new Vector2[] { Vector2.zero });
                }

                // Set the compute shader parameters
                meshProcessor.SetTexture(kernelIndex, "Result", resultTexture);
                meshProcessor.SetBuffer(kernelIndex, "triangles", triangleBuffer);
                meshProcessor.SetInt("numTriangles", triangles.Length / 3);

                // Set the bounds
                Vector2 minBounds = new Vector2(bounds.min.x, bounds.min.z);
                Vector2 maxBounds = new Vector2(bounds.max.x, bounds.max.z);
                meshProcessor.SetVector("minBounds", minBounds);
                meshProcessor.SetVector("maxBounds", maxBounds);

                // Set the texture size
                meshProcessor.SetVector("textureSize", new Vector2(textureSize.x, textureSize.y));

                // Set the sink parameters
                meshProcessor.SetBuffer(kernelIndex, "sinks", sinkBuffer);
                meshProcessor.SetInt("numSinks", sinks != null ? sinks.Length : 0);
                meshProcessor.SetFloat("sinkRadius", sinkRadius);

                // Dispatch the compute shader
                int threadGroupsX = Mathf.CeilToInt(textureSize.x / 8.0f);
                int threadGroupsY = Mathf.CeilToInt(textureSize.y / 8.0f);
                meshProcessor.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);

                // Ensure GPU has completed all work
                // This helps avoid synchronization issues when reading back the texture
                //resultTexture.MarkRestoreExpected();

                // Read the result texture
                resultTexture2D = new Texture2D(textureSize.x, textureSize.y, TextureFormat.ARGB32, false);
                RenderTexture.active = resultTexture;
                resultTexture2D.ReadPixels(new Rect(0, 0, textureSize.x, textureSize.y), 0, 0);
                resultTexture2D.Apply();

                // Return the result
                return resultTexture2D;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"MeshTextureGenerator: Error generating texture: {e.Message}");

                // Clean up the result texture if needed
                if (resultTexture2D != null)
                {
                    UnityEngine.Object.Destroy(resultTexture2D);
                }

                return null;
            }
            finally
            {
                // Restore previous active render texture
                RenderTexture.active = previousActive;

                // Release compute buffers
                if (triangleBuffer != null)
                {
                    triangleBuffer.Release();
                }

                if (sinkBuffer != null)
                {
                    sinkBuffer.Release();
                }

                // Release the render texture
                if (resultTexture != null)
                {
                    resultTexture.Release();
                }
            }
        }

        /// <summary>
        /// Generates a texture from a projected mesh.
        /// </summary>
        /// <param name="mesh">The mesh to process.</param>
        /// <param name="projectionAxis">The axis used for projection (0 = X, 1 = Y, 2 = Z).</param>
        /// <param name="sinks">Optional array of sink points.</param>
        /// <returns>A texture representing the mesh.</returns>
        public Texture2D GenerateTextureFromMesh(Mesh mesh, int projectionAxis = 1, Vector2[] sinks = null)
        {
            if (mesh == null)
            {
                Debug.LogError("MeshTextureGenerator: No mesh provided.");
                return null;
            }

            // Extract triangles from the mesh
            Vector2[] triangles = NavMeshConverter.ExtractTrianglesForProcessing(mesh, projectionAxis);
            
            // Calculate bounds
            Bounds bounds = NavMeshConverter.CalculateTrianglesBounds(triangles);
            
            // Generate the texture
            return GenerateTextureFromTriangles(triangles, bounds, sinks);
        }

        /// <summary>
        /// Generates a texture directly from a NavMesh.
        /// </summary>
        /// <param name="sinkAreaMask">The area mask for sink areas.</param>
        /// <param name="projectionAxis">The axis used for projection (0 = X, 1 = Y, 2 = Z).</param>
        /// <returns>A texture representing the NavMesh.</returns>
        public Texture2D GenerateTextureFromNavMesh(int sinkAreaMask = 0, int projectionAxis = 1)
        {
            // Convert NavMesh to mesh
            Mesh navMeshAsMesh = NavMeshConverter.NavMeshToMesh();
            
            if (navMeshAsMesh == null)
            {
                Debug.LogError("MeshTextureGenerator: Failed to convert NavMesh to mesh.");
                return null;
            }
            
            // Project the mesh
            Mesh projectedMesh = NavMeshConverter.CreateMeshProjection(navMeshAsMesh, projectionAxis);
            
            if (projectedMesh == null)
            {
                Debug.LogError("MeshTextureGenerator: Failed to create mesh projection.");
                return null;
            }
            
            // Extract sink points if needed
            Vector2[] sinks = null;
            if (sinkAreaMask != 0)
            {
                UnityEngine.AI.NavMeshTriangulation navMeshData = UnityEngine.AI.NavMesh.CalculateTriangulation();
                sinks = NavMeshConverter.ExtractSinkPoints(navMeshData, sinkAreaMask, projectionAxis);
            }
            
            // Generate the texture
            return GenerateTextureFromMesh(projectedMesh, projectionAxis, sinks);
        }
    }
}
