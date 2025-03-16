using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

public class VectorFlowFieldManager : MonoBehaviour
{
    private VectorFlowField.VectorFlowFieldManager fieldManager;
    [Header("Compute Shaders")]
    [SerializeField] ComputeShader NavMeshAdapter;
    [SerializeField] ComputeShader SimulationCompute;
    [Header("Boundry GameObjects")]
    [SerializeField] GameObject MinBound;
    [SerializeField] GameObject MaxBound;
    [Header("Simulation Parameters")]
    [SerializeField] int Iterations;
    [SerializeField] Vector2Int Resolution;

    [SerializeField] float DefaultSinkStrength;
    [SerializeField] float DefaultSourceStrength;
    [SerializeField] float MaxInfluenceStrength;

    [SerializeField] float PressureScalar;
    [SerializeField] float ViscosityScalar;
    
    [Header("Sink and Source GameObjects")]
    List<GameObject> SinkObjects = new List<GameObject>();
    List<GameObject> SourceObjects = new List<GameObject>();

    // Start is called before the first frame update
    void Start()
    {
        Vector3 minPoint = MinBound.transform.position;
        Vector3 maxPoint = MaxBound.transform.position;

        Vector3 realMin = new Vector3(
            Mathf.Min(minPoint.x, maxPoint.x),
            Mathf.Min(minPoint.y, maxPoint.y),
            Mathf.Min(minPoint.z, maxPoint.z)
        );

        Vector3 realMax = new Vector3(
            Mathf.Max(minPoint.x, maxPoint.x),
            Mathf.Max(minPoint.y, maxPoint.y),
            Mathf.Max(minPoint.z, maxPoint.z)
        );

        Vector3 center = (realMin + realMax) * 0.5f;
        Vector3 size = realMax - realMin;

        Bounds tempBound = new Bounds(center,size);

        VectorFlowField.SimulationParameters parameters = new VectorFlowField.SimulationParameters
                                 .Builder()
                                 .WithAdapter(this.NavMeshAdapter)
                                 .WithBounds(tempBound)
                                 .WithIterations(this.Iterations)
                                 .WithMaxInfluenceStrength(this.MaxInfluenceStrength)
                                 .WithPressure(this.PressureScalar)
                                 .WithResolution(this.Resolution)
                                 .WithSimulation(this.SimulationCompute)
                                 .WithSinkStrength(this.DefaultSinkStrength)
                                 .WithSourceStrength(this.DefaultSourceStrength)
                                 .WithViscosity(this.ViscosityScalar)
                                 .Build();
        fieldManager = VectorFlowField.VectorFlowFieldManager.GetInstance();
        fieldManager.Initialize(parameters);
        
        foreach(var obj in SinkObjects)
        {
            fieldManager.influenceManager.TryAddInfluence(obj, VectorFlowField.InfluenceType.Sink, DefaultSinkStrength);
        }
        foreach(var obj in SourceObjects)
        {
            fieldManager.influenceManager.TryAddInfluence(obj,VectorFlowField.InfluenceType.Source, DefaultSourceStrength);
        }

        fieldManager.navMeshAdapter.GenerateBoundaryTexture();
    }

    // Update is called once per frame
    void Update()
    {
        fieldManager.Update(Time.deltaTime);
    }
}
