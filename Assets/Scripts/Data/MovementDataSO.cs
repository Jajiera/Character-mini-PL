using UnityEngine;

namespace Scripts.Data
{
    [CreateAssetMenu(fileName = "MovementData", menuName = "Character/Data/Movement Data")]
    public class MovementDataSO : ScriptableObject
    {
        [Header("Locomotion Speeds")]
        [Tooltip("Velocidad máxima horizontal (m/s) al caminar de pie.")]
        [SerializeField] private float walkSpeed = 4.5f;

        [Tooltip("Velocidad máxima horizontal (m/s) al correr (Sprint).")]
        [SerializeField] private float sprintSpeed = 7.5f;

        [Tooltip("Velocidad máxima horizontal (m/s) al moverse agachado (Crouch).")]
        [SerializeField] private float crouchSpeed = 2.2f;

        [Tooltip("Velocidad máxima horizontal (m/s) al arrastrarse cuerpo a tierra (Prone).")]
        [SerializeField] private float proneSpeed = 1.2f;

        [Tooltip("Velocidad de impulso inicial (m/s) al ejecutar un deslizamiento (Slide).")]
        [SerializeField] private float slideInitialSpeed = 9.0f;

        [Tooltip("Velocidad horizontal (m/s) mantenida durante la voltereta de esquiva (Roll).")]
        [SerializeField] private float rollSpeed = 8.0f;

        [Header("Acceleration & Smoothing")]
        [Tooltip("Tasa de aceleración (m/s²) para alcanzar la velocidad deseada. Valores más altos = respuesta más reactiva/arcade.")]
        [SerializeField] private float acceleration = 12.0f;

        [Tooltip("Tasa de frenado (m/s²) aplicada en el deslizamiento (Slide) y al reducir marcha en posturas tácticas.")]
        [SerializeField] private float deceleration = 14.0f;

        [Tooltip("Tiempo de suavizado (segundos) para que el personaje encare la dirección de desplazamiento.")]
        [SerializeField] private float rotationSmoothTime = 0.1f;

        [Header("Physics, Gravity & Jump")]
        [Tooltip("Multiplicador aplicado a la gravedad global de Unity (Physics.gravity.y) en caídas.")]
        [SerializeField] private float gravityMultiplier = 2.0f;

        [Tooltip("Velocidad vertical inicial (impulso hacia arriba) aplicada al momento de saltar.")]
        [SerializeField] private float jumpForce = 7.0f;

        [Tooltip("Multiplicador de maniobrabilidad horizontal mientras se está en el aire (0 = inercia fija, 1 = control total).")]
        [SerializeField] private float airControl = 0.5f;


        [Tooltip("Máscara de capas (LayerMask) reconocidas como superficie transitable por el GroundDetector.")]
        [SerializeField] private LayerMask groundLayer = ~0;

        [Header("Stance Dimensions (CharacterController)")]
        [Tooltip("Altura (m) del CharacterController y escala visual del modelo al estar de pie.")]
        [SerializeField] private float standingHeight = 2.0f;

        [Tooltip("Centro local (X, Y, Z) de la cápsula del CharacterController al estar de pie.")]
        [SerializeField] private Vector3 standingCenter = new Vector3(0f, 0f, 0f);

        [Tooltip("Altura (m) del CharacterController al agacharse, deslizarse o rodar.")]
        [SerializeField] private float crouchingHeight = 1.3f;

        [Tooltip("Centro local (X, Y, Z) de la cápsula del CharacterController al agacharse.")]
        [SerializeField] private Vector3 crouchingCenter = new Vector3(0f, -0.35f, 0f);

        [Tooltip("Altura (m) del CharacterController al estar tendido cuerpo a tierra (Prone).")]
        [SerializeField] private float proneHeight = 0.6f;

        [Tooltip("Centro local (X, Y, Z) de la cápsula del CharacterController al estar cuerpo a tierra.")]
        [SerializeField] private Vector3 proneCenter = new Vector3(0f, -0.7f, 0f);

        [Header("Action Durations & Buffer")]
        [Tooltip("Duración máxima en segundos de la acción de deslizamiento (Slide).")]
        [SerializeField] private float slideDuration = 0.8f;

        [Tooltip("Duración en segundos de la voltereta (Roll) y ventana de invulnerabilidad asociada.")]
        [SerializeField] private float rollDuration = 0.6f;

        [Tooltip("Ventana de tiempo (segundos) durante la cual las acciones (ataques, interacción) se retienen en cola antes de descartarse.")]
        [SerializeField] private float commandBufferDuration = 0.35f;

        // Public getters to enforce immutable data abstraction
        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => sprintSpeed;
        public float CrouchSpeed => crouchSpeed;
        public float ProneSpeed => proneSpeed;
        public float SlideInitialSpeed => slideInitialSpeed;
        public float RollSpeed => rollSpeed;

        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float RotationSmoothTime => rotationSmoothTime;

        public float GravityMultiplier => gravityMultiplier;
        public float JumpForce => jumpForce;
        public float AirControl => airControl;
        public LayerMask GroundLayer => groundLayer;

        public float StandingHeight => standingHeight;
        public Vector3 StandingCenter => standingCenter;
        public float CrouchingHeight => crouchingHeight;
        public Vector3 CrouchingCenter => crouchingCenter;
        public float ProneHeight => proneHeight;
        public Vector3 ProneCenter => proneCenter;

        public float SlideDuration => slideDuration;
        public float RollDuration => rollDuration;
        public float CommandBufferDuration => commandBufferDuration;
    }
}
