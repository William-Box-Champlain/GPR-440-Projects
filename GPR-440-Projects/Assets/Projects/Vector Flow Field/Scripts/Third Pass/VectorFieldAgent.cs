using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VectorFieldAgent : MonoBehaviour
{
    VectorFieldManager VectorFieldManager;
    // Start is called before the first frame update
    void Start()
    {
        VectorFieldManager = VectorFieldManager.Instance;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 sampledVector;
        bool inField = VectorFieldManager.GetVelocityAtPosition(this.transform.position, out sampledVector);
        Debug.Log("InField: "+ inField + " Field Direction @" + this.transform.position + ": " + new Vector3(sampledVector.normalized.x, 0, sampledVector.normalized.y));
        Debug.DrawRay(this.transform.position, new Vector3(sampledVector.normalized.x,0,sampledVector.normalized.y), Color.red);
    }
}
