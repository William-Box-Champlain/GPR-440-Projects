using UnityEngine;
using UnityEditor;

namespace MipmapPathfinding
{
#if UNITY_EDITOR
    [CustomEditor(typeof(VectorFieldStorage))]
    public class VectorFieldStorageInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Just draw the default inspector for now
            DrawDefaultInspector();

            VectorFieldStorage storage = (VectorFieldStorage)target;

            EditorGUILayout.Space();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Runtime data will be available during play mode.", MessageType.Info);
                return;
            }

            // Simple memory display
            EditorGUILayout.LabelField("Memory Usage", EditorStyles.boldLabel);
            float memoryUsage = storage.GetMemoryUsageMB();
            EditorGUILayout.LabelField($"Total Memory: {memoryUsage:F2} MB");

            // Simple debug buttons
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Mark All Caches Dirty"))
            {
                storage.MarkAllCachesDirty();
            }

            if (GUILayout.Button("Update Base Level Cache"))
            {
                storage.UpdateCPUCacheImmediate(0);
            }
        }
    }
#endif
}