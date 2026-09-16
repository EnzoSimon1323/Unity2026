using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace JuegoSinNombre
{
    public class HealthUi : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private Image _healthFillImage;
        [SerializeField] private TextMeshProUGUI _healthText;

        private HealthModule _boundHealthModule;

        private void OnEnable()
        {
            HealthModule.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
            HealthModule.OnLocalPlayerDespawned += HandleLocalPlayerDespawned;

            // Si el jugador local ya está generado en la escena, vincularlo directamente
            if (HealthModule.LocalPlayer != null)
            {
                BindPlayerHealth(HealthModule.LocalPlayer);
            }
        }

        private void OnDisable()
        {
            HealthModule.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
            HealthModule.OnLocalPlayerDespawned -= HandleLocalPlayerDespawned;

            UnbindCurrentPlayerHealth();
        }

        private void HandleLocalPlayerSpawned(HealthModule localPlayerHealth)
        {
            BindPlayerHealth(localPlayerHealth);
        }

        private void HandleLocalPlayerDespawned()
        {
            UnbindCurrentPlayerHealth();
        }

        private void BindPlayerHealth(HealthModule healthModule)
        {
            if (_boundHealthModule != null)
            {
                UnbindCurrentPlayerHealth();
            }

            _boundHealthModule = healthModule;
            _boundHealthModule.OnHealthChanged += UpdateHealthUI;

            // Inicializar la interfaz con los valores actuales
            UpdateHealthUI(_boundHealthModule.CurrentHealth, _boundHealthModule.MaxHealth);
        }

        private void UnbindCurrentPlayerHealth()
        {
            if (_boundHealthModule != null)
            {
                _boundHealthModule.OnHealthChanged -= UpdateHealthUI;
                _boundHealthModule = null;
            }
        }

        private void UpdateHealthUI(float currentHealth, float maxHealth)
        {
            if (_healthSlider != null)
            {
                _healthSlider.maxValue = maxHealth;
                _healthSlider.value = currentHealth;
            }

            if (_healthFillImage != null)
            {
                _healthFillImage.fillAmount = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
            }

            if (_healthText != null)
            {
                _healthText.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
            }
        }
    }
}

