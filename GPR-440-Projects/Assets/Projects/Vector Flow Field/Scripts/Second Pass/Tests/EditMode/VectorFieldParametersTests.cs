using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VFF;

namespace VFF.Tests.EditMode
{
    /// <summary>
    /// Tests for the VectorFieldParameters class to ensure proper initialization and validation.
    /// </summary>
    [TestFixture]
    [Category("Parameters")]
    public class VectorFieldParametersTests
    {
        private VectorFieldParameters parameters;

        [SetUp]
        public void Setup()
        {
            parameters = ScriptableObject.CreateInstance<VectorFieldParameters>();
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(parameters);
        }

        [Test]
        [Description("Verifies that the default grid resolution is valid")]
        public void GridResolution_DefaultValue_IsValid()
        {
            Assert.GreaterOrEqual(parameters.GridResolution.x, 16, "Grid resolution x should be at least 16");
            Assert.GreaterOrEqual(parameters.GridResolution.y, 16, "Grid resolution y should be at least 16");
        }

        [Test]
        [Description("Verifies that the default viscosity is valid")]
        public void Viscosity_DefaultValue_IsValid()
        {
            Assert.GreaterOrEqual(parameters.Viscosity, 0.0001f, "Viscosity should be positive");
            Assert.LessOrEqual(parameters.Viscosity, 1.0f, "Viscosity should be at most 1.0");
        }

        [Test]
        [Description("Verifies that the default pressure iterations value is valid")]
        public void PressureIterations_DefaultValue_IsValid()
        {
            Assert.GreaterOrEqual(parameters.PressureIterations, 1, "Pressure iterations should be at least 1");
            Assert.LessOrEqual(parameters.PressureIterations, 50, "Pressure iterations should be at most 50");
        }

        [Test]
        [Description("Verifies that the default diffusion iterations value is valid")]
        public void DiffusionIterations_DefaultValue_IsValid()
        {
            Assert.GreaterOrEqual(parameters.DiffusionIterations, 1, "Diffusion iterations should be at least 1");
            Assert.LessOrEqual(parameters.DiffusionIterations, 50, "Diffusion iterations should be at most 50");
        }

        [Test]
        [Description("Verifies that the default time step multiplier is valid")]
        public void TimeStepMultiplier_DefaultValue_IsValid()
        {
            Assert.GreaterOrEqual(parameters.TimeStepMultiplier, 0.1f, "Time step multiplier should be at least 0.1");
            Assert.LessOrEqual(parameters.TimeStepMultiplier, 2.0f, "Time step multiplier should be at most 2.0");
        }

        [Test]
        [Description("Verifies that the default sink strength is valid")]
        public void SinkStrength_DefaultValue_IsValid()
        {
            Assert.GreaterOrEqual(parameters.SinkStrength, 0.1f, "Sink strength should be at least 0.1");
            Assert.LessOrEqual(parameters.SinkStrength, 10.0f, "Sink strength should be at most 10.0");
        }

        [Test]
        [Description("Verifies that the default source strength is valid")]
        public void SourceStrength_DefaultValue_IsValid()
        {
            Assert.GreaterOrEqual(parameters.SourceStrength, 0.1f, "Source strength should be at least 0.1");
            Assert.LessOrEqual(parameters.SourceStrength, 10.0f, "Source strength should be at most 10.0");
        }

        [Test]
        [Description("Verifies that UseFixedUpdate is true by default")]
        public void UseFixedUpdate_DefaultValue_IsValid()
        {
            Assert.IsTrue(parameters.UseFixedUpdate, "Use fixed update should be true by default");
        }

        [Test]
        [Description("Verifies that AutoUpdate is true by default")]
        public void AutoUpdate_DefaultValue_IsValid()
        {
            Assert.IsTrue(parameters.AutoUpdate, "Auto update should be true by default");
        }

        [Test]
        [Description("Verifies that OnValidate enforces minimum values")]
        public void OnValidate_EnforcesMinimumValues()
        {
            // Create parameters with invalid values
            VectorFieldParameters invalidParams = TestUtilities.CreateParameters(
                resolution: new Vector2Int(8, 8),
                viscosity: -0.1f,
                pressureIterations: 0,
                diffusionIterations: 0,
                timeStepMultiplier: 0.05f,
                sinkStrength: 0.05f,
                sourceStrength: 0.05f
            );
            
            // Manually invoke OnValidate
            TestUtilities.InvokePrivateMethod(invalidParams, "OnValidate");
            
            // Verify that values were clamped to minimum values
            Assert.GreaterOrEqual(invalidParams.GridResolution.x, 16, "Grid resolution x should be clamped to minimum");
            Assert.GreaterOrEqual(invalidParams.GridResolution.y, 16, "Grid resolution y should be clamped to minimum");
            Assert.GreaterOrEqual(invalidParams.Viscosity, 0.0001f, "Viscosity should be clamped to minimum");
            Assert.GreaterOrEqual(invalidParams.PressureIterations, 1, "Pressure iterations should be clamped to minimum");
            Assert.GreaterOrEqual(invalidParams.DiffusionIterations, 1, "Diffusion iterations should be clamped to minimum");
            Assert.GreaterOrEqual(invalidParams.TimeStepMultiplier, 0.1f, "Time step multiplier should be clamped to minimum");
            Assert.GreaterOrEqual(invalidParams.SinkStrength, 0.1f, "Sink strength should be clamped to minimum");
            Assert.GreaterOrEqual(invalidParams.SourceStrength, 0.1f, "Source strength should be clamped to minimum");
            
            // Clean up
            Object.DestroyImmediate(invalidParams);
        }

        [TestCase(32, 32, 0.2f, 10, 10, 1.0f, 2.0f, 2.0f, true, true)]
        [TestCase(64, 64, 0.5f, 20, 20, 1.5f, 5.0f, 5.0f, false, false)]
        [TestCase(128, 128, 0.8f, 30, 30, 2.0f, 8.0f, 8.0f, true, false)]
        [Description("Verifies that custom parameter values are correctly set and retrieved")]
        public void Parameters_CustomValues_AreSetCorrectly(
            int resX, int resY, float viscosity, int pressureIter, int diffusionIter, 
            float timeStep, float sinkStr, float sourceStr, bool useFixed, bool autoUpdate)
        {
            // Create parameters with custom values
            VectorFieldParameters customParams = TestUtilities.CreateParameters(
                resolution: new Vector2Int(resX, resY),
                viscosity: viscosity,
                pressureIterations: pressureIter,
                diffusionIterations: diffusionIter,
                timeStepMultiplier: timeStep,
                sinkStrength: sinkStr,
                sourceStrength: sourceStr,
                useFixedUpdate: useFixed,
                autoUpdate: autoUpdate
            );
            
            // Verify that values were set correctly
            Assert.AreEqual(resX, customParams.GridResolution.x, "Grid resolution x should match");
            Assert.AreEqual(resY, customParams.GridResolution.y, "Grid resolution y should match");
            Assert.AreEqual(viscosity, customParams.Viscosity, "Viscosity should match");
            Assert.AreEqual(pressureIter, customParams.PressureIterations, "Pressure iterations should match");
            Assert.AreEqual(diffusionIter, customParams.DiffusionIterations, "Diffusion iterations should match");
            Assert.AreEqual(timeStep, customParams.TimeStepMultiplier, "Time step multiplier should match");
            Assert.AreEqual(sinkStr, customParams.SinkStrength, "Sink strength should match");
            Assert.AreEqual(sourceStr, customParams.SourceStrength, "Source strength should match");
            Assert.AreEqual(useFixed, customParams.UseFixedUpdate, "Use fixed update should match");
            Assert.AreEqual(autoUpdate, customParams.AutoUpdate, "Auto update should match");
            
            // Clean up
            Object.DestroyImmediate(customParams);
        }
    }
}
