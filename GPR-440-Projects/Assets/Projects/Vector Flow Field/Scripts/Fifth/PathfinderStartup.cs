using MipmapPathfinding;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathfinderStartup : MonoBehaviour
{

    [SerializeField] MipmapGenerator Generator;
    [SerializeField] ResolutionBiasController ResolutionBiasController;
    [SerializeField] MultiResolutionVectorFieldGenerator MultiResolutionVectorFieldGenerator;
    [SerializeField] ChunkedProcessingSystem ChunkedProcessingSystem;
    [SerializeField] VectorFieldStorage VectorFieldStorage;
    [SerializeField] AgentQuerySystem AgentQuerySystem;

    [SerializeField] List<GameObject> Targets = new List<GameObject>();
    [SerializeField] float progress = 0.0f;

    private void Awake()
    {
        StartCoroutine(InitializeSystem());
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        ChunkedProcessingSystem?.Update();
        VectorFieldStorage?.Update();

        if(ChunkedProcessingSystem) progress = ChunkedProcessingSystem.GetChunkProgress();
    }

    private IEnumerator InitializeSystem()
    {
        // Step 1: Initialize MipmapGenerator
        // Creates the multi-resolution navigation textures
        Debug.Log("Initializing MipmapGenerator...");
        Generator.Initialize();
        yield return null;  // Wait a frame to prevent UI freezing

        // Step 2: Initialize ResolutionBiasController
        // Detects junctions and sets up bias system
        Debug.Log("Initializing ResolutionBiasController...");
        ResolutionBiasController.Initialize();
        yield return null;

        // Step 3: Initialize MultiResolutionVectorFieldGenerator
        // Sets up vector field textures for each resolution level
        Debug.Log("Initializing VectorFieldGenerator...");
        MultiResolutionVectorFieldGenerator.Initialize();
        yield return null;

        // Step 4: Initialize VectorFieldStorage
        // Creates CPU-side caches for efficient queries
        Debug.Log("Initializing VectorFieldStorage...");
        VectorFieldStorage.Initialize();
        yield return null;

        // Step 5: Initialize ChunkedProcessingSystem
        // Sets up chunk division and priority system
        Debug.Log("Initializing ChunkedProcessingSystem...");
        ChunkedProcessingSystem.Initialize();
        yield return null;

        // Step 6: Initialize AgentQuerySystem
        // Sets up the interface for agent queries
        Debug.Log("Initializing AgentQuerySystem...");
        AgentQuerySystem.Initialize();
        yield return null;

        // Step 7: Set initial targets (if any)
        if (Targets.Count > 0)
        {
            Debug.Log("Setting initial targets...");
            Vector3[] targetPositions = new Vector3[Targets.Count];
            for (int i = 0; i < Targets.Count; i++)
            {
                targetPositions[i] = Targets[i].transform.position;
            }
            SetTargets(targetPositions);
        }

        Debug.Log("Pathfinding system initialized successfully!");
    }

    private void StandardInitializeSystem()
    {
        // Step 1: Initialize MipmapGenerator
        // Creates the multi-resolution navigation textures
        Debug.Log("Initializing MipmapGenerator...");
        Generator.Initialize();

        // Step 2: Initialize ResolutionBiasController
        // Detects junctions and sets up bias system
        Debug.Log("Initializing ResolutionBiasController...");
        ResolutionBiasController.Initialize();

        // Step 3: Initialize MultiResolutionVectorFieldGenerator
        // Sets up vector field textures for each resolution level
        Debug.Log("Initializing VectorFieldGenerator...");
        MultiResolutionVectorFieldGenerator.Initialize();

        // Step 4: Initialize VectorFieldStorage
        // Creates CPU-side caches for efficient queries
        Debug.Log("Initializing VectorFieldStorage...");
        VectorFieldStorage.Initialize();

        // Step 5: Initialize ChunkedProcessingSystem
        // Sets up chunk division and priority system
        Debug.Log("Initializing ChunkedProcessingSystem...");
        ChunkedProcessingSystem.Initialize();

        // Step 6: Initialize AgentQuerySystem
        // Sets up the interface for agent queries
        Debug.Log("Initializing AgentQuerySystem...");
        AgentQuerySystem.Initialize();

        // Step 7: Set initial targets (if any)
        if (Targets.Count > 0)
        {
            Debug.Log("Setting initial targets...");
            Vector3[] targetPositions = new Vector3[Targets.Count];
            for (int i = 0; i < Targets.Count; i++)
            {
                targetPositions[i] = Targets[i].transform.position;
            }
            SetTargets(targetPositions);
        }

        Debug.Log("Pathfinding system initialized successfully!");
    }

    // Helper method for target management - use this when targets change
    public void SetTargets(Vector3[] targetPositions)
    {
        // The ChunkedProcessingSystem acts as the main interface for target updates
        // It will propagate the targets to other components as needed
        ChunkedProcessingSystem.SetTargets(targetPositions);

        // For more direct control, you can update individual components:
        // vectorFieldGenerator.SetTargets(targetPositions);
        // biasController.SetTargets(targetTransforms);
    }
}
