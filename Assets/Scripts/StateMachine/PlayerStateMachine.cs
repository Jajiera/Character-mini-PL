using System;
using UnityEngine;
using Scripts.Core;

namespace Scripts.StateMachine
{
    public class PlayerStateMachine : MonoBehaviour
    {
        public IState CurrentState { get; private set; }

        // Observer event for external systems (Audio, UI, VFX) to honor SRP
        public event Action<IState> StateChangedEvent;

        public void Initialize(IState startingState)
        {
            CurrentState = startingState;
            CurrentState?.Enter();
            StateChangedEvent?.Invoke(CurrentState);
        }

        public void ChangeState(IState newState)
        {
            if (newState == null || newState == CurrentState)
            {
                return;
            }

            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter();
            StateChangedEvent?.Invoke(CurrentState);
        }

        public void Tick()
        {
            CurrentState?.Execute();
        }

        public void FixedTick()
        {
            CurrentState?.FixedExecute();
        }
    }
}
