using UnityEngine;
using PurrNet;

namespace JuegoSinNombre.Gameplay
{
    public class PlayerCameraSetup : NetworkIdentity
    {
        [Header("Local Components")]
        [Tooltip("La cámara de este jugador (solo se activará para el jugador local).")]
        [SerializeField] private Camera _playerCamera;
        [Tooltip("El AudioListener de este jugador (solo se activará para el jugador local).")]
        [SerializeField] private AudioListener _audioListener;

        public Camera PlayerCamera => _playerCamera;
        public AudioListener AudioListener => _audioListener;

        private void Awake()
        {
            if (_playerCamera == null)
            {
                _playerCamera = GetComponentInChildren<Camera>(true);
            }
            if (_audioListener == null)
            {
                _audioListener = GetComponentInChildren<AudioListener>(true);
            }
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (isOwner && _playerCamera != null)
            {
                SetupLocalPlayerCamera();
            }
        }

        public void SetCameraActive(bool active)
        {
            if (_playerCamera != null)
            {
                _playerCamera.gameObject.SetActive(active);
                _playerCamera.enabled = active;
            }

            if (_audioListener != null)
            {
                _audioListener.enabled = active;
            }
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();

            if (!isOwner)
            {
                // Si es el clon del rival, desactivar su cámara y audio en nuestra pantalla
                if (_playerCamera != null)
                {
                    _playerCamera.gameObject.SetActive(false);
                    _playerCamera.enabled = false;
                }

                if (_audioListener != null)
                {
                    _audioListener.enabled = false;
                }
            }
            else
            {
                SetupLocalPlayerCamera();
            }
        }

        private void SetupLocalPlayerCamera()
        {
            // Activar nuestra cámara con prioridad y tag MainCamera
            if (_playerCamera != null)
            {
                _playerCamera.gameObject.SetActive(true);
                _playerCamera.enabled = true;
                _playerCamera.depth = 10;
                _playerCamera.tag = "MainCamera";
            }

            if (_audioListener != null)
            {
                _audioListener.enabled = true;
            }

            DeactivateSceneCameras();
        }

        private void DeactivateSceneCameras()
        {
            // Apagar cualquier cámara estática residual de la escena que no pertenezca a un jugador
            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam != _playerCamera && cam.GetComponentInParent<PlayerIdentity>() == null)
                {
                    cam.gameObject.SetActive(false);
                }
            }
        }
    }
}
