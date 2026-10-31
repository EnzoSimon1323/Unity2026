using System;
using UnityEngine;
using TMPro;

namespace JuegoSinNombre.Gameplay
{
    /// <summary>
    /// Controlador y Presentador UI para el menú principal y la sala de espera (Lobby).
    /// Conecta los elementos gráficos (botones, campos de texto y paneles) con
    /// el servicio de red subyacente (RoomNetworkService).
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkRoomController : MonoBehaviour
    {
        [Header("Default Connection Settings")]
        [SerializeField] private string _defaultAddress = "127.0.0.1";
        [SerializeField] private ushort _defaultPort = 7777;
        [SerializeField] private bool _autoStartAsHostInEditor = false;

        [Header("UI References")]
        [Tooltip("Panel del menú de inicio (con botones Host, Join, InputField, etc.).")]
        [SerializeField] private GameObject _menuPanel;

        [Tooltip("Panel de la sala de espera / lobby que se abre tras iniciar como Host o Cliente.")]
        [SerializeField] private GameObject _lobbyPanel;

        [Tooltip("Input field donde el usuario escribe su propio apodo/nombre.")]
        [SerializeField] private TMP_InputField _playerNameInputField;

        [Tooltip("Input field opcional donde el usuario escribe la IP o el Room Code.")]
        [SerializeField] private TMP_InputField _addressOrCodeInputField;

        [Tooltip("Botón de Join en el menú principal que se habilitará solo cuando haya código escrito.")]
        [SerializeField] private UnityEngine.UI.Button _joinButton;

        [Tooltip("Texto UI (TextMeshPro) donde se mostrará el código de sala generado para el Host.")]
        [SerializeField] private TMP_Text _roomCodeDisplayText;

        [Tooltip("Texto UI donde se muestra el nombre del Host en la sala de espera.")]
        [SerializeField] private TMP_Text _hostNameText;

        [Tooltip("Texto UI donde se muestra el nombre del Cliente (o 'Esperando jugador...').")]
        [SerializeField] private TMP_Text _clientNameText;

        [Tooltip("Botón de Iniciar Partida en el lobby (solo visible/interactuable para el Host).")]
        [SerializeField] private GameObject _startMatchButton;

        [Header("Room Code Settings")]
        [Tooltip("Largo del código alfanumérico generado al ser Host (ej: 5 caracteres: 'X9K2L').")]
        [SerializeField] private int _roomCodeLength = 5;

        [Header("Scene Transition Settings")]
        [Tooltip("Nombre de la escena de combate/mapa a cargar.")]
        [SerializeField] private string _gameplaySceneName = "Test";

        private readonly RoomNetworkService _networkService = new();

        public event Action OnHostStarted
        {
            add => _networkService.OnHostStarted += value;
            remove => _networkService.OnHostStarted -= value;
        }

        public event Action OnServerStarted
        {
            add => _networkService.OnServerStarted += value;
            remove => _networkService.OnServerStarted -= value;
        }

        public event Action OnClientConnected
        {
            add => _networkService.OnClientConnected += value;
            remove => _networkService.OnClientConnected -= value;
        }

        public event Action OnDisconnected
        {
            add => _networkService.OnDisconnected += value;
            remove => _networkService.OnDisconnected -= value;
        }

        public string CurrentRoomCode => _networkService.CurrentRoomCode;
        public string LocalPlayerName => _networkService.LocalPlayerName;

        private void Start()
        {
            _networkService.Initialize();
            _networkService.OnLobbyPlayersUpdated += HandleLobbyPlayersUpdated;
            _networkService.OnRoomCodeUpdated += HandleRoomCodeUpdated;
            _networkService.OnDisconnected += HandleDisconnected;

            if (_addressOrCodeInputField != null)
            {
                _addressOrCodeInputField.onValueChanged.AddListener(OnCodeInputChanged);
                OnCodeInputChanged(_addressOrCodeInputField.text);
            }

            SetPanelsState(inLobby: false);

#if UNITY_EDITOR
            if (_autoStartAsHostInEditor && PurrNet.NetworkManager.main != null)
            {
                StartHost();
            }
#endif
        }

        private void OnDestroy()
        {
            if (_addressOrCodeInputField != null)
            {
                _addressOrCodeInputField.onValueChanged.RemoveListener(OnCodeInputChanged);
            }

            _networkService.OnLobbyPlayersUpdated -= HandleLobbyPlayersUpdated;
            _networkService.OnRoomCodeUpdated -= HandleRoomCodeUpdated;
            _networkService.OnDisconnected -= HandleDisconnected;
            _networkService.Dispose();
        }

        private void OnCodeInputChanged(string text)
        {
            if (_joinButton != null)
            {
                _joinButton.interactable = !string.IsNullOrWhiteSpace(text);
            }
        }

        public void StartHost()
        {
            string name = GetEnteredPlayerName();
            SetPanelsState(inLobby: true);
            _networkService.StartHost(name, _roomCodeLength);
            UpdateStartMatchButtonVisibility(_networkService.IsHostSession);
        }

        public void StartServer()
        {
            SetPanelsState(inLobby: true);
            _networkService.StartServer();
            UpdateStartMatchButtonVisibility(false);
        }

        public void StartClient()
        {
            StartClient(_defaultAddress);
        }

        public void StartClientFromInputField()
        {
            if (_addressOrCodeInputField != null && !string.IsNullOrWhiteSpace(_addressOrCodeInputField.text))
            {
                StartClient(_addressOrCodeInputField.text);
            }
            else
            {
                Debug.LogWarning("[NetworkRoomController] Debe ingresar un código de sala o IP para unirse.");
            }
        }

        public void StartClient(string addressOrRoomCode)
        {
            string name = GetEnteredPlayerName();
            SetPanelsState(inLobby: true);
            _networkService.StartClient(addressOrRoomCode, name, _defaultAddress, _defaultPort);
            UpdateStartMatchButtonVisibility(false);
        }

        public void ChangeToGameplayScene()
        {
            _networkService.ChangeScene(_gameplaySceneName);
        }

        public void Disconnect()
        {
            _networkService.Disconnect();
        }

        private void HandleDisconnected()
        {
            SetPanelsState(inLobby: false);
            UpdateStartMatchButtonVisibility(false);
        }

        private void HandleRoomCodeUpdated(string code)
        {
            if (_roomCodeDisplayText != null)
            {
                _roomCodeDisplayText.text = code;
            }
        }

        private void HandleLobbyPlayersUpdated(string hostName, string clientName)
        {
            if (_hostNameText != null)
            {
                _hostNameText.text = !string.IsNullOrEmpty(hostName) ? hostName : "Host";
            }

            if (_clientNameText != null)
            {
                _clientNameText.text = !string.IsNullOrEmpty(clientName) ? clientName : "Esperando rival...";
            }

            UpdateStartMatchButtonVisibility(_networkService.IsHostSession);
        }

        private void UpdateStartMatchButtonVisibility(bool isHost)
        {
            if (_startMatchButton != null)
            {
                _startMatchButton.SetActive(isHost);
            }
        }

        private string GetEnteredPlayerName()
        {
            if (_playerNameInputField != null && !string.IsNullOrWhiteSpace(_playerNameInputField.text))
            {
                return _playerNameInputField.text.Trim();
            }
            return "Jugador";
        }

        private void SetPanelsState(bool inLobby)
        {
            if (_menuPanel != null) _menuPanel.SetActive(!inLobby);
            if (_lobbyPanel != null) _lobbyPanel.SetActive(inLobby);
        }
    }
}
