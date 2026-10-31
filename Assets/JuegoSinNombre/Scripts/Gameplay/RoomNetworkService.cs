using System;
using UnityEngine;
using PurrNet;
using PurrNet.Transports;

namespace JuegoSinNombre.Gameplay
{
    /// <summary>
    /// Servicio responsable exclusivo de la lógica de red para salas y lobby:
    /// - Configuración de transportes (PurrTransport / UDPTransport).
    /// - Generación autoritativa de códigos de sala alfanuméricos.
    /// - Manejo de conexiones (Host, Server, Client, Disconnect).
    /// - Sincronización de metadatos de lobby (apodos de host y cliente) vía broadcast.
    /// - Transición sincronizada de escenas.
    /// </summary>
    public class RoomNetworkService
    {
        [System.Serializable]
        public struct PlayerLobbyInfo : PurrNet.Packing.IPackedAuto
        {
            public string playerName;
            public bool isHost;
        }

        public event Action OnHostStarted;
        public event Action OnServerStarted;
        public event Action OnClientConnected;
        public event Action OnDisconnected;
        public event Action<string, string> OnLobbyPlayersUpdated;
        public event Action<string> OnRoomCodeUpdated;

        public string CurrentRoomCode { get; private set; } = string.Empty;
        public string LocalPlayerName { get; private set; } = "Jugador";
        public string RemoteHostName { get; private set; } = string.Empty;
        public string RemoteClientName { get; private set; } = string.Empty;
        public bool IsHostSession { get; private set; } = false;

        public void Initialize()
        {
            SubscribeToNetworkEvents();
        }

        public void Dispose()
        {
            UnsubscribeFromNetworkEvents();
        }

        public void StartHost(string playerName, int roomCodeLength = 5)
        {
            if (NetworkManager.main == null)
            {
                Debug.LogError("[RoomNetworkService] NetworkManager.main no encontrado en la escena.");
                return;
            }

            if (NetworkManager.main.isClient || NetworkManager.main.isServer)
            {
                Disconnect();
            }

            IsHostSession = true;
            LocalPlayerName = !string.IsNullOrWhiteSpace(playerName) ? playerName.Trim() : "Jugador";
            CurrentRoomCode = GenerateRandomRoomCode(roomCodeLength);

            PurrTransport purrTransport = GetActiveTransport<PurrTransport>();
            if (purrTransport != null)
            {
                purrTransport.roomName = CurrentRoomCode;
                Debug.Log($"[RoomNetworkService] PurrTransport roomName asignado: {CurrentRoomCode}");
            }

            OnRoomCodeUpdated?.Invoke(CurrentRoomCode);
            Debug.Log($"[RoomNetworkService] Host iniciado por '{LocalPlayerName}' con Sala '{CurrentRoomCode}'");

            NetworkManager.main.StartHost();
            OnHostStarted?.Invoke();
            NotifyPlayersUpdated();
        }

        public void StartServer()
        {
            if (NetworkManager.main == null)
            {
                Debug.LogError("[RoomNetworkService] NetworkManager.main no encontrado en la escena.");
                return;
            }

            Debug.Log("[RoomNetworkService] Iniciando Servidor dedicado...");
            NetworkManager.main.StartServer();
            OnServerStarted?.Invoke();
        }

        public void StartClient(string addressOrRoomCode, string playerName, string defaultAddress = "127.0.0.1", ushort defaultPort = 7777)
        {
            if (string.IsNullOrWhiteSpace(addressOrRoomCode))
            {
                Debug.LogWarning("[RoomNetworkService] Código de sala o dirección no válido.");
                return;
            }

            IsHostSession = false;
            LocalPlayerName = !string.IsNullOrWhiteSpace(playerName) ? playerName.Trim() : "Jugador";

            string trimmedInput = addressOrRoomCode.Trim();
            string targetAddress = trimmedInput;
            ushort targetPort = defaultPort;

            if (trimmedInput.Contains(":"))
            {
                string[] parts = trimmedInput.Split(':');
                targetAddress = parts[0];
                if (parts.Length > 1 && ushort.TryParse(parts[1], out ushort parsedPort))
                {
                    targetPort = parsedPort;
                }
            }

            if (NetworkManager.main != null && (NetworkManager.main.isClient || NetworkManager.main.isServer))
            {
                Disconnect();
            }

            PurrTransport purrTransport = GetActiveTransport<PurrTransport>();
            UDPTransport udpTransport = GetActiveTransport<UDPTransport>();

            bool isIpAddress = targetAddress.Contains(".") || targetAddress.Equals("localhost", StringComparison.OrdinalIgnoreCase);
            bool isUdpActive = (NetworkManager.main != null && NetworkManager.main.transport is UDPTransport) ||
                               (udpTransport != null && purrTransport == null);

            if (NetworkManager.main != null && NetworkManager.main.transport is CompositeTransport composite)
            {
                if (isUdpActive || isIpAddress)
                {
                    string destinationIp = isIpAddress ? targetAddress : defaultAddress;
                    if (udpTransport != null)
                    {
                        udpTransport.address = destinationIp;
                        udpTransport.serverPort = targetPort;
                    }

                    if (composite.clientTransport != udpTransport)
                    {
                        if (composite.clientTransport != null && composite.clientTransport.transport.clientState != ConnectionState.Disconnected)
                        {
                            composite.clientTransport.transport.Disconnect();
                        }
                        composite.SetClientTransport<UDPTransport>();
                    }

                    Debug.Log($"[RoomNetworkService] Conectando por UDPTransport a {destinationIp}:{targetPort}");
                    targetAddress = destinationIp;
                }
                else if (purrTransport != null)
                {
                    targetAddress = targetAddress.ToUpperInvariant();
                    purrTransport.roomName = targetAddress;

                    if (composite.clientTransport != purrTransport)
                    {
                        if (composite.clientTransport != null && composite.clientTransport.transport.clientState != ConnectionState.Disconnected)
                        {
                            composite.clientTransport.transport.Disconnect();
                        }
                        composite.SetClientTransport<PurrTransport>();
                    }

                    Debug.Log($"[RoomNetworkService] Conectando a sala PurrTransport: '{targetAddress}'");
                }
            }
            else
            {
                if (isUdpActive || isIpAddress)
                {
                    string destinationIp = isIpAddress ? targetAddress : defaultAddress;
                    if (udpTransport != null)
                    {
                        udpTransport.address = destinationIp;
                        udpTransport.serverPort = targetPort;
                    }

                    if (NetworkManager.main != null && udpTransport != null && NetworkManager.main.transport != udpTransport)
                    {
                        NetworkManager.main.transport = udpTransport;
                    }

                    Debug.Log($"[RoomNetworkService] Conectando por UDPTransport a {destinationIp}:{targetPort}");
                    targetAddress = destinationIp;
                }
                else if (purrTransport != null)
                {
                    purrTransport.roomName = targetAddress;
                    if (NetworkManager.main != null && NetworkManager.main.transport != purrTransport)
                    {
                        NetworkManager.main.transport = purrTransport;
                    }

                    Debug.Log($"[RoomNetworkService] Conectando a sala PurrTransport: '{targetAddress}'");
                }
            }

            if (NetworkManager.main == null)
            {
                Debug.LogError("[RoomNetworkService] NetworkManager.main no encontrado.");
                return;
            }

            CurrentRoomCode = targetAddress;
            OnRoomCodeUpdated?.Invoke(CurrentRoomCode);

            Debug.Log($"[RoomNetworkService] Conectando como Cliente a {targetAddress}:{targetPort}...");
            NetworkManager.main.StartClient();
            OnClientConnected?.Invoke();
            NotifyPlayersUpdated();
        }

        public void ChangeScene(string sceneName)
        {
            if (NetworkManager.main == null)
            {
                Debug.LogError("[RoomNetworkService] NetworkManager.main no encontrado.");
                return;
            }

            if (!NetworkManager.main.isServer)
            {
                Debug.LogWarning("[RoomNetworkService] Solo el Servidor o Host puede iniciar la transición de escena.");
                return;
            }

            if (NetworkManager.main.sceneModule == null)
            {
                Debug.LogError("[RoomNetworkService] sceneModule no está inicializado en NetworkManager.");
                return;
            }

            Debug.Log($"[RoomNetworkService] Cambiando escena sincronizada a: {sceneName}...");
            NetworkManager.main.sceneModule.LoadSceneAsync(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        public void Disconnect()
        {
            if (NetworkManager.main == null)
                return;

            Debug.Log("[RoomNetworkService] Deteniendo conexiones de red...");

            if (NetworkManager.main.isClient)
            {
                NetworkManager.main.StopClient();
            }

            if (NetworkManager.main.isServer)
            {
                NetworkManager.main.StopServer();
            }

            var emptyScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("EmptyScene");
            if (emptyScene.isLoaded)
            {
                UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(emptyScene);
            }

            IsHostSession = false;
            RemoteHostName = string.Empty;
            RemoteClientName = string.Empty;
            CurrentRoomCode = string.Empty;

            OnRoomCodeUpdated?.Invoke(string.Empty);
            NotifyPlayersUpdated();
            OnDisconnected?.Invoke();
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

                if (!IsHostSession)
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
                    isHost = IsHostSession
                };

                if (IsHostSession)
                {
                    NetworkManager.main.broadcastModule.SendToAll(info);
                }
                else
                {
                    NetworkManager.main.broadcastModule.SendToServer(info);
                }
            }
        }

        private void OnPlayerLobbyInfoReceived(PlayerID sender, PlayerLobbyInfo info, bool asServer)
        {
            if (info.isHost)
            {
                RemoteHostName = info.playerName;
            }
            else
            {
                RemoteClientName = info.playerName;

                if (IsHostSession)
                {
                    SendLocalLobbyInfo();
                }
            }

            NotifyPlayersUpdated();
        }

        private void HandlePlayerJoined(PlayerID player, bool isReconnect, bool asServer)
        {
            EnsureBroadcastSubscribed();

            if (IsHostSession)
            {
                SendLocalLobbyInfo();
            }

            NotifyPlayersUpdated();
        }

        private void HandlePlayerLeft(PlayerID player, bool asServer)
        {
            if (IsHostSession)
            {
                RemoteClientName = string.Empty;
            }
            else
            {
                RemoteHostName = string.Empty;
            }

            NotifyPlayersUpdated();
        }

        private void NotifyPlayersUpdated()
        {
            string host = IsHostSession ? LocalPlayerName : (!string.IsNullOrEmpty(RemoteHostName) ? RemoteHostName : "Host");
            string client = !IsHostSession ? LocalPlayerName : RemoteClientName;
            OnLobbyPlayersUpdated?.Invoke(host, client);
        }

        private static string GenerateRandomRoomCode(int length)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            char[] stringChars = new char[Mathf.Max(3, length)];
            var random = new System.Random();

            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            return new string(stringChars);
        }

        private static T GetActiveTransport<T>() where T : GenericTransport
        {
            if (NetworkManager.main == null)
                return null;

            if (NetworkManager.main.TryGetComponent(out T directComp))
                return directComp;

            if (NetworkManager.main.TryGetComponent(out CompositeTransport composite))
            {
                if (composite.TryGetTransport<T>(out var internalTransport))
                    return internalTransport;
            }

            return NetworkManager.main.GetComponentInChildren<T>();
        }
    }
}

