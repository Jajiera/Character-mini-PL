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
        #region Serialized Fields - Profiles & Dependencies

        [Header("Character Profile & Data (Flyweight)")]
        [SerializeField] private CharacterDataSO characterProfile;
        [SerializeField] private MovementDataSO fallbackMovementData;

        [Header("System Dependencies")]
        [SerializeField] private InputReader inputReader;

        [Header("Internal Component References")]
        [HideInInspector] [SerializeField] private GroundDetector groundDetector;
        [SerializeField] private PlayerStateMachine stateMachine;
        [SerializeField] private CombatCommandQueue commandQueue;
        [SerializeField] private InteractionDetector interactionDetector;
        [SerializeField] private UnityEngine.CharacterController characterController;

        #endregion

        #region Serialized Fields - Visuals & Stance

        [Header("Visual Representation & Stance Transition")]
        [SerializeField] private Transform visualModel;
        [SerializeField] private float stanceTransitionSpeed = 12.0f;

        #endregion

        #region Serialized Fields - Camera & Aiming

        [Header("Camera & Direction Alignment")]
        [Tooltip("Si es false, el personaje siempre le da la espalda a la cámara y hace strafe.")]
        [SerializeField] private bool shouldFaceMoveDirection = false;
        public Transform cameraTransform;

        [Header("Aim & Target Tracking")]
        [SerializeField] private Transform eyeTarget;
        [Tooltip("Referencia opcional a la mira (miraDisparo). Si se deja vacío se auto-detecta.")]
        [SerializeField] private GameObject crosshairUI;
        [SerializeField] private float aimSensitivityX = 0.15f;
        [SerializeField] private float aimSensitivityY = 0.15f;
        [SerializeField] private float aimPitchMin = -45f;
        [SerializeField] private float aimPitchMax = 60f;

        #endregion

        #region Serialized Fields - Combat & Weapon

        [Header("Combat & Weapon References")]
        [Tooltip("Arma equipada (ej. ArcadeGun con ProjectileWeapon). Si se deja vacío se auto-detecta.")]
        [SerializeField] private Weapon currentWeapon;

        [Header("Combat & Attack Charging (Fallbacks)")]
        [Tooltip("Si es true y el arma actual lo permite, el personaje acumulará carga al mantener presionado el botón.")]
        [SerializeField] private bool allowCharging = true;
        [SerializeField] private float chargeActivationDelay = 0.2f;
        [SerializeField] private float maxAttackChargeTime = 1.5f;
        [SerializeField] private float baseAttackDamage = 15f;
        [SerializeField] private float maxAttackDamage = 45f;

        #endregion

        #region Runtime State Fields

        // Runtime Physics & Health
        private Vector3 currentVelocity;
        private float verticalVelocity;
        private float currentHealth;
        private bool isInvulnerable;
        private float rotationVelocity;
        private float currentAimPitch = 0f;
        private UnityEngine.Camera cachedMainCamera;

        // Capsule & Stance Dimension Management
        private float targetStanceHeight = 2.0f;
        private Vector3 targetStanceCenter = Vector3.zero;
        private float currentStanceHeight = 2.0f;
        private Vector3 currentStanceCenter = Vector3.zero;
        private float baseRadius = 0.5f;
        private bool isStanceTransitioning = false;

        // Attack Charge State
        private bool isHoldingAttack = false;
        private float attackHoldTimer = 0f;
        private bool isChargingAttack = false;
        private bool maxChargeReachedLogged = false;

        #endregion

        #region Public Properties

        // Concrete States Instances
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
        public Vector3 CurrentVelocity => currentVelocity;

        public bool ShouldFaceMoveDirection
        {
            get => shouldFaceMoveDirection;
            set => shouldFaceMoveDirection = value;
        }

        public bool IsAiming => inputReader != null && inputReader.IsAiming;
        public Transform EyeTarget => eyeTarget;

        public GameObject CrosshairUI
        {
            get => crosshairUI;
            set => crosshairUI = value;
        }

        public Weapon CurrentWeapon
        {
            get => currentWeapon;
            set
            {
                currentWeapon = value;
                if (currentWeapon != null)
                {
                    currentWeapon.SetOwner(this);
                }
            }
        }

        // Combat Parameters (Prioritizing Weapon Definition, Fallback to Character)
        public bool AllowCharging => currentWeapon != null ? currentWeapon.AllowCharging : allowCharging;
        public float ChargeActivationDelay => currentWeapon != null ? currentWeapon.ChargeActivationDelay : chargeActivationDelay;
        public float BaseAttackDamage => currentWeapon != null ? currentWeapon.BaseDamage : baseAttackDamage;
        public float MaxAttackDamage => currentWeapon != null ? currentWeapon.MaxDamage : maxAttackDamage;
        public float MaxAttackChargeTime => currentWeapon != null ? currentWeapon.MaxAttackChargeTime : maxAttackChargeTime;

        // Combat Runtime Info
        public bool IsHoldingAttack => isHoldingAttack;
        public bool IsChargingAttack => isChargingAttack;
        public float CurrentAttackChargeTimer => isChargingAttack ? Mathf.Max(0f, attackHoldTimer - ChargeActivationDelay) : 0f;
        public float AttackChargeRatio => (isChargingAttack && MaxAttackChargeTime > 0f)
            ? Mathf.Clamp01((attackHoldTimer - ChargeActivationDelay) / MaxAttackChargeTime)
            : 0f;

        public int CurrentAmmo => currentWeapon != null ? currentWeapon.CurrentAmmo : 0;
        public int CartridgeCapacity => currentWeapon != null ? currentWeapon.CartridgeCapacity : 0;
        public bool IsReloading => currentWeapon != null && currentWeapon.IsReloading;
        public float ReloadProgress => currentWeapon != null ? currentWeapon.ReloadProgress : 0f;

        #endregion

        #region Observer Events (SRP)

        public event System.Action AttackTriggeredEvent;
        public event System.Action<float> AttackReleasedEvent;
        public event System.Action AttackChargeStartedEvent;
        public event System.Action AttackMaxChargeReachedEvent;
        public event System.Action InteractTriggeredEvent;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeDependencies();
            InitializeVisualsAndCamera();
            EnsureCurrentWeapon();
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
                inputReader.AttackStartedEvent += HandleAttackStarted;
                inputReader.AttackCanceledEvent += HandleAttackCanceled;
                inputReader.InteractPerformedEvent += HandleInteractPerformed;
                inputReader.AimEvent += HandleAimEvent;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.JumpStartedEvent -= HandleJumpStarted;
                inputReader.CrouchPerformedEvent -= HandleCrouchPerformed;
                inputReader.RollPerformedEvent -= HandleRollPerformed;
                inputReader.AttackStartedEvent -= HandleAttackStarted;
                inputReader.AttackCanceledEvent -= HandleAttackCanceled;
                inputReader.InteractPerformedEvent -= HandleInteractPerformed;
                inputReader.AimEvent -= HandleAimEvent;
            }

            // Limpieza estricta de estado de ataque al deshabilitar
            isHoldingAttack = false;
            isChargingAttack = false;
            attackHoldTimer = 0f;
            maxChargeReachedLogged = false;

            if (crosshairUI != null)
            {
                crosshairUI.SetActive(false);
            }
        }

        private void Start()
        {
            FindCrosshairIfNull();
            if (crosshairUI != null)
            {
                crosshairUI.SetActive(false);
            }

            EnsureCurrentWeapon();
            stateMachine.Initialize(IdleState);
        }

        private void Update()
        {
            UpdateStanceDimensions();
            UpdateAimOrientation();
            UpdateAttackCharge();
            stateMachine.Tick();
        }

        private void FixedUpdate()
        {
            stateMachine.FixedTick();
        }

        private void LateUpdate()
        {
            AlignWithCameraHeading();
        }

        #endregion

        #region Initialization Helpers

        private void InitializeDependencies()
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
                groundDetector = GetComponent<GroundDetector>() ?? gameObject.AddComponent<GroundDetector>();
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

            if (GetComponent<CharacterVisualFeedback>() == null)
            {
                gameObject.AddComponent<CharacterVisualFeedback>();
            }

            // Desactivar CapsuleCollider adicional si existiera para evitar interferencias
            if (TryGetComponent<CapsuleCollider>(out var redundantCollider))
            {
                redundantCollider.enabled = false;
            }
        }

        private void InitializeVisualsAndCamera()
        {
            cachedMainCamera = UnityEngine.Camera.main;
            if (cameraTransform == null && cachedMainCamera != null)
            {
                cameraTransform = cachedMainCamera.transform;
            }

            if (eyeTarget == null)
            {
                eyeTarget = transform.Find("EyeTarget");
            }

            SetupVisualModelReference();

            if (characterController != null)
            {
                baseRadius = characterController.radius;
                targetStanceHeight = characterController.height;
                targetStanceCenter = characterController.center;
                currentStanceHeight = targetStanceHeight;
                currentStanceCenter = targetStanceCenter;
            }
        }

        private void SetupVisualModelReference()
        {
            if (visualModel != null) return;

            Transform foundChild = transform.Find("Body");
            if (foundChild == null) foundChild = transform.Find("body");
            if (foundChild == null) foundChild = transform.Find("VisualModel");

            if (foundChild != null)
            {
                visualModel = foundChild;
            }
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

        private void EnsureCurrentWeapon()
        {
            if (currentWeapon == null)
            {
                currentWeapon = GetComponentInChildren<Weapon>();
                if (currentWeapon == null)
                {
                    Transform gunChild = transform.Find("ArcadeGun");
                    if (gunChild == null) gunChild = transform.Find("arcadegun");
                    if (gunChild == null) gunChild = transform.Find("Gun");
                    if (gunChild == null) gunChild = transform.Find("Weapon");
                    if (gunChild == null) gunChild = transform.Find("Body/ArcadeGun");

                    if (gunChild != null)
                    {
                        currentWeapon = gunChild.gameObject.AddComponent<ProjectileWeapon>();
                        Debug.Log($"[PlayerCharacter] 🔫 ProjectileWeapon asignado a '{gunChild.name}'");
                    }
                    else
                    {
                        currentWeapon = Object.FindAnyObjectByType<Weapon>();
                    }
                }
            }

            if (currentWeapon != null)
            {
                currentWeapon.SetOwner(this);
            }
        }

        private void FindCrosshairIfNull()
        {
            if (crosshairUI != null) return;

            GameObject found = GameObject.Find("miraDisparo");
            if (found == null) found = GameObject.Find("MiraDisparo");

            if (found != null)
            {
                crosshairUI = found;
                return;
            }

            // Búsqueda en objetos raíz activos de la escena
            var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                var root = rootObjects[i];
                if (root != null && root.name.Equals("miraDisparo", System.StringComparison.OrdinalIgnoreCase))
                {
                    crosshairUI = root;
                    return;
                }
            }
        }

        #endregion

        #region Input Event Handlers

        private void HandleJumpStarted()
        {
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
                stateMachine.ChangeState(ProneState);
            }
            else if (stateMachine.CurrentState == ProneState)
            {
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

        private void HandleAimEvent(bool isAiming)
        {
            FindCrosshairIfNull();
            if (crosshairUI != null)
            {
                crosshairUI.SetActive(isAiming);
            }

            if (isAiming)
            {
                Transform cam = GetActiveCameraTransform();
                if (cam != null)
                {
                    Vector3 camForward = cam.forward;
                    camForward.y = 0f;
                    if (camForward.sqrMagnitude > 0.001f)
                    {
                        transform.rotation = Quaternion.LookRotation(camForward.normalized, Vector3.up);
                    }

                    currentAimPitch = cam.eulerAngles.x;
                    if (currentAimPitch > 180f) currentAimPitch -= 360f;
                    currentAimPitch = Mathf.Clamp(currentAimPitch, aimPitchMin, aimPitchMax);
                    if (eyeTarget != null)
                    {
                        eyeTarget.localRotation = Quaternion.Euler(currentAimPitch, 0f, 0f);
                    }
                }
            }
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

        #region Combat & Attack Charging Flow

        private void HandleAttackStarted()
        {
            EnsureCurrentWeapon();

            if (currentWeapon != null && currentWeapon.IsReloading)
            {
                Debug.Log("[Combat] ⏳ El arma está recargándose...");
                return;
            }

            if (currentWeapon != null && currentWeapon.CurrentAmmo <= 0)
            {
                if (currentWeapon.AutoReloadOnEmptyAttack)
                {
                    currentWeapon.TryReload();
                }
                return;
            }

            if (AllowCharging)
            {
                // Al activar carga: NUNCA disparar. Iniciar retención y acumulación de energía.
                isHoldingAttack = true;
                attackHoldTimer = 0f;
                isChargingAttack = false;
                maxChargeReachedLogged = false;
                Debug.Log($"[Combat] ⏳ Input Ataque: Manteniendo botón para cargar (umbral: {ChargeActivationDelay:F2}s). ¡NO SE DISPARARÁ hasta soltar!");
            }
            else
            {
                // Disparo directo inmediato únicamente para armas sin carga
                ExecuteAttack(0f, 0f);
            }
        }

        private void UpdateAttackCharge()
        {
            if (!isHoldingAttack || !AllowCharging) return;

            attackHoldTimer += Time.deltaTime;

            if (!isChargingAttack && attackHoldTimer >= ChargeActivationDelay)
            {
                isChargingAttack = true;
                Debug.Log("[Combat] ⚡ ¡Umbral superado! Comenzando carga del ataque...");
                AttackChargeStartedEvent?.Invoke();
            }

            if (isChargingAttack)
            {
                float activeChargeTime = attackHoldTimer - ChargeActivationDelay;
                if (!maxChargeReachedLogged && activeChargeTime >= MaxAttackChargeTime)
                {
                    maxChargeReachedLogged = true;
                    Debug.Log($"[Combat] ★ ¡CARGA MÁXIMA COMPLETA! (Tiempo: {MaxAttackChargeTime:F1}s | Carga: 100%) - ¡Listo para liberar!");
                    AttackMaxChargeReachedEvent?.Invoke();
                }
            }
        }

        private void HandleAttackCanceled()
        {
            if (!isHoldingAttack) return;

            float holdDuration = attackHoldTimer;
            float chargeRatio = isChargingAttack ? AttackChargeRatio : 0f;

            // Limpieza estricta de banderas de carga ANTES de despachar el disparo
            isHoldingAttack = false;
            isChargingAttack = false;
            maxChargeReachedLogged = false;

            if (AllowCharging)
            {
                // Disparo único ejecutado tras liberar el botón
                ExecuteAttack(chargeRatio, holdDuration);
            }
        }

        private void ExecuteAttack(float chargeRatio, float duration)
        {
            if (currentWeapon != null && currentWeapon.IsReloading)
            {
                return;
            }

            float bufferTime = ActiveMovementData != null ? ActiveMovementData.CommandBufferDuration : 0.35f;
            commandQueue.EnqueueCommand(new AttackCommand(this, chargeRatio, duration, bufferTime, BaseAttackDamage, MaxAttackDamage));
            commandQueue.TryExecuteNextCommand();

            AttackTriggeredEvent?.Invoke();
            AttackReleasedEvent?.Invoke(chargeRatio);
        }

        #endregion

        #region Physical Movement & Rotation

        public void AccelerateTowards(Vector3 targetDirection, float targetSpeed, float rate)
        {
            Vector3 targetVelocity = targetDirection * targetSpeed;

            if (currentVelocity.sqrMagnitude > 0.1f && targetDirection.sqrMagnitude > 0.01f)
            {
                // Evaluar qué tan brusco es el cambio de dirección (1 = misma dirección, 0 = 90°, -1 = sentido contrario)
                float dot = Vector3.Dot(currentVelocity.normalized, targetDirection.normalized);

                if (dot < 0.65f)
                {
                    // Aumentar la tasa de reacción para cancelar la inercia contraria rápidamente (counter-steering)
                    // sin introducir componentes perpendiculares espurias (evita desplazamientos residuales en Z)
                    float turnResponsiveness = Mathf.Lerp(4.0f, 1.5f, (dot + 1f) * 0.5f);
                    rate *= turnResponsiveness;
                }
            }

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

            Transform cam = GetActiveCameraTransform();
            if (cam != null)
            {
                Vector3 forward = cam.forward;
                Vector3 right = cam.right;

                forward.y = 0f;
                right.y = 0f;

                forward.Normalize();
                right.Normalize();

                return ((forward * moveInput.y) + (right * moveInput.x)).normalized;
            }

            return new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        }

        public void RotateTowards(Vector3 targetDirection, float smoothTime)
        {
            if (IsAiming) return;
            if (!shouldFaceMoveDirection || targetDirection.sqrMagnitude < 0.001f) return;

            float targetAngle = Mathf.Atan2(targetDirection.x, targetDirection.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, smoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        public void RotateTowardsSlerp(Vector3 targetDirection, float slerpSpeed)
        {
            if (IsAiming) return;
            if (!shouldFaceMoveDirection || targetDirection.sqrMagnitude < 0.001f) return;

            Quaternion toRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, toRotation, slerpSpeed * Time.deltaTime);
        }

        public bool IsGrounded()
        {
            if (groundDetector != null)
            {
                return groundDetector.IsGroundedAndStable();
            }

            return characterController != null && characterController.isGrounded;
        }

        public Vector3 GroundNormal => groundDetector != null ? groundDetector.SurfaceNormal : Vector3.up;
        public float GroundAngle => groundDetector != null ? groundDetector.SurfaceAngle : 0f;
        public RaycastHit CurrentGroundHit => groundDetector != null ? groundDetector.GroundHit : default;

        public void ApplyGravity()
        {
            bool grounded = IsGrounded();

            if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                float gravityMultiplier = ActiveMovementData != null ? ActiveMovementData.GravityMultiplier : 2.0f;
                verticalVelocity += Physics.gravity.y * gravityMultiplier * Time.fixedDeltaTime;
            }
        }

        public void MoveWithCurrentVelocity()
        {
            if (characterController == null) return;
            Vector3 motion = (currentVelocity + new Vector3(0f, verticalVelocity, 0f)) * Time.fixedDeltaTime;
            characterController.Move(motion);
        }

        public Transform GetActiveCameraTransform()
        {
            if (cachedMainCamera == null)
            {
                cachedMainCamera = UnityEngine.Camera.main;
            }

            if (cachedMainCamera != null)
            {
                return cachedMainCamera.transform;
            }

            return cameraTransform != null ? cameraTransform : transform;
        }

        private void AlignWithCameraHeading()
        {
            if (shouldFaceMoveDirection || IsAiming) return;

            Transform cam = GetActiveCameraTransform();
            if (cam != null)
            {
                Vector3 camForward = cam.forward;
                camForward.y = 0f;
                if (camForward.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(camForward.normalized, Vector3.up);
                }
            }
        }

        private void UpdateAimOrientation()
        {
            if (IsAiming)
            {
                Vector2 look = inputReader != null ? inputReader.CurrentLookInput : Vector2.zero;

                if (Mathf.Abs(look.x) > 0.001f)
                {
                    transform.Rotate(Vector3.up, look.x * aimSensitivityX, Space.World);
                }

                if (Mathf.Abs(look.y) > 0.001f)
                {
                    currentAimPitch = Mathf.Clamp(currentAimPitch - look.y * aimSensitivityY, aimPitchMin, aimPitchMax);
                }

                if (eyeTarget != null)
                {
                    eyeTarget.localRotation = Quaternion.Euler(currentAimPitch, 0f, 0f);
                }
            }
            else if (eyeTarget != null && Quaternion.Angle(eyeTarget.localRotation, Quaternion.identity) > 0.05f)
            {
                eyeTarget.localRotation = Quaternion.Slerp(eyeTarget.localRotation, Quaternion.identity, 10f * Time.deltaTime);
            }
        }

        #endregion

        #region Stance & Capsule Optimization (PhysX Churn Elimination)

        public void SetStanceDimensions(float targetHeight, Vector3 targetCenter)
        {
            targetStanceHeight = targetHeight;

            // Anclaje matemático automático a la base de los pies (suelo):
            float standingHeight = ActiveMovementData != null ? ActiveMovementData.StandingHeight : 2.0f;
            Vector3 standingCenter = ActiveMovementData != null ? ActiveMovementData.StandingCenter : Vector3.zero;
            float baseFeetY = standingCenter.y - (standingHeight / 2.0f);

            float anchoredCenterY = baseFeetY + (targetHeight / 2.0f);
            targetStanceCenter = new Vector3(targetCenter.x, anchoredCenterY, targetCenter.z);
            isStanceTransitioning = true;
        }

        private void UpdateStanceDimensions()
        {
            if (characterController == null || !isStanceTransitioning) return;

            float heightDelta = Mathf.Abs(currentStanceHeight - targetStanceHeight);
            float centerDelta = Vector3.Distance(currentStanceCenter, targetStanceCenter);

            // Si ya convergió a la postura deseada, fijar valores y detener reconfiguración de PhysX
            if (heightDelta < 0.001f && centerDelta < 0.001f)
            {
                currentStanceHeight = targetStanceHeight;
                currentStanceCenter = targetStanceCenter;
                ApplyStanceToControllerAndVisuals();
                isStanceTransitioning = false;
                return;
            }

            currentStanceHeight = Mathf.Lerp(currentStanceHeight, targetStanceHeight, Time.deltaTime * stanceTransitionSpeed);
            currentStanceCenter = Vector3.Lerp(currentStanceCenter, targetStanceCenter, Time.deltaTime * stanceTransitionSpeed);
            ApplyStanceToControllerAndVisuals();
        }

        private void ApplyStanceToControllerAndVisuals()
        {
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

        #endregion

        #region IDamageable Implementation & Invulnerability

        public void SetInvulnerability(bool active)
        {
            isInvulnerable = active;
        }

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
