using UnityEngine;
using Scripts.Character;
using Scripts.Core;

namespace Scripts.Combat
{
    public class InteractCommand : BaseCombatCommand
    {
        private readonly PlayerCharacter character;

        public InteractCommand(PlayerCharacter character, float lifeTime = 0.35f) : base(lifeTime)
        {
            this.character = character;
        }

        public override bool CanExecute()
        {
            return character != null;
        }

        public override void Execute()
        {
            Debug.Log("[CombatCommandQueue] Executing buffered Interaction check!");
            if (character != null && character.InteractionDetector != null)
            {
                character.InteractionDetector.TryInteract();
            }
        }
    }
}
