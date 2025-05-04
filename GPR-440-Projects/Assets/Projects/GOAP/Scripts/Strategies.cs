using System;
using UnityEngine;
using UnityEngine.AI;

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
    public class AttackStrategy : IActionStrategy
    {
        public bool IsValid => true;

        public bool IsComplete {get; private set;}

        readonly CountdownTimer timer;

        public AttackStrategy(float attackDuration)
        {
            timer = new CountdownTimer(attackDuration);
            timer.OnTimerStart += () => IsComplete = false;
            timer.OnTimerStop += () => IsComplete = true;
        }

        public void Start()
        {
            timer.Start();
        }

        public void Update(float dt) => timer.Update(dt);
    }

    public class MoveStrategy : IActionStrategy
    {
        readonly NavMeshAgent agent;
        readonly Func<Vector3> destination;

        public bool IsValid => !IsComplete;
        public bool IsComplete => agent.remainingDistance <= 2f && !agent.pathPending;

        public MoveStrategy(NavMeshAgent agent, Func<Vector3> destination)
        {
            this.agent = agent;
            this.destination = destination;
        }

        public void Start() => agent.SetDestination(destination());
        public void Stop() => agent.ResetPath();
    }

    public class WanderStrategy : IActionStrategy
    {
        readonly NavMeshAgent agent;
        readonly float wanderRadius;

        public bool IsValid => !IsComplete;

        public bool IsComplete => agent.remainingDistance <= 2f && !agent.pathPending;

        public WanderStrategy(NavMeshAgent agent, float wanderRadius)
        {
            this.agent = agent;
            this.wanderRadius = wanderRadius;
        }

        public void Start()
        {
            for(int i = 0; i < 5; i++)
            {
                Vector3 randomDirection = (UnityEngine.Random.insideUnitSphere * wanderRadius).With(y: 0);
                NavMeshHit hit;

                if(NavMesh.SamplePosition(agent.transform.position + randomDirection, out hit, wanderRadius, 1))
                {
                    agent.SetDestination(hit.position);
                    return;
                }
            }
        }
    }
    public class IdleStrategy : IActionStrategy
    {
        public bool IsValid => true;

        public bool IsComplete {get; private set;}

        readonly CountdownTimer timer;

        public IdleStrategy(float duration)
        {
            timer = new CountdownTimer(duration);
            timer.OnTimerStart += () => IsComplete = false;
            timer.OnTimerStop += () => IsComplete = true;
        }

        public virtual void Start() => timer.Start();
        public void Update(float deltaTime) => timer.Update(deltaTime);
    }

    public class PerformActionStrategy : IActionStrategy
    {
        private Func<bool> actionFunction;
        private bool actionComplete = false;
        
        public bool IsValid => true;
        public bool IsComplete => actionComplete;
        
        public PerformActionStrategy(Func<bool> actionFunction)
        {
            this.actionFunction = actionFunction;
        }
        
        public void Start()
        {
            // Reset completion state when starting
            actionComplete = false;
        }
        
        public void Update(float dt)
        {
            // Execute the action function and check if it returns true
            if (!actionComplete && actionFunction != null)
            {
                actionComplete = actionFunction();
                if (actionComplete)
                {
                    Debug.Log("PerformActionStrategy: Action completed successfully");
                }
            }
        }
        
        public void Stop()
        {
            // Nothing to clean up
        }
    }
    #endregion
}
