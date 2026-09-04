using UnityEngine;
using Scripts.Input;
using Scripts.Character;

namespace Scripts.StateMachine.Evasive
{
    public class SlidingState : PlayerStateBase
    {
        private float slideTimer;
        private Vector3 slideDirection;

        public SlidingState(PlayerCharacter character, PlayerStateMachine stateMachine, InputReader inputReader) 
            : base(character, stateMachine, inputReader)
        {
        }

        public override void Enter()
        {
            character.SetStanceDimensions(MovementData.CrouchingHeight, MovementData.CrouchingCenter);
            slideTimer = MovementData.SlideDuration;

            Vector2 moveInput = inputReader.CurrentMoveInput;
            slideDirection = character.CalculateWorldMovementDirection(moveInput);

            if (slideDirection.sqrMagnitude < 0.01f)
            {
                slideDirection = character.transform.forward;
            }

            character.SetHorizontalVelocity(slideDirection * MovementData.SlideInitialSpeed);
        }

        public override void Execute()
        {
            slideTimer -= Time.deltaTime;

            if (slideTimer <= 0f)
            {
                if (inputReader.CurrentMoveInput.sqrMagnitude > 0.01f)
                {
                    stateMachine.ChangeState(character.CrouchingState);
                }
                else
                {
                    stateMachine.ChangeState(character.IdleState);
                }
            }
        }

        public override void FixedExecute()
        {
            // Decelerate naturally during the slide
            character.Decelerate(MovementData.Deceleration * 0.75f);
            character.ApplyGravity();
            character.MoveWithCurrentVelocity();
        }
    }
}
