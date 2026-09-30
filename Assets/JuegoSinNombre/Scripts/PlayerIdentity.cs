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

        public HealthModule HealthModule => _healthModule;
        public PlayerCameraSetup CameraSetup => _cameraSetup;

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
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();

            if (isOwner)
            {
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
