using UnityEngine;

namespace Scripts.Data
{
    [CreateAssetMenu(fileName = "NewWeaponData", menuName = "Character/Data/Weapon Data")]
    public class WeaponDataSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string weaponName = "Pistola Táctica";
        [SerializeField] private string weaponId = "weapon_pistol";
        [TextArea(2, 4)]
        [SerializeField] private string description = "Arma balística estándar de respuesta rápida y disparo preciso.";

        [Header("Damage & Timing")]
        [Tooltip("Daño base infligido al disparar sin carga")]
        [SerializeField] private float baseDamage = 20f;
        [Tooltip("Daño máximo infligido al disparar con carga completa (100%)")]
        [SerializeField] private float maxDamage = 50f;
        [Tooltip("Tiempo mínimo en segundos entre disparos consecutivos (Cadencia)")]
        [SerializeField] private float timeBetweenShots = 0.25f;

        [Header("Attack Charging Configuration")]
        [Tooltip("Si es false, el arma dispara directamente sin entrar en modo de carga")]
        [SerializeField] private bool allowCharging = false;
        [Tooltip("Tiempo en segundos que debe mantenerse presionado el botón antes de que empiece a cargar el disparo. Si se suelta antes, se realiza un disparo rápido estándar.")]
        [SerializeField] private float chargeActivationDelay = 0.2f;
        [Tooltip("Tiempo en segundos necesario para alcanzar el 100% de carga una vez superado el umbral")]
        [SerializeField] private float maxAttackChargeTime = 1.2f;

        [Header("Cartridge & Ammunition")]
        [Tooltip("Capacidad máxima de balas por cartucho")]
        [SerializeField] private int cartridgeCapacity = 12;
        [Tooltip("Tiempo en segundos que demora en recargarse un cartucho")]
        [SerializeField] private float reloadDuration = 1.2f;
        [Tooltip("Si es true, presionar el botón de disparo cuando el cartucho esté vacío iniciará automáticamente la recarga")]
        [SerializeField] private bool autoReloadOnEmptyAttack = true;

        [Header("Ballistics & Raycast")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private float shootForce = 60f;
        [SerializeField] private float maxRaycastDistance = 150f;
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Recoil Animation (Procedural)")]
        [SerializeField] private float recoilKickBack = 0.06f;
        [SerializeField] private float recoilKickUp = 4.0f;
        [SerializeField] private float recoilReturnSpeed = 10.0f;

        [Header("Audio & Visual Effects")]
        [SerializeField] private AudioClip shootSound;
        [SerializeField] private AudioClip reloadSound;
        [SerializeField] private AudioClip emptyClickSound;
        [SerializeField] private bool addTracerTrail = true;

        // Public Getters (Flyweight Pattern)
        public string WeaponName => weaponName;
        public string WeaponId => weaponId;
        public string Description => description;

        public float BaseDamage => baseDamage;
        public float MaxDamage => maxDamage;
        public float TimeBetweenShots => timeBetweenShots;

        public bool AllowCharging => allowCharging;
        public float ChargeActivationDelay => chargeActivationDelay;
        public float MaxAttackChargeTime => maxAttackChargeTime;

        public int CartridgeCapacity => cartridgeCapacity;
        public float ReloadDuration => reloadDuration;
        public bool AutoReloadOnEmptyAttack => autoReloadOnEmptyAttack;

        public GameObject BulletPrefab => bulletPrefab;
        public float ShootForce => shootForce;
        public float MaxRaycastDistance => maxRaycastDistance;
        public LayerMask HitLayers => hitLayers;

        public float RecoilKickBack => recoilKickBack;
        public float RecoilKickUp => recoilKickUp;
        public float RecoilReturnSpeed => recoilReturnSpeed;

        public AudioClip ShootSound => shootSound;
        public AudioClip ReloadSound => reloadSound;
        public AudioClip EmptyClickSound => emptyClickSound;
        public bool AddTracerTrail => addTracerTrail;
    }
}
