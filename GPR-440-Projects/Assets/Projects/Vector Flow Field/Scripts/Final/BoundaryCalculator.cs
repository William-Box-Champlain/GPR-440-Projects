using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VectorFlowFieldPathfinding
{
    public class BoundaryCalculator : MonoBehaviour, iBoundaryCalculator
    {
        [SerializeField] ComputeShader boundaryShader;
        public void Calculate()
        {
            throw new System.NotImplementedException();
        }

        public Texture2D GetBoundaryTexture()
        {
            throw new System.NotImplementedException();
        }

        public void Initialize(VFFParameters parameters)
        {
            throw new System.NotImplementedException();
        }
    }
}