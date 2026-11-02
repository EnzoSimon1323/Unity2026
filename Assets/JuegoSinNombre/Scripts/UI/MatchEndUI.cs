using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using PurrNet;
using JuegoSinNombre.Core;
using JuegoSinNombre.Gameplay;

namespace JuegoSinNombre.UI
{
    /// <summary>
    /// Displays the final Match Victory/Defeat screen when the Master Match concludes:
    /// - Keeps the gameplay HUD (such as health bar) visible during the game and hides it when the match ends.
    /// - Shows 'VICTORIA' or 'DERROTA' personalized for the local client.
    /// - Displays final score and winner information.
    /// - Provides a button to disconnect and return to the Main Menu.
    /// - Features an automatic OnGUI fallback for instant zero-configuration testing.
    /// </summary>
    [DisallowMultipleComponent]
    public class MatchEndUI : MonoBehaviour
    {
        public static MatchEndUI Instance { get; private set; }

        [Header("HUD de Combate (Se apaga al terminar la ronda maestra)")]
        [Tooltip("El Canvas o panel de juego (ej. HelathBarCanvas) que debe permanecer visible al jugar y ocultarse solo al terminar la partida.")]
        [SerializeField] private GameObject _gameplayHudToHide;

        [Header("Pantalla de Fin de Partida (Se enciende al terminar la ronda maestra)")]
        [Tooltip("Panel con los resultados (Victoria / Derrota). Si no se asigna, se dibuja automáticamente por OnGUI.")]
        [SerializeField] private GameObject _victoryDefeatPanel;

        [Header("Referencias del Panel de Fin de Partida (Opcionales)")]
        [SerializeField] private TMP_Text _resultTitleText;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private Button _returnToMenuButton;

        [Header("Colores del Banner")]
        [SerializeField] private Color _victoryColor = new(0.18f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color _defeatColor = new(0.92f, 0.22f, 0.22f, 1f);

        [Header("Navegación")]
        [SerializeField] private string _mainMenuSceneName = "MainMenu";

        [Header("OnGUI Fallback")]
        [SerializeField] private bool _enableOnGuiFallback = true;

        // Campo serializado para compatibilidad con asignaciones previas en la escena
        [SerializeField, HideInInspector] private GameObject _panelRoot;

        private bool _isMatchOver;
        private bool _isLocalWinner;
        private string _scoreTextString = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (FindAnyObjectByType<MatchEndUI>() == null)
            {
                var go = new GameObject("[MatchEndUI]");
                go.AddComponent<MatchEndUI>();
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

            // Compatibilidad: si el usuario asignó el canvas en _panelRoot previamente,
            // lo migramos como el HUD que debe apagarse al terminar la ronda maestra
            if (_gameplayHudToHide == null && _panelRoot != null)
            {
                _gameplayHudToHide = _panelRoot;
            }

            // El HUD de juego DEBE permanecer activo al iniciar la partida
            if (_gameplayHudToHide != null)
            {
                _gameplayHudToHide.SetActive(true);
            }

            // Solo el panel de victoria/derrota empieza apagado hasta el Game Over
            if (_victoryDefeatPanel != null)
            {
                _victoryDefeatPanel.SetActive(false);
            }

            if (_returnToMenuButton != null)
            {
                _returnToMenuButton.onClick.AddListener(ReturnToMainMenu);
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

            if (_returnToMenuButton != null)
            {
                _returnToMenuButton.onClick.RemoveListener(ReturnToMainMenu);
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
            if (phase == RoundPhase.GameOver && !_isMatchOver)
            {
                PlayerID? winner = GameManager.Instance != null ? GameManager.Instance.MatchWinner : null;
                HandleMatchOver(winner);
            }
        }

        private void Update()
        {
            if (!_isMatchOver && GameManager.Instance != null && GameManager.Instance.CurrentPhase == RoundPhase.GameOver)
            {
                HandleMatchOver(GameManager.Instance.MatchWinner);
            }

            if (_isMatchOver)
            {
                // Asegurar que el cursor permanezca visible y libre para interactuar con botones
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void HandleMatchOver(PlayerID? matchWinner)
        {
            if (_isMatchOver) return;
            _isMatchOver = true;

            Debug.Log($"<color=magenta><b>[MatchEndUI] Desplegando UI de fin de partida. Ganador: {matchWinner}</b></color>");

            // 1. Apagar el HUD de juego (barra de vida) al terminar la ronda maestra
            if (_gameplayHudToHide != null)
            {
                _gameplayHudToHide.SetActive(false);
            }

            // Ocultar las barras flotantes de los personajes enemigos
            var worldBars = FindObjectsByType<WorldHealthBar>(FindObjectsSortMode.None);
            for (int i = 0; i < worldBars.Length; i++)
            {
                worldBars[i].Hide();
            }

            // Liberar cursor del mouse
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 2. Determinar si el jugador local ganó
            _isLocalWinner = false;
            PlayerID? localId = null;

            if (ArcherPlayer.LocalPlayer != null)
            {
                localId = ArcherPlayer.LocalPlayer.owner;
            }
            else if (PlayerIdentity.allPlayers != null)
            {
                foreach (var kvp in PlayerIdentity.allPlayers)
                {
                    if (kvp.Value != null && kvp.Value.isOwner)
                    {
                        localId = kvp.Key;
                        break;
                    }
                }
            }

            if (localId.HasValue && matchWinner.HasValue)
            {
                _isLocalWinner = (localId.Value == matchWinner.Value);
            }

            int p1Wins = GameManager.Instance != null ? GameManager.Instance.Player1Wins : 0;
            int p2Wins = GameManager.Instance != null ? GameManager.Instance.Player2Wins : 0;

            int localWins = p1Wins;
            int rivalWins = p2Wins;

            if (PlayerIdentity.allPlayers != null && localId.HasValue)
            {
                var keys = new System.Collections.Generic.List<PlayerID>(PlayerIdentity.allPlayers.Keys);
                if (keys.Count >= 2 && localId.Value == keys[1])
                {
                    localWins = p2Wins;
                    rivalWins = p1Wins;
                }
            }

            // Fallback: asegurarse de que el ganador siempre vea su puntuación primero
            if (_isLocalWinner)
            {
                int max = Mathf.Max(localWins, rivalWins);
                int min = Mathf.Min(localWins, rivalWins);
                localWins = max;
                rivalWins = min;
            }
            else
            {
                int max = Mathf.Max(localWins, rivalWins);
                int min = Mathf.Min(localWins, rivalWins);
                localWins = min;
                rivalWins = max;
            }

            _scoreTextString = $"{localWins} - {rivalWins}";

            // 3. Encender la pantalla de fin de partida (si está asignada)
            if (_victoryDefeatPanel != null)
            {
                _victoryDefeatPanel.SetActive(true);

                if (_resultTitleText != null)
                {
                    _resultTitleText.text = _isLocalWinner ? "¡VICTORIA!" : "DERROTA";
                    _resultTitleText.color = _isLocalWinner ? _victoryColor : _defeatColor;
                }

                if (_scoreText != null)
                {
                    _scoreText.text = _scoreTextString;
                }
            }
        }

        public void ReturnToMainMenu()
        {
            if (NetworkManager.main != null)
            {
                if (NetworkManager.main.isClient)
                {
                    NetworkManager.main.StopClient();
                }

                if (NetworkManager.main.isServer)
                {
                    NetworkManager.main.StopServer();
                }
            }

            SceneManager.LoadScene(_mainMenuSceneName);
        }

        private void OnGUI()
        {
            if (!_isMatchOver || !_enableOnGuiFallback) return;

            // Si hay un panel Canvas dedicado activo, no dibujamos el fallback OnGUI
            if (_victoryDefeatPanel != null && _victoryDefeatPanel.activeInHierarchy) return;

            float boxWidth = 440f;
            float boxHeight = 220f;
            float posX = (Screen.width - boxWidth) * 0.5f;
            float posY = Screen.height * 0.12f;

            GUI.Box(new Rect(posX, posY, boxWidth, boxHeight), GUIContent.none);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = _isLocalWinner ? _victoryColor : _defeatColor;

            string title = _isLocalWinner ? "¡VICTORIA!" : "DERROTA";
            GUI.Label(new Rect(posX, posY + 20f, boxWidth, 65f), title, titleStyle);

            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter
            };
            bodyStyle.normal.textColor = Color.white;

            GUI.Label(new Rect(posX, posY + 90f, boxWidth, 35f), _scoreTextString, bodyStyle);

            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };

            if (GUI.Button(new Rect(posX + 70f, posY + 145f, boxWidth - 140f, 48f), "Volver al Menú Principal", buttonStyle))
            {
                ReturnToMainMenu();
            }
        }
    }
}
