using System;
using UnityEngine;
using UnityEngine.AI;

namespace GOAP
{
    [RequireComponent(typeof(Collider))]
    public class Sensor : MonoBehaviour
    {
        [SerializeField] float sensorRadius = 1.0f;
        [SerializeField] float timeIntervel = 1.0f;

        Collider detectionRange;

        public event System.Action OnTargetChanged = delegate { };

        GameObject target;
        Vector3 lastKnownPosition;
        //TODO: Implement countdown timer system.
        //CountDownTimer timer;

        public Vector3 TargetPosition => target ? target.transform.position : Vector3.zero;
        public bool IsTargetInRange => TargetPosition != Vector3.zero;

        private void Awake()
        {
            detectionRange = GetComponent<Collider>();
            detectionRange.isTrigger = true;

        }

        private void Start()
        {
            //timer = new CountDownTimer
        }

        private void Update()
        {
            //timer.tick(Time.deltaTime);
        }

        void UpdateTargetPosition(GameObject target = null)
        {
            this.target = target;
            if (IsTargetInRange && (lastKnownPosition != target.transform.position || lastKnownPosition != Vector3.zero))
            {
                lastKnownPosition = target.transform.position;
                OnTargetChanged.Invoke();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            UpdateTargetPosition(other.gameObject);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            UpdateTargetPosition();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsTargetInRange ? Color.red : Color.green;
            switch(detectionRange.GetType().Name)
            {
                case nameof(SphereCollider):
                    SphereCollider tempSphere = detectionRange as SphereCollider;
                    Gizmos.DrawWireSphere(transform.position, tempSphere.radius);
                    break;
                case nameof(CapsuleCollider):
                    CapsuleCollider tempCapsule = detectionRange as CapsuleCollider;
                    Ray center = new(transform.position, transform.position - tempCapsule.center);
                    Gizmos.DrawRay(center); //draw a ray to the center of the capsule from this.transform
                    //Gizmos.DrawWi(transform.position, temp)
                    break;
                case nameof(BoxCollider):
                    BoxCollider tempBox = detectionRange as BoxCollider;
                    Gizmos.DrawWireCube(transform.position, tempBox.bounds.max);
                    break;
            }
        }
    }
}