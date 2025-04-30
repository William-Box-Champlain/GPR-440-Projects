using UnityEngine;
using MipmapPathfinding;

/// <summary>
/// Debug utility for testing and diagnosing vector field issues
/// </summary>
public class VectorFieldDebugger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private VectorFieldStorage vectorFieldStorage;
    
    [Header("Debug Controls")]
    [SerializeField] private bool dumpAllCachesOnStart = true;
    [SerializeField] private bool sampleAtCurrentPosition = true;
    [SerializeField] private bool sampleAtCustomPosition = false;
    [SerializeField] private Vector3 customSamplePosition = Vector3.zero;
    [SerializeField] private bool drawDebugVectors = true;
    [SerializeField] private float debugVectorScale = 1.0f;
    [SerializeField] private Color debugVectorColor = Color.yellow;
    
    private Vector2 lastSampledVector = Vector2.zero;
    private Vector3 lastSampledPosition = Vector3.zero;
    
    private void Start()
    {
        if (vectorFieldStorage == null)
        {
            Debug.LogError("VectorFieldDebugger: VectorFieldStorage reference is missing!");
            return;
        }
        
        if (dumpAllCachesOnStart)
        {
            Debug.Log("VectorFieldDebugger: Dumping all caches on start...");
            vectorFieldStorage.ForceUpdateAndDumpAllCaches();
        }
    }
    
    private void Update()
    {
        if (vectorFieldStorage == null)
            return;
            
        if (sampleAtCurrentPosition)
        {
            SampleAtPosition(transform.position);
        }
        
        if (sampleAtCustomPosition)
        {
            SampleAtPosition(customSamplePosition);
        }
    }
    
    private void SampleAtPosition(Vector3 position)
    {
        lastSampledPosition = position;
        lastSampledVector = vectorFieldStorage.SampleVectorField(position);
        
        Debug.Log($"VectorFieldDebugger: Sampled at {position}, got vector: {lastSampledVector}");
    }
    
    private void OnDrawGizmos()
    {
        if (!drawDebugVectors || lastSampledVector.sqrMagnitude < 0.01f)
            return;
            
        Gizmos.color = debugVectorColor;
        
        // Draw a line representing the vector direction
        Vector3 start = lastSampledPosition;
        Vector3 direction = new Vector3(lastSampledVector.x, 0, lastSampledVector.y);
        Vector3 end = start + direction * debugVectorScale;
        
        Gizmos.DrawLine(start, end);
        
        // Draw an arrow head
        Vector3 right = Quaternion.Euler(0, 30, 0) * -direction * debugVectorScale * 0.3f;
        Vector3 left = Quaternion.Euler(0, -30, 0) * -direction * debugVectorScale * 0.3f;
        
        Gizmos.DrawLine(end, end + right);
        Gizmos.DrawLine(end, end + left);
    }
    
    /// <summary>
    /// Force a dump of all caches (can be called from the inspector)
    /// </summary>
    public void DumpAllCaches()
    {
        if (vectorFieldStorage != null)
        {
            vectorFieldStorage.ForceUpdateAndDumpAllCaches();
        }
    }
    
    /// <summary>
    /// Force a sample at the current position (can be called from the inspector)
    /// </summary>
    public void ForceSampleAtCurrentPosition()
    {
        if (vectorFieldStorage != null)
        {
            SampleAtPosition(transform.position);
        }
    }
    
    /// <summary>
    /// Force a sample at the custom position (can be called from the inspector)
    /// </summary>
    public void ForceSampleAtCustomPosition()
    {
        if (vectorFieldStorage != null)
        {
            SampleAtPosition(customSamplePosition);
        }
    }
}
