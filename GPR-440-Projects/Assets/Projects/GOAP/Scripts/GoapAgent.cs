using DependencyInjection;
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

        [Header("Stats and positions")]
        CountdownTimer statsTimer;
        [SerializeField] Dictionary<string,float> Stats;
        [SerializeField] List<Transform> foodLocations;
        [SerializeField] List<Transform> shelterLocations;
        [SerializeField] List<Transform> cookingLocations;

        GameObject CurrentTarget;
        Vector3 CurrentDestination;

        Goal LastGoal;
        public Goal CurrentGoal;
        public ActionPlan ActionPlan;
        public Action currentAction;

        public Dictionary<string, Belief> beliefs;
        public HashSet<Action> actions;
        public HashSet<Goal> goals;

        [Inject] GoapFactory gFactory;
        IGoapPlanner gPlanner;

        private void Awake()
        {
            Agent = this.gameObject.GetComponent<NavMeshAgent>();
            rb = this.gameObject.GetComponent<Rigidbody>();
            rb.freezeRotation = true;

            gPlanner = gFactory.CreatePlanner();
        }

        // Start is called before the first frame update
        void Start()
        {
            SetupStats();
            SetupTimers();
            SetupBeliefs();
            SetupActions();
            SetupGoals();
        }

        // Update is called once per frame
        void Update()
        {
            statsTimer.Update(Time.deltaTime);

            if(currentAction == null)
            {
                Debug.Log("Calculating any new plan");
                CalculatePlan();

                if (ActionPlan != null && ActionPlan.Actions.Count > 0)
                {
                    Agent.ResetPath();

                    CurrentGoal = ActionPlan.Goal;
                    Debug.Log($"Goal: {CurrentGoal.Name} with {ActionPlan.Actions.Count} actions in plan");
                    currentAction = ActionPlan.Actions.Pop();
                    Debug.Log($"Popped Action: {currentAction.Name}");

                    if(currentAction.Preconditions.All(b => b.Evaluate()))
                    {
                        currentAction.Start();
                    }
                    else
                    {
                        Debug.Log("Preconditions not met, clearing current action and goal");
                        Debug.Log($"Action to Clear:{currentAction?.Name}, Goal to Clear:{CurrentGoal?.Name}");
                        currentAction = null;
                        CurrentGoal = null;
                    }
                }
            }

            // Print the full action plan in execution order
            if (ActionPlan != null && ActionPlan.Actions.Count > 0)
            {
                string planLog = ($"Working on goal '{CurrentGoal.Name}' with {ActionPlan.Actions.Count} actions:");

                // Create a copy of the stack to preserve the original
                var planCopy = new Stack<Action>(new Stack<Action>(ActionPlan.Actions));
                int stepNumber = 1;

                while (planCopy.Count > 0)
                {
                    planLog += ($"  Step {stepNumber++}: {planCopy.Pop().Name}");
                }

                Debug.Log(planLog.ToString());
            }

            if (ActionPlan != null && currentAction != null)
            {
                Debug.Log($"Current Action:{currentAction?.Name}");
                currentAction.Update(Time.deltaTime);

                if (currentAction.Complete)
                {
                    Debug.Log($"{currentAction.Name} complete");
                    currentAction.Stop();
                    currentAction = null;

                    if(ActionPlan.Actions.Count == 0)
                    {
                        Debug.Log("Plan complete");
                        LastGoal = CurrentGoal;
                        CurrentGoal = null;
                    }
                }
            }

            Debug.Log($"Temp:{Stats["temp"]}, Hunger:{Stats["hunger"]}, Food:{Stats["food"]}");

            Debug.Log($"Current Goal:{CurrentGoal?.Name}");
        }

        public void CalculatePlan()
        {
            var priorityLevel = CurrentGoal?.Priority ?? 0;

            HashSet<Goal> goalsToCheck = goals;

            if (CurrentGoal != null)
            {
                Debug.Log("Current goal exists, checking for higher priority goal");
                goalsToCheck = new HashSet<Goal>(goals.Where(g => g.Priority > priorityLevel));
            }

            var potentialPlan = gPlanner.Plan(this, goalsToCheck, LastGoal);
            if (potentialPlan != null)
            {
                ActionPlan = potentialPlan;
            }
        }

        void SetupStats()
        {
            Stats = new Dictionary<string, float>();
            Stats.Add("temp", 80f);
            Stats.Add("hunger", 100f);
            Stats.Add("food", 0f);
        }

        void SetupBeliefs()
        {
            beliefs = new Dictionary<string, Belief>();
            BeliefFactory factory = new BeliefFactory(this, beliefs);

            factory.AddBelief("Nothing", () => false);

            factory.AddBelief("AgentIdle", () => !Agent.hasPath);
            factory.AddBelief("AgentMoving",() => Agent.hasPath);

            factory.AddBelief("AgentCold", () => Stats["temp"] < 30f);
            factory.AddBelief("AgentWarm", () => Stats["temp"] >= 50f);
            factory.AddBelief("AgentHungry", () => Stats["hunger"] < 30f);
            factory.AddBelief("AgentFull", () => Stats["hunger"] >= 50f);
            factory.AddBelief("AgentNoFood", () => Stats["food"] <= 2);
            factory.AddBelief("AgentHasFood", () => Stats["food"] >= 4);

            factory.AddLocationBelief("AgentAtFood", 3f, FindClosestTransform(foodLocations));
            factory.AddLocationBelief("AgentAtShelter", 3f, FindClosestTransform(shelterLocations));
            factory.AddLocationBelief("AgentAtCooking", 3f, FindClosestTransform(cookingLocations));
            factory.AddLocationWithCondition("AgentAtShelterWithFood", 2f, FindClosestTransform(shelterLocations).position, () => Stats["food"] > 0);
            factory.AddLocationWithCondition("AgentAtCookingWithFood", 2f, FindClosestTransform(cookingLocations).position, () => Stats["food"] > 0);
        }

        void SetupActions()
        {
            actions = new HashSet<Action>();

            actions.Add(new Action.Builder("Relax")
                .WithStrategy(new IdleStrategy(5f))
                .AddEffect(beliefs["Nothing"])
                .Build()
                );
            
            actions.Add(new Action.Builder("Wander Around")
                .WithStrategy(new WanderStrategy(this.Agent, 10))
                .AddEffect(beliefs["AgentMoving"])
                .Build()
                );

            actions.Add(new Action.Builder("MoveToShelter")
                .WithStrategy(new MoveStrategy(this.Agent, () => FindClosestTransform(shelterLocations).position))
                .AddEffect(beliefs["AgentAtShelter"])
                .Build()
                );

            actions.Add(new Action.Builder("BringFoodToCooking")
                .WithStrategy(new MoveStrategy(this.Agent, () => FindClosestTransform(cookingLocations).position))
                .AddPrecondition(beliefs["AgentHasFood"])
                .AddEffect(beliefs["AgentAtCookingWithFood"])
                .Build()
                );

            actions.Add(new Action.Builder("Eat")
                .WithStrategy(new PerformActionStrategy(() => {
                    // Immediately consume food and increase hunger
                    if (Stats["food"] > 0)
                    {
                        Stats["hunger"] += 30;
                        Stats["food"] -= 2;
                        Debug.Log($"Eat action: Consumed food, new hunger: {Stats["hunger"]}, food: {Stats["food"]}");
                        return true;
                    }
                    return false;
                }))
                .AddPrecondition(beliefs["AgentAtCookingWithFood"])
                .AddEffect(beliefs["AgentFull"])
                .Build()
                );

            actions.Add(new Action.Builder("CollectFood")
                .WithStrategy(new PerformActionStrategy(() => {
                    // Immediately collect a significant amount of food
                    Stats["food"] += 5;
                    Debug.Log($"CollectFood action: Collected food, new amount: {Stats["food"]}");
                    return true; // Action is complete immediately
                }))
                .AddPrecondition(beliefs["AgentAtFood"])
                .AddEffect(beliefs["AgentHasFood"])
                .Build()
                );

            actions.Add(new Action.Builder("WarmUp")
                .WithStrategy(new PerformActionStrategy(() => {
                    // Immediately warm up the agent
                    Stats["temp"] += 30;
                    Debug.Log($"WarmUp action: Warmed up, new temp: {Stats["temp"]}");
                    return true;
                }))
                .AddPrecondition(beliefs["AgentAtShelter"])
                .AddEffect(beliefs["AgentWarm"])
                .Build()
                );

            actions.Add(new Action.Builder("FindFood")
                .WithStrategy(new MoveStrategy(this.Agent, () => FindClosestTransform(foodLocations).position))
                .AddPrecondition(beliefs["AgentNoFood"])
                .AddEffect(beliefs["AgentAtFood"])
                .Build()
                );
        }

        void SetupGoals()
        {
            goals = new HashSet<Goal>();

            goals.Add(new Goal.Builder("Chill")
                .WithPriority(() => 1f)
                .WithEndState(beliefs["Nothing"])
                .Build()
                );

            goals.Add(new Goal.Builder("Wander")
                .WithPriority(() => 1f)
                .WithEndState(beliefs["AgentMoving"])
                .Build()
                );

            goals.Add(new Goal.Builder("FindFood")
                .WithPriority(() => 1f)
                .WithEndState(beliefs["AgentHasFood"])
                .Build()
                );

            goals.Add(new Goal.Builder("KeepTempUp")
                .WithPriority(() => 2f)
                .WithEndState(beliefs["AgentWarm"])
                .Build()
                );

            goals.Add(new Goal.Builder("KeepBellyFull")
                .WithPriority(() => 3f)
                .WithEndState(beliefs["AgentFull"])
                .Build()
                );

            
        }

        void SetupTimers()
        {
            statsTimer = new CountdownTimer(2f);
            statsTimer.OnTimerStop += () =>
            {
                UpdateStats();
                statsTimer.Start();
            };
            statsTimer.Start ();
        }

        void UpdateStats()
        {
            Stats["temp"] += InRangeOf(FindClosestTransform(shelterLocations).position, 3f) ? 10 : -5;
            Stats["food"] += InRangeOf(FindClosestTransform(foodLocations).position, 3f) ? 1 : 0;
            Stats["hunger"] += (InRangeOf(FindClosestTransform(cookingLocations).position, 3f) && Stats["food"] > 0) ? 10 : -5; //reduce hunger when at cooking location with food
            Stats["food"] += (InRangeOf(FindClosestTransform(cookingLocations).position, 3f) && Stats["food"] > 0) ? -1 : 0; //reduce food when at cooking location with food
        }

        bool InRangeOf(Vector3 pos, float range) => Vector3.Distance(transform.position, pos) < range;

        Transform FindClosestTransform(List<Transform> locations)
        {
            if (locations.Count == 0 || locations == null) return null;

            Transform closest = null;
            float closestDistance = Mathf.Infinity;
            Vector3 currentPosition = transform.position;

            foreach (var foodLocation in locations)
            {
                if (foodLocation == null) continue;

                float distance = Vector3.Distance(currentPosition, foodLocation.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = foodLocation;
                }
            }

            return closest;
        }
    }
}
