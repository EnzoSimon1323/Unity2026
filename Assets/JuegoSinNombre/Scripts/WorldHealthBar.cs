using UnityEngine;
using UnityEngine.UI;

namespace JuegoSinNombre
{
    public class WorldHealthBar : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image _healthFillImage;
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private GameObject _container;

        [Header("Settings")]
        [Tooltip("Si está activo, oculta la barra flotante sobre tu propio personaje (ya que tienes tu barra en el HUD).")]
        [SerializeField] private bool _hideForOwner = true;

        private HealthModule _healthModule;
        private Transform _cameraTransform;
        private Canvas _canvas;

        private void Awake()
        {
            _healthModule = GetComponentInParent<HealthModule>();
            _canvas = GetComponent<Canvas>();

            if (_container == null)
            {
                _container = gameObject;
            }
        }

        private void Start()
        {
            if (Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
                
                // Asignar automáticamente la cámara al Canvas si no tiene una
                if (_canvas != null && _canvas.worldCamera == null)
                {
                    _canvas.worldCamera = Camera.main;
                }
            }

            // Si es nuestro propio jugador y _hideForOwner es true, ocultar la barra flotante
            if (_hideForOwner && _healthModule != null && _healthModule.isOwner)
            {
                _container.SetActive(false);
                return;
            }

            // Inicializar el valor actual de la barra
            if (_healthModule != null)
            {
                UpdateHealthBar(_healthModule.CurrentHealth, _healthModule.MaxHealth);
            }
        }

        private void OnEnable()
        {
            if (_healthModule != null)
            {
                _healthModule.OnHealthChanged += UpdateHealthBar;
            }
        }

        private void OnDisable()
        {
            if (_healthModule != null)
            {
                _healthModule.OnHealthChanged -= UpdateHealthBar;
            }
        }

        private void LateUpdate()
        {
            // Reintentar obtener la cámara si aún no estaba lista en Start
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }

            // Efecto Billboard: mantener la barra siempre mirando hacia la cámara del jugador
            if (_cameraTransform != null && _container.activeSelf)
            {
                transform.forward = _cameraTransform.forward;
            }
        }

        private void UpdateHealthBar(float currentHealth, float maxHealth)
        {
            if (_healthFillImage != null)
            {
                _healthFillImage.fillAmount = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
            }

            if (_healthSlider != null)
            {
                _healthSlider.maxValue = maxHealth;
                _healthSlider.value = currentHealth;
            }
        }
    }
}
