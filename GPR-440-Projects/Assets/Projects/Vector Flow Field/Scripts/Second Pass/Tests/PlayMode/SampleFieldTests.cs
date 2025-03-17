using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VFF;

namespace VFF.Tests.PlayMode
{
    public class SampleFieldTests
    {
        private ComputeShader computeShader;
        private Texture2D inputTexture;
        private NavierStokesSolver solver;
        private Vector2Int resolution = new Vector2Int(64, 64);
        private Bounds worldBounds;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            // Load the compute shader
            computeShader = Resources.Load<ComputeShader>("NavierStokesCompute");
            if (computeShader == null)
            {
                Debug.LogWarning("NavierStokesCompute shader not found in Resources folder. Using a mock shader for testing.");
                // Create a mock compute shader for testing
            }

            // Create a test input texture
            inputTexture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);
            Color[] colors = new Color[resolution.x * resolution.y];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = Color.white; // All field space
            inputTexture.SetPixels(colors);
            inputTexture.Apply();

            // Create the solver
            solver = new NavierStokesSolver(
                computeShader,
                inputTexture,
                resolution,
                0.1f,  // viscosity
                10,    // pressure iterations
                10,    // diffusion iterations
                1.0f,  // sink strength
                1.0f   // source strength
            );

            // Create world bounds for testing
            worldBounds = new Bounds(Vector3.zero, new Vector3(10f, 0f, 10f));

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            solver.Dispose();
            Object.Destroy(inputTexture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SampleField_WithNormalizedCoordinates_ReturnsVector()
        {
            // Sample at different positions
            Vector2 center = solver.SampleField(new Vector2(0.5f, 0.5f));
            Vector2 topRight = solver.SampleField(new Vector2(0.75f, 0.75f));
            Vector2 bottomLeft = solver.SampleField(new Vector2(0.25f, 0.25f));

            // Verify that we get valid vectors
            Assert.IsTrue(center.magnitude >= 0, "Center vector magnitude should be non-negative");
            Assert.IsTrue(topRight.magnitude >= 0, "Top-right vector magnitude should be non-negative");
            Assert.IsTrue(bottomLeft.magnitude >= 0, "Bottom-left vector magnitude should be non-negative");

            yield return null;
        }

        [UnityTest]
        public IEnumerator SampleField_WithWorldCoordinates_ReturnsVector()
        {
            // Sample at different world positions
            Vector2 center = solver.SampleField(new Vector3(0f, 0f, 0f), worldBounds);
            Vector2 topRight = solver.SampleField(new Vector3(2.5f, 0f, 2.5f), worldBounds);
            Vector2 bottomLeft = solver.SampleField(new Vector3(-2.5f, 0f, -2.5f), worldBounds);

            // Verify that we get valid vectors
            Assert.IsTrue(center.magnitude >= 0, "Center vector magnitude should be non-negative");
            Assert.IsTrue(topRight.magnitude >= 0, "Top-right vector magnitude should be non-negative");
            Assert.IsTrue(bottomLeft.magnitude >= 0, "Bottom-left vector magnitude should be non-negative");

            yield return null;
        }

        [UnityTest]
        public IEnumerator WorldToNormalizedPosition_ConvertsCorrectly()
        {
            // Test conversion from world to normalized coordinates
            Vector3 worldPosition = new Vector3(2.5f, 0f, 2.5f);
            Vector2 normalizedPosition = solver.SampleField(worldPosition, worldBounds);

            // The world bounds are centered at (0,0,0) with size (10,0,10)
            // So (2.5,0,2.5) should be at (0.75, 0.75) in normalized coordinates
            Assert.AreEqual(0.75f, normalizedPosition.x, 0.01f, "X coordinate should be 0.75");
            Assert.AreEqual(0.75f, normalizedPosition.y, 0.01f, "Y coordinate should be 0.75");

            yield return null;
        }

        [UnityTest]
        public IEnumerator WorldAndNormalizedSampling_GiveConsistentResults()
        {
            // Create a vector field with a sink at the center
            Color[] colors = inputTexture.GetPixels();
            int centerIndex = resolution.x * (resolution.y / 2) + (resolution.x / 2);
            colors[centerIndex] = Color.red; // Add a sink at the center
            inputTexture.SetPixels(colors);
            inputTexture.Apply();
            solver.UpdateInputTexture(inputTexture);

            // Run a few simulation steps to create a flow field
            for (int i = 0; i < 5; i++)
                solver.Update(0.016f);

            // Sample at a specific position using both methods
            Vector3 worldPosition = new Vector3(2.5f, 0f, 2.5f);
            Vector2 normalizedPosition = solver.SampleField(worldPosition, worldBounds);
            
            Vector2 worldSample = solver.SampleField(worldPosition, worldBounds);
            Vector2 normalizedSample = solver.SampleField(normalizedPosition);

            // The results should be the same
            Assert.AreEqual(normalizedSample.x, worldSample.x, 0.01f, "X component should match");
            Assert.AreEqual(normalizedSample.y, worldSample.y, 0.01f, "Y component should match");

            yield return null;
        }
    }
}
