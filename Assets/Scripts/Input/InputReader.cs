using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scripts.Input
{
    [CreateAssetMenu(fileName = "InputReader", menuName = "Character/Input/Input Reader")]
    public class InputReader : ScriptableObject, InputSystem_Actions.IPlayerActions
    {
        // Observer Events for locomotion and view
        public event Action<Vector2> MoveEvent;
        public event Action<Vector2> LookEvent;

        // Observer Events for actions and tactical transitions
        public event Action JumpStartedEvent;
        public event Action JumpCanceledEvent;
        public event Action SprintStartedEvent;
        public event Action SprintCanceledEvent;
        public event Action CrouchPerformedEvent;
        public event Action RollPerformedEvent;

        // Observer Events for combat and interaction commands
        public event Action AttackPerformedEvent;
        public event Action InteractPerformedEvent;

        public Vector2 CurrentMoveInput { get; private set; }
        public Vector2 CurrentLookInput { get; private set; }

        public bool IsSprintActive { get; private set; }
        public bool IsSprintPressed => IsSprintActive;

        public void ToggleSprint()
        {
            SetSprintActive(!IsSprintActive);
        }

        public void SetSprintActive(bool active)
        {
            if (IsSprintActive == active) return;
            IsSprintActive = active;
            if (IsSprintActive)
            {
                SprintStartedEvent?.Invoke();
            }
            else
            {
                SprintCanceledEvent?.Invoke();
            }
        }

        private InputSystem_Actions inputActions;

        private void OnEnable()
        {
            if (inputActions == null)
            {
                inputActions = new InputSystem_Actions();
                inputActions.Player.SetCallbacks(this);
            }

            EnablePlayerInput();
        }

        private void OnDisable()
        {
            DisablePlayerInput();
        }

        public void EnablePlayerInput()
        {
            if (inputActions != null)
            {
                inputActions.Player.Enable();
            }
        }

        public void DisablePlayerInput()
        {
            if (inputActions != null)
            {
                inputActions.Player.Disable();
            }
        }

        #region InputSystem_Actions.IPlayerActions Implementation

        public void OnMove(InputAction.CallbackContext context)
        {
            CurrentMoveInput = context.ReadValue<Vector2>();
            MoveEvent?.Invoke(CurrentMoveInput);
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            CurrentLookInput = context.ReadValue<Vector2>();
            LookEvent?.Invoke(CurrentLookInput);
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                AttackPerformedEvent?.Invoke();
            }
        }

        private int lastInteractFrame = -1;

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.started || context.performed)
            {
                if (Time.frameCount == lastInteractFrame) return;
                lastInteractFrame = Time.frameCount;
                InteractPerformedEvent?.Invoke();
            }
        }

        public void OnCrouch(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                CrouchPerformedEvent?.Invoke();
            }
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                JumpStartedEvent?.Invoke();
            }
            else if (context.canceled)
            {
                JumpCanceledEvent?.Invoke();
            }
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                ToggleSprint();
            }
        }

        public void OnRoll(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                RollPerformedEvent?.Invoke();
            }
        }

        public void OnPrevious(InputAction.CallbackContext context)
        {
            // Reserved for weapon/item cycling
        }

        public void OnNext(InputAction.CallbackContext context)
        {
            // Reserved for weapon/item cycling
        }

        #endregion
    }
}
