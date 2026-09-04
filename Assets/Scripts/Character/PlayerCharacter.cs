using UnityEngine;
using Scripts.Combat;
using Scripts.Core;
using Scripts.Data;
using Scripts.Input;
using Scripts.StateMachine;
using Scripts.StateMachine.Evasive;
using Scripts.StateMachine.Locomotion;
using Scripts.StateMachine.Tactical;
using Scripts.Interaction;

namespace Scripts.Character
{
    [RequireComponent(typeof(UnityEngine.CharacterController))]
    [RequireComponent(typeof(PlayerStateMachine))]
    [RequireComponent(typeof(GroundDetector))]
    [RequireComponent(typeof(CombatCommandQueue))]
    [RequireComponent(typeof(InteractionDetector))]
    public class PlayerCharacter : MonoBehaviour, IDamageable
    {
        [Header("Character Profile & Data (Flyweight)")]
        [SerializeField] private CharacterDataSO characterProfile;
        [SerializeField] private MovementDataSO fallbackMovementData;

        [Header("System Dependencies")]
        [SerializeField] private InputReader inputReader;

        [Header("Internal References")]
        [SerializeField] private GroundDetector groundDetector;
        [SerializeField] private PlayerStateMachine stateMachine;
        [SerializeField] private CombatCommandQueue commandQueue;
        [SerializeField] private InteractionDetector interactionDetector;
        [SerializeField] private UnityEngine.CharacterController characterController;

        [Header("Visual Representation & Stance Transition")]
        [SerializeField] private Transform visualModel;
        [SerializeField] private float stanceTransitionSpeed = 12.0f;

        [Header("Camera & Direction Alignment")]
        [SerializeField] private bool shouldFaceMoveDirection = true;
        public Transform cameraTransform;


        private float targetStanceHeight = 2.0f;
        private Vector3 targetStanceCenter = Vector3.zero;
        private float currentStanceHeight = 2.0f;
        private Vector3 currentStanceCenter = Vector3.zero;

        // Current runtime physical values
        private Vector3 currentVelocity;
        private float verticalVelocity;
        private float currentHealth;
        private bool isInvulnerable;
        private float rotationVelocity;

        // Concrete States instances
        public IdleState IdleState { get; private set; }
        public WalkingState WalkingState { get; private set; }
        public SprintingState SprintingState { get; private set; }
        public JumpingState JumpingState { get; private set; }
        public CrouchingState CrouchingState { get; private set; }
        public ProneState ProneState { get; private set; }
        public SlidingState SlidingState { get; private set; }
        public RollingState RollingState { get; private set; }

        public MovementDataSO ActiveMovementData => 
            characterProfile != null && characterProfile.MovementParameters != null 
                ? characterProfile.MovementParameters 
                : fallbackMovementData;

        public CombatCommandQueue CommandQueue => commandQueue;
        public GroundDetector GroundDetector => groundDetector;
        public InteractionDetector InteractionDetector => interactionDetector;
        public bool IsInvulnerable => isInvulnerable;
        public float VerticalVelocity => verticalVelocity;
        public bool ShouldFaceMoveDirection
        {
            get => shouldFaceMoveDirection;
            set => shouldFaceMoveDirection = value;
        }

        // Visual and audio observer events (SRP)
        public event System.Action AttackTriggeredEvent;
        public event System.Action InteractTriggeredEvent;

        private void Awake()
        {
            if (characterController == null)
            {
                characterController = GetComponent<UnityEngine.CharacterController>();
            }

            if (stateMachine == null)
            {
                stateMachine = GetComponent<PlayerStateMachine>();
            }

            if (groundDetector == null)
            {
                groundDetector = GetComponent<GroundDetector>();
            }

            if (commandQueue == null)
            {
                commandQueue = GetComponent<CombatCommandQueue>();
            }

            if (interactionDetector == null)
            {
                interactionDetector = GetComponent<InteractionDetector>();
                if (interactionDetector == null)
                {
                    interactionDetector = gameObject.AddComponent<InteractionDetector>();
                }
            }


            // Disable redundant CapsuleCollider if present to avoid blocking crouch/prone
            CapsuleCollider redundantCollider = GetComponent<CapsuleCollider>();
            if (redundantCollider != null)
            {
                redundantCollider.enabled = false;
            }

            if (cameraTransform == null && UnityEngine.Camera.main != null)
            {
                cameraTransform = UnityEngine.Camera.main.transform;
            }

            SetupVisualModel();

            targetStanceHeight = characterController != null ? characterController.height : 2.0f;
            targetStanceCenter = characterController != null ? characterController.center : Vector3.zero;
            currentStanceHeight = targetStanceHeight;
            currentStanceCenter = targetStanceCenter;

            if (characterController != null)
            {
                baseRadius = characterController.radius;
            }

            InitializeCharacterProfile();
            InitializeStates();
        }

        private void OnEnable()
        {
            if (inputReader != null)
            {
                inputReader.EnablePlayerInput();
                inputReader.JumpStartedEvent += HandleJumpStarted;
                inputReader.CrouchPerformedEvent += HandleCrouchPerformed;
                inputReader.RollPerformedEvent += HandleRollPerformed;
                inputReader.AttackPerformedEvent += HandleAttackPerformed;
                inputReader.InteractPerformedEvent += HandleInteractPerformed;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.JumpStartedEvent -= HandleJumpStarted;
                inputReader.CrouchPerformedEvent -= HandleCrouchPerformed;
                inputReader.RollPerformedEvent -= HandleRollPerformed;
                inputReader.AttackPerformedEvent -= HandleAttackPerformed;
                inputReader.InteractPerformedEvent -= HandleInteractPerformed;
            }
        }

        private void Start()
        {
            stateMachine.Initialize(IdleState);
        }

        private void Update()
        {
            UpdateStanceDimensions();
            stateMachine.Tick();
        }

        private void FixedUpdate()
        {
            stateMachine.FixedTick();
        }

        private void InitializeCharacterProfile()
        {
            if (characterProfile != null)
            {
                currentHealth = characterProfile.MaxHealth;
                if (groundDetector != null)
                {
                    groundDetector.SetMovementData(ActiveMovementData);
                }
            }
            else if (fallbackMovementData != null && groundDetector != null)
            {
                groundDetector.SetMovementData(fallbackMovementData);
            }
        }

        private void InitializeStates()
        {
            IdleState = new IdleState(this, stateMachine, inputReader);
            WalkingState = new WalkingState(this, stateMachine, inputReader);
            SprintingState = new SprintingState(this, stateMachine, inputReader);
            JumpingState = new JumpingState(this, stateMachine, inputReader);
            CrouchingState = new CrouchingState(this, stateMachine, inputReader);
            ProneState = new ProneState(this, stateMachine, inputReader);
            SlidingState = new SlidingState(this, stateMachine, inputReader);
            RollingState = new RollingState(this, stateMachine, inputReader);
        }

        #region Input Observer Handlers

        private void HandleJumpStarted()
        {
            // Bloquea estrictamente el salto si ya está en el aire (JumpingState) o en posturas evasivas/prone
            if (stateMachine.CurrentState == JumpingState ||
                stateMachine.CurrentState == RollingState || 
                stateMachine.CurrentState == ProneState || 
                stateMachine.CurrentState == SlidingState)
            {
                return;
            }

            if (IsGrounded())
            {
                stateMachine.ChangeState(JumpingState);
            }
        }

        private void HandleCrouchPerformed()
        {
            if (stateMachine.CurrentState == CrouchingState)
            {
                // Al presionar C estando en Crouch, pasa a Prone (cuerpo a tierra)
                stateMachine.ChangeState(ProneState);
            }
            else if (stateMachine.CurrentState == ProneState)
            {
                // Al presionar C estando en Prone, se levanta de nuevo a Standing (Walk o Idle)
                stateMachine.ChangeState(inputReader.CurrentMoveInput.sqrMagnitude > 0.01f ? WalkingState : IdleState);
            }
            else if (stateMachine.CurrentState == SprintingState)
            {
                stateMachine.ChangeState(SlidingState);
            }
            else if (stateMachine.CurrentState == WalkingState || stateMachine.CurrentState == IdleState)
            {
                stateMachine.ChangeState(CrouchingState);
            }
        }

        private void HandleRollPerformed()
        {
            if (stateMachine.CurrentState != RollingState && stateMachine.CurrentState != SlidingState)
            {
                stateMachine.ChangeState(RollingState);
            }
        }

        private void HandleAttackPerformed()
        {
            float bufferTime = ActiveMovementData != null ? ActiveMovementData.CommandBufferDuration : 0.35f;
            commandQueue.EnqueueCommand(new AttackCommand(this, bufferTime));
            commandQueue.TryExecuteNextCommand();
            AttackTriggeredEvent?.Invoke();
        }

        private void HandleInteractPerformed()
        {
            Debug.Log("[PlayerCharacter] ¡Pulsación de Interactuar (E) detectada!");
            if (interactionDetector != null)
            {
                interactionDetector.TryInteract();
            }

            float bufferTime = ActiveMovementData != null ? ActiveMovementData.CommandBufferDuration : 0.35f;
            commandQueue.EnqueueCommand(new InteractCommand(this, bufferTime));
            commandQueue.TryExecuteNextCommand();
            InteractTriggeredEvent?.Invoke();
        }

        #endregion

        #region Physical Movement Utilities

        public void AccelerateTowards(Vector3 targetDirection, float targetSpeed, float rate)
        {
            Vector3 targetVelocity = targetDirection * targetSpeed;
            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);
        }

        public void Decelerate(float rate)
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, rate * Time.fixedDeltaTime);
        }

        public void SetVerticalVelocity(float velocity)
        {
            verticalVelocity = velocity;
        }

        public void SetHorizontalVelocity(Vector3 velocity)
        {
            currentVelocity = velocity;
        }

        public void ResetHorizontalVelocity()
        {
            currentVelocity = Vector3.zero;
        }

        public Vector3 CalculateWorldMovementDirection(Vector2 moveInput)
        {
            if (moveInput.sqrMagnitude < 0.001f)
            {
                return Vector3.zero;
            }

            if (cameraTransform == null && UnityEngine.Camera.main != null)
            {
                cameraTransform = UnityEngine.Camera.main.transform;
            }

            if (cameraTransform != null)
            {
                // 1. Obtener la dirección relativa de la cámara
                Vector3 forward = cameraTransform.forward;
                Vector3 right = cameraTransform.right;

                forward.y = 0f;
                right.y = 0f;

                forward.Normalize();
                right.Normalize();

                // 2. Calcular la dirección de movimiento basada en los inputs y la cámara
                return ((forward * moveInput.y) + (right * moveInput.x)).normalized;
            }

            return new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        }

        public void RotateTowards(Vector3 targetDirection, float smoothTime)
        {
            if (!shouldFaceMoveDirection || targetDirection.sqrMagnitude < 0.001f) return;

            float targetAngle = Mathf.Atan2(targetDirection.x, targetDirection.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, smoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        public void RotateTowardsSlerp(Vector3 targetDirection, float slerpSpeed)
        {
            if (!shouldFaceMoveDirection || targetDirection.sqrMagnitude < 0.001f) return;

            Quaternion toRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, toRotation, slerpSpeed * Time.deltaTime);
        }

        public bool IsGrounded()
        {
            if (characterController != null && characterController.isGrounded)
            {
                return true;
            }

            return groundDetector != null && groundDetector.IsGroundedAndStable();
        }

        public void ApplyGravity()
        {
            bool grounded = IsGrounded();

            if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f; // Slight downward force to keep grounded on uneven terrain
            }
            else
            {
                float gravityMultiplier = ActiveMovementData != null ? ActiveMovementData.GravityMultiplier : 2.0f;
                verticalVelocity += Physics.gravity.y * gravityMultiplier * Time.fixedDeltaTime;
            }
        }

        public void MoveWithCurrentVelocity()
        {
            Vector3 motion = (currentVelocity + new Vector3(0f, verticalVelocity, 0f)) * Time.fixedDeltaTime;
            characterController.Move(motion);
        }

        public void SetStanceDimensions(float targetHeight, Vector3 targetCenter)
        {
            targetStanceHeight = targetHeight;

            // Anclaje matemático automático a la base de los pies (suelo):
            // baseFeetY = standingCenter.y - (standingHeight / 2)
            // Para cualquier altura H: center.y = baseFeetY + (H / 2)
            // Esto garantiza que el fondo de la cápsula y del modelo 3D siempre toquen el suelo con precisión milimétrica sin flotar.
            float standingHeight = ActiveMovementData != null ? ActiveMovementData.StandingHeight : 2.0f;
            Vector3 standingCenter = ActiveMovementData != null ? ActiveMovementData.StandingCenter : Vector3.zero;
            float baseFeetY = standingCenter.y - (standingHeight / 2.0f);

            float anchoredCenterY = baseFeetY + (targetHeight / 2.0f);
            targetStanceCenter = new Vector3(targetCenter.x, anchoredCenterY, targetCenter.z);
        }

        private void SetupVisualModel()
        {
            if (visualModel != null) return;

            Transform existingChild = transform.Find("Body") ?? transform.Find("body") ?? transform.Find("VisualModel");
            if (existingChild != null)
            {
                visualModel = existingChild;
                return;
            }

            // If MeshFilter and MeshRenderer are directly on this root GameObject, move them to a child
            MeshFilter rootFilter = GetComponent<MeshFilter>();
            MeshRenderer rootRenderer = GetComponent<MeshRenderer>();
            if (rootFilter != null && rootRenderer != null)
            {
                GameObject childObj = new GameObject("VisualModel");
                childObj.transform.SetParent(transform, false);

                MeshFilter childFilter = childObj.AddComponent<MeshFilter>();
                childFilter.sharedMesh = rootFilter.sharedMesh;

                MeshRenderer childRenderer = childObj.AddComponent<MeshRenderer>();
                childRenderer.sharedMaterials = rootRenderer.sharedMaterials;

                Destroy(rootRenderer);
                Destroy(rootFilter);

                visualModel = childObj.transform;
            }
        }

        private float baseRadius = 0.5f;

        private void UpdateStanceDimensions()
        {
            if (characterController == null) return;

            currentStanceHeight = Mathf.Lerp(currentStanceHeight, targetStanceHeight, Time.deltaTime * stanceTransitionSpeed);
            currentStanceCenter = Vector3.Lerp(currentStanceCenter, targetStanceCenter, Time.deltaTime * stanceTransitionSpeed);

            // Restricción crítica de Unity: CharacterController.height NO PUEDE ser menor que 2 * radius.
            // Para permitir alturas bajas como 0.5m o 0.21m sin que Unity las bloquee en 1.0m, adaptamos el radio:
            float maxAllowedRadius = currentStanceHeight * 0.48f;
            characterController.radius = Mathf.Min(baseRadius, maxAllowedRadius);

            characterController.height = currentStanceHeight;
            characterController.center = currentStanceCenter;

            if (visualModel != null)
            {
                float baseHeight = ActiveMovementData != null ? ActiveMovementData.StandingHeight : 2.0f;
                float scaleY = Mathf.Max(0.05f, currentStanceHeight / baseHeight);
                float scaleXZ = Mathf.Clamp(characterController.radius / baseRadius, 0.4f, 1.0f);
                visualModel.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);
                visualModel.localPosition = currentStanceCenter;
            }
        }

        public void SetInvulnerability(bool active)
        {
            isInvulnerable = active;
        }

        #endregion

        #region IDamageable Implementation

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (isInvulnerable)
            {
                Debug.Log("[PlayerCharacter] Damage negated due to active invulnerability window!");
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            Debug.Log($"[PlayerCharacter] Took {amount} damage. Current health: {currentHealth}");
        }

        public bool IsAlive()
        {
            return currentHealth > 0f;
        }

        #endregion
    }
}
