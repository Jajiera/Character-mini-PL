using UnityEngine;
using Scripts.Core;
using Scripts.Data;
using Scripts.Input;
using Scripts.Character;

namespace Scripts.StateMachine
{
    public abstract class PlayerStateBase : IState
    {
        protected readonly PlayerCharacter character;
        protected readonly PlayerStateMachine stateMachine;
        protected readonly InputReader inputReader;

        protected MovementDataSO MovementData => character.ActiveMovementData;

        public PlayerStateBase(PlayerCharacter character, PlayerStateMachine stateMachine, InputReader inputReader)
        {
            this.character = character;
            this.stateMachine = stateMachine;
            this.inputReader = inputReader;
        }

        public virtual void Enter() { }

        public virtual void Execute() { }

        public virtual void FixedExecute() { }

        public virtual void Exit() { }
    }
}
