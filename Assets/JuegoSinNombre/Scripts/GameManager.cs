using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PurrNet;

namespace JuegoSinNombre
{
    public enum MatchFormat
    {
        BestOf3 = 3,
        BestOf5 = 5
    }

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

        [Header("Match Settings")]
        [SerializeField] private MatchFormat _matchFormat = MatchFormat.BestOf3;
        [SerializeField] private float _impactResolutionWait = 1.0f;
        [SerializeField] private float _nextRoundDelay = 2.5f;
        [SerializeField] private float _nextMicroRoundDelay = 1.5f;

        [Header("Network State")]
        public readonly SyncVar<RoundPhase> currentPhase = new(RoundPhase.WaitingForPlayers);
        public readonly SyncVar<int> roundNumber = new(1);
        public readonly SyncVar<int> player1Wins = new(0);
        public readonly SyncVar<int> player2Wins = new(0);

        public RoundPhase CurrentPhase => currentPhase.value;
        public int RoundNumber => roundNumber.value;
        public int Player1Wins => player1Wins.value;
        public int Player2Wins => player2Wins.value;
        public int WinsNeeded => ((int)_matchFormat / 2) + 1;

        public event Action<RoundPhase> OnPhaseChanged;
        public event Action<int, int> OnScoreChanged;
        public event Action<PlayerID?> OnMatchOver;

        // Disparos pendientes para la micro-ronda actual
        private readonly Dictionary<PlayerID, PendingShot> _pendingShots = new();

        private Coroutine _turnRoutine;
        private Coroutine _resolvingWatchdogRoutine;
        private bool _isTransitioning;

        protected override void OnSpawned()
        {
            base.OnSpawned();
            Instance = this;

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

        private void HandlePhaseChanged(RoundPhase newPhase)
        {
            OnPhaseChanged?.Invoke(newPhase);
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

            currentPhase.value = RoundPhase.WaitingForPlayers;
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

            // 2. Rollear viento si está disponible
            if (WindManager.Instance != null)
            {
                WindManager.Instance.RollWind();
            }

            // 3. Comenzar ciclo de micro-ronda de disparo
            _turnRoutine = StartCoroutine(ServerMicroRoundCycle());
        }

        private IEnumerator ServerMicroRoundCycle()
        {
            _isTransitioning = false;
            _pendingShots.Clear();

            // Fase: Inicio de micro-ronda
            currentPhase.value = RoundPhase.RoundStart;
            yield return new WaitForSeconds(0.5f);

            // Fase: Ventana de Consumibles
            currentPhase.value = RoundPhase.ConsumableWindow;
            yield return new WaitForSeconds(1.0f);

            // Fase: Apuntado y Disparo (Desbloquear arcos y habilitar 1 tiro por jugador)
            currentPhase.value = RoundPhase.Aiming;
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
            if (currentPhase.value != RoundPhase.Aiming) return;
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
            currentPhase.value = RoundPhase.Resolving;
            SetAllPlayersCanShoot(false);

            foreach (var kvp in _pendingShots)
            {
                SpawnAndLaunchArrowServer(kvp.Value);
            }

            _pendingShots.Clear();

            // Watchdog: si ambas flechas fallan (pegan en suelo/pared o vuelan fuera), avanzar tras timeout
            if (_resolvingWatchdogRoutine != null) StopCoroutine(_resolvingWatchdogRoutine);
            _resolvingWatchdogRoutine = StartCoroutine(ServerResolvingWatchdogRoutine());
        }

        private void SpawnAndLaunchArrowServer(PendingShot shot)
        {
            if (shot.arrowDef == null || shot.arrowDef.prefab == null) return;

            GameObject go = Instantiate(shot.arrowDef.prefab, shot.spawnPosition, Quaternion.LookRotation(shot.direction));

            if (!go.TryGetComponent(out ArrowProjectile arrow))
            {
                Debug.LogError($"El prefab de {shot.arrowDef.displayName} no tiene ArrowProjectile.", go);
                Destroy(go);
                return;
            }

            if (shot.shooterIdentity != null)
            {
                var arrowColliders = go.GetComponentsInChildren<Collider>(true);
                var ownColliders = shot.shooterIdentity.GetComponentsInChildren<Collider>(true);
                foreach (var arrowCol in arrowColliders)
                {
                    if (arrowCol == null) continue;
                    foreach (var col in ownColliders)
                    {
                        if (col != null)
                            Physics.IgnoreCollision(arrowCol, col, true);
                    }
                }
            }

            if (go.TryGetComponent(out NetworkIdentity identity))
                identity.GiveOwnership(shot.shooter);

            arrow.Launch(shot.arrowDef, shot.direction, shot.speed, shot.shooter);
        }

        /// <summary>
        /// Resguardo de resolución: si ninguna flecha impacta a un jugador (ambos erraron el tiro),
        /// la fase de resolución concluye automáticamente tras el tiempo de vuelo esperado.
        /// </summary>
        private IEnumerator ServerResolvingWatchdogRoutine()
        {
            yield return new WaitForSeconds(_impactResolutionWait + 3.0f);

            if (currentPhase.value == RoundPhase.Resolving && !_isTransitioning)
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
            if (currentPhase.value == RoundPhase.Aiming || currentPhase.value == RoundPhase.Resolving)
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

            if (currentPhase.value != RoundPhase.GameOver && !_isTransitioning)
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
            currentPhase.value = RoundPhase.Resolving;

            // Pausa breve de lectura de impacto
            yield return new WaitForSeconds(_impactResolutionWait);

            // 2. Verificación de vida: Pasar a Outcome
            currentPhase.value = RoundPhase.Outcome;
            yield return new WaitForSeconds(0.5f);

            // Verificar si algún jugador murió (currentHealth <= 0)
            HealthModule deadPlayer = null;
            if (affectedPlayer != null && affectedPlayer.IsDead)
            {
                deadPlayer = affectedPlayer;
            }
            else
            {
                foreach (var kvp in PlayerIdentity.allPlayers)
                {
                    if (kvp.Value != null && kvp.Value.HealthModule != null && kvp.Value.HealthModule.IsDead)
                    {
                        deadPlayer = kvp.Value.HealthModule;
                        break;
                    }
                }
            }

            // 3. Resolución de la micro-ronda o fin de ronda
            if (deadPlayer != null)
            {
                // Un jugador cayó: Sumar victoria al oponente
                AwardRoundVictoryServer(deadPlayer);
            }
            else
            {
                // Ambos siguen en pie: Pausa breve y preparar la siguiente flecha (micro-ronda)
                Debug.Log($"[GameManager] Ambos arqueros siguen con vida. Preparando siguiente micro-ronda en {_nextMicroRoundDelay}s...");
                yield return new WaitForSeconds(_nextMicroRoundDelay);

                // Caducan efectos o se rueda viento si corresponde
                if (WindManager.Instance != null)
                {
                    WindManager.Instance.RollWind();
                }

                _turnRoutine = StartCoroutine(ServerMicroRoundCycle());
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
            if (player1Wins.value >= targetWins || player2Wins.value >= targetWins)
            {
                TriggerMatchGameOverServer(winnerId);
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

            currentPhase.value = RoundPhase.GameOver;
            Debug.Log($"<color=yellow><b>[GameManager] ¡FIN DE LA PARTIDA MAESTRA! Ganador: {matchWinner}</b></color>");

            NotifyMatchOverObserversRpc(matchWinner);
        }

        [ObserversRpc(runLocally: true)]
        private void NotifyMatchOverObserversRpc(PlayerID? matchWinner)
        {
            OnMatchOver?.Invoke(matchWinner);
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

                // Resetear ragdoll si estaba activo
                if (kvp.Value.RagdollController != null)
                {
                    kvp.Value.RagdollController.ResetRagdoll();
                }

                // Resetear modificadores del arquero
                if (kvp.Value.TryGetComponent(out ArcherPlayer archer))
                {
                    archer.ResetModifiers();
                    archer.SetCanShoot(false);
                }

                if (kvp.Value.TryGetComponent(out PlayerShooter shooter))
                {
                    shooter.ResetRoundShotRpc();
                }
            }
        }
    }
}