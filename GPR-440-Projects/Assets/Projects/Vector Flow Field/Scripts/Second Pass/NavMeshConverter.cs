using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace VFF
{
    /// <summary>
    /// Converts a Unity NavMesh into a standard mesh and provides 2D projection functionality.
    /// </summary>
    public class NavMeshConverter
    {
        /// <summary>
        /// Converts a Unity NavMesh into a standard mesh.
        /// </summary>
        /// <returns>A mesh representing the NavMesh.</returns>
        public static Mesh NavMeshToMesh()
        {
            // Get the NavMesh as triangulation data
            NavMeshTriangulation navMeshTriangulation = NavMesh.CalculateTriangulation();

            // Create a new mesh
            Mesh mesh = new Mesh();
            mesh.vertices = navMeshTriangulation.vertices;
            mesh.triangles = navMeshTriangulation.indices;

            // Recalculate normals and bounds
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Creates a 2D projection of a mesh by flattening it along the specified axis.
        /// </summary>
        /// <param name="mesh">The mesh to project.</param>
        /// <param name="projectionAxis">The axis to project along (0 = X, 1 = Y, 2 = Z).</param>
        /// <returns>A new mesh representing the 2D projection.</returns>
        public static Mesh CreateMeshProjection(Mesh mesh, int projectionAxis = 1)
        {
            if (mesh == null)
                return null;

            // Get the vertices from the original mesh
            Vector3[] originalVertices = mesh.vertices;
            int[] originalTriangles = mesh.triangles;

            // Create new arrays for the projected mesh
            Vector3[] projectedVertices = new Vector3[originalVertices.Length];
            
            // Project the vertices
            for (int i = 0; i < originalVertices.Length; i++)
            {
                Vector3 vertex = originalVertices[i];
                
                // Zero out the component along the projection axis
                switch (projectionAxis)
                {
                    case 0: // Project along X-axis
                        projectedVertices[i] = new Vector3(0, vertex.y, vertex.z);
                        break;
                    case 1: // Project along Y-axis (most common for NavMesh)
                        projectedVertices[i] = new Vector3(vertex.x, 0, vertex.z);
                        break;
                    case 2: // Project along Z-axis
                        projectedVertices[i] = new Vector3(vertex.x, vertex.y, 0);
                        break;
                    default:
                        projectedVertices[i] = new Vector3(vertex.x, 0, vertex.z);
                        break;
                }
            }

            // Create a new mesh with the projected vertices
            Mesh projectedMesh = new Mesh();
            projectedMesh.vertices = projectedVertices;
            projectedMesh.triangles = originalTriangles;
            
            // Recalculate normals and bounds
            projectedMesh.RecalculateNormals();
            projectedMesh.RecalculateBounds();

            return projectedMesh;
        }

        /// <summary>
        /// Extracts the 2D triangles from a projected mesh for use with the MeshProcessor compute shader.
        /// </summary>
        /// <param name="mesh">The projected mesh.</param>
        /// <param name="projectionAxis">The axis that was used for projection (0 = X, 1 = Y, 2 = Z).</param>
        /// <returns>An array of Vector2 points representing the triangles (3 points per triangle).</returns>
        public static Vector2[] ExtractTrianglesForProcessing(Mesh mesh, int projectionAxis = 1)
        {
            if (mesh == null)
                return null;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            
            // Each triangle has 3 vertices
            Vector2[] result = new Vector2[triangles.Length];
            
            for (int i = 0; i < triangles.Length; i++)
            {
                Vector3 vertex = vertices[triangles[i]];
                
                // Convert to 2D based on the projection axis
                switch (projectionAxis)
                {
                    case 0: // X-axis projection (YZ plane)
                        result[i] = new Vector2(vertex.y, vertex.z);
                        break;
                    case 1: // Y-axis projection (XZ plane)
                        result[i] = new Vector2(vertex.x, vertex.z);
                        break;
                    case 2: // Z-axis projection (XY plane)
                        result[i] = new Vector2(vertex.x, vertex.y);
                        break;
                    default:
                        result[i] = new Vector2(vertex.x, vertex.z);
                        break;
                }
            }
            
            return result;
        }

        /// <summary>
        /// Calculates the bounds of a set of 2D triangles.
        /// </summary>
        /// <param name="triangles">The triangles to calculate bounds for.</param>
        /// <returns>A Bounds object representing the 2D bounds.</returns>
        public static Bounds CalculateTrianglesBounds(Vector2[] triangles)
        {
            if (triangles == null || triangles.Length == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            // Initialize with the first point
            Vector2 min = triangles[0];
            Vector2 max = triangles[0];
            
            // Find min and max for each dimension
            for (int i = 1; i < triangles.Length; i++)
            {
                min.x = Mathf.Min(min.x, triangles[i].x);
                min.y = Mathf.Min(min.y, triangles[i].y);
                
                max.x = Mathf.Max(max.x, triangles[i].x);
                max.y = Mathf.Max(max.y, triangles[i].y);
            }
            
            // Create bounds with center and size
            Vector3 center = new Vector3((min.x + max.x) * 0.5f, 0, (min.y + max.y) * 0.5f);
            Vector3 size = new Vector3(max.x - min.x, 0, max.y - min.y);
            
            return new Bounds(center, size);
        }

        /// <summary>
        /// Extracts the sink points from a NavMesh.
        /// </summary>
        /// <param name="navMesh">The NavMesh to extract sinks from.</param>
        /// <param name="sinkAreaMask">The area mask for sink areas.</param>
        /// <param name="projectionAxis">The axis used for projection (0 = X, 1 = Y, 2 = Z).</param>
        /// <returns>An array of Vector2 points representing the sink centers.</returns>
        public static Vector2[] ExtractSinkPoints(NavMeshTriangulation navMesh, int sinkAreaMask, int projectionAxis = 1)
        {
            if (navMesh.areas == null || navMesh.areas.Length == 0)
                return new Vector2[0];

            List<Vector2> sinks = new List<Vector2>();
            Dictionary<int, List<Vector3>> areaVertices = new Dictionary<int, List<Vector3>>();
            
            // Group vertices by area
            for (int i = 0; i < navMesh.indices.Length; i += 3)
            {
                int areaIndex = i / 3;
                int area = navMesh.areas[areaIndex];
                
                // Check if this is a sink area
                if ((area & sinkAreaMask) != 0)
                {
                    if (!areaVertices.ContainsKey(area))
                    {
                        areaVertices[area] = new List<Vector3>();
                    }
                    
                    // Add the three vertices of this triangle
                    areaVertices[area].Add(navMesh.vertices[navMesh.indices[i]]);
                    areaVertices[area].Add(navMesh.vertices[navMesh.indices[i + 1]]);
                    areaVertices[area].Add(navMesh.vertices[navMesh.indices[i + 2]]);
                }
            }
            
            // Calculate the center of each area
            foreach (var area in areaVertices.Keys)
            {
                Vector3 center = Vector3.zero;
                foreach (var vertex in areaVertices[area])
                {
                    center += vertex;
                }
                center /= areaVertices[area].Count;
                
                // Convert to 2D based on the projection axis
                Vector2 center2D;
                switch (projectionAxis)
                {
                    case 0: // X-axis projection (YZ plane)
                        center2D = new Vector2(center.y, center.z);
                        break;
                    case 1: // Y-axis projection (XZ plane)
                        center2D = new Vector2(center.x, center.z);
                        break;
                    case 2: // Z-axis projection (XY plane)
                        center2D = new Vector2(center.x, center.y);
                        break;
                    default:
                        center2D = new Vector2(center.x, center.z);
                        break;
                }
                
                sinks.Add(center2D);
            }
            
            return sinks.ToArray();
        }
    }
}
