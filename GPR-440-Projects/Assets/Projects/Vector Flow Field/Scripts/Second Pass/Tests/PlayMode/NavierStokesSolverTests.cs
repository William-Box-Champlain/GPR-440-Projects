using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VFF;

namespace VFF.Tests.PlayMode
{
    /// <summary>
    /// Tests for the NavierStokesSolver class to ensure proper simulation behavior.
    /// </summary>
    [TestFixture]
    [Category("Simulation")]
    [Timeout(5000)] // 5 second timeout for all tests
    public class NavierStokesSolverTests
    {
        private ComputeShader computeShader;
        private Texture2D inputTexture;
        private NavierStokesSolver solver;
        private Vector2Int resolution = new Vector2Int(64, 64);
        private const float DefaultTimeStep = 0.016f; // 60 FPS

        [UnitySetUp]
        public IEnumerator Setup()
        {
            // Load the compute shader using our utility
            computeShader = TestUtilities.LoadComputeShader("NavierStokesCompute");

            // Create a test input texture with all field space (white)
            inputTexture = TestUtilities.CreateTestTexture(resolution, Color.white);

            // Create the solver with default parameters
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
        [Description("Verifies that the solver initializes with the correct parameters")]
        public IEnumerator Solver_Initialize_WithCorrectParameters()
        {
            Assert.IsNotNull(solver.VelocityTexture, "Velocity texture should not be null");
            Assert.AreEqual(resolution.x, solver.VelocityTexture.width, "Velocity texture width should match resolution");
            Assert.AreEqual(resolution.y, solver.VelocityTexture.height, "Velocity texture height should match resolution");

            yield return null;
        }

        [UnityTest]
        [Description("Verifies that the velocity field changes after updates with a sink")]
        public IEnumerator Update_WithSink_ChangesVelocityField()
        {
            // Add a sink at the center to create flow
            Vector2[] sinkPositions = new Vector2[] { new Vector2(0.5f, 0.5f) };
            inputTexture = TestUtilities.CreateFieldTexture(resolution, sinkPositions);
            solver.UpdateInputTexture(inputTexture);

            // Get initial sample
            Vector2 initialSample = solver.SampleField(new Vector2(0.5f, 0.5f));

            // Run several simulation steps
            for (int i = 0; i < 10; i++)
            {
                solver.Update(DefaultTimeStep);
                yield return null;
            }

            // Get updated sample
            Vector2 updatedSample = solver.SampleField(new Vector2(0.5f, 0.5f));

            // The field should change due to forces and diffusion
            Assert.AreNotEqual(initialSample, updatedSample, "Velocity field should change after updates");
        }

        [UnityTest]
        [Description("Verifies that sampling the field with normalized coordinates returns valid vectors")]
        public IEnumerator SampleField_WithNormalizedCoordinates_ReturnsValidVectors()
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
        [Description("Verifies that sampling the field with world coordinates returns valid vectors")]
        public IEnumerator SampleField_WithWorldCoordinates_ReturnsValidVectors()
        {
            // Create world bounds
            Bounds worldBounds = new Bounds(Vector3.zero, new Vector3(10f, 0f, 10f));

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
        [Description("Verifies that updating parameters changes the simulation behavior")]
        public IEnumerator UpdateParameters_WithNewValues_ChangesSimulationBehavior()
        {
            // Add a sink and a source to create flow
            Vector2[] sinkPositions = new Vector2[] { new Vector2(0.25f, 0.5f) };
            Vector2[] sourcePositions = new Vector2[] { new Vector2(0.75f, 0.5f) };
            inputTexture = TestUtilities.CreateFieldTexture(resolution, sinkPositions, sourcePositions);
            solver.UpdateInputTexture(inputTexture);
            
            // Run a few simulation steps with default parameters
            for (int i = 0; i < 5; i++)
            {
                solver.Update(DefaultTimeStep);
                yield return null;
            }

            // Sample the field
            Vector2 initialSample = solver.SampleField(new Vector2(0.5f, 0.5f));

            // Update parameters
            solver.UpdateParameters(
                0.5f,  // higher viscosity
                20,    // more pressure iterations
                20,    // more diffusion iterations
                2.0f,  // stronger sink
                2.0f   // stronger source
            );

            // Run more simulation steps with new parameters
            for (int i = 0; i < 5; i++)
            {
                solver.Update(DefaultTimeStep);
                yield return null;
            }

            // Sample the field again
            Vector2 updatedSample = solver.SampleField(new Vector2(0.5f, 0.5f));

            // The behavior should be different with the new parameters
            Assert.AreNotEqual(initialSample, updatedSample, "Velocity field should change after parameter updates");
        }

        [UnityTest]
        [Description("Verifies that resizing changes the texture resolution")]
        public IEnumerator Resize_WithNewResolution_ChangesTextureResolution()
        {
            // Get initial resolution
            int initialWidth = solver.VelocityTexture.width;
            int initialHeight = solver.VelocityTexture.height;

            // Resize to a new resolution
            Vector2Int newResolution = new Vector2Int(128, 128);
            solver.Resize(newResolution);

            // Verify that the texture resolution changed
            Assert.AreEqual(newResolution.x, solver.VelocityTexture.width, "Velocity texture width should match new resolution");
            Assert.AreEqual(newResolution.y, solver.VelocityTexture.height, "Velocity texture height should match new resolution");

            yield return null;
        }
        
        [UnityTest]
        [Description("Verifies that the solver handles different time steps correctly")]
        public IEnumerator Update_WithDifferentTimeSteps_ScalesCorrectly()
        {
            // Add a sink to create flow
            Vector2[] sinkPositions = new Vector2[] { new Vector2(0.5f, 0.5f) };
            inputTexture = TestUtilities.CreateFieldTexture(resolution, sinkPositions);
            solver.UpdateInputTexture(inputTexture);
            
            // Run with a small time step
            for (int i = 0; i < 10; i++)
            {
                solver.Update(0.01f);
                yield return null;
            }
            
            Vector2 smallTimeStepSample = solver.SampleField(new Vector2(0.5f, 0.5f));
            
            // Reset the solver
            solver.Dispose();
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
            
            // Run with a larger time step (equivalent to 10 small steps)
            solver.Update(0.1f);
            yield return null;
            
            Vector2 largeTimeStepSample = solver.SampleField(new Vector2(0.5f, 0.5f));
            
            // The results won't be exactly the same due to non-linear behavior,
            // but they should be somewhat similar in magnitude
            float smallMagnitude = smallTimeStepSample.magnitude;
            float largeMagnitude = largeTimeStepSample.magnitude;
            
            // Check that the magnitudes are within a reasonable range of each other
            // This is a loose test since exact equivalence isn't expected
            Assert.IsTrue(
                largeMagnitude > 0.5f * smallMagnitude && largeMagnitude < 2.0f * smallMagnitude,
                $"Large time step magnitude ({largeMagnitude}) should be roughly proportional to small time step magnitude ({smallMagnitude})"
            );
        }
        
        [UnityTest]
        [Description("Verifies that the solver handles boundary conditions correctly")]
        public IEnumerator SampleField_AtBoundaries_ReturnsValidVectors()
        {
            // Sample at the boundaries
            Vector2 topLeft = solver.SampleField(new Vector2(0f, 0f));
            Vector2 topRight = solver.SampleField(new Vector2(1f, 0f));
            Vector2 bottomLeft = solver.SampleField(new Vector2(0f, 1f));
            Vector2 bottomRight = solver.SampleField(new Vector2(1f, 1f));
            
            // Verify that we get valid vectors (not NaN or infinity)
            Assert.IsFalse(float.IsNaN(topLeft.x) || float.IsNaN(topLeft.y), "Top-left vector should not contain NaN");
            Assert.IsFalse(float.IsNaN(topRight.x) || float.IsNaN(topRight.y), "Top-right vector should not contain NaN");
            Assert.IsFalse(float.IsNaN(bottomLeft.x) || float.IsNaN(bottomLeft.y), "Bottom-left vector should not contain NaN");
            Assert.IsFalse(float.IsNaN(bottomRight.x) || float.IsNaN(bottomRight.y), "Bottom-right vector should not contain NaN");
            
            Assert.IsFalse(float.IsInfinity(topLeft.x) || float.IsInfinity(topLeft.y), "Top-left vector should not contain infinity");
            Assert.IsFalse(float.IsInfinity(topRight.x) || float.IsInfinity(topRight.y), "Top-right vector should not contain infinity");
            Assert.IsFalse(float.IsInfinity(bottomLeft.x) || float.IsInfinity(bottomLeft.y), "Bottom-left vector should not contain infinity");
            Assert.IsFalse(float.IsInfinity(bottomRight.x) || float.IsInfinity(bottomRight.y), "Bottom-right vector should not contain infinity");
            
            yield return null;
        }
    }
}
