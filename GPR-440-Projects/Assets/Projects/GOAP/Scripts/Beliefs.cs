using System;
using System.Collections.Generic;
using UnityEngine;
using Unity;

namespace GOAP
{
    public class BeliefFactory
    {
        readonly GoapAgent agent;
        readonly Dictionary<string, Belief> beliefMap;

        public BeliefFactory(GoapAgent agent, Dictionary<string, Belief> beliefMap)
        {
            this.agent = agent;
            this.beliefMap = beliefMap;
        }

        public void AddBelief(string key, Func<bool> condition)
        {
            beliefMap.Add
                (
                key,
                new Belief.BeliefBuilder(key)
                .WithCondition(condition)
                .Build()
                );
        }

        public void AddSensorBelief(string key, Func<bool> condition)
        {
            beliefMap.Add(
                key,
                new Belief.BeliefBuilder(key)
                .WithCondition(condition)
                .Build()
                );
        }

        public void AddLocationBelief(string key, float range, Transform location)
        {
            AddLocationBelief(key, range, location.position);
        }

        public void AddLocationBelief(string key, float range, Vector3 location)
        {
            beliefMap.Add
                (
                key,
                new Belief.BeliefBuilder(key)
                .WithCondition(() => InRange(location, range))
                .WithLocation(() => location)
                .Build()
                );
        }

        bool InRange(UnityEngine.Vector3 position, float range) => UnityEngine.Vector3.Distance(agent.transform.position, position) < range;
    }
    public class Belief
    {
        #region member-variables
        public string Name { get; private set; }

        Func<bool> condition = () => default;
        Func<UnityEngine.Vector3> observation = () => default;

        public Vector3 Location => observation();
        #endregion

        #region constructors
        Belief(string name)
        {
            this.Name = name;
        }
        #endregion

        #region functions
        public bool Evaluate() => condition();
        #endregion

        public class BeliefBuilder
        {
            readonly Belief belief;

            public BeliefBuilder(string name)
            {
                belief = new Belief(name);
            }

            public BeliefBuilder WithCondition(Func<bool> condition)
            {
                belief.condition = condition;
                return this;
            }

            public BeliefBuilder WithLocation(Func<UnityEngine.Vector3> location)
            {
                belief.observation = location;
                return this;
            }

            public Belief Build()
            {
                return belief;
            }
        }
    }

}
