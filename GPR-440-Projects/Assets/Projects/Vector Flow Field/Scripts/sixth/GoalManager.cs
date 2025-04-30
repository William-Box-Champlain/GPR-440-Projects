using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace sixth{/// <summary>
/// Example script showing how to manage goals for the VFF system
/// </summary>
public class GoalManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private VectorFieldManager vectorFieldManager;
    [SerializeField] private AgentManager agentManager;

    [Header("Goals")]
    [SerializeField] private Transform[] goalTransforms;
    [SerializeField] private float goalWeight = 1.0f;

    // Goal IDs
    private List<int> goalIds = new List<int>();

    private void Start()
    {
        // Register all goals
        RegisterGoals();
    }

    /// <summary>
    /// Register all goal transforms with the VFF system
    /// </summary>
    private void RegisterGoals()
    {
        goalIds.Clear();

        foreach (Transform goalTransform in goalTransforms)
        {
            Vector2 position = new Vector2(goalTransform.position.x, goalTransform.position.z);
            int goalId = vectorFieldManager.AddGoal(position, goalWeight);

            if (goalId >= 0)
            {
                goalIds.Add(goalId);
                Debug.Log($"Registered goal at {position} with ID {goalId}");
            }
        }
    }

    /// <summary>
    /// Activate a random goal and deactivate others
    /// </summary>
    public void ActivateRandomGoal()
    {
        if (goalIds.Count == 0)
            return;

        // Choose a random goal
        int randomIndex = Random.Range(0, goalIds.Count);

        // Deactivate all goals
        for (int i = 0; i < goalIds.Count; i++)
        {
            vectorFieldManager.SetGoalActive(goalIds[i], false);
        }

        // Activate the chosen goal
        vectorFieldManager.SetGoalActive(goalIds[randomIndex], true);

        Debug.Log($"Activated goal with ID {goalIds[randomIndex]}");
    }

    /// <summary>
    /// Activate all goals with varying weights
    /// </summary>
    public void ActivateAllGoals()
    {
        if (goalIds.Count == 0)
            return;

        // Activate all goals with random weights
        for (int i = 0; i < goalIds.Count; i++)
        {
            float weight = Random.Range(0.5f, 2.0f);
            vectorFieldManager.SetGoalWeight(goalIds[i], weight);
            vectorFieldManager.SetGoalActive(goalIds[i], true);
        }

        Debug.Log($"Activated all {goalIds.Count} goals with varying weights");
    }

    /// <summary>
    /// Activate goals in sequence
    /// </summary>
    public IEnumerator ActivateGoalsInSequence(float delay = 5.0f)
    {
        if (goalIds.Count == 0)
            yield break;

        // Deactivate all goals
        for (int i = 0; i < goalIds.Count; i++)
        {
            vectorFieldManager.SetGoalActive(goalIds[i], false);
        }

        // Activate goals one by one
        for (int i = 0; i < goalIds.Count; i++)
        {
            vectorFieldManager.SetGoalActive(goalIds[i], true);
            Debug.Log($"Activated goal with ID {goalIds[i]}");

            // Wait for delay time
            yield return new WaitForSeconds(delay);

            // Deactivate the current goal
            vectorFieldManager.SetGoalActive(goalIds[i], false);
        }
    }

    // Example UI button callbacks

    public void OnClickActivateRandomGoal()
    {
        ActivateRandomGoal();
    }

    public void OnClickActivateAllGoals()
    {
        ActivateAllGoals();
    }

    public void OnClickActivateSequence()
    {
        StartCoroutine(ActivateGoalsInSequence());
    }
}}
