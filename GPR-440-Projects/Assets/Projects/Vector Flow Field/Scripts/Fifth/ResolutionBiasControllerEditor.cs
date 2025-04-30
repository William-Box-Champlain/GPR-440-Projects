#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace MipmapPathfinding.Editor
{
    [CustomEditor(typeof(ResolutionBiasController))]
    public class ResolutionBiasControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            ResolutionBiasController biasController = (ResolutionBiasController)target;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Detect Junctions"))
            {
                biasController.DetectJunctions();
            }
            
            if (GUILayout.Button("Regenerate Bias Texture"))
            {
                biasController.GenerateBiasTexture();
            }
            
            // Display the bias texture
            if (biasController.BiasTexture != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Bias Texture", EditorStyles.boldLabel);
                
                Rect rect = GUILayoutUtility.GetRect(256, 128);
                EditorGUI.DrawPreviewTexture(rect, biasController.BiasTexture);
            }
        }
    }
}
#endif