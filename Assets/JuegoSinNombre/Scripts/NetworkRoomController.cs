using System;
using UnityEngine;
using PurrNet;
using PurrNet.Transports;

namespace JuegoSinNombre
{
    [DisallowMultipleComponent]
    public class NetworkRoomController : MonoBehaviour
    {
        [Header("Default Connection Settings")]
        [SerializeField] private string _defaultAddress = "127.0.0.1";
        [SerializeField] private ushort _defaultPort = 7777;
        [SerializeField] private bool _autoStartAsHostInEditor = false;

        [Header("UI References")]
        [Tooltip("Panel del menú de inicio que se ocultará automáticamente al iniciar como Host o Cliente.")]
        [SerializeField] private GameObject _menuPanel;

        [Header("Transport Reference (Optional)")]
        [SerializeField] private CompositeTransport _compositeTransport;

        public event Action OnHostStarted;
        public event Action OnServerStarted;
        public event Action OnClientConnected;
        public event Action OnDisconnected;

        private void Start()
        {
            if (_compositeTransport == null && NetworkManager.main != null)
            {
                _compositeTransport = NetworkManager.main.GetComponent<CompositeTransport>();
            }

#if UNITY_EDITOR
            if (_autoStartAsHostInEditor && NetworkManager.main != null)
            {
                StartHost();
            }
#endif
        }

        public void StartHost()
        {
            if (NetworkManager.main == null)
            {
                Debug.LogError("[NetworkRoomController] NetworkManager.main no encontrado en la escena.");
                return;
            }

            Debug.Log("[NetworkRoomController] Iniciando Host (Servidor + Cliente local)...");
            SetMenuVisibility(false);
            NetworkManager.main.StartHost();
            OnHostStarted?.Invoke();
        }

        public void StartServer()
        {
            if (NetworkManager.main == null)
            {
                Debug.LogError("[NetworkRoomController] NetworkManager.main no encontrado en la escena.");
                return;
            }

            Debug.Log("[NetworkRoomController] Iniciando Servidor dedicado...");
            SetMenuVisibility(false);
            NetworkManager.main.StartServer();
            OnServerStarted?.Invoke();
        }

        public void StartClient()
        {
            StartClient(_defaultAddress, _defaultPort);
        }

        public void StartClient(string address, ushort port)
        {
            if (NetworkManager.main == null)
            {
                Debug.LogError("[NetworkRoomController] NetworkManager.main no encontrado en la escena.");
                return;
            }

            Debug.Log($"[NetworkRoomController] Conectando como Cliente a {address}:{port}...");
            SetMenuVisibility(false);
            NetworkManager.main.StartClient();
            OnClientConnected?.Invoke();
        }

        public void Disconnect()
        {
            if (NetworkManager.main == null)
                return;

            Debug.Log("[NetworkRoomController] Deteniendo conexiones de red...");

            if (NetworkManager.main.isClient)
            {
                NetworkManager.main.StopClient();
            }

            if (NetworkManager.main.isServer)
            {
                NetworkManager.main.StopServer();
            }

            SetMenuVisibility(true);
            OnDisconnected?.Invoke();
        }

        private void SetMenuVisibility(bool isVisible)
        {
            if (_menuPanel != null)
            {
                _menuPanel.SetActive(isVisible);
            }
        }
    }
}
