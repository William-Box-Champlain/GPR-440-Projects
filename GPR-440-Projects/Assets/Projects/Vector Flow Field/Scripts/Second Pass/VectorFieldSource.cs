using UnityEngine;

    /// <summary>
    /// Component that marks a GameObject as a source (area to avoid) in the vector field.
    /// </summary>
    public class VectorFieldSource : MonoBehaviour
    {
        [Tooltip("Radius of influence for this source")]
        [Range(0.01f, 5.0f)]
        public float radius = 0.5f;

        [Range(0.01f,5.0f)]
        public float strength = 1.0f;

        [Tooltip("Whether this source is active")]
        public bool isActive = true;
        
        // Reference to the manager (set when registered)
        private VFF.VectorFieldManager manager;
        
        /// <summary>
        /// Auto-register with the VectorFieldManager when enabled.
        /// </summary>
        private void OnEnable()
        {
            
        }

        private void Start()
        {
            if (VFF.VectorFieldManager.Instance != null)
            {
                VFF.VectorFieldManager.Instance.RegisterSource(this);
            }
        }

        /// <summary>
        /// Auto-unregister when disabled.
        /// </summary>
        private void OnDisable()
        {
            if (manager != null)
            {
                manager.UnregisterSource(this);
            }
        }
        
        /// <summary>
        /// Sets the manager reference.
        /// </summary>
        /// <param name="manager">The VectorFieldManager to associate with this source.</param>
        public void SetManager(VFF.VectorFieldManager manager)
        {
            this.manager = manager;
        }
        
        /// <summary>
        /// Sets the active state of this source.
        /// </summary>
        /// <param name="active">Whether the source is active.</param>
        public void SetActive(bool active)
        {
            if (isActive != active)
            {
                isActive = active;
                if (manager != null)
                {
                    manager.UpdateSinksAndSources();
                }
            }
        }
    }