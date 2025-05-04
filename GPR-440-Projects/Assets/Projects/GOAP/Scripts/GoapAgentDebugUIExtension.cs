using UnityEngine;

namespace GOAP
{
    /// <summary>
    /// Extension methods for adding debug UI to GOAP agents.
    /// </summary>
    public static class GoapAgentDebugUIExtension
    {
        /// <summary>
        /// Adds a debug UI component to a GOAP agent if it doesn't already have one.
        /// The UI will display the agent's current action, beliefs, and goal, and
        /// will draw a line from the UI to the agent.
        /// </summary>
        /// <param name="agent">The GOAP agent to add the debug UI to.</param>
        /// <returns>The GoapAgentDebugUI component (either existing or newly added).</returns>
        public static GoapAgentDebugUI AddDebugUI(this GoapAgent agent)
        {
            // Check if the agent already has a debug UI component
            GoapAgentDebugUI debugUI = agent.GetComponent<GoapAgentDebugUI>();
            
            // If not, add one
            if (debugUI == null)
            {
                debugUI = agent.gameObject.AddComponent<GoapAgentDebugUI>();
                Debug.Log($"Added Debug UI to GOAP Agent on {agent.gameObject.name}");
            }
            
            return debugUI;
        }
    }
}
