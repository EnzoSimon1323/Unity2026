using System;
using UnityEngine;
using PurrNet;

namespace JuegoSinNombre
{
    public class HealthModule : NetworkIdentity
    {
        public static HealthModule LocalPlayer { get; private set; }
        public static event Action<HealthModule> OnLocalPlayerSpawned;
        public static event Action OnLocalPlayerDespawned;

        [Header("Settings")]
        [SerializeField] private float _maxHealth = 100f;

        [Header("Network State")]
        public SyncVar<float> currentHealth = new(100f);

        public float CurrentHealth => currentHealth.value;
        public float MaxHealth => _maxHealth;
        public bool IsDead => currentHealth.value <= 0f;

        public event Action OnDeath;

        protected override void OnSpawned()
        {
            base.OnSpawned();
            
            if (isServer)
            {
                currentHealth.value = _maxHealth;
            }
            
            currentHealth.onChanged += CheckDeathCondition;

            if (isOwner)
            {
                LocalPlayer = this;
                OnLocalPlayerSpawned?.Invoke(this);
            }
        }
        
        protected override void OnDespawned()
        {
            base.OnDespawned();
            currentHealth.onChanged -= CheckDeathCondition;

            if (isOwner && LocalPlayer == this)
            {
                LocalPlayer = null;
                OnLocalPlayerDespawned?.Invoke();
            }
        }

        private void CheckDeathCondition(float newHealth)
        {
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

            currentHealth.value = Mathf.Clamp(currentHealth.value - damage, 0f, _maxHealth);
        }
        
        [ServerRpc(requireOwnership: false)]
        public void HealServerRpc(float amount)
        {
            if (!isServer || IsDead || amount <= 0f) 
                return;

            currentHealth.value = Mathf.Clamp(currentHealth.value + amount, 0f, _maxHealth);
        }
    }
}

