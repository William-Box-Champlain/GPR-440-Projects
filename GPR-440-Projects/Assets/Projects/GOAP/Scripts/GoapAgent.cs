using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace GOAP
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class GoapAgent : MonoBehaviour
    {
        [SerializeField] List<Sensor> Sensors;

        [SerializeField] List<Transform> KnownLocations;

        NavMeshAgent Agent;
        Rigidbody rb;

        [SerializeField] List<float> Stats;

        GameObject CurrentTarget;
        Vector3 CurrentDestination;

        Goal LastGoal;
        public Goal CurrentGoal;
        //public Plan

        // Start is called before the first frame update
        void Start()
        {
            
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}