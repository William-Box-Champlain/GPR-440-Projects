using UnityEngine;
using UnityEditor;

namespace VFF.Editor
{
    /// <summary>
    /// Custom editor for the VectorFieldVisualizer component.
    /// </summary>
    [CustomEditor(typeof(VectorFieldVisualizer))]
    public class VectorFieldVisualizerEditor : UnityEditor.Editor
    {
        // SerializedProperties for the inspector
        private SerializedProperty visualizationEnabledProperty;
        private SerializedProperty updateIntervalProperty;
        private SerializedProperty colorFieldMaterialProperty;
        private SerializedProperty colorIntensityProperty;
        private SerializedProperty heightOffsetProperty;
        
        /// <summary>
        /// Called when the editor is enabled.
        /// </summary>
        private void OnEnable()
        {
            // Get serialized properties
            visualizationEnabledProperty = serializedObject.FindProperty("visualizationEnabled");
            updateIntervalProperty = serializedObject.FindProperty("updateInterval");
            colorFieldMaterialProperty = serializedObject.FindProperty("colorFieldMaterial");
            colorIntensityProperty = serializedObject.FindProperty("colorIntensity");
            heightOffsetProperty = serializedObject.FindProperty("heightOffset");
        }
        
        /// <summary>
        /// Draws the inspector GUI.
        /// </summary>
        public override void OnInspectorGUI()
        {
            VectorFieldVisualizer visualizer = (VectorFieldVisualizer)target;
            
            serializedObject.Update();
            
            // Visualization Settings section
            EditorGUILayout.LabelField("Visualization Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(visualizationEnabledProperty);
            EditorGUILayout.PropertyField(updateIntervalProperty);
            
            // Color Field Visualization section
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Color Field Visualization", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(colorFieldMaterialProperty);
            
            // Show a warning if the material doesn't use the correct shader
            if (colorFieldMaterialProperty.objectReferenceValue != null)
            {
                Material material = (Material)colorFieldMaterialProperty.objectReferenceValue;
                if (material.shader.name != "VFF/VectorFieldVisualization")
                {
                    EditorGUILayout.HelpBox("The material should use the 'VFF/VectorFieldVisualization' shader.", MessageType.Warning);
                }
            }
            
            EditorGUILayout.PropertyField(colorIntensityProperty);
            EditorGUILayout.PropertyField(heightOffsetProperty);
            
            serializedObject.ApplyModifiedProperties();
            
            // Add buttons for visualization controls
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Visualization Controls", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Toggle Visualization"))
            {
                visualizer.ToggleVisualization();
                EditorUtility.SetDirty(visualizer);
            }
            
            // Color intensity quick-set buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Low Intensity (0.5)"))
            {
                visualizer.SetColorIntensity(0.5f);
                colorIntensityProperty.floatValue = 0.5f;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(visualizer);
            }
            if (GUILayout.Button("Medium Intensity (1.0)"))
            {
                visualizer.SetColorIntensity(1.0f);
                colorIntensityProperty.floatValue = 1.0f;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(visualizer);
            }
            if (GUILayout.Button("High Intensity (1.5)"))
            {
                visualizer.SetColorIntensity(1.5f);
                colorIntensityProperty.floatValue = 1.5f;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(visualizer);
            }
            EditorGUILayout.EndHorizontal();
            
            // Information about the VectorFieldManager
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("VectorFieldManager", EditorStyles.boldLabel);
            
            if (VectorFieldManager.Instance != null)
            {
                EditorGUILayout.HelpBox("Using VectorFieldManager singleton instance.", MessageType.Info);
                
                // Show a button to select the VectorFieldManager in the hierarchy
                if (GUILayout.Button("Select VectorFieldManager"))
                {
                    Selection.activeGameObject = VectorFieldManager.Instance.gameObject;
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No VectorFieldManager instance found in the scene.", MessageType.Warning);
            }
        }
    }
}
