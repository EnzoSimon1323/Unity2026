using System;
using UnityEngine;
using PurrNet;

namespace JuegoSinNombre
{
    [DisallowMultipleComponent]
    public class PlayerIdentity : PlayerIdentity<PlayerIdentity>
    {
        public static event Action<PlayerIdentity> OnLocalPlayerSpawned;
        public static event Action<PlayerIdentity> OnLocalPlayerDespawned;

        [Header("Cached Modules")]
        [SerializeField] private HealthModule _healthModule;
        [SerializeField] private PlayerCameraSetup _cameraSetup;
        [SerializeField] private RagdollController _ragdollController;

        public HealthModule HealthModule => _healthModule;
        public PlayerCameraSetup CameraSetup => _cameraSetup;
        public RagdollController RagdollController => _ragdollController;

        private void Awake()
        {
            CacheReferences();
        }

        private void Reset()
        {
            CacheReferences();
        }

        private void CacheReferences()
        {
            if (_healthModule == null)
            {
                TryGetComponent(out _healthModule);
            }

            if (_cameraSetup == null)
            {
                TryGetComponent(out _cameraSetup);
            }

            if (_ragdollController == null)
            {
                TryGetComponent(out _ragdollController);
            }
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();

            if (isOwner)
            {
                if (GetComponent<PlayerTurnStatusUI>() == null)
                {
                    gameObject.AddComponent<PlayerTurnStatusUI>();
                }

                OnLocalPlayerSpawned?.Invoke(this);
            }
        }

        protected override void OnDespawned(bool asServer)
        {
            if (isOwner)
            {
                OnLocalPlayerDespawned?.Invoke(this);
            }

            base.OnDespawned(asServer);
        }

        protected override void OnDestroy()
        {
            if (isOwner)
            {
                OnLocalPlayerDespawned?.Invoke(this);
            }

            base.OnDestroy();
        }
    }
}