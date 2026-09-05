using UnityEngine;

namespace Scripts.Combat
{
    public abstract class Weapon : MonoBehaviour
    {
        [Header("Base Weapon Configuration")]
        [SerializeField] protected string weaponName = "Arma";
        [SerializeField] protected Transform firePoint;
        [SerializeField] protected float baseDamage = 20f;
        [SerializeField] protected float maxDamage = 50f;
        [SerializeField] protected float fireRate = 0.25f;

        public string WeaponName => weaponName;
        public Transform FirePoint => firePoint;
        public float BaseDamage => baseDamage;
        public float MaxDamage => maxDamage;
        public float FireRate => fireRate;

        public abstract bool CanFire();
        public abstract void Fire(float chargeRatio = 0f);
    }
}