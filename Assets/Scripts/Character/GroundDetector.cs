using UnityEngine;
using Scripts.Data;

namespace Scripts.Character
{
    public class GroundDetector : MonoBehaviour
    {
        [SerializeField] private Transform groundCheckPoint;
        [SerializeField] private MovementDataSO movementParameters;

        [SerializeField] private UnityEngine.CharacterController characterController;

        private RaycastHit groundHitInfo;
        private bool isGrounded;
        private readonly Collider[] groundCollidersBuffer = new Collider[8];

        private void Awake()
        {
            if (characterController == null)
            {
                characterController = GetComponent<UnityEngine.CharacterController>();
            }

            if (groundCheckPoint == null)
            {
                Transform foundChild = transform.Find("GroundCheck") ?? transform.Find("ground check") ?? transform.Find("Ground Check");
                if (foundChild != null)
                {
                    groundCheckPoint = foundChild;
                }
            }
        }

        public bool IsGroundedAndStable()
        {
            if (characterController != null && characterController.isGrounded)
            {
                isGrounded = true;
                return true;
            }

            Vector3 checkPosition = groundCheckPoint != null 
                ? groundCheckPoint.position 
                : transform.position + (movementParameters != null ? movementParameters.GroundCheckOffset : new Vector3(0f, -0.9f, 0f));

            float checkRadius = movementParameters != null 
                ? movementParameters.GroundCheckRadius 
                : 0.25f;

            LayerMask groundLayer = movementParameters != null 
                ? movementParameters.GroundLayer 
                : ~0;

            int count = Physics.OverlapSphereNonAlloc(checkPosition, checkRadius, groundCollidersBuffer, groundLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider col = groundCollidersBuffer[i];
                if (col != null && col.transform.root != transform.root && !col.isTrigger)
                {
                    isGrounded = true;
                    return true;
                }
            }

            isGrounded = false;
            return false;
        }

        public bool TryGetGroundHit(out RaycastHit hit)
        {
            Vector3 checkPosition = groundCheckPoint != null 
                ? groundCheckPoint.position 
                : transform.position;

            LayerMask groundLayer = movementParameters != null 
                ? movementParameters.GroundLayer 
                : ~0;

            return Physics.Raycast(checkPosition, Vector3.down, out hit, 1.5f, groundLayer, QueryTriggerInteraction.Ignore);
        }

        public void SetMovementData(MovementDataSO data)
        {
            movementParameters = data;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 checkPosition = groundCheckPoint != null 
                ? groundCheckPoint.position 
                : transform.position + (movementParameters != null ? movementParameters.GroundCheckOffset : new Vector3(0f, -0.9f, 0f));

            float checkRadius = movementParameters != null 
                ? movementParameters.GroundCheckRadius 
                : 0.25f;

            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(checkPosition, checkRadius);
        }
    }
}
