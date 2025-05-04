# GOAP AI System for Unity

A flexible, extensible Goal Oriented Action Planning system for creating intelligent agents in Unity games.

## What's GOAP Anyway?

GOAP (Goal Oriented Action Planning) is an AI architecture that allows agents to formulate plans to achieve goals dynamically. Unlike more rigid systems like Behavior Trees, GOAP agents evaluate the current state of the world, select a goal with the highest priority, and then construct a sequence of actions to achieve that goal.

What makes GOAP special is its flexibility. Instead of hardcoding sequences of behaviors, we define:

- **Goals**: What the agent wants to achieve (e.g., "Stay warm," "Find food")
- **Actions**: What the agent can do (e.g., "Move to shelter," "Collect food")
- **Beliefs**: The agent's understanding of the world state (e.g., "I am cold," "I have food")

The agent then uses A* to find the most efficient sequence of actions that will transform the current world state into one that satisfies the goal. This creates emergent behavior that can adapt to changing circumstances without requiring explicit programming for every scenario.

## How This Implementation Works

This GOAP system is built around a few core components:

### GoapAgent

The main component that drives the AI decision-making process. It:
- Evaluates and selects the highest priority goal
- Uses the planner to create a plan of actions
- Executes the current action in the plan
- Monitors the world state through beliefs

```csharp
// Example of adding a GoapAgent to a GameObject
public GameObject enemyPrefab;

void SpawnEnemy() {
    var enemy = Instantiate(enemyPrefab);
    var agent = enemy.AddComponent<GoapAgent>();
    
    // Configure the agent...
    agent.AddGoal(new SurviveGoal());
    agent.AddAction(new FindShelterAction());
}
```

### GoapPlanner

The brains of the operation. The planner uses A* to search through the action space and find the optimal sequence of actions to achieve a goal. It's like a pathfinding algorithm, but instead of navigating physical space, it navigates through possible actions.

```csharp
// This is what's happening under the hood when the agent plans
foreach (var action in availableActions)
{
    if (!action.Effects.Any(unsatisfiedBeliefs.Contains))
    {
        continue;
    }
    
    var newRequiredEffects = new HashSet<Belief>(unsatisfiedBeliefs);
    
    newRequiredEffects.ExceptWith(action.Effects);
    
    newRequiredEffects.UnionWith(action.Preconditions);
    
    var newAvailableActions = new HashSet<Action>(availableActions);
    newAvailableActions.Remove(action);
    
    var newNode = new Node(current, action, newRequiredEffects, current.Cost + action.Cost);
    
    openSet.Add(newNode);
    
    current.Leaves.Add(newNode);
}
```

### Actions

Actions are the building blocks of GOAP. Each action has:
- **Preconditions**: What must be true for the action to be executable
- **Effects**: How the action changes the world state
- **Cost**: How "expensive" the action is (lower cost actions are preferred)

```csharp
public class FindFoodAction : Action
{
    public FindFoodAction()
    {
        Name = "Find Food";
        Cost = 2f;
        
        // This action requires the agent to be hungry
        Preconditions.Add(new Belief("IsHungry", true));
        
        // This action results in the agent having food
        Effects.Add(new Belief("HasFood", true));
    }
    
    public override bool IsExecutable()
    {
        // Check if there's food nearby
        return Physics.OverlapSphere(transform.position, 10f, foodLayer).Length > 0;
    }
    
    public override bool Execute()
    {
        // Logic to find and collect food
        var foodItems = Physics.OverlapSphere(transform.position, 10f, foodLayer);
        if (foodItems.Length > 0)
        {
            // Move to the nearest food item
            var nearestFood = foodItems.OrderBy(f => 
                Vector3.Distance(transform.position, f.transform.position)).First();
                
            // Move towards food...
            
            return true; // Action succeeded
        }
        
        return false; // Action failed
    }
}
```

### Beliefs

Beliefs represent the agent's understanding of the world state. They're essentially key-value pairs where the key is a string and the value is a boolean or a function that evaluates to a boolean.

```csharp
// Adding a simple belief
agent.beliefs.Add("HasWeapon", new Belief("HasWeapon", true));

// Adding a dynamic belief that evaluates based on a condition
agent.beliefs.Add("IsHealthLow", new Belief("IsHealthLow", () => agent.Health < 30));
```

### Goals

Goals define what the agent wants to achieve. Each goal has:
- **Priority**: How important the goal is (higher priority goals are selected first)
- **TargetState**: The world state the agent wants to achieve

```csharp
public class SurviveGoal : Goal
{
    public SurviveGoal()
    {
        Name = "Survive";
        Priority = 10; // High priority
        
        // The target state is to not be in danger
        TargetState.Add(new Belief("InDanger", false));
    }
    
    public override float CalculatePriority(Dictionary<string, Belief> beliefs)
    {
        // Increase priority if health is low
        if (beliefs.TryGetValue("IsHealthLow", out var healthBelief) && 
            healthBelief.Evaluate())
        {
            return Priority * 1.5f;
        }
        
        return Priority;
    }
}
```

## The Debug UI: Seeing Inside Your Agent's Brain

Here's the thing about GOAP - it's super powerful, but it can also be a total nightmare to debug. When your AI is doing something weird (and trust me, it will), how do you figure out what's going on in its digital brain?

That's why this implementation includes a debug UI system that shows you exactly what each agent is thinking in real-time. It's a floating panel that follows the agent around and displays three critical pieces of information:

1. The agent's current goal (what it's trying to achieve)
2. The current action it's executing (what it's doing right now)
3. Its beliefs about the world (what it thinks is true or false)

The UI is connected to the agent with a line renderer, so even when you have multiple agents running around, you can easily see which UI belongs to which agent. And the beliefs are color-coded - green for true, red for false - making it easy to spot at a glance what the agent believes about the world.

```csharp
// Adding the debug UI to an agent is as simple as:
myAgent.AddDebugUI();

// Or add it to all agents in the scene:
foreach (var agent in FindObjectsOfType<GoapAgent>())
{
    agent.AddDebugUI();
}
```

## Getting Started

### Installation

1. Clone this repository or download the latest release
2. Import the package into your Unity project
3. Add the `GoapAgent` component to any GameObject you want to have AI behavior

### Creating Your First GOAP Agent

1. **Create Actions**:
   ```csharp
   public class PatrolAction : Action
   {
       public PatrolAction()
       {
           Name = "Patrol";
           Cost = 1f;
           
           // No preconditions - can always patrol
           
           // Effect: area is patrolled
           Effects.Add(new Belief("AreaPatrolled", true));
       }
       
       public override bool Execute()
       {
           // Patrol logic here...
           return true;
       }
   }
   ```

2. **Create Goals**:
   ```csharp
   public class GuardAreaGoal : Goal
   {
       public GuardAreaGoal()
       {
           Name = "Guard Area";
           Priority = 5;
           
           // Target state: area is patrolled and secure
           TargetState.Add(new Belief("AreaPatrolled", true));
           TargetState.Add(new Belief("AreaSecure", true));
       }
   }
   ```

3. **Set Up the Agent**:
   ```csharp
   void Start()
   {
       var agent = GetComponent<GoapAgent>();
       
       // Add beliefs
       agent.beliefs.Add("AreaSecure", new Belief("AreaSecure", true));
       agent.beliefs.Add("AreaPatrolled", new Belief("AreaPatrolled", false));
       
       // Add actions
       agent.AddAction(new PatrolAction());
       agent.AddAction(new InvestigateAction());
       
       // Add goals
       agent.AddGoal(new GuardAreaGoal());
       
       // Add debug UI
       agent.AddDebugUI();
   }
   ```

## Performance Considerations

GOAP is more computationally intensive than simpler AI systems like Behavior Trees or Finite State Machines. Here are some tips to keep performance in check:

1. **Limit the number of actions and goals**: Each additional action increases the search space exponentially.
2. **Use reasonable costs**: Make sure action costs reflect their relative expense.
3. **Replan only when necessary**: Don't replan every frame if the world state hasn't changed significantly.
4. **Use the debug UI selectively**: While great for development, having debug UIs for hundreds of agents can impact performance.

## Examples

The repository includes several example scenes demonstrating different aspects of the GOAP system:

- **Basic Example**: A simple agent that patrols and investigates suspicious areas
- **Combat Example**: Agents that can fight, flee, and use different weapons
- **Survival Example**: Agents that need to find food, water, and shelter to survive

## Extending the System

The GOAP system is designed to be extensible. You can create custom actions, goals, and beliefs to fit your specific game needs. You can also extend the core classes to add new functionality:

```csharp
// Example of extending the GoapAgent class
public class EnemyAgent : GoapAgent
{
    public float health = 100f;
    public float attackRange = 5f;
    
    // Override the Update method to add custom behavior
    protected override void Update()
    {
        // Custom behavior before GOAP processing
        UpdateHealthBelief();
        
        // Call the base Update to run the GOAP system
        base.Update();
        
        // Custom behavior after GOAP processing
        UpdateAnimations();
    }
    
    private void UpdateHealthBelief()
    {
        beliefs["IsHealthLow"].SetValue(health < 30f);
    }
    
    private void UpdateAnimations()
    {
        // Update animations based on current action
        if (currentAction != null)
        {
            animator.SetTrigger(currentAction.Name);
        }
    }
}
```

## Conclusion

GOAP is a powerful AI system that gives your agents the ability to form dynamic plans to achieve their goals. By leveraging A* for plan generation and adding a robust debug UI, you can create intelligent, adaptable agents that are actually manageable to work with.

The next time you're working on an AI system, remember that visibility into your agent's decision-making process is just as important as the intelligence of the system itself. Good debug tools don't just make development easier - they make it possible to create more complex and interesting AI behaviors.

And isn't that what we all want? AI that surprises us, adapts to player actions, and creates those memorable gameplay moments that players will talk about long after they've put the game down.
