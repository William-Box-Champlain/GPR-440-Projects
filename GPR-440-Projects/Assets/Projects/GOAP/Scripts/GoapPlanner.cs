using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GoapPlanner
{
    // Simple solution just for testing, not very smart
    public List<Action> FastPlan(Beliefs worldState, Dictionary<string, bool> goal, List<Action> actions)
    {
        // First check if the world state already satisfies the goal
        if (GoalsSatisfied(goal, worldState))
        {
            return new List<Action>();
        }

        // Try to find a plan using the AStar search algorithm
        return AStarSearch(worldState, goal, actions);
        
        // If we can't find a plan, try the simpler approach
        // return EasySearch(worldState, goal, actions);
    }

    // A* search algorithm for finding a plan
    private List<Action> AStarSearch(Beliefs initialState, Dictionary<string, bool> goalState, List<Action> actions)
    {
        // Create a priority queue for the frontier
        List<Node> frontier = new List<Node>();
        
        // Create the initial node
        Node startNode = new Node();
        startNode.beliefs = new Beliefs(initialState); // Clone initial state
        startNode.cost = 0;
        startNode.parent = null;
        startNode.action = null;
        
        frontier.Add(startNode);
        
        // Keep track of visited states to avoid cycles
        HashSet<string> visitedStates = new HashSet<string>();
        
        // While we have nodes to explore
        while (frontier.Count > 0)
        {
            // Sort by cost and get the lowest cost node
            frontier.Sort((a, b) => a.cost.CompareTo(b.cost));
            Node current = frontier[0];
            frontier.RemoveAt(0);
            
            // Check if we've reached the goal
            if (GoalsSatisfied(goalState, current.beliefs))
            {
                // Return the sequence of actions from root to goal
                return ReconstructPlan(current);
            }
            
            // Skip if we've visited this state
            string stateKey = current.beliefs.ToString();
            if (visitedStates.Contains(stateKey))
            {
                continue;
            }
            
            visitedStates.Add(stateKey);
            
            // Find applicable actions
            foreach (Action action in actions)
            {
                if (ActionPrerequisitesSatisfied(action, current.beliefs))
                {
                    // Create a new node
                    Node nextNode = new Node();
                    nextNode.beliefs = new Beliefs(current.beliefs); // Clone beliefs
                    
                    // Apply action effects
                    foreach (var effect in action.effects)
                    {
                        nextNode.beliefs.SetBelief(effect.Key, effect.Value);
                    }
                    
                    // Set parent for backtracking
                    nextNode.parent = current;
                    
                    // Add this action to the node
                    nextNode.action = action;
                    
                    // Calculate cost
                    nextNode.cost = current.cost + action.cost;
                    
                    // Add to frontier
                    frontier.Add(nextNode);
                }
            }
        }
        
        // No plan found
        return null;
    }
    
    // Helper method to reconstruct plan from goal node to root
    private List<Action> ReconstructPlan(Node goalNode)
    {
        List<Action> plan = new List<Action>();
        Node current = goalNode;
        
        // Traverse up the parent chain until we reach the root
        while (current.parent != null)
        {
            plan.Add(current.action);
            current = current.parent;
        }
        
        // Reverse the plan to get the correct order (root to goal)
        plan.Reverse();
        return plan;
    }

    private List<Action> EasySearch(Beliefs worldState, Dictionary<string, bool> goal, List<Action> actions)
    {
        List<Action> results = new List<Action>();
        
        System.Type worldStateType = worldState.GetType();
        Debug.Log(worldStateType.Name);
        // While the goal isn't sastisfied
        while (!GoalsSatisfied(goal, worldState))
        {
            // Find all actions that could apply to the current world state
            List<Action> applicableActions = new List<Action>();
            
            foreach (Action a in actions)
            {
                if (ActionPrerequisitesSatisfied(a, worldState))
                {
                    applicableActions.Add(a);
                }
            }
            
            if (applicableActions.Count == 0) return null; // No solution
            
            // Find the lowest cost action
            applicableActions.Sort((x, y) => x.cost.CompareTo(y.cost));
            
            // Apply that action's effects to the world state
            Action bestAction = applicableActions[0];
            
            // Apply effects
            foreach (var effect in bestAction.effects)
            {
                worldState.SetBelief(effect.Key, effect.Value);
            }
            
            // Add the action to our results
            results.Add(bestAction);
            
            // Remove the best action from the list of available actions
            // This is what's preventing cycles & infinite loops
            actions = actions.Except(new List<Action>() { bestAction }).ToList();
        }
        
        return results;
    }

    // Helper method to check if the world state (aka a node's beliefs)
    // satisfies all goal conditions
    private bool GoalsSatisfied(Dictionary<string, bool> goal, Beliefs worldState)
    {
        foreach (var state in goal)
        {
            bool value;
            if (!worldState.TryGetBelief(state.Key, out value) || value != state.Value)
            {
                return false;
            }
        }
        
        return true;
    }

    // Helper method to check if all the prerequisites for an action
    // are present in the world state
    private bool ActionPrerequisitesSatisfied(Action action, Beliefs worldState)
    {
        foreach (var prereq in action.preconditions)
        {
            bool value;
            if (!worldState.TryGetBelief(prereq.Key, out value) || value != prereq.Value)
            {
                return false;
            }
        }
        
        return true;
    }

    private class Node
    {
        public Beliefs beliefs; // Current state
        public float cost; // Cost to reach this node
        public Node parent; // Parent node for backtracking
        public Action action; // Action that led to this node
    }
}
