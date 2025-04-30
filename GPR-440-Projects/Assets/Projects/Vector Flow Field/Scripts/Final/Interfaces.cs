using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VectorFlowFieldPathfinding
{
    public enum eInfluenceType
    {
        Sink,
        Source,
    }

    public interface iFlowFieldCalculator
    {
        public void Initialize(VFFParameters parameters);
        void UpdateSimulation(float deltaTime);
        Vector2 SampleField(Vector3 worldPosition);
    }

    public interface iBoundaryCalculator
    {
        public void Initialize(VFFParameters parameters);
        public void Calculate();
        public Texture2D GetBoundaryTexture();
    }

    public interface iFlowFieldAgent
    {
        public FlowFieldPathfindingManager manager { get; protected set; }
        public GameObject GameObject { get; protected set; }
        protected Vector3 movementDirection { get; set; }
        public void Initialize()
        {
            FlowFieldPathfindingManager.OnRegister += RegisterToManager;
        }
        public void Shutdown()
        {
            FlowFieldPathfindingManager.OnRegister -= RegisterToManager;
        }

        private void RegisterToManager(FlowFieldPathfindingManager manager)
        {
            manager.RegisterAgent(this);
            this.manager = manager;
        }

        public void UpdateDirection(Vector3 direction)
        {
            this.movementDirection = direction;
        }

        public void Update(float deltaTime)
        {
            //noop
        }
    }

    public interface iFlowFieldInfluence
    {
        public eInfluenceType type { get; protected set; }
        public GameObject GameObject{ get; protected set; }
        public void Initialize()
        {
            FlowFieldPathfindingManager.OnRegister += RegisterToManager;
            DeactivateInfluence();
        }
        public void Shutdown()
        {
            FlowFieldPathfindingManager.OnRegister -= RegisterToManager;
        }

        public void RegisterToManager(FlowFieldPathfindingManager manager)
        {
            manager.RegisterInfluence(this);
        }

        public void ActivateInfluence();
        public void DeactivateInfluence();
    }
}