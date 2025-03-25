//using System.Collections;
//using System.Collections.Generic;
//using NUnit.Framework;
//using UnityEngine;
//using UnityEngine.TestTools;
//using UnityEngine.AI;
//using VFF;

//namespace Tests.EditMode
//{
//    public class NavMeshConverterTests
//    {
//        private GameObject navMeshPlane;
//        private ComputeShader meshProcessor;

//        [SetUp]
//        public void Setup()
//        {
//            // Create a plane for the NavMesh
//            navMeshPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
//            navMeshPlane.transform.position = Vector3.zero;
//            navMeshPlane.transform.localScale = new Vector3(10, 1, 10);

//            // Add a NavMeshSurface component
//            //navMeshSurface = navMeshPlane.AddComponent<NavMeshSurface>();
//            //navMeshSurface.collectObjects = CollectObjects.All;
//            //navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

//            // Bake the NavMesh
//            //navMeshSurface.BuildNavMesh();

//            // Load the mesh processor compute shader
//            meshProcessor = Resources.Load<ComputeShader>("MeshProcessor");
//            if (meshProcessor == null)
//            {
//                Debug.LogWarning("MeshProcessor compute shader not found in Resources folder. Some tests may fail.");
//            }
//        }

//        [TearDown]
//        public void TearDown()
//        {
//            // Clean up
//            Object.DestroyImmediate(navMeshPlane);
//        }

//        [Test]
//        public void NavMeshToMesh_ReturnsValidMesh()
//        {
//            // Act
//            Mesh mesh = NavMeshConverter.NavMeshToMesh();

//            // Assert
//            Assert.IsNotNull(mesh, "NavMeshToMesh should return a valid mesh");
//            Assert.Greater(mesh.vertices.Length, 0, "Mesh should have vertices");
//            Assert.Greater(mesh.triangles.Length, 0, "Mesh should have triangles");
//        }

//        [Test]
//        public void CreateMeshProjection_ReturnsValidMesh()
//        {
//            // Arrange
//            Mesh navMeshAsMesh = NavMeshConverter.NavMeshToMesh();

//            // Act
//            Mesh projectedMesh = NavMeshConverter.CreateMeshProjection(navMeshAsMesh);

//            // Assert
//            Assert.IsNotNull(projectedMesh, "CreateMeshProjection should return a valid mesh");
//            Assert.Greater(projectedMesh.vertices.Length, 0, "Projected mesh should have vertices");
//            Assert.Greater(projectedMesh.triangles.Length, 0, "Projected mesh should have triangles");

//            // Check that all vertices have y=0 (for Y-axis projection)
//            foreach (Vector3 vertex in projectedMesh.vertices)
//            {
//                Assert.AreEqual(0, vertex.y, 0.001f, "Y component should be zero for Y-axis projection");
//            }
//        }

//        [Test]
//        public void ExtractTrianglesForProcessing_ReturnsValidTriangles()
//        {
//            // Arrange
//            Mesh navMeshAsMesh = NavMeshConverter.NavMeshToMesh();
//            Mesh projectedMesh = NavMeshConverter.CreateMeshProjection(navMeshAsMesh);

//            // Act
//            Vector2[] triangles = NavMeshConverter.ExtractTrianglesForProcessing(projectedMesh);

//            // Assert
//            Assert.IsNotNull(triangles, "ExtractTrianglesForProcessing should return a valid array");
//            Assert.Greater(triangles.Length, 0, "Triangles array should not be empty");
//            Assert.AreEqual(projectedMesh.triangles.Length, triangles.Length, "Triangles array should have the same length as mesh.triangles");
//        }

//        [Test]
//        public void CalculateTrianglesBounds_ReturnsValidBounds()
//        {
//            // Arrange
//            Mesh navMeshAsMesh = NavMeshConverter.NavMeshToMesh();
//            Mesh projectedMesh = NavMeshConverter.CreateMeshProjection(navMeshAsMesh);
//            Vector2[] triangles = NavMeshConverter.ExtractTrianglesForProcessing(projectedMesh);

//            // Act
//            Bounds bounds = NavMeshConverter.CalculateTrianglesBounds(triangles);

//            // Assert
//            Assert.IsTrue(bounds.size.x > 0, "Bounds should have positive width");
//            Assert.IsTrue(bounds.size.z > 0, "Bounds should have positive depth");
//        }

//        [Test]
//        public void ExtractSinkPoints_WithNoSinkAreas_ReturnsEmptyArray()
//        {
//            // Arrange
//            NavMeshTriangulation navMeshData = NavMesh.CalculateTriangulation();

//            // Act
//            Vector2[] sinks = NavMeshConverter.ExtractSinkPoints(navMeshData, 0);

//            // Assert
//            Assert.IsNotNull(sinks, "ExtractSinkPoints should return a valid array");
//            Assert.AreEqual(0, sinks.Length, "Sinks array should be empty when no sink areas are defined");
//        }

//        [UnityTest]
//        public IEnumerator MeshTextureGenerator_GeneratesValidTexture()
//        {
//            // Skip test if mesh processor is not available
//            if (meshProcessor == null)
//            {
//                Assert.Ignore("MeshProcessor compute shader not found. Skipping test.");
//                yield break;
//            }

//            // Arrange
//            Mesh navMeshAsMesh = NavMeshConverter.NavMeshToMesh();
//            Mesh projectedMesh = NavMeshConverter.CreateMeshProjection(navMeshAsMesh);
//            Vector2[] triangles = NavMeshConverter.ExtractTrianglesForProcessing(projectedMesh);
//            Bounds bounds = NavMeshConverter.CalculateTrianglesBounds(triangles);

//            MeshTextureGenerator textureGenerator = new MeshTextureGenerator(meshProcessor);
//            textureGenerator.SetTextureSize(256, 256);

//            // Act
//            Texture2D texture = textureGenerator.GenerateTextureFromTriangles(triangles, bounds);

//            // Wait one frame for the compute shader to complete
//            yield return null;

//            // Assert
//            Assert.IsNotNull(texture, "GenerateTextureFromTriangles should return a valid texture");
//            Assert.AreEqual(256, texture.width, "Texture should have the specified width");
//            Assert.AreEqual(256, texture.height, "Texture should have the specified height");

//            // Check that the texture has some white pixels (field space)
//            Color[] pixels = texture.GetPixels();
//            bool hasFieldSpace = false;
//            foreach (Color pixel in pixels)
//            {
//                if (pixel.r > 0.5f && pixel.g > 0.5f && pixel.b > 0.5f)
//                {
//                    hasFieldSpace = true;
//                    break;
//                }
//            }
//            Assert.IsTrue(hasFieldSpace, "Texture should have some white pixels (field space)");
//        }

//        [UnityTest]
//        public IEnumerator NavMeshToTextureExample_GeneratesValidTexture()
//        {
//            // Skip test if mesh processor is not available
//            if (meshProcessor == null)
//            {
//                Assert.Ignore("MeshProcessor compute shader not found. Skipping test.");
//                yield break;
//            }

//            // Arrange
//            GameObject testObject = new GameObject("TestObject");
//            VectorFieldManager vectorFieldManager = testObject.AddComponent<VectorFieldManager>();
//            NavMeshToTextureExample example = testObject.AddComponent<NavMeshToTextureExample>();
//            example.meshProcessor = meshProcessor;
//            example.textureResolution = new Vector2Int(256, 256);

//            // Act
//            example.GenerateTextureFromNavMesh();

//            // Wait one frame for the compute shader to complete
//            yield return null;

//            // Assert
//            // We can't directly access the generated texture, but we can check if the VectorFieldManager has a texture
//            //Assert.IsNotNull(vectorFieldManager.GetInputTexture(), "VectorFieldManager should have a texture assigned");

//            // Clean up
//            Object.DestroyImmediate(testObject);
//        }
//    }
//}
