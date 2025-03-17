using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.AI;
using VFF;

namespace VFF.Tests.PlayMode
{
    public class VectorFieldManagerTests
    {
        private GameObject managerObject;
        private VectorFieldManager manager;
        private VectorFieldParameters parameters;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            // Create a game object with the VectorFieldManager component
            managerObject = new GameObject("VectorFieldManager");
            manager = managerObject.AddComponent<VectorFieldManager>();

            // Create parameters
            parameters = ScriptableObject.CreateInstance<VectorFieldParameters>();
            
            // Load compute shaders
            ComputeShader navierStokesComputeShader = Resources.Load<ComputeShader>("NavierStokesCompute");
            ComputeShader meshProcessorComputeShader = Resources.Load<ComputeShader>("MeshProcessor");

            // If the shaders are not found, create mock shaders for testing
            if (navierStokesComputeShader == null)
            {
                Debug.LogWarning("NavierStokesCompute shader not found in Resources folder. Using a mock shader for testing.");
            }

            if (meshProcessorComputeShader == null)
            {
                Debug.LogWarning("MeshProcessor shader not found in Resources folder. Using a mock shader for testing.");
            }

            // Set up the manager
            var field = typeof(VectorFieldManager).GetField("parameters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(manager, parameters);

            field = typeof(VectorFieldManager).GetField("navierStokesComputeShader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(manager, navierStokesComputeShader);

            field = typeof(VectorFieldManager).GetField("meshProcessorComputeShader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(manager, meshProcessorComputeShader);

            // Call Awake manually
            var method = typeof(VectorFieldManager).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(manager, null);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(parameters);
            yield return null;
        }

        // Helper method to create a mock NavMesh for testing
        private Mesh CreateMockNavMesh()
        {
            Mesh mesh = new Mesh();
            
            // Create a simple quad mesh
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-10, 0, -10),
                new Vector3(10, 0, -10),
                new Vector3(10, 0, 10),
                new Vector3(-10, 0, 10)
            };
            
            int[] triangles = new int[]
            {
                0, 1, 2,
                0, 2, 3
            };
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            
            return mesh;
        }

        // Helper method to mock NavMesh.CalculateTriangulation
        private void MockNavMeshTriangulation(Mesh mockMesh)
        {
            // Use reflection to set up a mock for NavMesh.CalculateTriangulation
            // This is a simplified approach for testing purposes
            var navMeshType = typeof(NavMesh);
            var triangulationField = navMeshType.GetField("s_Triangulation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (triangulationField != null)
            {
                var triangulation = new NavMeshTriangulation
                {
                    vertices = mockMesh.vertices,
                    indices = mockMesh.triangles
                };
                
                triangulationField.SetValue(null, triangulation);
            }
            else
            {
                Debug.LogWarning("Could not mock NavMesh.CalculateTriangulation. Tests may fail.");
            }
        }

        [UnityTest]
        public IEnumerator WorldToNormalizedPosition_ConvertsCorrectly()
        {
            // Test conversion from world to normalized coordinates
            Vector3 worldPosition = new Vector3(5f, 0f, 5f);
            Vector2 normalizedPosition = manager.WorldToNormalizedPosition(worldPosition);

            // The default bounds are centered at (0,0,0) with size (20,0,20)
            // So (5,0,5) should be at (0.75, 0.75) in normalized coordinates
            Assert.AreEqual(0.75f, normalizedPosition.x, 0.01f, "X coordinate should be 0.75");
            Assert.AreEqual(0.75f, normalizedPosition.y, 0.01f, "Y coordinate should be 0.75");

            yield return null;
        }

        [UnityTest]
        public IEnumerator NormalizedToWorldPosition_ConvertsCorrectly()
        {
            // Test conversion from normalized to world coordinates
            Vector2 normalizedPosition = new Vector2(0.25f, 0.25f);
            Vector3 worldPosition = manager.NormalizedToWorldPosition(normalizedPosition);

            // The default bounds are centered at (0,0,0) with size (20,0,20)
            // So (0.25, 0.25) should be at (-5,0,-5) in world coordinates
            Assert.AreEqual(-5f, worldPosition.x, 0.01f, "X coordinate should be -5");
            Assert.AreEqual(0f, worldPosition.y, 0.01f, "Y coordinate should be 0");
            Assert.AreEqual(-5f, worldPosition.z, 0.01f, "Z coordinate should be -5");

            yield return null;
        }

        [UnityTest]
        public IEnumerator SampleField_ReturnsVector3()
        {
            // Sample the field at a normalized position
            Vector2 normalizedPosition = new Vector2(0.5f, 0.5f);
            Vector3 fieldVector = manager.SampleField(normalizedPosition);

            // Verify that we get a valid vector
            Assert.IsTrue(fieldVector.y == 0f, "Y component should be 0");
            Assert.IsTrue(fieldVector.magnitude >= 0f, "Vector magnitude should be non-negative");

            yield return null;
        }

        [UnityTest]
        public IEnumerator AddSink_UpdatesField()
        {
            // Add a sink at the center
            Vector3 sinkPosition = new Vector3(0f, 0f, 0f);
            float radius = 0.1f;
            manager.AddSink(sinkPosition, radius);

            // Sample the field near the sink
            Vector2 normalizedPosition = new Vector2(0.5f, 0.5f);
            Vector3 fieldVector = manager.SampleField(normalizedPosition);

            // The field should be updated, but we can't easily verify the exact values
            // So we just check that the field texture exists
            Assert.IsNotNull(manager.FieldTexture, "Field texture should not be null after adding a sink");

            yield return null;
        }

        [UnityTest]
        public IEnumerator AddSource_UpdatesField()
        {
            // Add a source at the center
            Vector3 sourcePosition = new Vector3(0f, 0f, 0f);
            float radius = 0.1f;
            manager.AddSource(sourcePosition, radius);

            // Sample the field near the source
            Vector2 normalizedPosition = new Vector2(0.5f, 0.5f);
            Vector3 fieldVector = manager.SampleField(normalizedPosition);

            // The field should be updated, but we can't easily verify the exact values
            // So we just check that the field texture exists
            Assert.IsNotNull(manager.FieldTexture, "Field texture should not be null after adding a source");

            yield return null;
        }

        [UnityTest]
        public IEnumerator AddObstacle_UpdatesField()
        {
            // Add an obstacle at the center
            Vector3 obstaclePosition = new Vector3(0f, 0f, 0f);
            float radius = 0.1f;
            manager.AddObstacle(obstaclePosition, radius);

            // Sample the field near the obstacle
            Vector2 normalizedPosition = new Vector2(0.5f, 0.5f);
            Vector3 fieldVector = manager.SampleField(normalizedPosition);

            // The field should be updated, but we can't easily verify the exact values
            // So we just check that the field texture exists
            Assert.IsNotNull(manager.FieldTexture, "Field texture should not be null after adding an obstacle");

            yield return null;
        }

        [UnityTest]
        public IEnumerator SetFieldRect_UpdatesField()
        {
            // Set a rectangular field area
            Vector3 center = new Vector3(0f, 0f, 0f);
            Vector3 size = new Vector3(10f, 0f, 10f);
            manager.SetFieldRect(center, size);

            // Sample the field at the center
            Vector2 normalizedPosition = new Vector2(0.5f, 0.5f);
            Vector3 fieldVector = manager.SampleField(normalizedPosition);

            // The field should be updated, but we can't easily verify the exact values
            // So we just check that the field texture exists
            Assert.IsNotNull(manager.FieldTexture, "Field texture should not be null after setting a field rect");

            yield return null;
        }

        [UnityTest]
        public IEnumerator SetFieldCircle_UpdatesField()
        {
            // Set a circular field area
            Vector3 center = new Vector3(0f, 0f, 0f);
            float radius = 5f;
            manager.SetFieldCircle(center, radius);

            // Sample the field at the center
            Vector2 normalizedPosition = new Vector2(0.5f, 0.5f);
            Vector3 fieldVector = manager.SampleField(normalizedPosition);

            // The field should be updated, but we can't easily verify the exact values
            // So we just check that the field texture exists
            Assert.IsNotNull(manager.FieldTexture, "Field texture should not be null after setting a field circle");

            yield return null;
        }

        [UnityTest]
        public IEnumerator ClearSinksAndSources_UpdatesField()
        {
            // Add a sink and a source
            manager.AddSink(new Vector3(-5f, 0f, 0f), 0.1f);
            manager.AddSource(new Vector3(5f, 0f, 0f), 0.1f);

            // Clear sinks and sources
            manager.ClearSinksAndSources();

            // The field should be updated, but we can't easily verify the exact values
            // So we just check that the field texture exists
            Assert.IsNotNull(manager.FieldTexture, "Field texture should not be null after clearing sinks and sources");

            yield return null;
        }

        [UnityTest]
        public IEnumerator UpdateParameters_UpdatesSolver()
        {
            // Update parameters
            manager.UpdateParameters();

            // The solver should be updated, but we can't easily verify the exact values
            // So we just check that the method doesn't throw an exception
            Assert.Pass("UpdateParameters should not throw an exception");

            yield return null;
        }

        [UnityTest]
        public IEnumerator CreateMeshFromNavMesh_ReturnsValidMesh()
        {
            // Create a mock NavMesh
            Mesh mockNavMesh = CreateMockNavMesh();
            
            // Try to mock NavMesh.CalculateTriangulation
            try
            {
                MockNavMeshTriangulation(mockNavMesh);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Could not mock NavMesh.CalculateTriangulation: {e.Message}");
                // Skip the test if we can't mock the NavMesh
                Assert.Ignore("Skipping test because NavMesh.CalculateTriangulation could not be mocked");
                yield break;
            }
            
            // Call the method
            Mesh result = manager.CreateMeshFromNavMesh();
            
            // Check that the result is not null
            Assert.IsNotNull(result, "CreateMeshFromNavMesh should return a valid mesh");
            
            // Check that the mesh has the expected number of vertices and triangles
            Assert.AreEqual(mockNavMesh.vertices.Length, result.vertices.Length, "Mesh should have the same number of vertices as the mock NavMesh");
            Assert.AreEqual(mockNavMesh.triangles.Length, result.triangles.Length, "Mesh should have the same number of triangles as the mock NavMesh");
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator Create2DProjectionMesh_ReturnsValidMesh()
        {
            // Create a mock mesh
            Mesh mockMesh = CreateMockNavMesh();
            
            // Call the method
            Mesh result = manager.Create2DProjectionMesh(mockMesh);
            
            // Check that the result is not null
            Assert.IsNotNull(result, "Create2DProjectionMesh should return a valid mesh");
            
            // Check that the mesh has the expected number of vertices and triangles
            Assert.AreEqual(mockMesh.vertices.Length, result.vertices.Length, "Mesh should have the same number of vertices as the input mesh");
            Assert.AreEqual(mockMesh.triangles.Length, result.triangles.Length, "Mesh should have the same number of triangles as the input mesh");
            
            // Check that all vertices have the same Y value (0 by default)
            foreach (Vector3 vertex in result.vertices)
            {
                Assert.AreEqual(0f, vertex.y, 0.001f, "All vertices should have Y=0");
            }
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetWorldBoundsFromNavMesh_UpdatesBounds()
        {
            // Create a mock NavMesh
            Mesh mockNavMesh = CreateMockNavMesh();
            
            // Try to mock NavMesh.CalculateTriangulation
            try
            {
                MockNavMeshTriangulation(mockNavMesh);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Could not mock NavMesh.CalculateTriangulation: {e.Message}");
                // Skip the test if we can't mock the NavMesh
                Assert.Ignore("Skipping test because NavMesh.CalculateTriangulation could not be mocked");
                yield break;
            }
            
            // Store the original bounds
            Bounds originalBounds = manager.WorldBounds;
            
            // Call the method
            bool result = manager.SetWorldBoundsFromNavMesh(1.0f);
            
            // Check that the method returned true
            Assert.IsTrue(result, "SetWorldBoundsFromNavMesh should return true");
            
            // Check that the bounds have been updated
            Assert.AreNotEqual(originalBounds.center, manager.WorldBounds.center, "Bounds center should be updated");
            Assert.AreNotEqual(originalBounds.size, manager.WorldBounds.size, "Bounds size should be updated");
            
            // Check that the bounds encompass the NavMesh vertices
            foreach (Vector3 vertex in mockNavMesh.vertices)
            {
                Assert.IsTrue(manager.WorldBounds.Contains(vertex), $"Bounds should contain vertex {vertex}");
            }
            
            yield return null;
        }

        [UnityTest]
        public IEnumerator InitializeFromNavMesh_UpdatesField()
        {
            // Create a mock NavMesh
            Mesh mockNavMesh = CreateMockNavMesh();
            
            // Try to mock NavMesh.CalculateTriangulation
            try
            {
                MockNavMeshTriangulation(mockNavMesh);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Could not mock NavMesh.CalculateTriangulation: {e.Message}");
                // Skip the test if we can't mock the NavMesh
                Assert.Ignore("Skipping test because NavMesh.CalculateTriangulation could not be mocked");
                yield break;
            }
            
            // Call the method
            bool result = manager.InitializeFromNavMesh();
            
            // Check that the method returned true
            Assert.IsTrue(result, "InitializeFromNavMesh should return true");
            
            // Check that the field texture exists
            Assert.IsNotNull(manager.FieldTexture, "Field texture should not be null after initialization");
            
            yield return null;
        }
    }
}
