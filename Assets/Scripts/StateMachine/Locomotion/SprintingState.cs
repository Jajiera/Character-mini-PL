using UnityEngine;
using Scripts.Input;
using Scripts.Character;

namespace Scripts.StateMachine.Locomotion
{
    public class SprintingState : PlayerStateBase
    {
        public SprintingState(PlayerCharacter character, PlayerStateMachine stateMachine, InputReader inputReader) 
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

            if (!inputReader.IsSprintActive)
            {
                stateMachine.ChangeState(character.WalkingState);
                return;
            }
        }

        public override void FixedExecute()
        {
            Vector2 moveInput = inputReader.CurrentMoveInput;
            Vector3 worldDirection = character.CalculateWorldMovementDirection(moveInput);

            character.AccelerateTowards(worldDirection, MovementData.SprintSpeed, MovementData.Acceleration * 1.25f);
            character.RotateTowards(worldDirection, MovementData.RotationSmoothTime);
            character.ApplyGravity();
            character.MoveWithCurrentVelocity();
        }
    }
}
