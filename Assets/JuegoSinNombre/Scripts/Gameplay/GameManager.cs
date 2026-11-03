using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PurrNet;
using JuegoSinNombre.Core;

namespace JuegoSinNombre.Gameplay
{

    /// <summary>
    /// Servidor y Host autoritativo del ciclo de juego:
    /// - Estructura de partida Maestra (al mejor de 3 o 5 rondas).
    /// - Detección de vida en 0 (currentHealth) para otorgar victoria de ronda.
    /// - Sincronización y secuenciación de micro-rondas de disparo:
    ///   (Espera de tiro de ambos jugadores -> Disparo simultáneo -> Resolución de impacto -> Siguiente micro-ronda).
    /// </summary>
    [DisallowMultipleComponent]
    public class GameManager : NetworkIdentity
    {
        public static GameManager Instance { get; private set; }

        public static event Action<GameManager> OnGameManagerSpawned;
        public static event Action<PlayerID?> OnMatchOverGlobal;
        public static event Action<RoundPhase> OnPhaseChangedGlobal;

        [Header("Match Settings")]
        [SerializeField] private MatchFormat _matchFormat = MatchFormat.BestOf3;
        [SerializeField] private float _impactResolutionWait = 1.0f;
        [SerializeField] private float _nextRoundDelay = 2.5f;
        [SerializeField] private float _nextMicroRoundDelay = 1.5f;

        [Header("Network State")]
        public readonly SyncVar<int> currentPhase = new((int)RoundPhase.WaitingForPlayers);
        public readonly SyncVar<int> roundNumber = new(1);
        public readonly SyncVar<int> player1Wins = new(0);
        public readonly SyncVar<int> player2Wins = new(0);

        public RoundPhase CurrentPhase => (RoundPhase)currentPhase.value;
        public int RoundNumber => roundNumber.value;
        public int Player1Wins => player1Wins.value;
        public int Player2Wins => player2Wins.value;
        public int WinsNeeded => ((int)_matchFormat / 2) + 1;
        public PlayerID? MatchWinner { get; private set; }

        public event Action<RoundPhase> OnPhaseChanged;
        public event Action<int, int> OnScoreChanged;
        public event Action<PlayerID?> OnMatchOver;

        // Disparos pendientes para la micro-ronda actual
        private readonly Dictionary<PlayerID, PendingShot> _pendingShots = new();

        private Coroutine _turnRoutine;
        private Coroutine _resolvingWatchdogRoutine;
        private bool _isTransitioning;

        private void Awake()
        {
            NetworkTypeRegistry.RegisterNetworkTypes();
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            Instance = this;
            OnGameManagerSpawned?.Invoke(this);

            currentPhase.onChanged += HandlePhaseChanged;
            player1Wins.onChanged += HandleScoreChanged;
            player2Wins.onChanged += HandleScoreChanged;

            if (isServer)
            {
                CombatResolver.OnHitResolved += HandleHitResolvedServer;
                HealthModule.OnAnyPlayerDied += HandleAnyPlayerDiedServer;
                
                // Si ya hay jugadores conectados al spawnear el GameManager
                StartCoroutine(ServerWaitForPlayersAndStart());
            }
        }

        protected override void OnDespawned(bool asServer)
        {
            currentPhase.onChanged -= HandlePhaseChanged;
            player1Wins.onChanged -= HandleScoreChanged;
            player2Wins.onChanged -= HandleScoreChanged;

            if (isServer)
            {
                CombatResolver.OnHitResolved -= HandleHitResolvedServer;
                HealthModule.OnAnyPlayerDied -= HandleAnyPlayerDiedServer;
            }

            if (Instance == this) Instance = null;
            base.OnDespawned(asServer);
        }

        private void HandlePhaseChanged(int newPhaseInt)
        {
            RoundPhase phase = (RoundPhase)newPhaseInt;
            OnPhaseChanged?.Invoke(phase);
            OnPhaseChangedGlobal?.Invoke(phase);
        }

        private void HandleScoreChanged(int _)
        {
            OnScoreChanged?.Invoke(player1Wins.value, player2Wins.value);
        }

        // =========================================================================
        // LÓGICA DE SERVIDOR: Flujo de Partida y Transiciones de Micro-Rondas
        // =========================================================================

        private IEnumerator ServerWaitForPlayersAndStart()
        {
            if (!isServer) yield break;

            currentPhase.value = (int)RoundPhase.WaitingForPlayers;
            Debug.Log("[GameManager] Servidor en espera de jugadores...");

            while (PlayerIdentity.allPlayers.Count < 2)
            {
                yield return new WaitForSeconds(0.5f);
            }

            Debug.Log("[GameManager] Jugadores listos. Iniciando Partida Maestra!");
            StartNewMatchServer();
        }

        [ContextMenu("Server: Start New Match")]
        public void StartNewMatchServer()
        {
            if (!isServer) return;

            if (_turnRoutine != null) StopCoroutine(_turnRoutine);
            if (_resolvingWatchdogRoutine != null) StopCoroutine(_resolvingWatchdogRoutine);

            roundNumber.value = 1;
            player1Wins.value = 0;
            player2Wins.value = 0;
            MatchWinner = null;
            _isTransitioning = false;
            _pendingShots.Clear();

            StartRoundServer();
        }

        private void StartRoundServer()
        {
            if (!isServer) return;

            Debug.Log($"<color=cyan><b>[GameManager] === INICIO DE RONDA {roundNumber.value} ===</b></color>");

            // 1. Restaurar vida y estado de todos los jugadores
            ResetAllPlayersHealthAndState();

            // Repartir mano si las 3 flechas fueron gastadas
            CheckAndDealDepletedHandsServer();
            
            // 3. Comenzar ciclo de micro-ronda de disparo
            _turnRoutine = StartCoroutine(ServerMicroRoundCycle());
        }

        private IEnumerator ServerMicroRoundCycle()
        {
            _isTransitioning = false;
            _pendingShots.Clear();

            // Fase: Inicio de micro-ronda
            currentPhase.value = (int)RoundPhase.RoundStart;
            
            if (WindManager.Instance != null)
                WindManager.Instance.RollWind();
            
            yield return new WaitForSeconds(0.5f);

            // Fase: Ventana de Consumibles
            currentPhase.value = (int)RoundPhase.ConsumableWindow;
            yield return new WaitForSeconds(1.0f);

            // Fase: Apuntado y Disparo (Desbloquear arcos y habilitar 1 tiro por jugador)
            currentPhase.value = (int)RoundPhase.Aiming;
            SetAllPlayersCanShoot(true);
            Debug.Log("[GameManager] Fase Aiming: Arcos desbloqueados. Esperando a que ambos jugadores apunten y disparen...");
        }

        /// <summary>
        /// Recibe la intención de tiro de un jugador. Cuando ambos jugadores hayan disparado,
        /// se lanzan las dos flechas simultáneamente.
        /// </summary>
        public void RegisterPendingShotServer(PendingShot shot)
        {
            if (!isServer) return;
            if (CurrentPhase != RoundPhase.Aiming) return;
            if (_pendingShots.ContainsKey(shot.shooter)) return;

            _pendingShots[shot.shooter] = shot;
            Debug.Log($"[GameManager] Tiro registrado de jugador {shot.shooter}. ({_pendingShots.Count}/{PlayerIdentity.allPlayers.Count} listos)");

            // Bloquear el arco para que no pueda volver a disparar en esta micro-ronda
            if (shot.shooterIdentity != null && shot.shooterIdentity.TryGetComponent(out ArcherPlayer archer))
            {
                archer.SetCanShoot(false);
            }

            // Cuando todos los jugadores activos en la partida hayan disparado
            int targetCount = Mathf.Max(1, PlayerIdentity.allPlayers.Count);
            if (_pendingShots.Count >= targetCount)
            {
                ExecuteSimultaneousShotsServer();
            }
        }

        private void ExecuteSimultaneousShotsServer()
        {
            if (!isServer) return;

            Debug.Log($"<color=yellow><b>[GameManager] ¡Ambos jugadores listos! Lanzando flechas simultáneas ({_pendingShots.Count})...</b></color>");
            currentPhase.value = (int)RoundPhase.Resolving;
            SetAllPlayersCanShoot(false);

            foreach (var kvp in _pendingShots)
            {
                // Consumir el slot de la flecha autoritativamente AHORA que se dispara en Resolving
                if (kvp.Value.shooterIdentity != null && kvp.Value.shooterIdentity.TryGetComponent(out PlayerShooter shooter))
                {
                    shooter.ConsumeSlotServer(kvp.Value.slotIndex);
                }

                ArrowSpawner.SpawnAndLaunch(kvp.Value);
            }

            _pendingShots.Clear();

            // Watchdog: si ambas flechas fallan (pegan en suelo/pared o vuelan fuera), avanzar tras timeout
            if (_resolvingWatchdogRoutine != null) StopCoroutine(_resolvingWatchdogRoutine);
            _resolvingWatchdogRoutine = StartCoroutine(ServerResolvingWatchdogRoutine());
        }

        /// <summary>
        /// Resguardo de resolución: si ninguna flecha impacta a un jugador (ambos erraron el tiro),
        /// la fase de resolución concluye automáticamente tras el tiempo de vuelo esperado.
        /// </summary>
        private IEnumerator ServerResolvingWatchdogRoutine()
        {
            yield return new WaitForSeconds(_impactResolutionWait + 3.0f);

            if (CurrentPhase == RoundPhase.Resolving && !_isTransitioning)
            {
                Debug.Log("[GameManager] Watchdog: Tiempo de vuelo concluido sin impactos directos (tiros fallados). Pasando a Outcome...");
                if (_turnRoutine != null) StopCoroutine(_turnRoutine);
                _turnRoutine = StartCoroutine(ServerProcessImpactAndTransition(null));
            }
        }

        /// <summary>
        /// Recibe la confirmación de impacto de flecha desde CombatResolver en el servidor.
        /// </summary>
        private void HandleHitResolvedServer(ArrowHitInfo hit, HealthModule victim)
        {
            if (!isServer || _isTransitioning) return;

            if (_resolvingWatchdogRoutine != null)
            {
                StopCoroutine(_resolvingWatchdogRoutine);
                _resolvingWatchdogRoutine = null;
            }

            // Si estábamos en apuntado o resolución, iniciamos la transición
            if (CurrentPhase == RoundPhase.Aiming || CurrentPhase == RoundPhase.Resolving)
            {
                if (_turnRoutine != null) StopCoroutine(_turnRoutine);
                _turnRoutine = StartCoroutine(ServerProcessImpactAndTransition(victim));
            }
        }

        /// <summary>
        /// Detección inmediata en servidor cuando la vida de cualquier jugador llega a 0.
        /// </summary>
        private void HandleAnyPlayerDiedServer(HealthModule deadPlayer)
        {
            if (!isServer) return;

            if (_resolvingWatchdogRoutine != null)
            {
                StopCoroutine(_resolvingWatchdogRoutine);
                _resolvingWatchdogRoutine = null;
            }

            if (CurrentPhase != RoundPhase.GameOver && !_isTransitioning)
            {
                if (_turnRoutine != null) StopCoroutine(_turnRoutine);
                _turnRoutine = StartCoroutine(ServerProcessImpactAndTransition(deadPlayer));
            }
        }

        /// <summary>
        /// Secuencia autoritativa de micro-ronda:
        /// 1. Resolución de impacto (bloquear arcos, pasar a Resolving)
        /// 2. Pausa breve de lectura de impacto
        /// 3. Verificación de vida (Outcome)
        /// 4. Si la vida llega a 0 -> Sumar victoria de ronda y evaluar partida Maestra
        /// 5. Si siguen vivos -> Pausa breve y reinicio de la siguiente flecha
        /// </summary>
        private IEnumerator ServerProcessImpactAndTransition(HealthModule affectedPlayer)
        {
            _isTransitioning = true;

            // 1. Resolución de impacto: bloquear arcos inmediatamente
            SetAllPlayersCanShoot(false);
            currentPhase.value = (int)RoundPhase.Resolving;

            // Pausa breve de lectura de impacto
            yield return new WaitForSeconds(_impactResolutionWait);

            // 2. Verificación de vida: Pasar a Outcome
            currentPhase.value = (int)RoundPhase.Outcome;
            yield return new WaitForSeconds(0.5f);

            // Verificar si algún jugador murió (currentHealth <= 0)
            List<HealthModule> deadPlayers = new List<HealthModule>();
            foreach (var kvp in PlayerIdentity.allPlayers)
            {
                if (kvp.Value != null && kvp.Value.HealthModule != null && kvp.Value.HealthModule.IsDead)
                {
                    deadPlayers.Add(kvp.Value.HealthModule);
                }
            }

            if (affectedPlayer != null && affectedPlayer.IsDead && !deadPlayers.Contains(affectedPlayer))
            {
                deadPlayers.Add(affectedPlayer);
            }

            // 3. Resolución de la micro-ronda o fin de ronda
            if (deadPlayers.Count >= 2)
            {
                // Ambos arqueros cayeron al mismo tiempo (Doble K.O.)
                HandleDoubleDeathServer();
            }
            else if (deadPlayers.Count == 1)
            {
                // Un jugador cayó: Sumar victoria al oponente
                AwardRoundVictoryServer(deadPlayers[0]);
            }
            else
            {
                // Ambos siguen en pie: Pausa breve y preparar la siguiente flecha (micro-ronda)
                Debug.Log($"[GameManager] Ambos arqueros siguen con vida. Preparando siguiente micro-ronda en {_nextMicroRoundDelay}s...");
                yield return new WaitForSeconds(_nextMicroRoundDelay);

                // Reasignar mano de flechas en el servidor ÚNICAMENTE tras la resolución del combate si las 3 fueron gastadas
                CheckAndDealDepletedHandsServer();

                _turnRoutine = StartCoroutine(ServerMicroRoundCycle());
            }
        }

        private void HandleDoubleDeathServer()
        {
            if (!isServer) return;

            // Ambos jugadores murieron al mismo tiempo en esta micro-ronda
            player1Wins.value++;
            player2Wins.value++;
            Debug.Log($"<color=yellow>[GameManager] ¡Muerte simultánea (Doble K.O.) en la Ronda {roundNumber.value}!</color> Marcador: P1 {player1Wins.value} - P2 {player2Wins.value}");

            int targetWins = WinsNeeded;
            bool p1Reached = player1Wins.value >= targetWins;
            bool p2Reached = player2Wins.value >= targetWins;

            if (p1Reached && p2Reached)
            {
                // Ambos arqueros alcanzaron la meta de victorias al mismo tiempo (ej: 1 - 1 y ambos mueren -> 2 - 2) -> Empate
                Debug.Log("<color=yellow><b>[GameManager] ¡Empate en la Partida Maestra! Ambos jugadores alcanzaron la meta simultáneamente.</b></color>");
                TriggerMatchGameOverServer(null);
            }
            else if (p1Reached)
            {
                List<PlayerID> playerKeys = new List<PlayerID>(PlayerIdentity.allPlayers.Keys);
                TriggerMatchGameOverServer(playerKeys.Count > 0 ? playerKeys[0] : (PlayerID?)null);
            }
            else if (p2Reached)
            {
                List<PlayerID> playerKeys = new List<PlayerID>(PlayerIdentity.allPlayers.Keys);
                TriggerMatchGameOverServer(playerKeys.Count > 1 ? playerKeys[1] : (PlayerID?)null);
            }
            else
            {
                // Todavía nadie alcanza la meta de victorias, pasar a la siguiente ronda
                roundNumber.value++;
                _turnRoutine = StartCoroutine(ServerWaitAndStartNextRound());
            }
        }

        private void AwardRoundVictoryServer(HealthModule deadPlayer)
        {
            if (!isServer) return;

            // Identificar qué jugador ganó la ronda
            List<PlayerID> playerKeys = new List<PlayerID>(PlayerIdentity.allPlayers.Keys);
            PlayerID? winnerId = null;

            if (playerKeys.Count >= 2)
            {
                PlayerID p1 = playerKeys[0];
                PlayerID p2 = playerKeys[1];

                if (deadPlayer.owner == p1)
                {
                    player2Wins.value++;
                    winnerId = p2;
                    Debug.Log($"<color=green>[GameManager] ¡P2 gana la Ronda {roundNumber.value}!</color> Marcador: P1 {player1Wins.value} - P2 {player2Wins.value}");
                }
                else
                {
                    player1Wins.value++;
                    winnerId = p1;
                    Debug.Log($"<color=green>[GameManager] ¡P1 gana la Ronda {roundNumber.value}!</color> Marcador: P1 {player1Wins.value} - P2 {player2Wins.value}");
                }
            }
            else
            {
                // Fallback si se está testeando en solitario
                player1Wins.value++;
                Debug.Log($"[GameManager] Victoria de ronda otorgada. Marcador: P1 {player1Wins.value}");
            }

            // 4. Evaluar condición de victoria de la Partida Maestra
            int targetWins = WinsNeeded;
            if (player1Wins.value >= targetWins && player2Wins.value >= targetWins)
            {
                TriggerMatchGameOverServer(null);
            }
            else if (player1Wins.value >= targetWins)
            {
                PlayerID? p1Winner = playerKeys.Count > 0 ? playerKeys[0] : winnerId;
                TriggerMatchGameOverServer(p1Winner);
            }
            else if (player2Wins.value >= targetWins)
            {
                PlayerID? p2Winner = playerKeys.Count > 1 ? playerKeys[1] : winnerId;
                TriggerMatchGameOverServer(p2Winner);
            }
            else
            {
                // Pasar a la siguiente ronda tras una breve pausa
                roundNumber.value++;
                _turnRoutine = StartCoroutine(ServerWaitAndStartNextRound());
            }
        }

        private IEnumerator ServerWaitAndStartNextRound()
        {
            Debug.Log($"[GameManager] Próxima ronda ({roundNumber.value}) en {_nextRoundDelay} segundos...");
            yield return new WaitForSeconds(_nextRoundDelay);
            StartRoundServer();
        }

        private void TriggerMatchGameOverServer(PlayerID? matchWinner)
        {
            if (!isServer) return;

            MatchWinner = matchWinner;
            SetAllPlayersCanShoot(false);
            Debug.Log($"<color=yellow><b>[GameManager] ¡FIN DE LA PARTIDA MAESTRA! Ganador: {matchWinner}</b></color>");

            NotifyMatchOverObserversRpc(matchWinner);
            currentPhase.value = (int)RoundPhase.GameOver;
        }

        [ObserversRpc(runLocally: true)]
        private void NotifyMatchOverObserversRpc(PlayerID? matchWinner)
        {
            MatchWinner = matchWinner;
            Debug.Log($"<color=yellow>[GameManager] ObserversRpc recibido: Notificando OnMatchOver. Ganador: {matchWinner}</color>");
            OnMatchOver?.Invoke(matchWinner);
            OnMatchOverGlobal?.Invoke(matchWinner);
        }

        // =========================================================================
        // HELPERS DE CONTROL DE JUGADORES
        // =========================================================================

        private void SetAllPlayersCanShoot(bool canShoot)
        {
            if (!isServer) return;

            foreach (var kvp in PlayerIdentity.allPlayers)
            {
                if (kvp.Value == null) continue;

                if (kvp.Value.TryGetComponent(out ArcherPlayer archer))
                {
                    archer.SetCanShoot(canShoot);
                }

                if (canShoot && kvp.Value.TryGetComponent(out PlayerShooter shooter))
                {
                    shooter.ResetRoundShotRpc();
                }
            }
        }

        private void ResetAllPlayersHealthAndState()
        {
            if (!isServer) return;

            _pendingShots.Clear();

            foreach (var kvp in PlayerIdentity.allPlayers)
            {
                if (kvp.Value == null) continue;

                // Resetear salud autoritativa
                if (kvp.Value.HealthModule != null)
                {
                    kvp.Value.HealthModule.ResetHealthServerRpc();
                }

                // Resetear ragdoll, pose y posición en todos los clientes
                kvp.Value.ResetPlayerRoundStateObserversRpc(kvp.Value.InitialSpawnPosition, kvp.Value.InitialSpawnRotation);

                // Limpiar efectos de estado en el servidor
                if (kvp.Value.TryGetComponent(out ArcherPlayer archer))
                {
                    archer.ClearAllEffects();
                }
            }
        }

        private void CheckAndDealDepletedHandsServer()
        {
            if (!isServer) return;

            foreach (var kvp in PlayerIdentity.allPlayers)
            {
                if (kvp.Value != null && kvp.Value.TryGetComponent(out PlayerShooter shooter))
                {
                    shooter.CheckAndDealNewHandOnServer();
                }
            }
        }
    }
}