using UnityEngine;

namespace Scripts.Core
{
    public interface IDamageable
    {
        void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection);
        bool IsAlive();
    }
}
