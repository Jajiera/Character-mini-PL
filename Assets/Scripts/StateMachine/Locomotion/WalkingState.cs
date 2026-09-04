using UnityEngine;
using Scripts.Input;
using Scripts.Character;

namespace Scripts.StateMachine.Locomotion
{
    public class WalkingState : PlayerStateBase
    {
        public WalkingState(PlayerCharacter character, PlayerStateMachine stateMachine, InputReader inputReader) 
            : base(character, stateMachine, inputReader)
        {
        }

        public override void Enter()
        {
            character.SetStanceDimensions(MovementData.StandingHeight, MovementData.StandingCenter);
        }

        public override void Execute()
        {
            Vector2 moveInput = inputReader.CurrentMoveInput;

            if (moveInput.sqrMagnitude < 0.01f)
            {
                inputReader.SetSprintActive(false);
                stateMachine.ChangeState(character.IdleState);
                return;
            }

            if (inputReader.IsSprintActive)
            {
                stateMachine.ChangeState(character.SprintingState);
                return;
            }
        }

        public override void FixedExecute()
        {
            Vector2 moveInput = inputReader.CurrentMoveInput;
            Vector3 worldDirection = character.CalculateWorldMovementDirection(moveInput);

            character.AccelerateTowards(worldDirection, MovementData.WalkSpeed, MovementData.Acceleration);
            character.RotateTowards(worldDirection, MovementData.RotationSmoothTime);
            character.ApplyGravity();
            character.MoveWithCurrentVelocity();
        }
    }
}
