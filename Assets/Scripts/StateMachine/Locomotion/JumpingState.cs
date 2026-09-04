using UnityEngine;
using Scripts.Input;
using Scripts.Character;

namespace Scripts.StateMachine.Locomotion
{
    public class JumpingState : PlayerStateBase
    {
        private float airTime;
        private const float MinAirTimeToLand = 0.15f;

        public JumpingState(PlayerCharacter character, PlayerStateMachine stateMachine, InputReader inputReader) 
            : base(character, stateMachine, inputReader)
        {
        }

        public override void Enter()
        {
            character.SetStanceDimensions(MovementData.StandingHeight, MovementData.StandingCenter);
            character.SetVerticalVelocity(MovementData.JumpForce);
            airTime = 0f;
        }

        public override void Execute()
        {
            airTime += Time.deltaTime;
        }

        public override void FixedExecute()
        {
            Vector2 moveInput = inputReader.CurrentMoveInput;
            Vector3 worldDirection = character.CalculateWorldMovementDirection(moveInput);

            float speed = inputReader.IsSprintPressed ? MovementData.SprintSpeed : MovementData.WalkSpeed;
            character.AccelerateTowards(worldDirection, speed, MovementData.Acceleration * MovementData.AirControl);

            if (worldDirection.sqrMagnitude > 0.01f)
            {
                character.RotateTowards(worldDirection, MovementData.RotationSmoothTime * 1.5f);
            }

            character.ApplyGravity();
            character.MoveWithCurrentVelocity();

            // Check landing after a minimal threshold to prevent instant landing on takeoff
            if (airTime > MinAirTimeToLand && character.VerticalVelocity <= 0f && character.IsGrounded())
            {
                if (moveInput.sqrMagnitude > 0.01f)
                {
                    stateMachine.ChangeState(inputReader.IsSprintPressed ? character.SprintingState : character.WalkingState);
                }
                else
                {
                    stateMachine.ChangeState(character.IdleState);
                }
            }
        }
    }
}
