using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    private float _currentHealth;

    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnDeath;

    public bool IsDead => _currentHealth <= 0;

    private void Awake()
    {
        _currentHealth = maxHealth;
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (IsDead) return;

        _currentHealth -= amount;
        OnHealthChanged?.Invoke(_currentHealth / maxHealth);

        Debug.Log($"[{gameObject.name}] Hit! HP: {_currentHealth}");

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"[{gameObject.name}] Died.");
        OnDeath?.Invoke();
    }
}