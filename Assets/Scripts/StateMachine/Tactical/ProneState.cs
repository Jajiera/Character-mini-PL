using UnityEngine;
using Scripts.Input;
using Scripts.Character;

namespace Scripts.StateMachine.Tactical
{
    public class ProneState : PlayerStateBase
    {
        public ProneState(PlayerCharacter character, PlayerStateMachine stateMachine, InputReader inputReader) 
            : base(character, stateMachine, inputReader)
        {
        }

        public override void Enter()
        {
            character.SetStanceDimensions(MovementData.ProneHeight, MovementData.ProneCenter);
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
                character.AccelerateTowards(worldDirection, MovementData.ProneSpeed, MovementData.Acceleration * 0.7f);
                character.RotateTowards(worldDirection, MovementData.RotationSmoothTime * 1.5f);
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
