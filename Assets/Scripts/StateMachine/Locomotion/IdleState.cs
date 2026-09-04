using UnityEngine;
using Scripts.Input;
using Scripts.Character;

namespace Scripts.StateMachine.Locomotion
{
    public class IdleState : PlayerStateBase
    {
        public IdleState(PlayerCharacter character, PlayerStateMachine stateMachine, InputReader inputReader) 
            : base(character, stateMachine, inputReader)
        {
        }

        public override void Enter()
        {
            inputReader.SetSprintActive(false);
            character.SetStanceDimensions(MovementData.StandingHeight, MovementData.StandingCenter);
            character.ResetHorizontalVelocity();
        }

        public override void Execute()
        {
            if (inputReader.CurrentMoveInput.sqrMagnitude > 0.01f)
            {
                if (inputReader.IsSprintPressed)
                {
                    stateMachine.ChangeState(character.SprintingState);
                }
                else
                {
                    stateMachine.ChangeState(character.WalkingState);
                }
                return;
            }
        }

        public override void FixedExecute()
        {
            character.ApplyGravity();
            character.MoveWithCurrentVelocity();
        }
    }
}
