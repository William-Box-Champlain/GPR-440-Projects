using DependencyInjection;
using UnityServiceLocator;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GOAP
{
    public class GoapFactory : MonoBehaviour, IDependencyProvider
    {
        void Awake()
        {
            ServiceLocator.Global.Register(this);
        }
        [Provide] public GoapFactory ProvideFactory() => this;

        public IGoapPlanner CreatePlanner()
        {
            return new GoapPlanner();
        }
    }
}
