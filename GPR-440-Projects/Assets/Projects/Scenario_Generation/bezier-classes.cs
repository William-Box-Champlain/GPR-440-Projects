using UnityEngine;

[System.Serializable]
public class BezierCurve
{
    private Vector3[] mPoints;

    public Vector3[] Points 
    { 
        get => mPoints; 
        set => mPoints = value; 
    }

    public Vector3 StartPosition 
    { 
        get => mPoints[0]; 
    }

    public Vector3 EndPosition 
    { 
        get => mPoints[3]; 
    }

    public BezierCurve()
    {
        mPoints = new Vector3[4];
    }

    public BezierCurve(Vector3[] points)
    {
        mPoints = points;
    }

    public Vector3 GetSegment(float time)
    {
        time = Mathf.Clamp01(time);
        float inverseTime = 1 - time;
        
        return (inverseTime * inverseTime * inverseTime * mPoints[0])
            + (3 * inverseTime * inverseTime * time * mPoints[1])
            + (3 * inverseTime * time * time * mPoints[2])
            + (time * time * time * mPoints[3]);
    }

    /// <summary>
    /// Generates points along the bezier curve
    /// </summary>
    /// <param name="subdivisions">Number of segments to generate</param>
    /// <returns>Array of positions along the curve</returns>
    public Vector3[] GetSegments(int subdivisions)
    {
        Vector3[] segments = new Vector3[subdivisions];
        float time;
        
        for (int i = 0; i < subdivisions; i++)
        {
            time = (float)i / subdivisions;
            segments[i] = GetSegment(time);
        }

        return segments;
    }
}

[RequireComponent(typeof(LineRenderer))]
public class LineRendererSmoother : MonoBehaviour
{
    private LineRenderer mLine;
    
    [SerializeField]
    private Vector3[] mInitialState = new Vector3[1];
    
    [SerializeField]
    private float mSmoothingLength = 2f;
    
    [SerializeField]
    private int mSmoothingSections = 10;

    public LineRenderer Line 
    { 
        get => mLine; 
        set => mLine = value; 
    }
    
    public Vector3[] InitialState 
    { 
        get => mInitialState; 
        set => mInitialState = value; 
    }
    
    public float SmoothingLength 
    { 
        get => mSmoothingLength; 
        set => mSmoothingLength = value; 
    }
    
    public int SmoothingSections 
    { 
        get => mSmoothingSections; 
        set => mSmoothingSections = value; 
    }
}