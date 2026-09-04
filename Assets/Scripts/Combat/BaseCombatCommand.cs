using UnityEngine;
using Scripts.Core;

namespace Scripts.Combat
{
    public abstract class BaseCombatCommand : ICommand
    {
        private readonly float timestamp;
        private readonly float lifeTime;

        public BaseCombatCommand(float lifeTime = 0.35f)
        {
            this.timestamp = Time.time;
            this.lifeTime = lifeTime;
        }

        public bool IsExpired()
        {
            return Time.time > (timestamp + lifeTime);
        }

        public abstract bool CanExecute();
        public abstract void Execute();
    }
}
