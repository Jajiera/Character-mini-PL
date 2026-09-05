using UnityEngine;
using Scripts.Core;
using Scripts.Character;

namespace Scripts.Combat
{
    public class Bullet : MonoBehaviour
    {
        [Header("Bullet Attributes")]
        [SerializeField] private float damage = 20f;
        [SerializeField] private float speed = 60f;
        [SerializeField] private float lifeTime = 5f;
        [SerializeField] private float impactForce = 8f;

        [Header("Impact Effects")]
        [UnityEngine.Serialization.FormerlySerializedAs("ImpactParticles")]
        [SerializeField] private GameObject impactParticles;

        private Rigidbody rb;
        private bool hasImpacted = false;
        private float spawnTime;

        public float Damage => damage;
        public float Speed => speed;

        public void Initialize(float bulletDamage, float bulletSpeed)
        {
            damage = bulletDamage;
            speed = bulletSpeed;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            spawnTime = Time.time;
        }

        private void Start()
        {
            Destroy(gameObject, lifeTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleHit(other.gameObject, transform.position);
        }

        private void OnCollisionEnter(Collision collision)
        {
            Vector3 contactPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
            HandleHit(collision.gameObject, contactPoint);
        }

        private void HandleHit(GameObject hitObject, Vector3 contactPoint)
        {
            if (hasImpacted) return;

            // Ignorar al jugador, sus componentes hijos y otras balas
            if (hitObject.CompareTag("Player") || hitObject.CompareTag("Bullet") || hitObject.transform.root.CompareTag("Player"))
            {
                return;
            }

            // Ignorar colisiones prematuras contra la jerarquía del jugador
            if (Time.time < spawnTime + 0.1f && (hitObject.GetComponentInParent<PlayerCharacter>() != null || hitObject.name.Contains("Player") || hitObject.name.Contains("Gun")))
            {
                return;
            }

            hasImpacted = true;

            // 1. Aplicar daño si el objetivo implementa IDamageable
            IDamageable damageable = hitObject.GetComponent<IDamageable>() ?? hitObject.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage, contactPoint, transform.forward);
            }

            // 2. Aplicar fuerza física si tiene Rigidbody
            if (hitObject.TryGetComponent(out Rigidbody hitRb))
            {
                hitRb.AddForce(transform.forward * impactForce, ForceMode.Impulse);
            }

            Impact(contactPoint);
        }

        private void Impact(Vector3 contactPoint)
        {
            if (impactParticles != null)
            {
                GameObject particles = Instantiate(impactParticles, contactPoint, Quaternion.identity);
                Destroy(particles, 1.5f);
            }

            Destroy(gameObject);
        }
    }
}
