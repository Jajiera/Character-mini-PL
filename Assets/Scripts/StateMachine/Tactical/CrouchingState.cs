using UnityEngine;
using Scripts.Input;
using Scripts.Character;

namespace Scripts.StateMachine.Tactical
{
    public class CrouchingState : PlayerStateBase
    {
        public CrouchingState(PlayerCharacter character, PlayerStateMachine stateMachine, InputReader inputReader) 
            : base(character, stateMachine, inputReader)
        {
        }

        public override void Enter()
        {
            character.SetStanceDimensions(MovementData.CrouchingHeight, MovementData.CrouchingCenter);
        }

        public override void Execute()
        {
            if (inputReader.IsSprintPressed)
            {
                stateMachine.ChangeState(character.WalkingState);
                return;
            }
        }

        public override void FixedExecute()
        {
            Vector2 moveInput = inputReader.CurrentMoveInput;
            Vector3 worldDirection = character.CalculateWorldMovementDirection(moveInput);

            if (worldDirection.sqrMagnitude > 0.01f)
            {
                character.AccelerateTowards(worldDirection, MovementData.CrouchSpeed, MovementData.Acceleration);
                character.RotateTowards(worldDirection, MovementData.RotationSmoothTime);
            }
            else
            {
                character.Decelerate(MovementData.Deceleration);
            }

            character.ApplyGravity();
            character.MoveWithCurrentVelocity();
        }
    }
}
