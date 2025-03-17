using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VFF;

namespace VFF.Tests
{
    /// <summary>
    /// Provides utility methods and helpers for Vector Flow Field tests.
    /// </summary>
    public static class TestUtilities
    {
        /// <summary>
        /// Creates a test texture with the specified resolution and color.
        /// </summary>
        /// <param name="resolution">The resolution of the texture.</param>
        /// <param name="color">The color to fill the texture with.</param>
        /// <returns>A new texture with the specified resolution and color.</returns>
        public static Texture2D CreateTestTexture(Vector2Int resolution, Color color)
        {
            Texture2D texture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);
            Color[] colors = new Color[resolution.x * resolution.y];
            
            for (int i = 0; i < colors.Length; i++)
                colors[i] = color;
                
            texture.SetPixels(colors);
            texture.Apply();
            
            return texture;
        }
        
        /// <summary>
        /// Creates a test texture with field space, sinks, and sources.
        /// </summary>
        /// <param name="resolution">The resolution of the texture.</param>
        /// <param name="sinkPositions">Array of normalized sink positions.</param>
        /// <param name="sourcePositions">Array of normalized source positions.</param>
        /// <param name="radius">Radius of sinks and sources in normalized coordinates.</param>
        /// <returns>A new texture with field space, sinks, and sources.</returns>
        public static Texture2D CreateFieldTexture(
            Vector2Int resolution, 
            Vector2[] sinkPositions = null, 
            Vector2[] sourcePositions = null, 
            float radius = 0.05f)
        {
            // Create a field texture generator
            FieldTextureGenerator generator = new FieldTextureGenerator(resolution);
            
            // Set the entire texture as field space
            generator.SetFullField();
            
            // Add sinks
            if (sinkPositions != null)
            {
                foreach (Vector2 position in sinkPositions)
                {
                    generator.AddSink(position, radius);
                }
            }
            
            // Add sources
            if (sourcePositions != null)
            {
                foreach (Vector2 position in sourcePositions)
                {
                    generator.AddSource(position, radius);
                }
            }
            
            return generator.FieldTexture;
        }
        
        /// <summary>
        /// Loads a compute shader from Resources or creates a mock if not found.
        /// </summary>
        /// <param name="shaderName">The name of the compute shader.</param>
        /// <returns>The loaded compute shader or a mock.</returns>
        public static ComputeShader LoadComputeShader(string shaderName)
        {
            ComputeShader shader = Resources.Load<ComputeShader>(shaderName);
            
            if (shader == null)
            {
                Debug.LogWarning($"{shaderName} shader not found in Resources folder. Using a mock shader for testing.");
            }
            
            return shader;
        }
        
        /// <summary>
        /// Creates a VectorFieldParameters instance with the specified values.
        /// </summary>
        /// <param name="resolution">Grid resolution.</param>
        /// <param name="viscosity">Fluid viscosity.</param>
        /// <param name="pressureIterations">Number of pressure solver iterations.</param>
        /// <param name="diffusionIterations">Number of diffusion solver iterations.</param>
        /// <param name="timeStepMultiplier">Time step multiplier.</param>
        /// <param name="sinkStrength">Strength of sink forces.</param>
        /// <param name="sourceStrength">Strength of source forces.</param>
        /// <param name="useFixedUpdate">Whether to use FixedUpdate.</param>
        /// <param name="autoUpdate">Whether to automatically update.</param>
        /// <returns>A new VectorFieldParameters instance.</returns>
        public static VectorFieldParameters CreateParameters(
            Vector2Int? resolution = null,
            float? viscosity = null,
            int? pressureIterations = null,
            int? diffusionIterations = null,
            float? timeStepMultiplier = null,
            float? sinkStrength = null,
            float? sourceStrength = null,
            bool? useFixedUpdate = null,
            bool? autoUpdate = null)
        {
            VectorFieldParameters parameters = ScriptableObject.CreateInstance<VectorFieldParameters>();
            
            // Use reflection to set private fields
            Type type = typeof(VectorFieldParameters);
            
            if (resolution.HasValue)
                SetPrivateField(parameters, "gridResolution", resolution.Value);
                
            if (viscosity.HasValue)
                SetPrivateField(parameters, "viscosity", viscosity.Value);
                
            if (pressureIterations.HasValue)
                SetPrivateField(parameters, "pressureIterations", pressureIterations.Value);
                
            if (diffusionIterations.HasValue)
                SetPrivateField(parameters, "diffusionIterations", diffusionIterations.Value);
                
            if (timeStepMultiplier.HasValue)
                SetPrivateField(parameters, "timeStepMultiplier", timeStepMultiplier.Value);
                
            if (sinkStrength.HasValue)
                SetPrivateField(parameters, "sinkStrength", sinkStrength.Value);
                
            if (sourceStrength.HasValue)
                SetPrivateField(parameters, "sourceStrength", sourceStrength.Value);
                
            if (useFixedUpdate.HasValue)
                SetPrivateField(parameters, "useFixedUpdate", useFixedUpdate.Value);
                
            if (autoUpdate.HasValue)
                SetPrivateField(parameters, "autoUpdate", autoUpdate.Value);
            
            return parameters;
        }
        
        /// <summary>
        /// Sets a private field value using reflection.
        /// </summary>
        /// <param name="obj">The object to set the field on.</param>
        /// <param name="fieldName">The name of the field.</param>
        /// <param name="value">The value to set.</param>
        public static void SetPrivateField(object obj, string fieldName, object value)
        {
            FieldInfo field = obj.GetType().GetField(
                fieldName, 
                BindingFlags.NonPublic | BindingFlags.Instance);
                
            if (field != null)
                field.SetValue(obj, value);
            else
                Debug.LogError($"Field '{fieldName}' not found on {obj.GetType().Name}");
        }
        
        /// <summary>
        /// Gets a private field value using reflection.
        /// </summary>
        /// <typeparam name="T">The type of the field.</typeparam>
        /// <param name="obj">The object to get the field from.</param>
        /// <param name="fieldName">The name of the field.</param>
        /// <returns>The value of the field.</returns>
        public static T GetPrivateField<T>(object obj, string fieldName)
        {
            FieldInfo field = obj.GetType().GetField(
                fieldName, 
                BindingFlags.NonPublic | BindingFlags.Instance);
                
            if (field != null)
                return (T)field.GetValue(obj);
                
            Debug.LogError($"Field '{fieldName}' not found on {obj.GetType().Name}");
            return default;
        }
        
        /// <summary>
        /// Invokes a private method using reflection.
        /// </summary>
        /// <param name="obj">The object to invoke the method on.</param>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="parameters">The parameters to pass to the method.</param>
        /// <returns>The result of the method invocation.</returns>
        public static object InvokePrivateMethod(object obj, string methodName, params object[] parameters)
        {
            MethodInfo method = obj.GetType().GetMethod(
                methodName, 
                BindingFlags.NonPublic | BindingFlags.Instance);
                
            if (method != null)
                return method.Invoke(obj, parameters);
                
            Debug.LogError($"Method '{methodName}' not found on {obj.GetType().Name}");
            return null;
        }
        
        /// <summary>
        /// Asserts that two Vector2 values are approximately equal.
        /// </summary>
        /// <param name="expected">The expected Vector2.</param>
        /// <param name="actual">The actual Vector2.</param>
        /// <param name="tolerance">The tolerance for the comparison.</param>
        /// <param name="message">The message to display if the assertion fails.</param>
        public static void AreApproximatelyEqual(Vector2 expected, Vector2 actual, float tolerance = 0.001f, string message = null)
        {
            bool xEqual = Mathf.Abs(expected.x - actual.x) <= tolerance;
            bool yEqual = Mathf.Abs(expected.y - actual.y) <= tolerance;
            
            if (!xEqual || !yEqual)
            {
                string errorMessage = message ?? $"Expected: {expected}, Actual: {actual}, Tolerance: {tolerance}";
                Assert.Fail(errorMessage);
            }
        }
        
        /// <summary>
        /// Asserts that two Vector3 values are approximately equal.
        /// </summary>
        /// <param name="expected">The expected Vector3.</param>
        /// <param name="actual">The actual Vector3.</param>
        /// <param name="tolerance">The tolerance for the comparison.</param>
        /// <param name="message">The message to display if the assertion fails.</param>
        public static void AreApproximatelyEqual(Vector3 expected, Vector3 actual, float tolerance = 0.001f, string message = null)
        {
            bool xEqual = Mathf.Abs(expected.x - actual.x) <= tolerance;
            bool yEqual = Mathf.Abs(expected.y - actual.y) <= tolerance;
            bool zEqual = Mathf.Abs(expected.z - actual.z) <= tolerance;
            
            if (!xEqual || !yEqual || !zEqual)
            {
                string errorMessage = message ?? $"Expected: {expected}, Actual: {actual}, Tolerance: {tolerance}";
                Assert.Fail(errorMessage);
            }
        }
    }
}
