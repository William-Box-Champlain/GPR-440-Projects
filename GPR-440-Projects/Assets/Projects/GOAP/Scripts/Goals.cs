using System;
using System.Collections;
using System.Collections.Generic;

namespace GOAP
{
    public class Goal
    {
        public string Name { get; }
        private Func<float> PriorityCalc { get; set; }
        public float Priority => PriorityCalc();
        public HashSet<Belief> EndState { get; } = new();

        Goal(string name)
        {
            Name = name;
        }

        public class Builder
        {
            readonly Goal goal;

            public Builder(string name)
            {
                goal = new Goal(name);
            }

            public Builder WithPriority(Func<float> priorityFunc)
            {
                goal.PriorityCalc = priorityFunc;
                return this;
            }

            public Builder WithEndState(Belief effect)
            {
                goal.EndState.Add(effect);
                return this;
            }

            public Goal Build()
            {
                return goal;
            }
        }
    }
}