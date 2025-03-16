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
    public class GoapPlanner
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

                //TODO:
                //if(FindPath(goalNode, agent.actions))
                //{
                //    if (goalNode.IsLeafDead) continue;
                //    Stack<Action> actionStack = new Stack<Action>();
                //    while(goalNode.Leaves.Count > 0)
                //    {
                //        var cheapestLeaf = goalNode.Leaves.OrderBy(leaf => leaf.Cost).First();
                //        goalNode = cheapestLeaf;
                //        actionStack.Push(cheapestLeaf.Action);
                //    }
                //    return new ActionPlan(goal, actionStack, goalNode.Cost);
                //}
            }
            Debug.LogWarning("No plan found");
            return null;
        }

        bool FindPath(Node parent, HashSet<Action> actions)
        {
            return EasySearch(parent, actions);
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