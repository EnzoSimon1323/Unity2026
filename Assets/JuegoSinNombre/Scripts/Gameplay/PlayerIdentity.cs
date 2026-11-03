using System;
using UnityEngine;
using PurrNet;

namespace JuegoSinNombre.Gameplay
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

        public Vector3 InitialSpawnPosition { get; set; }
        public Quaternion InitialSpawnRotation { get; set; } = Quaternion.identity;

        private void Awake()
        {
            CacheReferences();
            InitialSpawnPosition = transform.position;
            InitialSpawnRotation = transform.rotation;
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

            // Guardar posición inicial de spawn
            InitialSpawnPosition = transform.position;
            InitialSpawnRotation = transform.rotation;

            if (isOwner)
            {
                OnLocalPlayerSpawned?.Invoke(this);
            }
        }

        /// <summary>
        /// Resetea autoritativamente el estado del personaje en TODOS los clientes para la nueva ronda:
        /// - Reposiciona al jugador en su spawn point original.
        /// - Desactiva el ragdoll y reconstruye la pose de pie con el Animator.
        /// - Resetea arcos, apuntado y modificadores.
        /// </summary>
        [ObserversRpc(runLocally: true)]
        public void ResetPlayerRoundStateObserversRpc(Vector3 spawnPos, Quaternion spawnRot)
        {
            if (spawnPos != Vector3.zero)
            {
                transform.position = spawnPos;
                transform.rotation = spawnRot;

                if (TryGetComponent(out Rigidbody rootRb))
                {
                    rootRb.linearVelocity = Vector3.zero;
                    rootRb.angularVelocity = Vector3.zero;
                    rootRb.position = spawnPos;
                    rootRb.rotation = spawnRot;
                }
            }

            if (_ragdollController != null)
            {
                _ragdollController.ResetRagdoll();
            }

            if (TryGetComponent(out ArcherPlayer archer))
            {
                archer.ResetModifiers();
                archer.SetCanShoot(false);
            }

            if (TryGetComponent(out AimController aim))
            {
                aim.ResetAim();
            }

            if (TryGetComponent(out PlayerShooter shooter))
            {
                shooter.ResetRoundShotRpc();
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
