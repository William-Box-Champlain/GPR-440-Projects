using UnityEngine;

namespace VFF
{
    /// <summary>
    /// Scriptable object to store and manage Vector Flow Field simulation parameters.
    /// </summary>
    [CreateAssetMenu(fileName = "VectorFieldParameters", menuName = "Vector Flow Field/Parameters", order = 1)]
    public class VectorFieldParameters : ScriptableObject
    {
        [Header("Grid Settings")]
        [Tooltip("Resolution of the simulation grid")]
        [SerializeField] private Vector2Int gridResolution = new Vector2Int(256, 256);
        public Vector2Int GridResolution => gridResolution;

        [Header("Simulation Settings")]
        [Tooltip("Viscosity of the fluid (higher values make the fluid more viscous)")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float viscosity = 0.1f;
        public float Viscosity => viscosity;

        [Tooltip("Number of pressure solver iterations (higher values increase accuracy but decrease performance)")]
        [Range(1, 100)]
        [SerializeField] private int pressureIterations = 20;
        public int PressureIterations => pressureIterations;

        [Tooltip("Number of diffusion solver iterations (higher values create smoother velocity fields)")]
        [Range(1, 50)]
        [SerializeField] private int diffusionIterations = 20;
        public int DiffusionIterations => diffusionIterations;

        [Tooltip("Time step multiplier for the simulation (higher values make the simulation faster but less stable)")]
        [Range(0.1f, 2.0f)]
        [SerializeField] private float timeStepMultiplier = 1.0f;
        public float TimeStepMultiplier => timeStepMultiplier;

        [Header("Force Settings")]
        [Tooltip("Strength of sink forces (higher values create stronger attraction to sinks)")]
        [Range(0.1f, 10.0f)]
        [SerializeField] private float sinkStrength = 2.0f;
        public float SinkStrength => sinkStrength;

        [Tooltip("Strength of source forces (higher values create stronger repulsion from sources)")]
        [Range(0.1f, 10.0f)]
        [SerializeField] private float sourceStrength = 2.0f;
        public float SourceStrength => sourceStrength;

        [Header("Global Pressure Settings")]
        [Tooltip("Strength of global pressure influence (higher values make pressure propagate more strongly across the entire field)")]
        [Range(0.0f, 2.0f)]
        [SerializeField] private float globalPressureStrength = 0.5f;
        public float GlobalPressureStrength => globalPressureStrength;

        [Tooltip("Number of global pressure propagation iterations (higher values increase propagation distance but decrease performance)")]
        [Range(1, 50)]
        [SerializeField] private int globalPressureIterations = 20;
        public int GlobalPressureIterations => globalPressureIterations;

        [Header("Update Settings")]
        [Tooltip("Whether to update the simulation in FixedUpdate instead of Update")]
        [SerializeField] private bool useFixedUpdate = true;
        public bool UseFixedUpdate => useFixedUpdate;

        [Tooltip("Whether to automatically update the simulation every frame")]
        [SerializeField] private bool autoUpdate = true;
        public bool AutoUpdate => autoUpdate;

        /// <summary>
        /// Validates the parameters to ensure they are within acceptable ranges.
        /// </summary>
        private void OnValidate()
        {
            gridResolution.x = Mathf.Max(1, gridResolution.x);
            gridResolution.y = Mathf.Max(1, gridResolution.y);

            viscosity = Mathf.Max(0.0001f, viscosity);

            pressureIterations = Mathf.Max(1, pressureIterations);
            
            diffusionIterations = Mathf.Max(1, diffusionIterations);

            timeStepMultiplier = Mathf.Max(0.1f, timeStepMultiplier);

            sinkStrength = Mathf.Max(0.1f, sinkStrength);
            sourceStrength = Mathf.Max(0.1f, sourceStrength);

            globalPressureStrength = Mathf.Max(0.0f, globalPressureStrength);
            globalPressureIterations = Mathf.Max(1, globalPressureIterations);
        }
    }
}
