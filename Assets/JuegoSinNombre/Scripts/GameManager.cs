using System;
using UnityEngine;

namespace JuegoSinNombre
{
    public enum GameState
    {
        Playing,
        GameOver
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("State")]
        [SerializeField] private GameState _currentState = GameState.Playing;

        public GameState CurrentState => _currentState;
        public event Action<GameState> OnGameStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            HealthModule.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
            HealthModule.OnLocalPlayerDespawned += HandleLocalPlayerDespawned;

            if (HealthModule.LocalPlayer != null)
            {
                HandleLocalPlayerSpawned(HealthModule.LocalPlayer);
            }
        }

        private void OnDisable()
        {
            HealthModule.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
            HealthModule.OnLocalPlayerDespawned -= HandleLocalPlayerDespawned;

            if (HealthModule.LocalPlayer != null)
            {
                HandleLocalPlayerDespawned();
            }
        }

        private void HandleLocalPlayerSpawned(HealthModule playerHealth)
        {
            playerHealth.OnHealthChanged += CheckHealthCondition;
            playerHealth.OnDeath += HandlePlayerDeath;
        }

        private void HandleLocalPlayerDespawned()
        {
            if (HealthModule.LocalPlayer != null)
            {
                HealthModule.LocalPlayer.OnHealthChanged -= CheckHealthCondition;
                HealthModule.LocalPlayer.OnDeath -= HandlePlayerDeath;
            }
        }

        private void CheckHealthCondition(float currentHealth, float maxHealth)
        {
            if (currentHealth <= 0f && _currentState != GameState.GameOver)
            {
                TriggerGameOver();
            }
        }

        private void HandlePlayerDeath()
        {
            if (_currentState != GameState.GameOver)
            {
                TriggerGameOver();
            }
        }

        public void TriggerGameOver()
        {
            if (_currentState == GameState.GameOver) return;

            _currentState = GameState.GameOver;
            Debug.Log("<color=red><b>[GameManager] ¡FIN DE LA PARTIDA!</b> La vida ha llegado a 0.</color>");

            OnGameStateChanged?.Invoke(_currentState);
        }

        public void RestartGame()
        {
            _currentState = GameState.Playing;
            Debug.Log("[GameManager] Partida reiniciada.");
            OnGameStateChanged?.Invoke(_currentState);
        }
    }
}
