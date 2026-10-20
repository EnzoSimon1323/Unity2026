using System;
using UnityEngine;
using PurrNet;
using PurrNet.Transports;

using TMPro;

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

        [Header("Transport Reference (Optional)")]
        [SerializeField] private CompositeTransport _compositeTransport;

        public event Action OnHostStarted;
        public event Action OnServerStarted;
        public event Action OnClientConnected;
        public event Action OnDisconnected;

        public string CurrentRoomCode { get; private set; } = string.Empty;
        public string LocalPlayerName { get; private set; } = "Jugador";
        private string _remoteHostName = string.Empty;
        private string _remoteClientName = string.Empty;
        private bool _isHostSession = false;

        [System.Serializable]
        public struct PlayerLobbyInfo : PurrNet.Packing.IPackedAuto
        {
            public string playerName;
            public bool isHost;
        }

        private void Start()
        {
            if (_compositeTransport == null && NetworkManager.main != null)
            {
                _compositeTransport = NetworkManager.main.GetComponent<CompositeTransport>();
            }

            if (_addressOrCodeInputField != null)
            {
                _addressOrCodeInputField.onValueChanged.AddListener(OnCodeInputChanged);
                OnCodeInputChanged(_addressOrCodeInputField.text);
            }

            SubscribeToNetworkEvents();
            SetPanelsState(inLobby: false);

#if UNITY_EDITOR
            if (_autoStartAsHostInEditor && NetworkManager.main != null)
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

            UnsubscribeFromNetworkEvents();
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
            if (NetworkManager.main == null)
            {
                Debug.LogError("[NetworkRoomController] NetworkManager.main no encontrado en la escena.");
                return;
            }

            _isHostSession = true;
            CapturePlayerName();

            // Generar código de sala amigable
            CurrentRoomCode = GenerateRandomRoomCode(_roomCodeLength);

            // Si existe PurrTransport (directo o dentro de CompositeTransport), asignarle el roomName
            PurrTransport purrTransport = GetActiveTransport<PurrTransport>();
            if (purrTransport != null)
            {
                purrTransport.roomName = CurrentRoomCode;
                Debug.Log($"[NetworkRoomController] PurrTransport roomName asignado: {CurrentRoomCode}");
            }
            else
            {
                Debug.Log($"[NetworkRoomController] Código de sala generado: {CurrentRoomCode}");
            }

            // 1. Activar el panel del lobby primero para que sus hijos estén activos
            SetPanelsState(inLobby: true);

            // 2. Actualizar los textos de la UI
            UpdateRoomCodeDisplay();
            UpdateLobbyUI(asHost: true);

            Debug.Log($"[NetworkRoomController] Host iniciado por '{LocalPlayerName}' con Sala '{CurrentRoomCode}'");
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
            SetPanelsState(inLobby: true);
            NetworkManager.main.StartServer();
            OnServerStarted?.Invoke();
        }

        public void StartClient()
        {
            StartClient(_defaultAddress, _defaultPort);
        }

        /// <summary>
        /// Método pensado para invocar directamente desde un botón de la UI (OnClick).
        /// Toma el texto ingresado en el TMP_InputField configurado.
        /// </summary>
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
            if (string.IsNullOrWhiteSpace(addressOrRoomCode))
            {
                Debug.LogWarning("[NetworkRoomController] Código de sala o dirección no válido.");
                return;
            }

            string trimmedInput = addressOrRoomCode.Trim();

            // Determinar si el valor ingresado es un par IP:Puerto o simplemente una IP / Room Code
            string targetAddress = trimmedInput;
            ushort targetPort = _defaultPort;

            if (trimmedInput.Contains(":"))
            {
                string[] parts = trimmedInput.Split(':');
                targetAddress = parts[0];
                if (parts.Length > 1 && ushort.TryParse(parts[1], out ushort parsedPort))
                {
                    targetPort = parsedPort;
                }
            }

            // Identificar cuál transporte es el principal o activo en NetworkManager
            PurrTransport purrTransport = GetActiveTransport<PurrTransport>();
            UDPTransport udpTransport = GetActiveTransport<UDPTransport>();

            bool isIpAddress = targetAddress.Contains(".") || targetAddress.Equals("localhost", StringComparison.OrdinalIgnoreCase);

            // Si el transporte principal asignado en NetworkManager es UDPTransport (o no hay PurrTransport activo)
            bool isUdpActive = (NetworkManager.main != null && NetworkManager.main.transport is UDPTransport) ||
                               (udpTransport != null && purrTransport == null);

            if (isUdpActive || isIpAddress)
            {
                // En testeo local / UDP: si es un código de sala o no es una IP, conectamos al Host local 127.0.0.1
                string destinationIp = isIpAddress ? targetAddress : _defaultAddress;

                if (udpTransport != null)
                {
                    udpTransport.address = destinationIp;
                    udpTransport.serverPort = targetPort;
                }

                if (NetworkManager.main != null && udpTransport != null)
                {
                    NetworkManager.main.transport = udpTransport;
                }

                Debug.Log($"[NetworkRoomController] Conectando por UDPTransport a {destinationIp}:{targetPort}");
                targetAddress = destinationIp;
            }
            else if (purrTransport != null)
            {
                purrTransport.roomName = targetAddress;

                if (NetworkManager.main != null)
                {
                    NetworkManager.main.transport = purrTransport;
                }

                Debug.Log($"[NetworkRoomController] Conectando a sala PurrTransport: '{targetAddress}'");
            }

            StartClient(targetAddress, targetPort);
        }

        public void StartClient(string address, ushort port)
        {
            if (NetworkManager.main == null)
            {
                Debug.LogError("[NetworkRoomController] NetworkManager.main no encontrado en la escena.");
                return;
            }

            CapturePlayerName();
            SetPanelsState(inLobby: true);
            UpdateLobbyUI(asHost: false);

            Debug.Log($"[NetworkRoomController] Conectando como Cliente a {address}:{port}...");
            NetworkManager.main.StartClient();
            OnClientConnected?.Invoke();
        }

        /// <summary>
        /// Cambia sincronizadamente la escena para todos los clientes conectados usando PurrNet.
        /// Sólo el servidor/host puede ejecutar esta acción.
        /// </summary>
        public void StartMatch()
        {
            StartMatch(_gameplaySceneName);
        }

        public void StartMatch(string sceneName)
        {
            if (NetworkManager.main == null)
            {
                Debug.LogError("[NetworkRoomController] NetworkManager.main no encontrado.");
                return;
            }

            if (!NetworkManager.main.isServer)
            {
                Debug.LogWarning("[NetworkRoomController] Solo el Servidor o Host puede iniciar la transición de escena.");
                return;
            }

            if (NetworkManager.main.sceneModule == null)
            {
                Debug.LogError("[NetworkRoomController] sceneModule no está inicializado en NetworkManager.");
                return;
            }

            Debug.Log($"[NetworkRoomController] Cambiando escena sincronizada a: {sceneName}...");
            NetworkManager.main.sceneModule.LoadSceneAsync(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
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

            _isHostSession = false;
            _remoteHostName = string.Empty;
            _remoteClientName = string.Empty;
            CurrentRoomCode = string.Empty;
            UpdateRoomCodeDisplay();
            SetPanelsState(inLobby: false);
            OnDisconnected?.Invoke();
        }

        private void CapturePlayerName()
        {
            if (_playerNameInputField != null && !string.IsNullOrWhiteSpace(_playerNameInputField.text))
            {
                LocalPlayerName = _playerNameInputField.text.Trim();
            }
            else
            {
                LocalPlayerName = "Jugador";
            }
        }

        private void SubscribeToNetworkEvents()
        {
            if (NetworkManager.main != null)
            {
                NetworkManager.main.onPlayerJoined += HandlePlayerJoined;
                NetworkManager.main.onPlayerLeft += HandlePlayerLeft;
                NetworkManager.main.onClientConnectionState += HandleClientConnectionState;
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (NetworkManager.main != null)
            {
                NetworkManager.main.onPlayerJoined -= HandlePlayerJoined;
                NetworkManager.main.onPlayerLeft -= HandlePlayerLeft;
                NetworkManager.main.onClientConnectionState -= HandleClientConnectionState;

                if (NetworkManager.main.broadcastModule != null)
                {
                    NetworkManager.main.broadcastModule.Unsubscribe<PlayerLobbyInfo>(OnPlayerLobbyInfoReceived);
                }
            }
        }

        private void HandleClientConnectionState(ConnectionState state)
        {
            if (state == ConnectionState.Connected)
            {
                EnsureBroadcastSubscribed();

                // Si soy el Cliente, envío mi nombre al Host
                if (!_isHostSession)
                {
                    SendLocalLobbyInfo();
                }
            }
        }

        private void EnsureBroadcastSubscribed()
        {
            if (NetworkManager.main != null && NetworkManager.main.broadcastModule != null)
            {
                NetworkManager.main.broadcastModule.Unsubscribe<PlayerLobbyInfo>(OnPlayerLobbyInfoReceived);
                NetworkManager.main.broadcastModule.Subscribe<PlayerLobbyInfo>(OnPlayerLobbyInfoReceived);
            }
        }

        private void SendLocalLobbyInfo()
        {
            if (NetworkManager.main != null && NetworkManager.main.broadcastModule != null)
            {
                var info = new PlayerLobbyInfo
                {
                    playerName = LocalPlayerName,
                    isHost = _isHostSession
                };

                if (_isHostSession)
                {
                    // El Host sí puede enviar a todos los clientes
                    NetworkManager.main.broadcastModule.SendToAll(info);
                }
                else
                {
                    // El Cliente solo puede enviar al Servidor/Host
                    NetworkManager.main.broadcastModule.SendToServer(info);
                }
            }
        }

        private void OnPlayerLobbyInfoReceived(PlayerID sender, PlayerLobbyInfo info, bool asServer)
        {
            if (info.isHost)
            {
                _remoteHostName = info.playerName;
            }
            else
            {
                _remoteClientName = info.playerName;

                // Si soy el Host y me llega el nombre del nuevo Cliente, le respondo con mi nombre para que lo vea de inmediato
                if (_isHostSession)
                {
                    SendLocalLobbyInfo();
                }
            }

            UpdateLobbyUI();
        }

        private void HandlePlayerJoined(PlayerID player, bool isReconnect, bool asServer)
        {
            EnsureBroadcastSubscribed();

            if (_isHostSession)
            {
                // El Host le envía su nombre al cliente que se acaba de conectar
                SendLocalLobbyInfo();
            }

            UpdateLobbyUI();
        }

        private void HandlePlayerLeft(PlayerID player, bool asServer)
        {
            if (_isHostSession)
            {
                _remoteClientName = string.Empty;
            }
            else
            {
                _remoteHostName = string.Empty;
            }

            UpdateLobbyUI();
        }

        private void UpdateLobbyUI(bool? asHost = null)
        {
            bool isHost = asHost ?? _isHostSession;
            int count = NetworkManager.main != null ? NetworkManager.main.playerCount : 0;

            if (_hostNameText != null)
            {
                if (isHost)
                {
                    _hostNameText.text = LocalPlayerName;
                }
                else
                {
                    _hostNameText.text = !string.IsNullOrEmpty(_remoteHostName) ? _remoteHostName : "Host";
                }
            }
            else
            {
                Debug.LogWarning("[NetworkRoomController] '_hostNameText' no está asignado en el Inspector.");
            }

            if (_clientNameText != null)
            {
                if (isHost)
                {
                    if (!string.IsNullOrEmpty(_remoteClientName))
                    {
                        _clientNameText.text = _remoteClientName;
                    }
                    else
                    {
                        _clientNameText.text = count > 1 ? "Conectando..." : "Esperando rival...";
                    }
                }
                else
                {
                    _clientNameText.text = LocalPlayerName;
                }
            }
            else
            {
                Debug.LogWarning("[NetworkRoomController] '_clientNameText' no está asignado en el Inspector.");
            }

            if (_startMatchButton != null)
            {
                // Solo el Host puede ver/pulsar el botón para empezar la partida
                _startMatchButton.SetActive(isHost);
            }
        }

        private void SetPanelsState(bool inLobby)
        {
            if (_menuPanel != null)
            {
                _menuPanel.SetActive(!inLobby);
            }
            else
            {
                Debug.LogWarning("[NetworkRoomController] '_menuPanel' no está asignado en el Inspector.");
            }

            if (_lobbyPanel != null)
            {
                _lobbyPanel.SetActive(inLobby);
            }
            else
            {
                Debug.LogWarning("[NetworkRoomController] '_lobbyPanel' no está asignado en el Inspector.");
            }
        }

        private void UpdateRoomCodeDisplay()
        {
            if (_roomCodeDisplayText != null)
            {
                // Muestra únicamente la combinación/código sin texto adicional
                _roomCodeDisplayText.text = string.IsNullOrEmpty(CurrentRoomCode) 
                    ? string.Empty 
                    : CurrentRoomCode;
                Debug.Log($"[NetworkRoomController] Código asignado a la UI: '{_roomCodeDisplayText.text}'");
            }
            else
            {
                Debug.LogWarning("[NetworkRoomController] '_roomCodeDisplayText' no está asignado en el Inspector.");
            }
        }

        private static string GenerateRandomRoomCode(int length)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Sin caracteres ambiguos como O, 0, I, 1
            char[] stringChars = new char[Mathf.Max(3, length)];
            var random = new System.Random();

            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            return new string(stringChars);
        }

        private T GetActiveTransport<T>() where T : GenericTransport
        {
            if (NetworkManager.main == null)
                return null;

            // 1. Buscar en el mismo objeto del NetworkManager
            if (NetworkManager.main.TryGetComponent(out T directComp))
                return directComp;

            // 2. Si usa CompositeTransport, verificar si lo contiene
            if (NetworkManager.main.TryGetComponent(out CompositeTransport composite))
            {
                if (composite.TryGetTransport<T>(out var internalTransport))
                    return internalTransport;
            }

            // 3. Buscar en hijos
            return NetworkManager.main.GetComponentInChildren<T>();
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
