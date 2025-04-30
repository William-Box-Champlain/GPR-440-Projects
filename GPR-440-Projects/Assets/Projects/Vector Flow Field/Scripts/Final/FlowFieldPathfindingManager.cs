using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace VectorFlowFieldPathfinding
{
    public class FlowFieldPathfindingManager : MonoBehaviour
    {
        [SerializeField] private iBoundaryCalculator boundaryCalculator;
        [SerializeField] private iFlowFieldCalculator flowFieldCalculator;

        private List<iFlowFieldInfluence> influences = new List<iFlowFieldInfluence>();
        private List<iFlowFieldAgent> agents = new List<iFlowFieldAgent>();

        public static event Action<FlowFieldPathfindingManager> OnRegister;

        private VFFParameters parameters = new VFFParameters();

        
        #region Registration functions
        /// <summary>
        /// Adds influence to list of influences on the map.
        /// </summary>
        /// <param name="influence">Influence to add.</param>
        public void RegisterInfluence(iFlowFieldInfluence influence)
        {
            if(!influences.Contains(influence)) influences.Add(influence);
        }

        /// <summary>
        /// Adds agent to list of agents on the map.
        /// </summary>
        /// <param name="agent">Agent to add.</param>
        public void RegisterAgent(iFlowFieldAgent agent)
        {
            if(!agents.Contains(agent)) agents.Add(agent);
        }
        #endregion
        #region Unity functions
        private void OnEnable()
        {
            OnRegister.Invoke(this);
        }
        // Start is called before the first frame update
        void Start()
        {
            boundaryCalculator.Initialize(parameters);
            flowFieldCalculator.Initialize(parameters);
        }

        // Update is called once per frame
        void Update()
        {
            //flowFieldCalculator.UpdateSimulation(Time.deltaTime);
            //foreach (var agent in agents)
            //{
            //    Vector3 direction = flowFieldCalculator.SampleField(agent.GameObject.transform.position);
            //    if(direction == Vector3.zero) //use navmesh for direction.
            //    agent.UpdateDirection(direction);
            //}
            flowFieldCalculator.UpdateSimulation(Time.deltaTime);
        }

        ~FlowFieldPathfindingManager()
        {

        }
        #endregion
        /// <summary>
        /// This function provides a way to safely transition between using the VFF and traditional nav-mesh solution.
        /// When a position and agent are passed into this function, if the VFF has valid data, the function will return
        /// a unit-vector pointing in the direction of travel the agent should follow. Otherwise, it sets the Nav-Mesh agent's 
        /// destination to be the closest Sink.
        /// </summary>
        /// <param name="worldPosition">The worldspace position of the agent</param>
        /// <param name="agent">The agent looking for velocity data</param>
        /// <returns>a vector that points in the direction the agent must travel to reach a sink</returns>
        public Vector3 SafeSampleField(Vector3 worldPosition, ref NavMeshAgent agent)
        {
            //Best case, samplefield works and you get a vector, early return
            Vector3 output = flowFieldCalculator.SampleField(worldPosition);
            if (output != Vector3.zero)
            {
                if (agent != null && agent.hasPath)
                {
                    agent.ResetPath(); //we reset the path because we're now using the VFF
                }
                return output;
            }

            iFlowFieldInfluence closest = null;
            float minDistance = float.MaxValue;
            //Find closest influence
            foreach (var influence in influences)
            {
                if (influence.type == eInfluenceType.Sink)
                {
                    float distance = Vector3.Distance(worldPosition, influence.GameObject.transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closest = influence;
                    }
                }
            }
            //Set destination to closest influence
            if(agent) agent.SetDestination(closest.GameObject.transform.position);
            return Vector3.zero;
        }

        /// <summary>
        /// A simple velocity sampler, will return false if velocity is equal to zero.
        /// </summary>
        /// <param name="worldPosition">world-position of the agent</param>
        /// <param name="sampledVelocity">the velocity of the VFF at worldPosition</param>
        /// <returns></returns>
        public bool TrySampleField(Vector3 worldPosition, out Vector3 sampledVelocity)
        {
            Vector3 output = flowFieldCalculator.SampleField(worldPosition);
            sampledVelocity = output;
            return output != Vector3.zero;
        }
    }
}