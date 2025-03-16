using System.Collections.Generic;

namespace GOAP
{
    public class Action
    {
        #region member-variables
        public string Name { get; }
        public float Cost { get; set; }

        public HashSet<Belief> Preconditions { get; } = new();
        public HashSet<Belief> Effects { get; } = new();

        IActionStrategy strategy;
        public bool Complete => strategy.IsComplete;
        #endregion

        #region Constructors
        Action(string name)
        {
            Name = name;
        }
        #endregion

        #region functions
        /// <summary>
        /// Fires the start function for this action-strategy
        /// </summary>
        public void Start() => strategy.Start();

        /// <summary>
        /// Updates the update function for this action-strategy
        /// </summary>
        /// <param name="dt">time since last frame to current (delta-time)</param>
        public void Update(float dt)
        {
            if(strategy.IsValid) strategy.Update(dt);
            if (!strategy.IsComplete) return;

            foreach (var belief in Effects) belief.Evaluate();
        }

        /// <summary>
        /// Fires the stop function for this action-strategy
        /// </summary>
        public void Stop() => strategy.Stop();
        #endregion

        public class Builder 
        {
            readonly Action action;

            public Builder(string name)
            {
                action = new Action(name)
                {
                    Cost = 1
                };
            }
            public Builder WithCost(float cost)
            {
                action.Cost = cost;
                return this;
            }
            public Builder WithStrategy(IActionStrategy strategy)
            {
                action.strategy = strategy;
                return this;
            }
            public Builder AddPrecondition(Belief precondition)
            {
                action.Preconditions.Add(precondition);
                return this;
            }
            public Builder AddEffect(Belief effect)
            {
                action.Effects.Add(effect);
                return this;
            }
            public Action Build()
            {
                return action;
            }
        }
    }
}