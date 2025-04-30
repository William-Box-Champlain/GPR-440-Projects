using fourth;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Agent : MonoBehaviour
{
    VectorFlowFieldManager manager;
    // Start is called before the first frame update
    void Start()
    {
        manager = FindAnyObjectByType<VectorFlowFieldManager>();   
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log($"{this.gameObject.GetInstanceID()} is sampling the field at {manager?.GetUVOfPosition(this.transform.position)}, and got velocity {manager?.SampleFlowField(this.transform.position)}");
    }
}
