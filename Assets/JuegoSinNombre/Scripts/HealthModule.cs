using System;
using UnityEngine;
using PurrNet;

namespace JuegoSinNombre
{
    public class HealthModule : NetworkIdentity
    {
        [Header("Settings")]
        [SerializeField] private float _maxHealth = 100f;

        [Header("Network State")]
        [SerializeField] private SyncVar<float> _currentHealth = new(100f);

        public float CurrentHealth => _currentHealth.value;
        public float MaxHealth => _maxHealth;
        public bool IsDead => _currentHealth.value <= 0f;

        public event Action<float, float> OnHealthChanged; // currentHealth, maxHealth
        public event Action OnDeath;

        protected override void OnSpawned()
        {
            base.OnSpawned();
            
            if (isServer)
            {
                _currentHealth.value = _maxHealth;
            }
            
            _currentHealth.onChanged += HandleHealthChanged;
        }
        
        protected override void OnDespawned()
        {
            base.OnDespawned();
            _currentHealth.onChanged -= HandleHealthChanged;
        }

        private void HandleHealthChanged(float newHealth)
        {
            OnHealthChanged?.Invoke(newHealth, _maxHealth);
            if (newHealth <= 0f)
            {
                OnDeath?.Invoke();
            }
        }
        
        [ServerRpc(requireOwnership: false)]
        public void ApplyDamageServerRpc(float damage)
        {
            if (!isServer || IsDead || damage <= 0f) 
                return;

            _currentHealth.value = Mathf.Clamp(_currentHealth.value - damage, 0f, _maxHealth);
        }
        
        [ServerRpc(requireOwnership: false)]
        public void HealServerRpc(float amount)
        {
            if (!isServer || IsDead || amount <= 0f) 
                return;

            _currentHealth.value = Mathf.Clamp(_currentHealth.value + amount, 0f, _maxHealth);
        }
    }
}

