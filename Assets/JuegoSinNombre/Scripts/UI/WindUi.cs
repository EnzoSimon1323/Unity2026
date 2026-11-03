using JuegoSinNombre.Core;
using JuegoSinNombre.Gameplay;
using UnityEngine;
using TMPro;

namespace JuegoSinNombre
{
    /// <summary>
    /// Rosa de los vientos del HUD. Lee WindManager via IWindProvider (nunca la clase concreta),
    /// rota una flecha 2D hacia la direccion del viento y muestra su velocidad.
    /// </summary>
    public class WindUi : MonoBehaviour
    {
        private enum SpeedUnit
        {
            MetersPerSecond,
            KilometersPerHour
        }

        [Header("UI References")]
        [Tooltip("RectTransform de la flecha (Image 2D) que rota hacia el viento.")]
        [SerializeField] private RectTransform arrowImage;
        [SerializeField] private TextMeshProUGUI speedText;

        [Header("Settings")]
        [SerializeField] private SpeedUnit unit = SpeedUnit.MetersPerSecond;
        [Tooltip("Ajuste fino si la flecha no apunta bien de entrada. Depende de como este dibujado el sprite.")]
        [SerializeField] private float rotationOffset = 0f;

        private IWindProvider _wind;
        private Vector3 _lastWind;

        private void Update()
        {
            // WindManager.Instance puede no existir todavia cuando este objeto arranca
            // (depende del orden de spawn de red). Se engancha apenas aparece.
            if (_wind == null)
            {
                if (WindManager.Instance == null) return;

                _wind = WindManager.Instance;
                _wind.OnWindChanged += HandleWindChanged;
                HandleWindChanged(_wind.Wind);
                return;
            }

            // El jugador local puede spawnear despues del primer viento recibido, y su
            // transform es la referencia para la rotacion. Se reaplica mientras no haya
            // referencia todavia; una vez encontrada, el costo es un simple re-dibujo.
            if (ArcherPlayer.LocalPlayer != null)
                ApplyArrowRotation(_lastWind);
        }

        private void OnDestroy()
        {
            if (_wind != null)
                _wind.OnWindChanged -= HandleWindChanged;
        }

        private void HandleWindChanged(Vector3 wind)
        {
            _lastWind = wind;
            ApplyArrowRotation(wind);

            if (speedText != null)
            {
                float speed = wind.magnitude;
                speedText.text = unit == SpeedUnit.MetersPerSecond
                    ? $"{speed:0.0} m/s"
                    : $"{speed * 3.6f:0.0} km/h";
            }
        }

        private void ApplyArrowRotation(Vector3 wind)
        {
            if (arrowImage == null) return;

            Transform reference = ArcherPlayer.LocalPlayer != null ? ArcherPlayer.LocalPlayer.transform : null;
            if (reference == null) return;

            // Proyecta el viento (espacio mundo, compartido) sobre los ejes locales del
            // jugador propio: como los dos arqueros estan uno frente al otro, esto hace
            // que cada cliente vea la flecha apuntando correctamente desde su propio punto
            // de vista sin ningun caso especial para host/cliente.
            float right = Vector3.Dot(wind, reference.right);
            float forward = Vector3.Dot(wind, reference.forward);

            float angle = Mathf.Atan2(right, forward) * Mathf.Rad2Deg;
            arrowImage.localEulerAngles = new Vector3(0f, 0f, -angle + rotationOffset);
        }
    }
}