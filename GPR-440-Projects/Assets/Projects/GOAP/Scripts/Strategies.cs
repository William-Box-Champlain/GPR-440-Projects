using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

namespace GOAP
{
    public interface IActionStrategy
    {
        bool IsValid { get; }
        bool IsComplete { get; }

        void Start()
        {
            //noop
        }
        void Update(float dt)
        {
            //noop
        }
        void Stop()
        {
            //noop
        }
    }

    #region ExampleStrategies
    
    #endregion
}