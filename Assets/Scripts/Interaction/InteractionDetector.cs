using UnityEngine;
using Scripts.Core;

namespace Scripts.Interaction
{
    public class InteractionDetector : MonoBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField] private float detectionRadius = 3.0f;
        [SerializeField] private Vector3 detectionOffset = new Vector3(0f, 0.5f, 0f);
        [SerializeField] private LayerMask interactableLayer = ~0;
        [SerializeField] private Transform detectionOrigin;

        private IInteractable currentInteractable;
        private readonly Collider[] hitColliders = new Collider[16];

        public IInteractable CurrentInteractable => currentInteractable;

        private void Awake()
        {
            if (detectionOrigin == null)
            {
                detectionOrigin = transform;
            }
        }

        private void Update()
        {
            DetectNearestInteractable();
        }

        private void DetectNearestInteractable()
        {
            Vector3 origin = (detectionOrigin != null ? detectionOrigin.position : transform.position) + detectionOffset;
            int hits = Physics.OverlapSphereNonAlloc(origin, detectionRadius, hitColliders, interactableLayer, QueryTriggerInteraction.Collide);

            IInteractable closestInteractable = null;
            float closestDistanceSqr = float.MaxValue;

            for (int i = 0; i < hits; i++)
            {
                Collider col = hitColliders[i];
                if (col == null || col.transform.root == transform.root) continue;

                IInteractable interactable = col.GetComponentInParent<IInteractable>();
                if (interactable == null)
                {
                    interactable = col.GetComponentInChildren<IInteractable>();
                }

                if (interactable != null && interactable.CanInteract(gameObject))
                {
                    float distSqr = (col.transform.position - origin).sqrMagnitude;
                    if (distSqr < closestDistanceSqr)
                    {
                        closestDistanceSqr = distSqr;
                        closestInteractable = interactable;
                    }
                }
            }

            // State change in focused interactable
            if (closestInteractable != currentInteractable)
            {
                currentInteractable?.OnFocusLost();
                currentInteractable = closestInteractable;
                currentInteractable?.OnFocusGained();
            }
        }

        public bool TryInteract()
        {
            // Immediate fresh detection on interaction request
            DetectNearestInteractable();

            if (currentInteractable != null && currentInteractable.CanInteract(gameObject))
            {
                IInteractable target = currentInteractable;
                currentInteractable = null;
                target.Interact(gameObject);
                return true;
            }

            Debug.LogWarning($"[InteractionDetector] No hay ningún objeto interactuable dentro del radio ({detectionRadius}m)!");
            return false;
        }

        private void OnDisable()
        {
            if (currentInteractable != null)
            {
                currentInteractable.OnFocusLost();
                currentInteractable = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = detectionOrigin != null ? detectionOrigin.position : transform.position;
            Gizmos.color = currentInteractable != null ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(origin, detectionRadius);
        }
    }
}
