using UnityEngine;
using Scripts.Character;

namespace Scripts.Combat
{
    public class AttackCommand : BaseCombatCommand
    {
        private readonly PlayerCharacter character;
        private readonly float chargeRatio;
        private readonly float chargeDuration;
        private readonly float baseDamage;
        private readonly float maxDamage;

        public float ChargeRatio => chargeRatio;
        public float ChargeDuration => chargeDuration;
        public float CalculatedDamage { get; private set; }

        public AttackCommand(
            PlayerCharacter character,
            float chargeRatio = 0f,
            float chargeDuration = 0f,
            float lifeTime = 0.35f,
            float baseDamage = 15f,
            float maxDamage = 45f) : base(lifeTime)
        {
            this.character = character;
            this.chargeRatio = Mathf.Clamp01(chargeRatio);
            this.chargeDuration = chargeDuration;
            this.baseDamage = baseDamage;
            this.maxDamage = maxDamage;
            this.CalculatedDamage = Mathf.Lerp(baseDamage, maxDamage, this.chargeRatio);
        }

        public override bool CanExecute()
        {
            return character != null && !character.IsInvulnerable;
        }

        public override void Execute()
        {
            string chargeType = chargeRatio >= 0.99f ? "CARGA MÁXIMA" : (chargeRatio >= 0.35f ? "SEMI-CARGADO" : "RÁPIDO");
            Debug.Log($"[CombatCommandQueue] ⚔️ ¡Ataque Ejecutado [{chargeType}]! Daño: {CalculatedDamage:F1} | Carga: {chargeRatio * 100f:F0}% ({chargeDuration:F2}s)");

            // Disparar el arma equipada pasando la carga del ataque
            if (character != null)
            {
                if (character.CurrentWeapon != null)
                {
                    character.CurrentWeapon.Fire(chargeRatio);
                }
                else
                {
                    Debug.LogWarning("[CombatCommandQueue] ⚠️ No se encontró ningún Weapon equipado en el Player (CurrentWeapon es null). Asegúrate de que ArcadeGun tenga el componente ProjectileWeapon.");
                }
            }
        }
    }
}
