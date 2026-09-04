using UnityEngine;
using Scripts.Character;

namespace Scripts.Combat
{
    public class AttackCommand : BaseCombatCommand
    {
        private readonly PlayerCharacter character;

        public AttackCommand(PlayerCharacter character, float lifeTime = 0.35f) : base(lifeTime)
        {
            this.character = character;
        }

        public override bool CanExecute()
        {
            return character != null && !character.IsInvulnerable;
        }

        public override void Execute()
        {
            Debug.Log("[CombatCommandQueue] Executing buffered Attack command!");
            // Triggers attack animation / combo logic
        }
    }
}
