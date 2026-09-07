using System;
using UnityEngine;
using Scripts.Data;

namespace Scripts.Combat
{
    public abstract class Weapon : MonoBehaviour
    {
        [Header("Weapon Data Definition (Flyweight)")]
        [Tooltip("ScriptableObject con todos los parámetros de configuración balística, munición y cadencia")]
        [SerializeField] protected WeaponDataSO weaponData;

        [Header("Weapon Transforms & Fallbacks")]
        [SerializeField] protected Transform firePoint;
        [UnityEngine.Serialization.FormerlySerializedAs("weaponName")]
        [SerializeField] protected string fallbackWeaponName = "Arma Balística";
        [UnityEngine.Serialization.FormerlySerializedAs("baseDamage")]
        [SerializeField] protected float fallbackBaseDamage = 20f;
        [UnityEngine.Serialization.FormerlySerializedAs("maxDamage")]
        [SerializeField] protected float fallbackMaxDamage = 50f;
        [UnityEngine.Serialization.FormerlySerializedAs("fireRate")]
        [SerializeField] protected float fallbackTimeBetweenShots = 0.25f;
        [SerializeField] protected int fallbackCartridgeCapacity = 12;
        [SerializeField] protected float fallbackReloadDuration = 1.2f;
        [SerializeField] protected bool fallbackAllowCharging = false;
        [SerializeField] protected float fallbackChargeActivationDelay = 0.2f;
        [SerializeField] protected float fallbackMaxAttackChargeTime = 1.2f;

        public WeaponDataSO WeaponData
        {
            get => weaponData;
            set
            {
                weaponData = value;
                InitializeAmmo();
            }
        }

        public Transform FirePoint => firePoint;

        public string WeaponName => weaponData != null ? weaponData.WeaponName : fallbackWeaponName;
        public float BaseDamage => weaponData != null ? weaponData.BaseDamage : fallbackBaseDamage;
        public float MaxDamage => weaponData != null ? weaponData.MaxDamage : fallbackMaxDamage;
        public float FireRate => weaponData != null ? weaponData.TimeBetweenShots : fallbackTimeBetweenShots;
        public float TimeBetweenShots => FireRate;
        public int CartridgeCapacity => weaponData != null ? weaponData.CartridgeCapacity : fallbackCartridgeCapacity;
        public float ReloadDuration => weaponData != null ? weaponData.ReloadDuration : fallbackReloadDuration;

        public bool AllowCharging => weaponData != null ? weaponData.AllowCharging : fallbackAllowCharging;
        public float ChargeActivationDelay => weaponData != null ? weaponData.ChargeActivationDelay : fallbackChargeActivationDelay;
        public float MaxAttackChargeTime => weaponData != null ? weaponData.MaxAttackChargeTime : fallbackMaxAttackChargeTime;
        public bool AutoReloadOnEmptyAttack => weaponData != null ? weaponData.AutoReloadOnEmptyAttack : true;

        // Runtime State
        public int CurrentAmmo { get; protected set; }
        public bool IsReloading { get; protected set; }
        public float ReloadProgress { get; protected set; }
        public Scripts.Character.PlayerCharacter Owner { get; private set; }

        public void SetOwner(Scripts.Character.PlayerCharacter owner)
        {
            Owner = owner;
        }

        // Observer Events (SRP & Desacoplamiento)
        public event Action<int, int> OnAmmoChangedEvent;
        public event Action OnReloadStartedEvent;
        public event Action OnReloadFinishedEvent;
        public event Action OnWeaponFiredEvent;

        protected virtual void Awake()
        {
            InitializeAmmo();
        }

        public virtual void InitializeAmmo()
        {
            CurrentAmmo = CartridgeCapacity;
            IsReloading = false;
            ReloadProgress = 0f;
            OnAmmoChangedEvent?.Invoke(CurrentAmmo, CartridgeCapacity);
        }

        public virtual bool CanFire()
        {
            if (IsReloading || CurrentAmmo <= 0)
            {
                return false;
            }

            // Si el personaje dueño está manteniendo o acumulando carga de ataque, prohibir terminantemente el disparo
            Scripts.Character.PlayerCharacter owner = Owner != null ? Owner : GetComponentInParent<Scripts.Character.PlayerCharacter>();
            if (owner != null && owner.AllowCharging && (owner.IsHoldingAttack || owner.IsChargingAttack))
            {
                return false;
            }

            return true;
        }

        public abstract bool TryReload();
        public abstract void Fire(float chargeRatio = 0f);

        protected void NotifyAmmoChanged()
        {
            OnAmmoChangedEvent?.Invoke(CurrentAmmo, CartridgeCapacity);
        }

        protected void NotifyReloadStarted()
        {
            OnReloadStartedEvent?.Invoke();
        }

        protected void NotifyReloadFinished()
        {
            OnReloadFinishedEvent?.Invoke();
        }

        protected void NotifyWeaponFired()
        {
            OnWeaponFiredEvent?.Invoke();
        }
    }
}