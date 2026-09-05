using UnityEngine;
using UnityEngine.InputSystem;
using Scripts.Character;

namespace Scripts.Combat
{
    public class ProjectileWeapon : Weapon
    {
        [Header("Projectile & Physics")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private float shootForce = 60f;
        [SerializeField] private LayerMask hitLayers = ~0;
        [SerializeField] private float maxRaycastDistance = 150f;

        [Header("Camera & Aim Alignment")]
        [Tooltip("Cámara de referencia para trazar la línea de visión hacia la mirilla (miraDisparo)")]
        [SerializeField] private Transform aimCamera;

        [Header("Audio & Visual Effects")]
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField] private AudioClip shootSound;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private bool addTracerTrail = true;

        [Header("Recoil Animation")]
        [SerializeField] private Transform modelTransform;
        [SerializeField] private float recoilKickBack = 0.06f;
        [SerializeField] private float recoilKickUp = 4.0f;
        [SerializeField] private float recoilReturnSpeed = 10.0f;

        [Header("Standalone Input (Opcional)")]
        [Tooltip("Activar solo si se desea que el arma escuche el botón Attack de forma autónoma")]
        [SerializeField] private bool standaloneInput = false;
        [SerializeField] private bool isSemiAutomatic = true;

        private float nextShootTime;
        private InputAction shootAction;

        private Vector3 startLocalPosition;
        private Quaternion startLocalRotation;

        public GameObject BulletPrefab
        {
            get => bulletPrefab;
            set => bulletPrefab = value;
        }

        private void Awake()
        {
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

            if (standaloneInput)
            {
                shootAction = InputSystem.actions != null ? InputSystem.actions.FindAction("Attack") : null;
                if (shootAction != null)
                {
                    shootAction.started += OnStandaloneAttackPressed;
                }
            }
        }

        private void OnDestroy()
        {
            if (standaloneInput && shootAction != null)
            {
                shootAction.started -= OnStandaloneAttackPressed;
            }
        }

        private void OnStandaloneAttackPressed(InputAction.CallbackContext context)
        {
            if (isSemiAutomatic)
            {
                Fire(0f);
            }
        }

        private void Update()
        {
            if (standaloneInput && !isSemiAutomatic && shootAction != null && shootAction.IsPressed())
            {
                Fire(0f);
            }

            // Recuperación suave del retroceso
            if (modelTransform != null)
            {
                modelTransform.localPosition = Vector3.Lerp(modelTransform.localPosition, startLocalPosition, recoilReturnSpeed * Time.deltaTime);
                modelTransform.localRotation = Quaternion.Slerp(modelTransform.localRotation, startLocalRotation, recoilReturnSpeed * Time.deltaTime);
            }
        }

        public override bool CanFire()
        {
            return Time.time >= nextShootTime;
        }

        public override void Fire(float chargeRatio = 0f)
        {
            if (Time.time < nextShootTime) return;
            nextShootTime = Time.time + fireRate;

            EnsureFirePoint();
            EnsureCameraReference();
            EnsureBulletPrefab();

            // 1. Trazar rayo desde el centro de la cámara (donde apunta la mira de disparo)
            Vector3 targetPoint;
            if (aimCamera != null)
            {
                Ray ray = new Ray(aimCamera.position, aimCamera.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, hitLayers, QueryTriggerInteraction.Ignore))
                {
                    targetPoint = hit.point;
                }
                else
                {
                    targetPoint = ray.GetPoint(maxRaycastDistance);
                }
            }
            else
            {
                targetPoint = firePoint.position + firePoint.forward * maxRaycastDistance;
            }

            // 2. Calcular la dirección balística precisa desde el cañón (firePoint) hacia el objetivo
            Vector3 shootDirection = (targetPoint - firePoint.position).normalized;

            // 3. Posición de salida segura (desplazada ligeramente al frente para evitar colisión con el cañón/jugador)
            Vector3 spawnPosition = firePoint.position + (shootDirection * 0.35f);

            // 4. Instanciar la bala o crear una de respaldo si no hay prefab
            GameObject bulletObj;
            if (bulletPrefab != null)
            {
                bulletObj = Instantiate(bulletPrefab, spawnPosition, Quaternion.LookRotation(shootDirection));
            }
            else
            {
                bulletObj = CreateFallbackBullet(spawnPosition, shootDirection);
            }

            // 5. Ajustar escala si el modelo es microscópico
            if (bulletObj.transform.localScale.x < 0.12f)
            {
                bulletObj.transform.localScale = Vector3.one * 0.2f;
            }

            // 6. Ignorar colisiones entre la bala y el jugador/arma para evitar auto-destrucción inmediata
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

            // 7. Configurar script Bullet y daño según la carga del ataque
            float calculatedDamage = Mathf.Lerp(baseDamage, maxDamage, Mathf.Clamp01(chargeRatio));
            Bullet bullet = bulletObj.GetComponent<Bullet>();
            if (bullet == null)
            {
                bullet = bulletObj.AddComponent<Bullet>();
            }
            bullet.Initialize(calculatedDamage, shootForce);

            // 8. Añadir estela visual luminosa (Tracer) si no tiene
            if (addTracerTrail && bulletObj.GetComponent<TrailRenderer>() == null)
            {
                AddBulletTracer(bulletObj);
            }

            // 9. Aplicar impulso balístico
            Rigidbody rb = bulletObj.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = bulletObj.AddComponent<Rigidbody>();
                rb.useGravity = false;
            }
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(shootDirection * shootForce, ForceMode.VelocityChange);

            Debug.Log($"[ProjectileWeapon] 💥 ¡Bala disparada desde {firePoint.name}! Daño: {calculatedDamage:F1} | Dirección: {shootDirection}");

            // 10. Efectos audiovisuales y retroceso
            PlayMuzzleAndAudio();
            ApplyRecoil();
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

            if (audioSource != null && shootSound != null)
            {
                audioSource.pitch = Random.Range(0.96f, 1.04f);
                audioSource.PlayOneShot(shootSound);
            }
        }

        private void ApplyRecoil()
        {
            if (modelTransform != null)
            {
                modelTransform.localPosition -= new Vector3(0f, 0f, recoilKickBack);
                modelTransform.localRotation *= Quaternion.Euler(-recoilKickUp, 0f, 0f);
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
            if (bulletPrefab == null)
            {
#if UNITY_EDITOR
                bulletPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bullet.prefab");
                if (bulletPrefab == null)
                {
                    bulletPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3dModels/Bullet/Bullet.prefab");
                }

                if (bulletPrefab != null)
                {
                    Debug.Log($"[ProjectileWeapon] 📦 Auto-asignado '{bulletPrefab.name}' en BulletPrefab.");
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
