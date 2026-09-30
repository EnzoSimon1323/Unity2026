using UnityEngine;
using PurrNet;

namespace JuegoSinNombre
{
    public class PlayerCameraSetup : NetworkIdentity
    {
        [Header("Local Components")]
        [Tooltip("La cámara de este jugador (solo se activará para el jugador local).")]
        [SerializeField] private Camera _playerCamera;
        [Tooltip("El AudioListener de este jugador (solo se activará para el jugador local).")]
        [SerializeField] private AudioListener _audioListener;

        protected override void OnSpawned()
        {
            base.OnSpawned();

            if (!isOwner)
            {
                // Si es el clon del rival, desactivar su cámara y audio en nuestra pantalla
                if (_playerCamera != null)
                {
                    _playerCamera.gameObject.SetActive(false);
                }

                if (_audioListener != null)
                {
                    _audioListener.enabled = false;
                }
            }
            else
            {
                // Si es nuestro propio personaje, activar su cámara y audio
                if (_playerCamera != null)
                {
                    _playerCamera.gameObject.SetActive(true);
                }

                if (_audioListener != null)
                {
                    _audioListener.enabled = true;
                }
            }
        }
    }
}
