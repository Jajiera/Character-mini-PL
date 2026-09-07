using UnityEngine;
using Scripts.Data;

namespace Scripts.Character
{
    /// <summary>
    /// Sistema de detección de suelo de precisión cinemática mediante Physics.SphereCast.
    /// Diseñado para adaptarse automáticamente a las posturas (Standing, Crouch, Prone) 
    /// utilizando la geometría real de la cápsula del CharacterController, validando pendientes
    /// según el slopeLimit y sin necesidad de configuración manual en el Inspector.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.CharacterController))]
    public class GroundDetector : MonoBehaviour
    {
        private UnityEngine.CharacterController characterController;
        private MovementDataSO movementParameters;

        private RaycastHit groundHitInfo;
        private bool isGrounded;
        private bool isStableOnSlope = true;
        private Vector3 surfaceNormal = Vector3.up;
        private float surfaceAngle;

        public bool IsGrounded => isGrounded;
        public bool IsStableOnSlope => isStableOnSlope;
        public Vector3 SurfaceNormal => surfaceNormal;
        public float SurfaceAngle => surfaceAngle;
        public RaycastHit GroundHit => groundHitInfo;

        private void Awake()
        {
            if (characterController == null)
            {
                characterController = GetComponent<UnityEngine.CharacterController>();
            }
        }

        /// <summary>
        /// Evalúa el contacto con el suelo mediante SphereCast y verifica si la superficie es transitable.
        /// </summary>
        public bool IsGroundedAndStable()
        {
            if (characterController == null)
            {
                characterController = GetComponent<UnityEngine.CharacterController>();
                if (characterController == null) return false;
            }

            // 1. Cálculo dinámico del origen basado en la semiesfera inferior de la cápsula
            Vector3 capsuleCenterWorld = transform.position + characterController.center;
            float bottomHemisphereOffset = (characterController.height * 0.5f) - characterController.radius;
            if (bottomHemisphereOffset < 0f) bottomHemisphereOffset = 0f;

            Vector3 origin = capsuleCenterWorld - (Vector3.up * bottomHemisphereOffset);

            // Radio ligeramente menor que el del CharacterController para evitar enganches con paredes verticales
            float sphereRadius = Mathf.Max(0.05f, characterController.radius * 0.9f);
            
            // Distancia de proyección: margen de la piel (skinWidth) + tolerancia para terreno irregular
            float castDistance = characterController.skinWidth + 0.15f;

            LayerMask groundLayer = movementParameters != null ? movementParameters.GroundLayer : ~0;

            // 2. Proyección de la esfera hacia abajo
            bool hit = Physics.SphereCast(
                origin, 
                sphereRadius, 
                Vector3.down, 
                out groundHitInfo, 
                castDistance, 
                groundLayer, 
                QueryTriggerInteraction.Ignore);

            // Si detecta impacto, verificar que no sea el propio personaje
            if (hit && groundHitInfo.collider.transform.root == transform.root)
            {
                hit = false;
            }

            if (hit)
            {
                surfaceNormal = groundHitInfo.normal;
                surfaceAngle = Vector3.Angle(surfaceNormal, Vector3.up);

                // Validar pendiente contra el límite configurado en el CharacterController
                isStableOnSlope = surfaceAngle <= characterController.slopeLimit;
                isGrounded = isStableOnSlope;
                return isGrounded;
            }

            // 3. Fallback complementario: si el CharacterController nativo detectó suelo
            if (characterController.isGrounded)
            {
                // Intentar recuperar la normal mediante un raycast vertical rápido
                if (Physics.Raycast(origin, Vector3.down, out groundHitInfo, castDistance + sphereRadius, groundLayer, QueryTriggerInteraction.Ignore))
                {
                    if (groundHitInfo.collider.transform.root != transform.root)
                    {
                        surfaceNormal = groundHitInfo.normal;
                        surfaceAngle = Vector3.Angle(surfaceNormal, Vector3.up);
                        isStableOnSlope = surfaceAngle <= characterController.slopeLimit;
                        isGrounded = isStableOnSlope;
                        return isGrounded;
                    }
                }

                // Si no hay normal recuperable, asumir superficie plana estable
                surfaceNormal = Vector3.up;
                surfaceAngle = 0f;
                isStableOnSlope = true;
                isGrounded = true;
                return true;
            }

            // 4. En el aire
            isGrounded = false;
            isStableOnSlope = false;
            surfaceNormal = Vector3.up;
            surfaceAngle = 0f;
            return false;
        }

        public bool TryGetGroundHit(out RaycastHit hit)
        {
            hit = groundHitInfo;
            return isGrounded;
        }

        public void SetMovementData(MovementDataSO data)
        {
            movementParameters = data;
        }

        private void OnDrawGizmosSelected()
        {
            if (characterController == null)
            {
                characterController = GetComponent<UnityEngine.CharacterController>();
                if (characterController == null) return;
            }

            Vector3 capsuleCenterWorld = transform.position + characterController.center;
            float bottomHemisphereOffset = (characterController.height * 0.5f) - characterController.radius;
            if (bottomHemisphereOffset < 0f) bottomHemisphereOffset = 0f;

            Vector3 origin = capsuleCenterWorld - (Vector3.up * bottomHemisphereOffset);
            float sphereRadius = Mathf.Max(0.05f, characterController.radius * 0.9f);
            float castDistance = characterController.skinWidth + 0.15f;

            if (isGrounded && isStableOnSlope)
            {
                Gizmos.color = Color.green;
            }
            else if (isGrounded && !isStableOnSlope)
            {
                Gizmos.color = Color.yellow; // Pendiente resbaladiza
            }
            else
            {
                Gizmos.color = Color.red; // En el aire
            }

            // Esfera de origen del cast
            Gizmos.DrawWireSphere(origin, sphereRadius);
            // Trayecto
            Gizmos.DrawLine(origin, origin + (Vector3.down * castDistance));
            // Esfera final / proyección
            Vector3 endPos = isGrounded ? groundHitInfo.point + (Vector3.up * sphereRadius) : origin + (Vector3.down * castDistance);
            Gizmos.DrawWireSphere(endPos, sphereRadius);

            // Dibujar vector normal si está tocando suelo
            if (isGrounded)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(groundHitInfo.point, surfaceNormal * 0.75f);
            }
        }
    }
}
