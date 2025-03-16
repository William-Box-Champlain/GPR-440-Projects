using UnityEngine;

public class FlowFieldAgent : MonoBehaviour
{
    // Movement speed of the agent.
    public float Speed = 3.0f;
    
    // Reference to the FlowFieldAccessor in the scene.
    private FlowFieldManager flowAccessor;
    private eAgentSize mSize;

    void Start()
    {
        // Locate the FlowFieldAccessor in the scene.
        flowAccessor = FindObjectOfType<FlowFieldManager>();
        if (flowAccessor == null)
        {
            Debug.LogError("FlowFieldAgent: No FlowFieldAccessor found in the scene.");
        }
    }

    void Update()
    {
        if (flowAccessor != null)
        {
            // Get the 2D flow direction based on the agent's current position.
            Vector2 flowDir;
            flowAccessor.GetFlowDirection(transform.position,out flowDir,mSize);
            // Convert the 2D flow direction to a 3D direction vector.
            Vector3 moveDir = new Vector3(flowDir.x, 0, flowDir.y);
            // Move the agent using a normalized direction, scaled by Speed and Time.deltaTime.
            transform.Translate(moveDir.normalized * Speed * Time.deltaTime, Space.World);
        }
    }
}
