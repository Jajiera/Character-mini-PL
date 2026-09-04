using UnityEngine;
using Scripts.Input;
using Scripts.Character;

namespace Scripts.StateMachine.Evasive
{
    public class RollingState : PlayerStateBase
    {
        private float rollTimer;
        private Vector3 rollDirection;

        public RollingState(PlayerCharacter character, PlayerStateMachine stateMachine, InputReader inputReader) 
            : base(character, stateMachine, inputReader)
        {
        }

        public override void Enter()
        {
            character.SetStanceDimensions(MovementData.CrouchingHeight, MovementData.CrouchingCenter);
            rollTimer = MovementData.RollDuration;

            Vector2 moveInput = inputReader.CurrentMoveInput;
            rollDirection = character.CalculateWorldMovementDirection(moveInput);

            if (rollDirection.sqrMagnitude < 0.01f)
            {
                rollDirection = character.transform.forward;
            }

            // Rotate instantly to roll heading
            character.transform.rotation = Quaternion.LookRotation(rollDirection);
            character.SetHorizontalVelocity(rollDirection * MovementData.RollSpeed);

            // Invulnerability window opened (can also be triggered by animation events)
            character.SetInvulnerability(true);
        }

        public override void Execute()
        {
            rollTimer -= Time.deltaTime;

            if (rollTimer <= 0f)
            {
                if (inputReader.CurrentMoveInput.sqrMagnitude > 0.01f)
                {
                    stateMachine.ChangeState(inputReader.IsSprintPressed ? character.SprintingState : character.WalkingState);
                }
                else
                {
                    stateMachine.ChangeState(character.IdleState);
                }
            }
        }

        public override void FixedExecute()
        {
            character.ApplyGravity();
            character.MoveWithCurrentVelocity();
        }

        public override void Exit()
        {
            // Close invulnerability window
            character.SetInvulnerability(false);
            character.SetStanceDimensions(MovementData.StandingHeight, MovementData.StandingCenter);
        }
    }
}
