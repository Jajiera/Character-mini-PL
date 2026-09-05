using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using Scripts.Input;

namespace Scripts.CameraSystem
{
    [RequireComponent(typeof(CinemachineCamera))]
    public class ThirdPersonCameraController : MonoBehaviour
    {
        [Header("Zoom Parameters")]
        [Tooltip("Distancia en metros que se modifica por cada muesca de la rueda del ratón")]
        [SerializeField] private float zoomStep = 1.0f;
        [Tooltip("Velocidad de zoom continuo (para mando o bumpers)")]
        [SerializeField] private float gamepadZoomSpeed = 5.0f;
        [Tooltip("Velocidad de interpolación suave (Lerp) hacia el zoom objetivo")]
        [SerializeField] private float zoomLerpSpeed = 10f;
        [SerializeField] private float minDistance = 1.5f;
        [SerializeField] private float maxDistance = 15f;

        [Header("Cursor Settings")]
        [SerializeField] private bool lockCursor = true;

        [Header("Input Dependency (Optional)")]
        [SerializeField] private InputReader inputReader;

        [Header("Aim Camera Integration")]
        [Tooltip("Referencia a la cámara de apuntado ThirdPersonAimCamera")]
        [SerializeField] private CinemachineCamera aimCamera;
        [SerializeField] private int normalPriority = 10;
        [SerializeField] private int aimPriority = 20;

        public CinemachineCamera AimCamera
        {
            get => aimCamera;
            set => aimCamera = value;
        }

        private InputSystem_Actions controls;
        private CinemachineCamera cam;
        private CinemachineOrbitalFollow orbital;

        private float targetZoom = 5f;
        private float currentZoom = 5f;
        private float gamepadZoomInput = 0f;
        private bool hasScrolledThisFrame = false;

        // Caché de radios base para modo ThreeRing
        private float baseTopRadius = 2f;
        private float baseCenterRadius = 4f;
        private float baseBottomRadius = 2.5f;
        private float initialReferenceRadius = 4f;

        private void Awake()
        {
            cam = GetComponent<CinemachineCamera>();
            orbital = GetComponent<CinemachineOrbitalFollow>();
            if (orbital == null)
            {
                orbital = GetComponentInChildren<CinemachineOrbitalFollow>();
            }
        }

        private void Start()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (orbital != null)
            {
                if (orbital.OrbitStyle == CinemachineOrbitalFollow.OrbitStyles.ThreeRing)
                {
                    baseTopRadius = orbital.Orbits.Top.Radius;
                    baseCenterRadius = orbital.Orbits.Center.Radius;
                    baseBottomRadius = orbital.Orbits.Bottom.Radius;
                    initialReferenceRadius = baseCenterRadius > 0.01f ? baseCenterRadius : 5f;
                    targetZoom = initialReferenceRadius;
                }
                else
                {
                    targetZoom = orbital.Radius > 0.01f ? orbital.Radius : 5f;
                }

                currentZoom = targetZoom;
            }

            // Inicializar prioridades de cámaras
            if (cam != null)
            {
                cam.Priority.Value = normalPriority;
            }

            FindAimCameraIfNull();

            if (aimCamera != null)
            {
                aimCamera.Priority.Value = 0;
            }

            // Asegurar que las acciones del Input System estén activas
            if (inputReader != null)
            {
                inputReader.EnablePlayerInput();
                inputReader.ZoomDeltaEvent += HandleZoomDelta;
                inputReader.AimEvent += HandleAim;
            }
            else
            {
                controls = new InputSystem_Actions();
                controls.Enable();

                controls.Player.MouseZoom.performed += HandleMouseScroll;
                controls.Player.GamepadZoom.performed += HandleGamepadZoom;
                controls.Player.GamepadZoom.canceled += HandleGamepadZoomCanceled;
                controls.Player.Aim.performed += HandleAimAction;
                controls.Player.Aim.canceled += HandleAimAction;
            }
        }

        private void FindAimCameraIfNull()
        {
            if (aimCamera == null)
            {
                GameObject aimObj = GameObject.Find("ThirdPersonAimCamera");
                if (aimObj != null)
                {
                    aimCamera = aimObj.GetComponent<CinemachineCamera>();
                }
            }
        }

        private void HandleAim(bool isAiming)
        {
            FindAimCameraIfNull();
            if (aimCamera != null)
            {
                aimCamera.Priority.Value = isAiming ? aimPriority : 0;
            }
        }

        private void HandleAimAction(InputAction.CallbackContext context)
        {
            bool isAiming = context.performed;
            HandleAim(isAiming);
        }

        private void HandleMouseScroll(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            Vector2 delta = context.ReadValue<Vector2>();
            ApplyScrollImpulse(delta.y);
        }

        private void HandleGamepadZoom(InputAction.CallbackContext context)
        {
            gamepadZoomInput = context.ReadValue<float>();
        }

        private void HandleGamepadZoomCanceled(InputAction.CallbackContext context)
        {
            gamepadZoomInput = 0f;
        }

        private void HandleZoomDelta(Vector2 delta)
        {
            ApplyScrollImpulse(delta.y);
        }

        private void ApplyScrollImpulse(float scrollY)
        {
            if (Mathf.Abs(scrollY) < 0.01f) return;

            hasScrolledThisFrame = true;

            // En Windows cada muesca típica de rueda genera +/- 120; en trackpads valores menores.
            float notches = Mathf.Abs(scrollY) >= 120f ? (scrollY / 120f) : Mathf.Sign(scrollY);
            targetZoom -= notches * zoomStep;
            targetZoom = Mathf.Clamp(targetZoom, minDistance, maxDistance);
        }

        private void Update()
        {
            // 1. Alternar bloqueo de cursor para depuración fácil en el Editor
            HandleCursorToggle();

            // 2. Detección directa de hardware por si el evento de acción no se capturó
            if (!hasScrolledThisFrame && Mouse.current != null)
            {
                float directScroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(directScroll) > 0.01f)
                {
                    ApplyScrollImpulse(directScroll);
                }
            }
            hasScrolledThisFrame = false;

            // 3. Zoom continuo para mando (bumpers / triggers)
            float continuousGamepad = gamepadZoomInput;
            if (Gamepad.current != null)
            {
                if (Gamepad.current.rightShoulder.isPressed) continuousGamepad += 1f;
                if (Gamepad.current.leftShoulder.isPressed) continuousGamepad -= 1f;
            }

            if (Mathf.Abs(continuousGamepad) > 0.01f)
            {
                targetZoom -= continuousGamepad * gamepadZoomSpeed * Time.deltaTime;
                targetZoom = Mathf.Clamp(targetZoom, minDistance, maxDistance);
            }

            // 4. Aplicar zoom con suavizado (Lerp) al componente de Cinemachine
            if (orbital != null)
            {
                currentZoom = Mathf.Lerp(currentZoom, targetZoom, zoomLerpSpeed * Time.deltaTime);

                if (orbital.OrbitStyle == CinemachineOrbitalFollow.OrbitStyles.Sphere)
                {
                    orbital.Radius = currentZoom;
                }
                else if (orbital.OrbitStyle == CinemachineOrbitalFollow.OrbitStyles.ThreeRing && initialReferenceRadius > 0.01f)
                {
                    float scale = currentZoom / initialReferenceRadius;
                    orbital.Orbits.Top.Radius = baseTopRadius * scale;
                    orbital.Orbits.Center.Radius = baseCenterRadius * scale;
                    orbital.Orbits.Bottom.Radius = baseBottomRadius * scale;
                    orbital.Radius = currentZoom;
                }
            }
        }

        private void HandleCursorToggle()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (lockCursor && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.ZoomDeltaEvent -= HandleZoomDelta;
                inputReader.AimEvent -= HandleAim;
            }

            if (controls != null)
            {
                controls.Player.MouseZoom.performed -= HandleMouseScroll;
                controls.Player.GamepadZoom.performed -= HandleGamepadZoom;
                controls.Player.GamepadZoom.canceled -= HandleGamepadZoomCanceled;
                controls.Player.Aim.performed -= HandleAimAction;
                controls.Player.Aim.canceled -= HandleAimAction;
                controls.Disable();
            }
        }
    }
}
