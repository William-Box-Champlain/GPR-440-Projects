using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VFF;

namespace VFF.Tests.PlayMode
{
    /// <summary>
    /// Integration tests for the Vector Flow Field system to ensure components work together correctly.
    /// </summary>
    [TestFixture]
    [Category("Integration")]
    [Timeout(10000)] // 10 second timeout for all tests
    public class IntegrationTests
    {
        private GameObject managerObject;
        private VectorFieldManager manager;
        private GameObject agentObject;
        private PathfindingAgent agent;
        private const float DefaultTimeStep = 0.016f; // 60 FPS

        [UnitySetUp]
        public IEnumerator Setup()
        {
            // Create a game object with the VectorFieldManager component
            managerObject = new GameObject("VectorFieldManager");
            manager = managerObject.AddComponent<VectorFieldManager>();

            // Create parameters
            VectorFieldParameters parameters = TestUtilities.CreateParameters();
            
            // Load compute shaders using our utility
            ComputeShader navierStokesComputeShader = TestUtilities.LoadComputeShader("NavierStokesCompute");
            ComputeShader meshProcessorComputeShader = TestUtilities.LoadComputeShader("MeshProcessor");

            // Set up the manager using our utility methods
            TestUtilities.SetPrivateField(manager, "parameters", parameters);
            TestUtilities.SetPrivateField(manager, "navierStokesComputeShader", navierStokesComputeShader);
            TestUtilities.SetPrivateField(manager, "meshProcessorComputeShader", meshProcessorComputeShader);

            // Call Awake manually
            TestUtilities.InvokePrivateMethod(manager, "Awake");

            // Create a game object with the PathfindingAgent component
            agentObject = new GameObject("PathfindingAgent");
            agent = agentObject.AddComponent<PathfindingAgent>();
            
            // Add a Rigidbody component (required by PathfindingAgent)
            Rigidbody rb = agentObject.AddComponent<Rigidbody>();
            rb.useGravity = false; // Disable gravity for testing

            // Register the agent with the manager
            manager.RegisterAgent(agent);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(agentObject);
            yield return null;
        }

        [UnityTest]
        [Description("Verifies that the complete simulation pipeline runs without errors")]
        public IEnumerator CompleteSimulationPipeline_WithSinkAndSource_RunsWithoutErrors()
        {
            // Set up a field with a sink and a source
            manager.SetFieldRect(Vector3.zero, new Vector3(10f, 0f, 10f));
            manager.AddSink(new Vector3(-5f, 0f, 0f), 0.1f);
            manager.AddSource(new Vector3(5f, 0f, 0f), 0.1f);

            // Run several simulation steps
            for (int i = 0; i < 10; i++)
            {
                manager.UpdateSimulation(DefaultTimeStep);
                yield return null;
            }

            // Verify that the simulation ran without errors
            Assert.IsNotNull(manager.VelocityTexture, "Velocity texture should not be null after simulation");
            Assert.IsTrue(manager.VelocityTexture.width > 0, "Velocity texture width should be positive");
            Assert.IsTrue(manager.VelocityTexture.height > 0, "Velocity texture height should be positive");
        }

        [UnityTest]
        [Description("Verifies that agents move according to the vector field")]
        public IEnumerator AgentMovement_WithSink_FollowsVectorField()
        {
            // Set up a field with a sink
            manager.SetFieldRect(Vector3.zero, new Vector3(10f, 0f, 10f));
            manager.AddSink(new Vector3(-5f, 0f, 0f), 0.1f);

            // Position the agent
            agentObject.transform.position = new Vector3(5f, 0f, 0f);
            Vector3 initialPosition = agentObject.transform.position;

            // Run several simulation and agent update steps
            for (int i = 0; i < 10; i++)
            {
                manager.UpdateSimulation(DefaultTimeStep);
                
                // Manually update the agent
                TestUtilities.InvokePrivateMethod(agent, "FixedUpdate");
                
                yield return null;
            }

            // Verify that the agent moved
            Vector3 finalPosition = agentObject.transform.position;
            Assert.AreNotEqual(initialPosition, finalPosition, "Agent should move when following the vector field");
            
            // Verify that the agent moved in the general direction of the sink
            Vector3 directionToSink = new Vector3(-5f, 0f, 0f) - initialPosition;
            directionToSink.Normalize();
            
            Vector3 movementDirection = finalPosition - initialPosition;
            if (movementDirection.magnitude > 0.01f) // Only check direction if it moved significantly
            {
                movementDirection.Normalize();
                
                // Calculate dot product to check if vectors are pointing in roughly the same direction
                float dotProduct = Vector3.Dot(directionToSink, movementDirection);
                Assert.IsTrue(dotProduct > 0, 
                    $"Agent should move toward the sink. Direction to sink: {directionToSink}, Movement direction: {movementDirection}, Dot product: {dotProduct}");
            }
        }

        [UnityTest]
        [Description("Measures performance with different grid resolutions")]
        [Category("Performance")]
        public IEnumerator PerformanceTest_WithDifferentResolutions_LogsResults()
        {
            // Test with different resolutions
            Vector2Int[] resolutions = new Vector2Int[]
            {
                new Vector2Int(32, 32),
                new Vector2Int(64, 64),
                new Vector2Int(128, 128)
            };

            foreach (Vector2Int resolution in resolutions)
            {
                // Update the parameters
                VectorFieldParameters parameters = TestUtilities.GetPrivateField<VectorFieldParameters>(manager, "parameters");
                
                // Create new parameters with the desired resolution
                VectorFieldParameters newParameters = TestUtilities.CreateParameters(resolution: resolution);
                
                // Set the parameters
                TestUtilities.SetPrivateField(manager, "parameters", newParameters);

                // Reinitialize the manager
                TestUtilities.InvokePrivateMethod(manager, "Awake");

                // Set up a field with a sink and a source
                manager.SetFieldRect(Vector3.zero, new Vector3(10f, 0f, 10f));
                manager.AddSink(new Vector3(-5f, 0f, 0f), 0.1f);
                manager.AddSource(new Vector3(5f, 0f, 0f), 0.1f);

                // Measure the time to run 10 simulation steps
                float startTime = Time.realtimeSinceStartup;
                
                for (int i = 0; i < 10; i++)
                {
                    manager.UpdateSimulation(DefaultTimeStep);
                    yield return null;
                }
                
                float endTime = Time.realtimeSinceStartup;
                float elapsedTime = endTime - startTime;
                
                Debug.Log($"Resolution {resolution}: {elapsedTime:F4} seconds for 10 steps");
                
                // We don't assert anything here, just log the performance
                // In a real test, you might want to assert that performance is within acceptable bounds
                // Assert.Less(elapsedTime, maxAllowedTime, $"Performance at resolution {resolution} is too slow");
            }
        }

        [UnityTest]
        [Description("Verifies that world space sampling matches normalized sampling")]
        public IEnumerator WorldSpaceSampling_WithNormalizedEquivalent_MatchesResults()
        {
            // Set up a field
            manager.SetFieldRect(Vector3.zero, new Vector3(10f, 0f, 10f));
            
            // Add a sink and source to create a non-uniform field
            manager.AddSink(new Vector3(-3f, 0f, 0f), 0.1f);
            manager.AddSource(new Vector3(3f, 0f, 0f), 0.1f);
            
            // Run a few simulation steps
            for (int i = 0; i < 5; i++)
            {
                manager.UpdateSimulation(DefaultTimeStep);
                yield return null;
            }
            
            // Test multiple positions
            Vector2[] testPositions = new Vector2[]
            {
                new Vector2(0.25f, 0.25f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.75f, 0.75f)
            };
            
            foreach (Vector2 normalizedPosition in testPositions)
            {
                // Sample at a normalized position
                Vector3 normalizedSample = manager.SampleField(normalizedPosition);
                
                // Sample at the equivalent world position
                Vector3 worldPosition = manager.NormalizedToWorldPosition(normalizedPosition);
                
                // Get the world bounds
                Bounds worldBounds = TestUtilities.GetPrivateField<Bounds>(manager, "worldBounds");
                
                // Get the NavierStokesSolver
                NavierStokesSolver solver = TestUtilities.GetPrivateField<NavierStokesSolver>(manager, "navierStokesSolver");
                
                // Sample using the world-space method
                Vector2 worldSample2D = solver.SampleField(worldPosition, worldBounds);
                Vector3 worldSample = new Vector3(worldSample2D.x, 0, worldSample2D.y);
                
                // The samples should be approximately the same
                TestUtilities.AreApproximatelyEqual(normalizedSample, worldSample, 0.01f, 
                    $"Sampling mismatch at position {normalizedPosition}. Normalized: {normalizedSample}, World: {worldSample}");
            }
        }
        
        [UnityTest]
        [Description("Verifies that clearing sinks and sources works correctly")]
        public IEnumerator ClearSinksAndSources_AfterAdding_RemovesAll()
        {
            // Set up a field
            manager.SetFieldRect(Vector3.zero, new Vector3(10f, 0f, 10f));
            
            // Add multiple sinks and sources
            manager.AddSink(new Vector3(-3f, 0f, 0f), 0.1f);
            manager.AddSink(new Vector3(-2f, 0f, 2f), 0.1f);
            manager.AddSource(new Vector3(3f, 0f, 0f), 0.1f);
            manager.AddSource(new Vector3(2f, 0f, -2f), 0.1f);
            
            // Run a simulation step to ensure the field is updated
            manager.UpdateSimulation(DefaultTimeStep);
            yield return null;
            
            // Sample at a position that should be affected by the sinks and sources
            Vector2 testPosition = new Vector2(0.5f, 0.5f);
            Vector3 initialSample = manager.SampleField(testPosition);
            
            // Clear sinks and sources
            manager.ClearSinksAndSources();
            
            // Run a simulation step to ensure the field is updated
            manager.UpdateSimulation(DefaultTimeStep);
            yield return null;
            
            // Sample at the same position
            Vector3 finalSample = manager.SampleField(testPosition);
            
            // The field should change after clearing sinks and sources
            // The velocity should decay toward zero without any forces
            Assert.AreNotEqual(initialSample, finalSample, 
                "Field should change after clearing sinks and sources");
                
            // Run several more steps to let the field stabilize
            for (int i = 0; i < 20; i++)
            {
                manager.UpdateSimulation(DefaultTimeStep);
                yield return null;
            }
            
            // Sample again
            Vector3 stabilizedSample = manager.SampleField(testPosition);
            
            // The field should be close to zero after stabilizing
            Assert.IsTrue(stabilizedSample.magnitude < 0.1f, 
                $"Field should be close to zero after stabilizing, but magnitude is {stabilizedSample.magnitude}");
        }
    }
}
