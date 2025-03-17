using System.Collections.Generic;
using UnityEngine;

namespace VFF
{
    /// <summary>
    /// Handles the creation and management of the input bitmap texture for the Vector Flow Field.
    /// </summary>
    public class FieldTextureGenerator
    {
        private static readonly Color FieldColor = Color.white;
        private static readonly Color ObstacleColor = Color.black;
        private static readonly Color SinkColor = Color.red;
        private static readonly Color SourceColor = Color.green;

        private Vector2Int resolution;
        private Texture2D fieldTexture;

        private List<(Vector2 position, float radius)> sinks = new List<(Vector2 position, float radius)>();
        private List<(Vector2 position, float radius)> sources = new List<(Vector2 position, float radius)>();

        /// <summary>
        /// Gets the generated field texture.
        /// </summary>
        public Texture2D FieldTexture => fieldTexture;

        /// <summary>
        /// Initializes a new instance of the FieldTextureGenerator class.
        /// </summary>
        /// <param name="resolution">Resolution of the field texture.</param>
        public FieldTextureGenerator(Vector2Int resolution)
        {
            this.resolution = resolution;
            InitializeTexture();
        }

        /// <summary>
        /// Initializes the field texture with default values.
        /// </summary>
        private void InitializeTexture()
        {
            fieldTexture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);
            fieldTexture.filterMode = FilterMode.Point;
            fieldTexture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[resolution.x * resolution.y];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = ObstacleColor;
            }
            fieldTexture.SetPixels(pixels);
            fieldTexture.Apply();
        }

        /// <summary>
        /// Sets a rectangular area as valid field space.
        /// </summary>
        /// <param name="center">Center of the rectangle in normalized coordinates (0-1).</param>
        /// <param name="size">Size of the rectangle in normalized coordinates (0-1).</param>
        public void SetFieldRect(Vector2 center, Vector2 size)
        {
            int startX = Mathf.FloorToInt((center.x - size.x / 2) * resolution.x);
            int startY = Mathf.FloorToInt((center.y - size.y / 2) * resolution.y);
            int endX = Mathf.CeilToInt((center.x + size.x / 2) * resolution.x);
            int endY = Mathf.CeilToInt((center.y + size.y / 2) * resolution.y);

            startX = Mathf.Clamp(startX, 0, resolution.x - 1);
            startY = Mathf.Clamp(startY, 0, resolution.y - 1);
            endX = Mathf.Clamp(endX, 0, resolution.x - 1);
            endY = Mathf.Clamp(endY, 0, resolution.y - 1);

            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    fieldTexture.SetPixel(x, y, FieldColor);
                }
            }
            fieldTexture.Apply();
        }

        /// <summary>
        /// Sets a circular area as valid field space.
        /// </summary>
        /// <param name="center">Center of the circle in normalized coordinates (0-1).</param>
        /// <param name="radius">Radius of the circle in normalized coordinates (0-1).</param>
        public void SetFieldCircle(Vector2 center, float radius)
        {
            int centerX = Mathf.FloorToInt(center.x * resolution.x);
            int centerY = Mathf.FloorToInt(center.y * resolution.y);
            int radiusPixels = Mathf.CeilToInt(radius * Mathf.Min(resolution.x, resolution.y));

            for (int y = centerY - radiusPixels; y <= centerY + radiusPixels; y++)
            {
                for (int x = centerX - radiusPixels; x <= centerX + radiusPixels; x++)
                {
                    if (x < 0 || x >= resolution.x || y < 0 || y >= resolution.y)
                        continue;

                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                    if (distance <= radiusPixels)
                    {
                        fieldTexture.SetPixel(x, y, FieldColor);
                    }
                }
            }
            fieldTexture.Apply();
        }

        /// <summary>
        /// Sets the entire texture as valid field space.
        /// </summary>
        public void SetFullField()
        {
            Color[] pixels = new Color[resolution.x * resolution.y];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = FieldColor;
            }
            fieldTexture.SetPixels(pixels);
            fieldTexture.Apply();
        }
        
        /// <summary>
        /// Sets the entire texture as obstacles (non-field space).
        /// </summary>
        public void ClearField()
        {
            Color[] pixels = new Color[resolution.x * resolution.y];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = ObstacleColor;
            }
            fieldTexture.SetPixels(pixels);
            fieldTexture.Apply();
        }

        /// <summary>
        /// Adds an obstacle (area outside field space) to the texture.
        /// </summary>
        /// <param name="center">Center of the obstacle in normalized coordinates (0-1).</param>
        /// <param name="radius">Radius of the obstacle in normalized coordinates (0-1).</param>
        public void AddObstacle(Vector2 center, float radius)
        {
            int centerX = Mathf.FloorToInt(center.x * resolution.x);
            int centerY = Mathf.FloorToInt(center.y * resolution.y);
            int radiusPixels = Mathf.CeilToInt(radius * Mathf.Min(resolution.x, resolution.y));

            for (int y = centerY - radiusPixels; y <= centerY + radiusPixels; y++)
            {
                for (int x = centerX - radiusPixels; x <= centerX + radiusPixels; x++)
                {
                    if (x < 0 || x >= resolution.x || y < 0 || y >= resolution.y)
                        continue;

                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                    if (distance <= radiusPixels)
                    {
                        fieldTexture.SetPixel(x, y, ObstacleColor);
                    }
                }
            }
            fieldTexture.Apply();
        }

        /// <summary>
        /// Adds a sink (destination) to the texture.
        /// </summary>
        /// <param name="position">Position of the sink in normalized coordinates (0-1).</param>
        /// <param name="radius">Radius of the sink in normalized coordinates (0-1).</param>
        public void AddSink(Vector2 position, float radius)
        {
            sinks.Add((position, radius));
            UpdateSink(position, radius);
        }

        /// <summary>
        /// Updates a sink on the texture.
        /// </summary>
        /// <param name="position">Position of the sink in normalized coordinates (0-1).</param>
        /// <param name="radius">Radius of the sink in normalized coordinates (0-1).</param>
        private void UpdateSink(Vector2 position, float radius)
        {
            int centerX = Mathf.FloorToInt(position.x * resolution.x);
            int centerY = Mathf.FloorToInt(position.y * resolution.y);
            int radiusPixels = Mathf.CeilToInt(radius * Mathf.Min(resolution.x, resolution.y));

            for (int y = centerY - radiusPixels; y <= centerY + radiusPixels; y++)
            {
                for (int x = centerX - radiusPixels; x <= centerX + radiusPixels; x++)
                {
                    if (x < 0 || x >= resolution.x || y < 0 || y >= resolution.y)
                        continue;

                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                    if (distance <= radiusPixels)
                    {
                        fieldTexture.SetPixel(x, y, SinkColor);
                    }
                }
            }
            fieldTexture.Apply();
        }

        /// <summary>
        /// Adds a source (area to avoid) to the texture.
        /// </summary>
        /// <param name="position">Position of the source in normalized coordinates (0-1).</param>
        /// <param name="radius">Radius of the source in normalized coordinates (0-1).</param>
        public void AddSource(Vector2 position, float radius)
        {
            sources.Add((position, radius));
            UpdateSource(position, radius);
        }

        /// <summary>
        /// Updates a source on the texture.
        /// </summary>
        /// <param name="position">Position of the source in normalized coordinates (0-1).</param>
        /// <param name="radius">Radius of the source in normalized coordinates (0-1).</param>
        private void UpdateSource(Vector2 position, float radius)
        {
            int centerX = Mathf.FloorToInt(position.x * resolution.x);
            int centerY = Mathf.FloorToInt(position.y * resolution.y);
            int radiusPixels = Mathf.CeilToInt(radius * Mathf.Min(resolution.x, resolution.y));

            for (int y = centerY - radiusPixels; y <= centerY + radiusPixels; y++)
            {
                for (int x = centerX - radiusPixels; x <= centerX + radiusPixels; x++)
                {
                    if (x < 0 || x >= resolution.x || y < 0 || y >= resolution.y)
                        continue;

                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                    if (distance <= radiusPixels)
                    {
                        fieldTexture.SetPixel(x, y, SourceColor);
                    }
                }
            }
            fieldTexture.Apply();
        }

        /// <summary>
        /// Clears all sinks and sources from the texture.
        /// </summary>
        public void ClearSinksAndSources()
        {
            sinks.Clear();
            sources.Clear();
            
            Color[] pixels = fieldTexture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i] == SinkColor || pixels[i] == SourceColor)
                {
                    pixels[i] = FieldColor;
                }
            }
            fieldTexture.SetPixels(pixels);
            fieldTexture.Apply();
        }

        /// <summary>
        /// Loads a field layout from an external texture.
        /// </summary>
        /// <param name="texture">The texture to load.</param>
        public void LoadFromTexture(Texture2D texture)
        {
            if (texture == null)
                return;

            RenderTexture rt = RenderTexture.GetTemporary(resolution.x, resolution.y, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(texture, rt);

            RenderTexture.active = rt;
            fieldTexture.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0);
            fieldTexture.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            Color[] pixels = fieldTexture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                
                if (IsColorSimilar(pixel, Color.red))
                {
                    pixels[i] = SinkColor;
                }
                else if (IsColorSimilar(pixel, Color.green))
                {
                    pixels[i] = SourceColor;
                }
                else if (pixel.r > 0.5f && pixel.g > 0.5f && pixel.b > 0.5f)
                {
                    pixels[i] = FieldColor;
                }
                else
                {
                    pixels[i] = ObstacleColor;
                }
            }
            fieldTexture.SetPixels(pixels);
            fieldTexture.Apply();

            UpdateSinkAndSourceLists();
        }

        /// <summary>
        /// Updates the lists of sinks and sources based on the current texture.
        /// </summary>
        private void UpdateSinkAndSourceLists()
        {
            sinks.Clear();
            sources.Clear();

            bool[,] visited = new bool[resolution.x, resolution.y];

            for (int y = 0; y < resolution.y; y++)
            {
                for (int x = 0; x < resolution.x; x++)
                {
                    if (visited[x, y])
                        continue;

                    Color pixel = fieldTexture.GetPixel(x, y);
                    if (pixel == SinkColor)
                    {
                        List<Vector2Int> sinkPixels = FloodFill(x, y, SinkColor, visited);
                        if (sinkPixels.Count > 0)
                        {
                            Vector2 center = CalculateCenter(sinkPixels);
                            float radius = CalculateRadius(sinkPixels, center);
                            
                            sinks.Add((new Vector2(center.x / resolution.x, center.y / resolution.y), 
                                      radius / Mathf.Min(resolution.x, resolution.y)));
                        }
                    }
                    else if (pixel == SourceColor)
                    {
                        List<Vector2Int> sourcePixels = FloodFill(x, y, SourceColor, visited);
                        if (sourcePixels.Count > 0)
                        {
                            Vector2 center = CalculateCenter(sourcePixels);
                            float radius = CalculateRadius(sourcePixels, center);
                            
                            sources.Add((new Vector2(center.x / resolution.x, center.y / resolution.y), 
                                        radius / Mathf.Min(resolution.x, resolution.y)));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Performs a flood fill to find connected pixels of the same color.
        /// </summary>
        private List<Vector2Int> FloodFill(int startX, int startY, Color targetColor, bool[,] visited)
        {
            List<Vector2Int> pixels = new List<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            
            queue.Enqueue(new Vector2Int(startX, startY));
            visited[startX, startY] = true;
            
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                pixels.Add(current);
                
                int[] dx = { 0, 1, 0, -1 };
                int[] dy = { -1, 0, 1, 0 };
                
                for (int i = 0; i < 4; i++)
                {
                    int nx = current.x + dx[i];
                    int ny = current.y + dy[i];
                    
                    if (nx >= 0 && nx < resolution.x && ny >= 0 && ny < resolution.y && 
                        !visited[nx, ny] && fieldTexture.GetPixel(nx, ny) == targetColor)
                    {
                        queue.Enqueue(new Vector2Int(nx, ny));
                        visited[nx, ny] = true;
                    }
                }
            }
            
            return pixels;
        }

        /// <summary>
        /// Calculates the center of a group of pixels.
        /// </summary>
        private Vector2 CalculateCenter(List<Vector2Int> pixels)
        {
            Vector2 sum = Vector2.zero;
            foreach (Vector2Int pixel in pixels)
            {
                sum += new Vector2(pixel.x, pixel.y);
            }
            return sum / pixels.Count;
        }

        /// <summary>
        /// Calculates the radius of a group of pixels.
        /// </summary>
        private float CalculateRadius(List<Vector2Int> pixels, Vector2 center)
        {
            float maxDistSq = 0;
            foreach (Vector2Int pixel in pixels)
            {
                float distSq = ((Vector2)pixel - center).sqrMagnitude;
                maxDistSq = Mathf.Max(maxDistSq, distSq);
            }
            return Mathf.Sqrt(maxDistSq);
        }

        /// <summary>
        /// Checks if two colors are similar.
        /// </summary>
        private bool IsColorSimilar(Color a, Color b, float threshold = 0.2f)
        {
            return Vector4.Distance(new Vector4(a.r, a.g, a.b, a.a), new Vector4(b.r, b.g, b.b, b.a)) < threshold;
        }

        /// <summary>
        /// Generates a field texture from a mesh using a compute shader.
        /// </summary>
        /// <param name="mesh">The mesh to process.</param>
        /// <param name="meshProcessor">The compute shader for processing the mesh.</param>
        /// <param name="sinkLocations">Optional array of sink locations in world space.</param>
        /// <param name="sinkRadius">Radius of the sinks in world space.</param>
        public void GenerateFromMesh(Mesh mesh, ComputeShader meshProcessor, Vector2[] sinkLocations = null, float sinkRadius = 0.05f)
        {
            if (mesh == null || meshProcessor == null)
            {
                Debug.LogError("FieldTextureGenerator: Mesh or compute shader is null.");
                return;
            }

            RenderTexture rt = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.ARGB32);
            rt.enableRandomWrite = true;
            rt.Create();

            Vector2[] trianglesData = new Vector2[mesh.triangles.Length];
            for (int i = 0; i < mesh.triangles.Length / 3; i++)
            {
                int triangle = i * 3;
                Vector3 v1 = mesh.vertices[mesh.triangles[triangle]];
                Vector3 v2 = mesh.vertices[mesh.triangles[triangle + 1]];
                Vector3 v3 = mesh.vertices[mesh.triangles[triangle + 2]];
                
                trianglesData[triangle] = new Vector2(v1.x, v1.z);
                trianglesData[triangle + 1] = new Vector2(v2.x, v2.z);
                trianglesData[triangle + 2] = new Vector2(v3.x, v3.z);
            }

            ComputeBuffer triangleBuffer = new ComputeBuffer(trianglesData.Length, sizeof(float) * 2);
            triangleBuffer.SetData(trianglesData);
            
            ComputeBuffer sinkBuffer = null;
            if (sinkLocations != null && sinkLocations.Length > 0)
            {
                sinkBuffer = new ComputeBuffer(sinkLocations.Length, sizeof(float) * 2);
                sinkBuffer.SetData(sinkLocations);
            }
            else
            {
                sinkBuffer = new ComputeBuffer(1, sizeof(float) * 2);
                sinkBuffer.SetData(new Vector2[] { Vector2.zero });
            }

            int kernel = meshProcessor.FindKernel("CSMain");
            meshProcessor.SetBuffer(kernel, "triangles", triangleBuffer);
            meshProcessor.SetInt("numTriangles", mesh.triangles.Length / 3);
            meshProcessor.SetVector("minBounds", new Vector4(mesh.bounds.min.x, mesh.bounds.min.z, 0, 0));
            meshProcessor.SetVector("maxBounds", new Vector4(mesh.bounds.max.x, mesh.bounds.max.z, 0, 0));
            meshProcessor.SetVector("textureSize", new Vector2(resolution.x, resolution.y));
            meshProcessor.SetTexture(kernel, "Result", rt);
            meshProcessor.SetBuffer(kernel, "sinks", sinkBuffer);
            meshProcessor.SetInt("numSinks", sinkLocations != null ? sinkLocations.Length : 0);
            meshProcessor.SetFloat("sinkRadius", sinkRadius);

            meshProcessor.Dispatch(
                kernel,
                Mathf.CeilToInt(resolution.x / 8.0f),
                Mathf.CeilToInt(resolution.y / 8.0f),
                1
            );

            RenderTexture.active = rt;
            fieldTexture.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0);
            fieldTexture.Apply();
            RenderTexture.active = null;

            triangleBuffer.Release();
            sinkBuffer.Release();
            rt.Release();

            UpdateSinkAndSourceLists();
        }

        /// <summary>
        /// Resizes the field texture to a new resolution.
        /// </summary>
        /// <param name="newResolution">The new resolution.</param>
        public void Resize(Vector2Int newResolution)
        {
            if (newResolution.x <= 0 || newResolution.y <= 0)
                return;

            Texture2D tempTexture = new Texture2D(fieldTexture.width, fieldTexture.height, TextureFormat.RGBA32, false);
            tempTexture.SetPixels(fieldTexture.GetPixels());
            tempTexture.Apply();

            resolution = newResolution;

            fieldTexture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);
            fieldTexture.filterMode = FilterMode.Point;
            fieldTexture.wrapMode = TextureWrapMode.Clamp;

            RenderTexture rt = RenderTexture.GetTemporary(resolution.x, resolution.y, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tempTexture, rt);

            RenderTexture.active = rt;
            fieldTexture.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0);
            fieldTexture.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            Object.Destroy(tempTexture);

            UpdateSinkAndSourceLists();
        }
    }
}
