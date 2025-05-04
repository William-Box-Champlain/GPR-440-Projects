using System.Collections.Generic;
using System.Linq;
using System.Xml.Schema;
using UnityEngine;

namespace GOAP
{
    public interface IGoapPlanner
    {
        ActionPlan Plan(GoapAgent agent, HashSet<Goal> goals, Goal mostRecentGoal = null);
    }
    public class GoapPlanner : IGoapPlanner
    {
        public ActionPlan Plan(GoapAgent agent, HashSet<Goal> goals, Goal mostRecentGoal = null)
        {
            List<Goal> orderedGoals = goals
                .Where(goal => goal.EndState.Any(belief => !belief.Evaluate()))
                .OrderByDescending(goal=> goal == mostRecentGoal ? goal.Priority - 0.01 : goal.Priority)
                .ToList();
            
            foreach(var goal in orderedGoals)
            {
                Node goalNode = new Node(null, null, goal.EndState, 0);

                if(FindPath(goalNode, agent.actions))
                {
                    if (goalNode.IsLeafDead) continue;
                    Stack<Action> actionStack = new Stack<Action>();
                    Node currentNode = goalNode;
                    
                    while(currentNode.Leaves.Count > 0)
                    {
                        var cheapestLeaf = currentNode.Leaves.OrderBy(leaf => leaf.Cost).First();
                        currentNode = cheapestLeaf;
                        
                        if (currentNode.Action != null)
                        {
                            actionStack.Push(currentNode.Action);
                        }
                    }
                    
                    return new ActionPlan(goal, actionStack, currentNode.Cost);
                }
            }
            Debug.LogWarning("No plan found");
            return null;
        }

        bool FindPath(Node parent, HashSet<Action> actions)
        {
            // First try with A* search
            bool aStarResult = AStarSearch(parent, actions);
            
            // If A* failed or didn't produce enough leaves, try EasySearch as fallback
            if (!aStarResult || parent.Leaves.Count == 0)
            {
                Debug.Log("A* failed, using easySearch");
                return EasySearch(parent, actions);
            }
            
            return aStarResult;
        }

        /// <summary>
        /// A* search algorithm for finding an optimal action plan
        /// </summary>
        /// <param name="goalNode">The node containing goal beliefs</param>
        /// <param name="availableActions">Available actions to use</param>
        /// <returns>True if a plan was found, false otherwise</returns>
        bool AStarSearch(Node goalNode, HashSet<Action> availableActions)
        {
            // Priority queue for nodes to explore (sorted by cost)
            var openSet = new List<Node>();
            
            // Set of visited states to avoid cycles
            var closedSet = new HashSet<string>();
            
            // Add the goal node to the open set
            openSet.Add(goalNode);
            
            while (openSet.Count > 0)
            {
                // Get the node with lowest cost
                openSet.Sort((a, b) => a.Cost.CompareTo(b.Cost));
                var current = openSet[0];
                openSet.RemoveAt(0);
                
                // Skip if we've already processed this state
                string stateKey = GetStateKey(current.RequiredEffects);
                if (closedSet.Contains(stateKey))
                {
                    continue;
                }
                
                closedSet.Add(stateKey);
                
                // Check if all required beliefs are satisfied
                var unsatisfiedBeliefs = new HashSet<Belief>(current.RequiredEffects);
                unsatisfiedBeliefs.RemoveWhere(belief => belief.Evaluate());
                
                if (unsatisfiedBeliefs.Count == 0)
                {
                    // We've found a valid plan!
                    return true;
                }
                
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
            }
            
            // If we've exhausted all possibilities without finding a solution
            return goalNode.Leaves.Count > 0;
        }

        /// <summary>
        /// Creates a unique string key for a set of beliefs
        /// </summary>
        /// <param name="beliefs">Set of beliefs</param>
        /// <returns>String key</returns>
        private string GetStateKey(HashSet<Belief> beliefs)
        {
            return string.Join(";", beliefs.Select(b => b.Name).OrderBy(name => name));
        }

        bool EasySearch(Node parent, HashSet<Action> actions)
        {
            var orderedActions = actions.OrderBy(action => action.Cost);

            foreach (var action in orderedActions)
            {
                var requiredEffects = parent.RequiredEffects;

                requiredEffects.RemoveWhere(belief => belief.Evaluate());

                if (requiredEffects.Count == 0)
                {
                    return true;
                }

                if (action.Effects.Any(requiredEffects.Contains))
                {
                    var newRequiredEffects = new HashSet<Belief>(requiredEffects);
                    newRequiredEffects.Except(action.Effects);
                    newRequiredEffects.UnionWith(action.Preconditions);

                    var newAvailableActions = new HashSet<Action>(actions);
                    newAvailableActions.Remove(action);

                    var newNode = new Node(parent, action, newRequiredEffects, parent.Cost + action.Cost);

                    if (FindPath(newNode, newAvailableActions))
                    {
                        parent.Leaves.Add(newNode);
                        newRequiredEffects.ExceptWith(newNode.Action.Preconditions);
                    }

                    if (newRequiredEffects.Count == 0) return true;
                }
            }
            return parent.Leaves.Count > 0;
        }
    }
    public class Node
    {
        public Node Parent { get; }
        public Action Action { get; }
        public HashSet<Belief> RequiredEffects { get; }
        public List<Node> Leaves { get; }
        public float Cost { get; }

        public bool IsLeafDead => Leaves.Count == 0 && Action == default;
        public Node(Node parent, Action action, HashSet<Belief> effects, float cost)
        {
            Parent = parent;
            Action = action;
            RequiredEffects = new(effects);
            Leaves = new();
            Cost = cost;
        }
    }
    public class  ActionPlan
    {
        public Goal Goal { get; }
        public Stack<Action> Actions { get; }
        public float TotalCost { get; set; }

        public ActionPlan(Goal goal, Stack<Action> actions, float totalCost)
        {
            Goal = goal;
            Actions = actions;
            TotalCost = totalCost;
        }
    }
}
