using UnityEngine;

namespace GOAP
{
    /// <summary>
    /// Example script showing how to add the debug UI to a GOAP agent.
    /// </summary>
    public class GoapAgentDebugUIExample : MonoBehaviour
    {
        [Tooltip("Reference to the GOAP agent that should display debug UI")]
        [SerializeField] private GoapAgent targetAgent;

        [Tooltip("Whether to automatically add debug UI on start")]
        [SerializeField] private bool addDebugUIOnStart = true;

        [Tooltip("Whether to customize the debug UI appearance")]
        [SerializeField] private bool customizeUI = false;

        [Header("Custom UI Settings (Optional)")]
        [SerializeField] private Vector2 uiPosition = new Vector2(20, 20);
        [SerializeField] private int maxBeliefsToShow = 10;
        [SerializeField] private Color trueBeliefColor = Color.green;
        [SerializeField] private Color falseBeliefColor = Color.red;
        
        [Header("Line Settings (Optional)")]
        [SerializeField] private Color lineColor = Color.yellow;
        [SerializeField] private float lineWidth = 2f;

        private void Start()
        {
            // If no agent is assigned, try to find one on this GameObject
            if (targetAgent == null)
            {
                targetAgent = GetComponent<GoapAgent>();
                
                // If still null, try to find one in the scene
                if (targetAgent == null)
                {
                    targetAgent = FindObjectOfType<GoapAgent>();
                    
                    if (targetAgent == null)
                    {
                        Debug.LogWarning("No GOAP agent found. Please assign one in the inspector.");
                        return;
                    }
                }
            }

            if (addDebugUIOnStart)
            {
                AddDebugUI();
            }
        }

        /// <summary>
        /// Adds the debug UI to the target agent.
        /// </summary>
        public void AddDebugUI()
        {
            if (targetAgent == null) return;

            // Use the extension method to add the debug UI
            GoapAgentDebugUI debugUI = targetAgent.AddDebugUI();

            // Customize the UI if needed
            if (customizeUI && debugUI != null)
            {
                // Access the public fields using reflection since they're serialized private
                var uiPositionField = typeof(GoapAgentDebugUI).GetField("uiPosition", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var maxBeliefsField = typeof(GoapAgentDebugUI).GetField("maxBeliefsToShow", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var trueBeliefsColorField = typeof(GoapAgentDebugUI).GetField("trueBeliefColor", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var falseBeliefsColorField = typeof(GoapAgentDebugUI).GetField("falseBeliefColor", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var lineColorField = typeof(GoapAgentDebugUI).GetField("lineColor", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var lineWidthField = typeof(GoapAgentDebugUI).GetField("lineWidth", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                // Apply custom settings if fields were found
                if (uiPositionField != null) uiPositionField.SetValue(debugUI, uiPosition);
                if (maxBeliefsField != null) maxBeliefsField.SetValue(debugUI, maxBeliefsToShow);
                if (trueBeliefsColorField != null) trueBeliefsColorField.SetValue(debugUI, trueBeliefColor);
                if (falseBeliefsColorField != null) falseBeliefsColorField.SetValue(debugUI, falseBeliefColor);
                if (lineColorField != null) lineColorField.SetValue(debugUI, lineColor);
                if (lineWidthField != null) lineWidthField.SetValue(debugUI, lineWidth);

                // Force update the line renderer to apply the new settings immediately
                var updateLineRendererMethod = typeof(GoapAgentDebugUI).GetMethod("UpdateLineRenderer", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (updateLineRendererMethod != null)
                {
                    updateLineRendererMethod.Invoke(debugUI, null);
                }

                Debug.Log("Customized GOAP Agent Debug UI");
            }
        }

        /// <summary>
        /// Example of how to add debug UI through code.
        /// </summary>
        public static void AddDebugUIToAllAgents()
        {
            // Find all GOAP agents in the scene
            GoapAgent[] allAgents = FindObjectsOfType<GoapAgent>();
            
            // Add debug UI to each one
            foreach (var agent in allAgents)
            {
                agent.AddDebugUI();
            }
            
            Debug.Log($"Added Debug UI to {allAgents.Length} GOAP agents");
        }
    }
}
