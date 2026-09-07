using System.Collections;
using UnityEngine;
using Scripts.Data;

namespace Scripts.Combat
{
    public class ProjectileWeapon : Weapon
    {
        [Header("Fallback Ballistics & Overrides")]
        [UnityEngine.Serialization.FormerlySerializedAs("bulletPrefab")]
        [SerializeField] private GameObject fallbackBulletPrefab;
        [UnityEngine.Serialization.FormerlySerializedAs("shootForce")]
        [SerializeField] private float fallbackShootForce = 60f;
        [UnityEngine.Serialization.FormerlySerializedAs("hitLayers")]
        [SerializeField] private LayerMask fallbackHitLayers = ~0;
        [UnityEngine.Serialization.FormerlySerializedAs("maxRaycastDistance")]
        [SerializeField] private float fallbackMaxRaycastDistance = 150f;

        [Header("Camera & Aim Alignment")]
        [Tooltip("Cámara de referencia para trazar la línea de visión hacia la mirilla (miraDisparo)")]
        [SerializeField] private Transform aimCamera;

        [Header("Audio & Visual Components")]
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField] private AudioSource audioSource;
        [UnityEngine.Serialization.FormerlySerializedAs("shootSound")]
        [SerializeField] private AudioClip fallbackShootSound;
        [SerializeField] private AudioClip fallbackReloadSound;
        [SerializeField] private AudioClip fallbackEmptyClickSound;

        [Header("Recoil Animation (Procedural)")]
        [SerializeField] private Transform modelTransform;
        [UnityEngine.Serialization.FormerlySerializedAs("recoilKickBack")]
        [SerializeField] private float fallbackRecoilKickBack = 0.06f;
        [UnityEngine.Serialization.FormerlySerializedAs("recoilKickUp")]
        [SerializeField] private float fallbackRecoilKickUp = 4.0f;
        [UnityEngine.Serialization.FormerlySerializedAs("recoilReturnSpeed")]
        [SerializeField] private float fallbackRecoilReturnSpeed = 10.0f;

        private float nextShootTime;
        private Coroutine reloadCoroutine;

        private Vector3 startLocalPosition;
        private Quaternion startLocalRotation;

        public GameObject BulletPrefab => weaponData != null && weaponData.BulletPrefab != null ? weaponData.BulletPrefab : fallbackBulletPrefab;
        public float ShootForce => weaponData != null ? weaponData.ShootForce : fallbackShootForce;
        public float MaxRaycastDistance => weaponData != null ? weaponData.MaxRaycastDistance : fallbackMaxRaycastDistance;
        public LayerMask HitLayers => weaponData != null ? weaponData.HitLayers : fallbackHitLayers;

        public AudioClip ShootSound => weaponData != null && weaponData.ShootSound != null ? weaponData.ShootSound : fallbackShootSound;
        public AudioClip ReloadSound => weaponData != null && weaponData.ReloadSound != null ? weaponData.ReloadSound : fallbackReloadSound;
        public AudioClip EmptyClickSound => weaponData != null && weaponData.EmptyClickSound != null ? weaponData.EmptyClickSound : fallbackEmptyClickSound;

        public float RecoilKickBack => weaponData != null ? weaponData.RecoilKickBack : fallbackRecoilKickBack;
        public float RecoilKickUp => weaponData != null ? weaponData.RecoilKickUp : fallbackRecoilKickUp;
        public float RecoilReturnSpeed => weaponData != null ? weaponData.RecoilReturnSpeed : fallbackRecoilReturnSpeed;

        protected override void Awake()
        {
            base.Awake();

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0.5f;
                }
            }

            if (modelTransform == null)
            {
                modelTransform = transform;
            }

            startLocalPosition = modelTransform.localPosition;
            startLocalRotation = modelTransform.localRotation;

            EnsureFirePoint();
            EnsureBulletPrefab();
        }

        private void Start()
        {
            EnsureCameraReference();
            EnsureBulletPrefab();
        }

        private void Update()
        {
            // Recuperación suave del retroceso procedimental
            if (modelTransform != null)
            {
                modelTransform.localPosition = Vector3.Lerp(modelTransform.localPosition, startLocalPosition, RecoilReturnSpeed * Time.deltaTime);
                modelTransform.localRotation = Quaternion.Slerp(modelTransform.localRotation, startLocalRotation, RecoilReturnSpeed * Time.deltaTime);
            }
        }

        public override bool CanFire()
        {
            return base.CanFire() && Time.time >= nextShootTime;
        }

        public override bool TryReload()
        {
            if (IsReloading || CurrentAmmo >= CartridgeCapacity)
            {
                return false;
            }

            if (reloadCoroutine != null)
            {
                StopCoroutine(reloadCoroutine);
            }

            reloadCoroutine = StartCoroutine(ReloadRoutine());
            return true;
        }

        private IEnumerator ReloadRoutine()
        {
            IsReloading = true;
            ReloadProgress = 0f;
            NotifyReloadStarted();

            Debug.Log($"[ProjectileWeapon] 🔄 Recargando nuevo cartucho de '{WeaponName}' ({CartridgeCapacity} balas, {ReloadDuration:F1}s)...");

            if (audioSource != null && ReloadSound != null)
            {
                audioSource.PlayOneShot(ReloadSound);
            }

            float timer = 0f;
            float duration = Mathf.Max(0.05f, ReloadDuration);

            while (timer < duration)
            {
                timer += Time.deltaTime;
                ReloadProgress = Mathf.Clamp01(timer / duration);
                yield return null;
            }

            CurrentAmmo = CartridgeCapacity;
            IsReloading = false;
            ReloadProgress = 1f;
            reloadCoroutine = null;

            NotifyReloadFinished();
            NotifyAmmoChanged();

            Debug.Log($"[ProjectileWeapon] ✅ ¡Cartucho recargado! Munición: {CurrentAmmo}/{CartridgeCapacity}");
        }

        public override void Fire(float chargeRatio = 0f)
        {
            // 0. Si el personaje dueño está actualmente sosteniendo o cargando el ataque, bloquear cualquier disparo anticipado
            Scripts.Character.PlayerCharacter owner = Owner != null ? Owner : GetComponentInParent<Scripts.Character.PlayerCharacter>();
            if (owner != null && owner.AllowCharging && (owner.IsHoldingAttack || owner.IsChargingAttack))
            {
                Debug.LogWarning("[ProjectileWeapon] 🛑 Disparo bloqueado: El arma se encuentra en proceso de CARGA. No disparará hasta soltar el botón.");
                return;
            }

            // 1. Si está recargando, ignorar disparo
            if (IsReloading)
            {
                Debug.Log("[ProjectileWeapon] ⏳ Imposible disparar: El arma se está recargando.");
                return;
            }

            // 2. Si el cartucho está vacío, activar recarga automática al pulsar el botón de ataque
            if (CurrentAmmo <= 0)
            {
                if (audioSource != null && EmptyClickSound != null)
                {
                    audioSource.PlayOneShot(EmptyClickSound);
                }

                if (AutoReloadOnEmptyAttack)
                {
                    Debug.Log("[ProjectileWeapon] ⚠️ ¡Cartucho vacío! Iniciando recarga automática...");
                    TryReload();
                }
                else
                {
                    Debug.LogWarning("[ProjectileWeapon] ⚠️ ¡Sin munición en el cartucho!");
                }
                return;
            }

            // 3. Respetar cadencia de fuego (TimeBetweenShots)
            if (Time.time < nextShootTime)
            {
                return;
            }
            nextShootTime = Time.time + TimeBetweenShots;

            // 4. Consumir munición del cartucho
            CurrentAmmo--;
            NotifyAmmoChanged();

            EnsureFirePoint();
            EnsureCameraReference();
            EnsureBulletPrefab();

            // 5. Trazar rayo desde el centro de la cámara (donde apunta la mirilla de disparo)
            Vector3 targetPoint;
            if (aimCamera != null)
            {
                Ray ray = new Ray(aimCamera.position, aimCamera.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, MaxRaycastDistance, HitLayers, QueryTriggerInteraction.Ignore))
                {
                    targetPoint = hit.point;
                }
                else
                {
                    targetPoint = ray.GetPoint(MaxRaycastDistance);
                }
            }
            else
            {
                targetPoint = firePoint.position + firePoint.forward * MaxRaycastDistance;
            }

            // 6. Calcular dirección balística precisa desde el cañón (firePoint) hacia el objetivo
            Vector3 shootDirection = (targetPoint - firePoint.position).normalized;
            Vector3 spawnPosition = firePoint.position + (shootDirection * 0.35f);

            // 7. Instanciar bala o generar respaldo
            GameObject bulletObj;
            if (BulletPrefab != null)
            {
                bulletObj = Instantiate(BulletPrefab, spawnPosition, Quaternion.LookRotation(shootDirection));
            }
            else
            {
                bulletObj = CreateFallbackBullet(spawnPosition, shootDirection);
            }

            if (bulletObj.transform.localScale.x < 0.12f)
            {
                bulletObj.transform.localScale = Vector3.one * 0.2f;
            }

            // 8. Ignorar colisiones con el personaje que dispara
            Collider bulletCollider = bulletObj.GetComponent<Collider>();
            if (bulletCollider != null)
            {
                Transform playerRoot = transform.root;
                Collider[] playerColliders = playerRoot.GetComponentsInChildren<Collider>();
                foreach (Collider pCol in playerColliders)
                {
                    if (pCol != null && pCol != bulletCollider)
                    {
                        Physics.IgnoreCollision(bulletCollider, pCol, true);
                    }
                }
            }

            // 9. Configurar daño según la carga del ataque
            float effectiveCharge = AllowCharging ? Mathf.Clamp01(chargeRatio) : 0f;
            float calculatedDamage = Mathf.Lerp(BaseDamage, MaxDamage, effectiveCharge);

            Bullet bullet = bulletObj.GetComponent<Bullet>();
            if (bullet == null)
            {
                bullet = bulletObj.AddComponent<Bullet>();
            }
            bullet.Initialize(calculatedDamage, ShootForce);

            // 10. Añadir estela visual luminosa (Tracer) si corresponde
            bool addTracer = weaponData != null ? weaponData.AddTracerTrail : true;
            if (addTracer && bulletObj.GetComponent<TrailRenderer>() == null)
            {
                AddBulletTracer(bulletObj);
            }

            // 11. Impulso balístico
            Rigidbody rb = bulletObj.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = bulletObj.AddComponent<Rigidbody>();
                rb.useGravity = false;
            }
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(shootDirection * ShootForce, ForceMode.VelocityChange);

            string chargeText = AllowCharging && effectiveCharge > 0.05f ? $" | Carga: {effectiveCharge * 100f:F0}%" : "";
            Debug.Log($"[ProjectileWeapon] 💥 Disparo de '{WeaponName}'! Balas: {CurrentAmmo}/{CartridgeCapacity} | Daño: {calculatedDamage:F1}{chargeText}");

            // 12. Efectos audiovisuales y retroceso
            PlayMuzzleAndAudio();
            ApplyRecoil();
            NotifyWeaponFired();

            // 13. Si se agotó la última bala, avisar que el siguiente toque recargará
            if (CurrentAmmo == 0 && AutoReloadOnEmptyAttack)
            {
                Debug.Log("[ProjectileWeapon] ⚠️ ¡Última bala disparada! Presiona atacar nuevamente para recargar el cartucho.");
            }
        }

        private GameObject CreateFallbackBullet(Vector3 position, Vector3 direction)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Bullet_Procedural";
            sphere.tag = "Bullet";
            sphere.transform.position = position;
            sphere.transform.rotation = Quaternion.LookRotation(direction);
            sphere.transform.localScale = Vector3.one * 0.2f;

            Renderer r = sphere.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = new Color(1f, 0.85f, 0.2f);
            }

            Collider col = sphere.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            Rigidbody rb = sphere.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            sphere.AddComponent<Bullet>();
            return sphere;
        }

        private void AddBulletTracer(GameObject bulletObj)
        {
            TrailRenderer trail = bulletObj.AddComponent<TrailRenderer>();
            trail.time = 0.15f;
            trail.startWidth = 0.08f;
            trail.endWidth = 0.01f;
            trail.autodestruct = false;

            Material trailMat = new Material(Shader.Find("Sprites/Default"));
            trailMat.color = new Color(1f, 0.9f, 0.3f);
            trail.material = trailMat;

            trail.startColor = new Color(1f, 0.95f, 0.4f, 0.95f);
            trail.endColor = new Color(1f, 0.4f, 0.1f, 0f);
        }

        private void PlayMuzzleAndAudio()
        {
            if (muzzleFlash != null)
            {
                muzzleFlash.Play();
            }

            if (audioSource != null && ShootSound != null)
            {
                audioSource.pitch = Random.Range(0.96f, 1.04f);
                audioSource.PlayOneShot(ShootSound);
            }
        }

        private void ApplyRecoil()
        {
            if (modelTransform != null)
            {
                modelTransform.localPosition -= new Vector3(0f, 0f, RecoilKickBack);
                modelTransform.localRotation *= Quaternion.Euler(-RecoilKickUp, 0f, 0f);
            }
        }

        private void EnsureFirePoint()
        {
            if (firePoint == null)
            {
                Transform foundChild = transform.Find("FirePoint") 
                                       ?? transform.Find("firepoint") 
                                       ?? transform.Find("Muzzle") 
                                       ?? transform.Find("muzzle");
                if (foundChild != null)
                {
                    firePoint = foundChild;
                }
                else
                {
                    GameObject newPoint = new GameObject("FirePoint");
                    newPoint.transform.SetParent(transform, false);
                    newPoint.transform.localPosition = new Vector3(0f, 0f, 0.6f);
                    firePoint = newPoint.transform;
                }
            }
        }

        private void EnsureBulletPrefab()
        {
            if (fallbackBulletPrefab == null)
            {
#if UNITY_EDITOR
                fallbackBulletPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bullet.prefab");
                if (fallbackBulletPrefab == null)
                {
                    fallbackBulletPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3dModels/Bullet/Bullet.prefab");
                }
#endif
            }
        }

        private void EnsureCameraReference()
        {
            if (aimCamera == null)
            {
                if (Camera.main != null)
                {
                    aimCamera = Camera.main.transform;
                }
                else
                {
                    GameObject camObj = GameObject.Find("Main Camera") ?? GameObject.FindWithTag("MainCamera");
                    if (camObj != null)
                    {
                        aimCamera = camObj.transform;
                    }
                }
            }
        }
    }
}
