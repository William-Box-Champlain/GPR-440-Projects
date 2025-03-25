using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace fourth
{ 
public class VFFInfluenceObject : MonoBehaviour
{
    [SerializeField] bool IsActive;
    [SerializeField] float Strength;
    [SerializeField] eInfluenceType Type;

    private FlowFieldInfluence me;

    private void Awake()
    {
        me = new FlowFieldInfluence();
        me.active = IsActive;
        me.position = this.transform.position;
        me.strength = Strength;
        me.type = Type;
    }

    private void Start()
    {
        var manager = FindObjectOfType<VectorFlowFieldManager>();
        manager.AddInfluence(me);
    }

    public void ToggleIsActive()
    {
        IsActive = !IsActive;
    }
}
}