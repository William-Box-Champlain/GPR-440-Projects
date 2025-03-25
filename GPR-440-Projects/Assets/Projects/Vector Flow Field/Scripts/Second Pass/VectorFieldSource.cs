using UnityEngine;

namespace VFF
{
    /// <summary>
    /// Component that marks a GameObject as a sink (destination) in the vector field.
    /// </summary>
    public class VectorFieldSource : MonoBehaviour
    {
        [Tooltip("Radius of influence for this sink")]
        [Range(0.01f, 5.0f)]
        public float radius = 0.5f;

        [Range(-0.01f, -5.0f)]
        public float strength = 1.0f;

        [Tooltip("Whether this sink is active")]
        public bool isActive = true;

        // Reference to the manager (set when registered)
        private SecondPassVectorFieldManager manager;

        private void Start()
        {
            if (SecondPassVectorFieldManager.GetInstance() != null)
            {
                SecondPassVectorFieldManager.GetInstance().RegisterSource(this);
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
        /// <param name="manager">The VectorFieldManager to associate with this sink.</param>
        public void SetManager(SecondPassVectorFieldManager manager)
        {
            this.manager = manager;
        }

        /// <summary>
        /// Sets the active state of this sink.
        /// </summary>
        /// <param name="active">Whether the sink is active.</param>
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
}