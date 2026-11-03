using System;
using System.Collections;
using UnityEngine;
using PurrNet;
using JuegoSinNombre.Core;

namespace JuegoSinNombre.Gameplay
{
    /// <summary>
    /// Coordinates the visual presentation when the Master Match concludes:
    /// - Teleports players to a victory stage or places them side-by-side in the arena.
    /// - Keeps the winning archer standing upright and triggers their victory animation.
    /// - Teleports the defeated opponent right beside the winner and collapses them into full physics ragdoll on the floor.
    /// - Switches to a dedicated third-person cinematic camera framing both players together.
    /// </summary>
    [DisallowMultipleComponent]
    public class MatchEndPresenter : MonoBehaviour
    {
        public static MatchEndPresenter Instance { get; private set; }

        [Header("Cinematic Camera Settings")]
        [Tooltip("Optional dedicated camera in the scene. If unassigned, a runtime camera will be instantiated.")]
        [SerializeField] private Camera _endgameCamera;

        [Header("Escenario de Victoria / Podio (Opcional)")]
        [Tooltip("Punto opcional de la escena donde se colocará al ganador. Si está vacío, se usa su posición actual.")]
        [SerializeField] private Transform _winnerStagePoint;

        [Tooltip("Punto opcional de la escena donde se colocará al perdedor en el piso. Si está vacío, se coloca automáticamente al lado del ganador.")]
        [SerializeField] private Transform _loserStagePoint;

        [Tooltip("Punto opcional de la cámara para enfocar el podio. Si está vacío, la cámara encuadra proceduralmente a ambos.")]
        [SerializeField] private Transform _stageCameraPoint;

        [Header("Posicionamiento Relativo (Cuando no hay puntos de escenario asignados)")]
        [Tooltip("Desplazamiento del perdedor respecto al ganador (X = al costado, Z = al frente).")]
        [SerializeField] private Vector3 _loserOffsetFromWinner = new(1.4f, 0f, 1.6f);

        [Tooltip("Desplazamiento de la cámara respecto al centro entre ambos jugadores.")]
        [SerializeField] private Vector3 _cameraOffsetFromCenter = new(0f, 1.25f, -3.2f);

        [Tooltip("Altura del punto de mira sobre los personajes.")]
        [SerializeField] private float _lookAtHeight = 1.0f;

        [Tooltip("Trigger de animación de victoria en el Animator del ganador.")]
        [SerializeField] private string _winnerAnimationTrigger = "Victory";

        [Header("Timing")]
        [Tooltip("Retardo antes de la transición para permitir que el impacto final se registre.")]
        [SerializeField] private float _presentationDelay = 0.5f;

        public event Action<PlayerIdentity, PlayerIdentity> OnMatchEndPresented;

        private bool _isPresented;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (FindAnyObjectByType<MatchEndPresenter>() == null)
            {
                var go = new GameObject("[MatchEndPresenter]");
                go.AddComponent<MatchEndPresenter>();
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // Desactivar la cámara cinemática de podio al inicio
            if (_endgameCamera != null)
            {
                _endgameCamera.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            if (_endgameCamera != null)
            {
                _endgameCamera.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            GameManager.OnMatchOverGlobal += HandleMatchOver;
            GameManager.OnPhaseChangedGlobal += HandlePhaseChanged;
            GameManager.OnGameManagerSpawned += HandleGameManagerSpawned;

            if (GameManager.Instance != null)
            {
                HandleGameManagerSpawned(GameManager.Instance);
            }
        }

        private void OnDisable()
        {
            GameManager.OnMatchOverGlobal -= HandleMatchOver;
            GameManager.OnPhaseChangedGlobal -= HandlePhaseChanged;
            GameManager.OnGameManagerSpawned -= HandleGameManagerSpawned;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnMatchOver -= HandleMatchOver;
                GameManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
            }
        }

        private void HandleGameManagerSpawned(GameManager gm)
        {
            if (gm == null) return;
            gm.OnMatchOver -= HandleMatchOver;
            gm.OnMatchOver += HandleMatchOver;
            gm.OnPhaseChanged -= HandlePhaseChanged;
            gm.OnPhaseChanged += HandlePhaseChanged;

            if (gm.CurrentPhase == RoundPhase.GameOver)
            {
                HandleMatchOver(gm.MatchWinner);
            }
        }

        private void HandlePhaseChanged(RoundPhase phase)
        {
            if (phase == RoundPhase.GameOver && !_isPresented)
            {
                PlayerID? winner = GameManager.Instance != null ? GameManager.Instance.MatchWinner : null;
                HandleMatchOver(winner);
            }
        }

        private void Update()
        {
            if (!_isPresented && GameManager.Instance != null && GameManager.Instance.CurrentPhase == RoundPhase.GameOver)
            {
                HandleMatchOver(GameManager.Instance.MatchWinner);
            }
        }

        private void HandleMatchOver(PlayerID? matchWinner)
        {
            if (_isPresented) return;
            _isPresented = true;

            Debug.Log($"<color=magenta><b>[MatchEndPresenter] Ejecutando presentación de fin de partida. Ganador: {matchWinner}</b></color>");
            StartCoroutine(PresentationRoutine(matchWinner));
        }

        private IEnumerator PresentationRoutine(PlayerID? matchWinner)
        {
            if (_presentationDelay > 0f)
            {
                yield return new WaitForSeconds(_presentationDelay);
            }

            // 1. Identificar ganador y perdedor
            PlayerIdentity winner = null;
            PlayerIdentity loser = null;

            if (matchWinner.HasValue && PlayerIdentity.allPlayers != null)
            {
                foreach (var kvp in PlayerIdentity.allPlayers)
                {
                    if (kvp.Value == null) continue;

                    if (kvp.Key == matchWinner.Value)
                    {
                        winner = kvp.Value;
                    }
                    else
                    {
                        loser = kvp.Value;
                    }
                }
            }

            // 2. Bloquear controles y cámaras en primera persona
            DisableAllPlayerControlsAndCameras();

            // 3. Posicionar al Ganador (de pie)
            if (winner != null)
            {
                if (_winnerStagePoint != null)
                {
                    winner.transform.position = _winnerStagePoint.position;
                    winner.transform.rotation = _winnerStagePoint.rotation;

                    if (winner.TryGetComponent(out Rigidbody wRb))
                    {
                        wRb.linearVelocity = Vector3.zero;
                        wRb.angularVelocity = Vector3.zero;
                        wRb.position = _winnerStagePoint.position;
                        wRb.rotation = _winnerStagePoint.rotation;
                    }
                }

                if (winner.RagdollController != null)
                {
                    winner.RagdollController.SetRagdollState(false);
                }

                var winnerAnim = winner.GetComponentInChildren<Animator>();
                if (winnerAnim != null)
                {
                    winnerAnim.enabled = true;
                    winnerAnim.SetBool("IsAiming", false);

                    if (!string.IsNullOrEmpty(_winnerAnimationTrigger))
                    {
                        winnerAnim.SetTrigger(_winnerAnimationTrigger);
                    }
                }
            }

            // 4. Posicionar al Perdedor (colapsado en ragdoll al lado del ganador)
            if (loser != null && loser.RagdollController != null)
            {
                Vector3 loserTargetPos;
                Quaternion loserTargetRot;

                if (_loserStagePoint != null)
                {
                    loserTargetPos = _loserStagePoint.position;
                    loserTargetRot = _loserStagePoint.rotation;
                }
                else if (winner != null)
                {
                    // Al lado y ligeramente al frente del ganador
                    loserTargetPos = winner.transform.position + (winner.transform.rotation * _loserOffsetFromWinner);
                    loserTargetRot = Quaternion.LookRotation((loserTargetPos - winner.transform.position).normalized);
                }
                else
                {
                    loserTargetPos = loser.transform.position;
                    loserTargetRot = loser.transform.rotation;
                }

                loser.RagdollController.TeleportAndCollapse(loserTargetPos, loserTargetRot);
            }

            // 5. Configurar y orientar la cámara cinemática
            SetupEndgameCamera(winner, loser);

            OnMatchEndPresented?.Invoke(winner, loser);
        }

        private void DisableAllPlayerControlsAndCameras()
        {
            // Liberar y mostrar el cursor del mouse para interacción con UI
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (ArcherPlayer.LocalPlayer != null)
            {
                ArcherPlayer.LocalPlayer.SetCanShoot(false);

                if (ArcherPlayer.LocalPlayer.TryGetComponent(out AimController aim))
                {
                    aim.OverrideCameraControl = true;
                    aim.ForceIdle();
                }
            }

            if (PlayerIdentity.allPlayers != null)
            {
                foreach (var kvp in PlayerIdentity.allPlayers)
                {
                    if (kvp.Value == null) continue;

                    if (kvp.Value.TryGetComponent(out PlayerCameraSetup camSetup))
                    {
                        camSetup.SetCameraActive(false);
                    }

                    if (kvp.Value.TryGetComponent(out ArcherPlayer archer))
                    {
                        archer.SetCanShoot(false);
                    }
                }
            }
        }

        private void SetupEndgameCamera(PlayerIdentity winner, PlayerIdentity loser)
        {
            if (_endgameCamera == null)
            {
                var existingCam = GameObject.FindWithTag("MainCamera");
                if (existingCam != null && existingCam.GetComponentInParent<PlayerIdentity>() == null)
                {
                    _endgameCamera = existingCam.GetComponent<Camera>();
                }
                else
                {
                    var camObj = new GameObject("[MatchEndCinematicCamera]");
                    _endgameCamera = camObj.AddComponent<Camera>();
                    if (FindAnyObjectByType<AudioListener>() == null)
                    {
                        camObj.AddComponent<AudioListener>();
                    }
                    _endgameCamera.tag = "MainCamera";
                }
            }

            _endgameCamera.depth = 100f;
            _endgameCamera.gameObject.SetActive(true);
            _endgameCamera.enabled = true;

            // Opción A: Punto de cámara explícito en la escena
            if (_stageCameraPoint != null)
            {
                _endgameCamera.transform.position = _stageCameraPoint.position;
                _endgameCamera.transform.rotation = _stageCameraPoint.rotation;
                return;
            }

            // Opción B: Encuadre procedural que engloba a ambos personajes
            if (winner == null) return;

            Vector3 centerPos = (loser != null)
                ? (winner.transform.position + loser.transform.position) * 0.5f
                : winner.transform.position;

            Vector3 lookTarget = centerPos + (Vector3.up * _lookAtHeight);

            // Colocar la cámara al frente del centro en base a la rotación del ganador
            Vector3 camPos = centerPos + (winner.transform.rotation * _cameraOffsetFromCenter);

            _endgameCamera.transform.position = camPos;
            _endgameCamera.transform.LookAt(lookTarget);
        }
    }
}
