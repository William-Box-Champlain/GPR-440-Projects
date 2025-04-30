using sixth;
using UnityEngine;
using UnityEngine.UIElements;


namespace sixth{/// <summary>
/// Component for objects that will follow the vector field
/// </summary>
public class VFFAgent : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float directionSmoothTime = 0.5f;
    [SerializeField] private bool debugDirection = false;
    [SerializeField] private float rayScalar = 1.0f;
    [SerializeField] private float moveScalar = 1.0f;

    // Internal state
    private Vector2 currentDirection;
    private Vector2 targetDirection;
    private float currentFieldStrength;
    private Vector2 directionSmoothVelocity;

    // Optional component references
    private AgentManager agentManager;

    private void Start()
    {
        // Find the agent manager if not already assigned
        if (agentManager == null)
        {
            agentManager = FindObjectOfType<AgentManager>();
        }

        // Register with the agent manager
        if (agentManager != null)
        {
            agentManager.RegisterAgent(this);
        }
        else
        {
            Debug.LogWarning("No AgentManager found in the scene. This agent will not receive path directions.");
        }
    }

    private void OnDestroy()
    {
        // Unregister from agent manager
        if (agentManager != null)
        {
            agentManager.UnregisterAgent(this);
        }
    }

    /// <summary>
    /// Update the direction guidance for this agent
    /// </summary>
    public void UpdateDirection(Vector2 direction, float fieldStrength)
    {
        targetDirection = direction;
        currentFieldStrength = fieldStrength;
    }

    private void Update()
    {
            Debug.Log($"Gameobject {this.gameObject.GetInstanceID()} is targeting {targetDirection}");

        // Smooth the direction changes
        currentDirection = Vector2.SmoothDamp(
            currentDirection,
            targetDirection,
            ref directionSmoothVelocity,
            directionSmoothTime
        );

        this.transform.position += new Vector3(currentDirection.x,0,currentDirection.y).normalized * moveScalar * Time.deltaTime;
        
        // Draw debug direction gizmo
        if (debugDirection)
        {
            Debug.DrawRay(
                transform.position,
                new Vector3(currentDirection.x, 0, currentDirection.y) * rayScalar,
                Color.Lerp(Color.blue, Color.red, currentFieldStrength)
            );
        }

        // Note: This class doesn't move the agent - it just provides the direction
        // Your existing movement code should use currentDirection and currentFieldStrength 
        // to determine how to move the agent
    }

    /// <summary>
    /// Get the current guidance direction (normalized)
    /// </summary>
    public Vector2 GetDirection()
    {
        return currentDirection;
    }

    /// <summary>
    /// Get the current field strength (how strong the influence is)
    /// </summary>
    public float GetFieldStrength()
    {
        return currentFieldStrength;
    }

    /// <summary>
    /// Get the direction as a 3D vector (suitable for movement)
    /// </summary>
    public Vector3 GetDirection3D()
    {
        return new Vector3(currentDirection.x, 0, currentDirection.y);
    }
}}