using UnityEngine;

public class Target : MonoBehaviour
{
    [SerializeField] private int MaxHealth = 100;
    private int currentHealth;

    private void Start()
    {
        currentHealth = MaxHealth;
    }

    private void Update()
    {
        if (currentHealth <= 0)
            Destroy(gameObject);
    }

    public void TakeDamage(int amount)
    {
        currentHealth = Mathf.Max(0, currentHealth - amount);
    }
}
