using UnityEngine;

namespace Scripts.Data
{
    [CreateAssetMenu(fileName = "MovementData", menuName = "Character/Data/Movement Data")]
    public class MovementDataSO : ScriptableObject
    {
        [Header("Locomotion Speeds")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float sprintSpeed = 7.5f;
        [SerializeField] private float crouchSpeed = 2.2f;
        [SerializeField] private float proneSpeed = 1.2f;
        [SerializeField] private float slideInitialSpeed = 9.0f;
        [SerializeField] private float rollSpeed = 8.0f;

        [Header("Acceleration & Smoothing")]
        [SerializeField] private float acceleration = 12.0f;
        [SerializeField] private float deceleration = 14.0f;
        [SerializeField] private float rotationSmoothTime = 0.1f;

        [Header("Physics, Gravity & Jump")]
        [SerializeField] private float gravityMultiplier = 2.0f;
        [SerializeField] private float jumpForce = 7.0f;
        [SerializeField] private float airControl = 0.5f;
        [SerializeField] private float groundCheckRadius = 0.25f;
        [SerializeField] private Vector3 groundCheckOffset = new Vector3(0f, -0.9f, 0f);
        [SerializeField] private LayerMask groundLayer = ~0;

        [Header("Stance Dimensions (CharacterController)")]
        [SerializeField] private float standingHeight = 2.0f;
        [SerializeField] private Vector3 standingCenter = new Vector3(0f, 0f, 0f);
        [SerializeField] private float crouchingHeight = 1.3f;
        [SerializeField] private Vector3 crouchingCenter = new Vector3(0f, -0.35f, 0f);
        [SerializeField] private float proneHeight = 0.6f;
        [SerializeField] private Vector3 proneCenter = new Vector3(0f, -0.7f, 0f);

        [Header("Action Durations & Buffer")]
        [SerializeField] private float slideDuration = 0.8f;
        [SerializeField] private float rollDuration = 0.6f;
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
        public float GroundCheckRadius => groundCheckRadius;
        public Vector3 GroundCheckOffset => groundCheckOffset;
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
