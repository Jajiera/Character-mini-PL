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
        [Tooltip("Si es false, el personaje siempre le da la espalda a la cámara y hace strafe (sin girar antinaturalmente al moverse).")]
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

        public GameObject CrosshairUI
        {
            get => crosshairUI;
            set => crosshairUI = value;
        }

        private float currentAimPitch = 0f;

        [Header("Combat & Weapon References")]
        [Tooltip("Arma equipada (ej. ArcadeGun con ProjectileWeapon). Si se deja vacío se auto-detecta en los hijos.")]
        [SerializeField] private Weapon currentWeapon;

        public Weapon CurrentWeapon
        {
            get => currentWeapon;
            set => currentWeapon = value;
        }

        [Header("Combat & Attack Charging")]
        [SerializeField] private float maxAttackChargeTime = 1.5f;
        [SerializeField] private float baseAttackDamage = 15f;
        [SerializeField] private float maxAttackDamage = 45f;

        private bool isChargingAttack = false;
        private float currentAttackChargeTimer = 0f;
        private bool maxChargeReachedLogged = false;


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

        public bool IsAiming => inputReader != null && inputReader.IsAiming;
        public Transform EyeTarget => eyeTarget;

        public bool IsChargingAttack => isChargingAttack;
        public float AttackChargeRatio => maxAttackChargeTime > 0f ? Mathf.Clamp01(currentAttackChargeTimer / maxAttackChargeTime) : 0f;
        public float CurrentAttackChargeTimer => currentAttackChargeTimer;
        public float MaxAttackChargeTime => maxAttackChargeTime;

        // Visual and audio observer events (SRP)
        public event System.Action AttackTriggeredEvent;
        public event System.Action<float> AttackReleasedEvent;
        public event System.Action AttackChargeStartedEvent;
        public event System.Action AttackMaxChargeReachedEvent;
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

            if (GetComponent<CharacterVisualFeedback>() == null)
            {
                gameObject.AddComponent<CharacterVisualFeedback>();
            }

            EnsureCurrentWeapon();


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

            if (eyeTarget == null)
            {
                eyeTarget = transform.Find("EyeTarget");
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

                isChargingAttack = false;
                currentAttackChargeTimer = 0f;
                maxChargeReachedLogged = false;

                if (crosshairUI != null)
                {
                    crosshairUI.SetActive(false);
                }
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

        private void UpdateAimOrientation()
        {
            if (IsAiming)
            {
                Vector2 look = inputReader != null ? inputReader.CurrentLookInput : Vector2.zero;

                // Rotar horizontalmente el cuerpo del jugador (Yaw)
                if (Mathf.Abs(look.x) > 0.001f)
                {
                    transform.Rotate(Vector3.up, look.x * aimSensitivityX, Space.World);
                }

                // Rotar verticalmente el EyeTarget (Pitch)
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

        private void FindCrosshairIfNull()
        {
            if (crosshairUI == null)
            {
                crosshairUI = GameObject.Find("miraDisparo") ?? GameObject.Find("MiraDisparo");

                if (crosshairUI == null)
                {
                    var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                    foreach (var root in rootObjects)
                    {
                        if (root != null && root.name.Equals("miraDisparo", System.StringComparison.OrdinalIgnoreCase))
                        {
                            crosshairUI = root;
                            break;
                        }
                    }
                }

                if (crosshairUI == null)
                {
                    var allCanvases = Resources.FindObjectsOfTypeAll<Canvas>();
                    foreach (var c in allCanvases)
                    {
                        if (c != null && c.gameObject.scene.isLoaded && c.gameObject.name.Equals("miraDisparo", System.StringComparison.OrdinalIgnoreCase))
                        {
                            crosshairUI = c.gameObject;
                            break;
                        }
                    }
                }
            }
        }

        private void EnsureCurrentWeapon()
        {
            if (currentWeapon == null)
            {
                currentWeapon = GetComponentInChildren<Scripts.Combat.Weapon>();
                if (currentWeapon == null)
                {
                    Transform gunChild = transform.Find("ArcadeGun") 
                                          ?? transform.Find("arcadegun") 
                                          ?? transform.Find("Gun") 
                                          ?? transform.Find("Weapon")
                                          ?? transform.Find("Body/ArcadeGun");
                    if (gunChild != null)
                    {
                        currentWeapon = gunChild.gameObject.AddComponent<Scripts.Combat.ProjectileWeapon>();
                        Debug.Log($"[PlayerCharacter] 🔫 ProjectileWeapon añadido automáticamente a '{gunChild.name}'");
                    }
                }
            }
        }

        private void HandleAimEvent(bool isAiming)
        {
            FindCrosshairIfNull();
            if (crosshairUI != null)
            {
                crosshairUI.SetActive(isAiming);
            }

            if (isAiming && cameraTransform != null)
            {
                // Al comenzar a apuntar, alinea el Yaw del jugador inmediatamente con el frente de la cámara
                Vector3 camForward = cameraTransform.forward;
                camForward.y = 0f;
                if (camForward.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(camForward.normalized, Vector3.up);
                }

                // Obtener el pitch inicial de la cámara para transición suave continua
                currentAimPitch = cameraTransform.eulerAngles.x;
                if (currentAimPitch > 180f) currentAimPitch -= 360f;
                currentAimPitch = Mathf.Clamp(currentAimPitch, aimPitchMin, aimPitchMax);
                if (eyeTarget != null)
                {
                    eyeTarget.localRotation = Quaternion.Euler(currentAimPitch, 0f, 0f);
                }
            }
        }

        private void FixedUpdate()
        {
            stateMachine.FixedTick();
        }

        private void LateUpdate()
        {
            AlignWithCameraHeading();
        }

        private void AlignWithCameraHeading()
        {
            if (IsAiming) return;

            if (cameraTransform == null && UnityEngine.Camera.main != null)
            {
                cameraTransform = UnityEngine.Camera.main.transform;
            }

            if (cameraTransform != null)
            {
                Vector3 camForward = cameraTransform.forward;
                camForward.y = 0f;
                if (camForward.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(camForward.normalized, Vector3.up);
                }
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

        private void HandleAttackStarted()
        {
            isChargingAttack = true;
            currentAttackChargeTimer = 0f;
            maxChargeReachedLogged = false;
            Debug.Log("[Combat] ⏳ Input Ataque Detectado: Comenzando carga de ataque (mantén presionado)...");
            AttackChargeStartedEvent?.Invoke();
        }

        private void UpdateAttackCharge()
        {
            if (isChargingAttack)
            {
                currentAttackChargeTimer += Time.deltaTime;
                if (!maxChargeReachedLogged && currentAttackChargeTimer >= maxAttackChargeTime)
                {
                    maxChargeReachedLogged = true;
                    Debug.Log($"[Combat] ★ ¡CARGA MÁXIMA DE ATAQUE COMPLETA! (Tiempo: {maxAttackChargeTime:F1}s | Carga: 100%) - ¡Listo para liberar!");
                    AttackMaxChargeReachedEvent?.Invoke();
                }
            }
        }

        private void HandleAttackCanceled()
        {
            if (!isChargingAttack) return;

            float chargeDuration = currentAttackChargeTimer;
            float chargeRatio = maxAttackChargeTime > 0f ? Mathf.Clamp01(chargeDuration / maxAttackChargeTime) : 1f;

            isChargingAttack = false;
            currentAttackChargeTimer = 0f;
            maxChargeReachedLogged = false;

            Debug.Log($"[Combat] 💥 Input Ataque Liberado: Botón soltado tras {chargeDuration:F2}s de carga (Carga: {chargeRatio * 100f:F0}%). Liberando ataque...");

            float bufferTime = ActiveMovementData != null ? ActiveMovementData.CommandBufferDuration : 0.35f;
            commandQueue.EnqueueCommand(new AttackCommand(this, chargeRatio, chargeDuration, bufferTime, baseAttackDamage, maxAttackDamage));
            commandQueue.TryExecuteNextCommand();

            AttackTriggeredEvent?.Invoke();
            AttackReleasedEvent?.Invoke(chargeRatio);
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
