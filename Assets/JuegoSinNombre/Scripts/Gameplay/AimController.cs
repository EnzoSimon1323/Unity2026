using System;
using PurrNet;
using UnityEngine;

namespace JuegoSinNombre.Gameplay
{
    public class AimController : NetworkIdentity
    {
        [Header("Referencias")]
        [SerializeField] private ArcherPlayer player;
        [SerializeField] private PlayerShooter shooter;
        [SerializeField] private BowController bow;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Transform bodyPivot;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Animator animator;

        [Header("Pulso humano (sway al tensar, estilo Wii Sports Resort)")]
        [Tooltip("Que tan rapido oscila el pulso. Bajo = temblor lento y humano, alto = vibracion nerviosa.")]
        [SerializeField] private float swayFrequency = 1.1f;
        [Tooltip("Amplitud maxima del temblor en grados, a tension 1 y sin fatiga.")]
        [SerializeField] private float swayAmplitude = 1.5f;
        [Tooltip("Como crece el temblor segun la tension del arco (0 = relajado, 1 = tension maxima).")]
        [SerializeField] private AnimationCurve swayByTension = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("Segundos sosteniendo el tiro a full tension hasta llegar a la fatiga maxima.")]
        [SerializeField] private float fatigueRampTime = 2.5f;
        [Tooltip("Multiplicador extra de amplitud cuando la fatiga esta al maximo.")]
        [SerializeField] private float fatigueMultiplier = 1f;

        private float _swaySeedYaw;
        private float _swaySeedPitch;
        private float _drawHoldTime;

        [Header("Limites")]
        [SerializeField] private float minPitch = -40f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float maxYawOffset = 60f;

        [Header("Apuntado (clic derecho mantenido)")]
        [SerializeField, Range(0.1f, 1f)] private float aimSensitivityMultiplier = 0.45f;
        [SerializeField, Range(0.5f, 1f)] private float aimFovMultiplier = 0.9f;
        [SerializeField] private string isAimingParam = "IsAiming";
        private readonly SyncVar<bool> _isAiming = new(false, ownerAuth: true);
        public bool IsAiming { get; private set; }
        public event Action<bool> OnAimingChanged;
        
        /// <summary>Indica si un sistema externo (como ArrowFollowCamera) tiene el control exclusivo de la cámara.</summary>
        public bool OverrideCameraControl { get; set; }

        public Transform CameraPivot => cameraPivot;
        public Camera PlayerCamera => playerCamera;

        public void ForceIdle()
        {
            if (!isOwner) return;
            _arrowFollowStarted = true;
            SetAiming(false);
        }
        
        [Header("Camara al apuntar (estilo primera persona)")]
        [SerializeField] private Vector3 normalCameraOffset = new Vector3(0f, 0f, -4.42f);
        [SerializeField] private Vector3 aimCameraOffset = new Vector3(0f, 0f, 0.05f);
        [SerializeField] private float cameraMoveSpeed = 8f;

        private float _yaw;
        private float _pitch;
        private float _initialYaw;
        private bool _arrowFollowStarted;
        private Quaternion _defaultCameraLocalRot = Quaternion.identity;

        private void Awake()
        {
            if (player == null) TryGetComponent(out player);
            if (shooter == null) TryGetComponent(out shooter);
            if (bow == null) TryGetComponent(out bow);
            if (animator == null) TryGetComponent(out animator);
        }

        protected override void OnSpawned(bool asServer)
        {
            _isAiming.onChanged += HandleAimingChanged;

            if (!isOwner) return;

            _initialYaw = cameraPivot != null ? cameraPivot.eulerAngles.y : transform.eulerAngles.y;
            _yaw = _initialYaw;
            _pitch = cameraPivot != null ? cameraPivot.eulerAngles.x : 0f;

            // Semillas aleatorias para que el Perlin noise de cada jugador no oscile identico.
            _swaySeedYaw = UnityEngine.Random.Range(0f, 1000f);
            _swaySeedPitch = UnityEngine.Random.Range(0f, 1000f);

            if (playerCamera != null)
                _defaultCameraLocalRot = playerCamera.transform.localRotation;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        protected override void OnDespawned(bool asServer)
        {
            _isAiming.onChanged -= HandleAimingChanged;
        }

        private void HandleAimingChanged(bool aiming)
        {
            IsAiming = aiming;
            if (animator != null) animator.SetBool(isAimingParam, aiming);
            OnAimingChanged?.Invoke(aiming);
        }

        private void SetAiming(bool aiming)
        {
            if (!isOwner) return;
            if (IsAiming == aiming) return;
            IsAiming = aiming;
            _isAiming.value = aiming;
            if (animator != null) animator.SetBool(isAimingParam, aiming);
            OnAimingChanged?.Invoke(aiming);
        }

        public void ResetAim()
        {
            if (isOwner)
            {
                SetAiming(false);
            }
            else
            {
                IsAiming = false;
            }
            if (animator != null) animator.SetBool(isAimingParam, false);
        }

        private void Update()
        {
            if (!isOwner) return;

            bool hasShot = shooter != null && shooter.HasShotThisRound;

            if (!hasShot)
            {
                _arrowFollowStarted = false;
            }
            else if (OverrideCameraControl)
            {
                _arrowFollowStarted = true;
            }

            if (OverrideCameraControl || _arrowFollowStarted)
            {
                // Una vez disparada la flecha / en cinemática o tras retornar la cámara,
                // el bloqueo se rompe y el jugador permanece en Idle hasta que vuelva a apuntar en su nuevo turno.
                SetAiming(false);
            }
            else if (hasShot)
            {
                // El jugador ya confirmó su tiro pero espera que el rival dispare:
                // Queda apuntando fijado y bloqueado (primera persona) sin poder mover la
                // mira ni la cámara. Vuelve a tercera persona recién cuando hasShot se
                // resetea (ambos jugadores dispararon y la siguiente micro-ronda arranca,
                // via ResetRoundShotRpc). Esto es intencional, no un bug.
                SetAiming(true);
            }
            else
            {
                // Turno libre: solo apunta si mantiene presionado clic derecho
                bool aimingNow = Input.GetMouseButton(1);
                SetAiming(aimingNow);
            }

            // Solo se lee el mouse si está en su turno libre (sin haber confirmado el tiro ni en cinemática)
            bool canLook = !hasShot && !OverrideCameraControl;

            if (canLook)
            {
                float sign = player != null && player.AxesInverted ? -1f : 1f;
                float sens = (player != null ? player.Sensitivity : 2f) * (IsAiming ? aimSensitivityMultiplier : 1f);

                _yaw += Input.GetAxisRaw("Mouse X") * sens * sign;
                _pitch -= Input.GetAxisRaw("Mouse Y") * sens * sign;

                _yaw = Mathf.Clamp(_yaw, _initialYaw - maxYawOffset, _initialYaw + maxYawOffset);
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }

            // Pulso humano: tiembla mas cuanto mas tenso esta el arco, y crece con el tiempo
            // sostenido (fatiga), como en el arco de Wii Sports Resort. Es un offset aparte
            // de _yaw/_pitch para no desviar permanentemente la mira que el jugador controla.
            bool isDrawing = bow != null && bow.IsDrawing;
            _drawHoldTime = isDrawing ? _drawHoldTime + Time.deltaTime : 0f;

            float tension = bow != null ? bow.Tension : 0f;
            float fatigue = fatigueRampTime > 0f ? Mathf.Clamp01(_drawHoldTime / fatigueRampTime) : 0f;
            float swayIntensity = swayByTension.Evaluate(tension) * (1f + fatigue * fatigueMultiplier);

            float noiseYaw = (Mathf.PerlinNoise(Time.time * swayFrequency + _swaySeedYaw, 0f) - 0.5f) * 2f;
            float noisePitch = (Mathf.PerlinNoise(0f, Time.time * swayFrequency + _swaySeedPitch) - 0.5f) * 2f;

            float swayYaw = IsAiming ? noiseYaw * swayAmplitude * swayIntensity : 0f;
            float swayPitch = IsAiming ? noisePitch * swayAmplitude * swayIntensity : 0f;

            float appliedYaw = _yaw + swayYaw;
            float appliedPitch = _pitch + swayPitch;

            if (cameraPivot != null && !OverrideCameraControl)
                cameraPivot.rotation = Quaternion.Euler(appliedPitch, appliedYaw, 0f);

            if (bodyPivot != null)
                bodyPivot.rotation = Quaternion.Euler(0f, appliedYaw, 0f);

            // Solo actualizar posición, rotación y FOV si la cámara no está en modo seguimiento cinemático
            if (playerCamera != null && !OverrideCameraControl)
            {
                Vector3 targetOffset = IsAiming ? aimCameraOffset : normalCameraOffset;
                playerCamera.transform.localPosition = Vector3.Lerp(
                    playerCamera.transform.localPosition, targetOffset, Time.deltaTime * cameraMoveSpeed);

                Quaternion targetRot = IsAiming ? Quaternion.identity : _defaultCameraLocalRot;
                playerCamera.transform.localRotation = Quaternion.Slerp(
                    playerCamera.transform.localRotation, targetRot, Time.deltaTime * cameraMoveSpeed);

                float baseFov = player != null ? player.Fov : 60f;
                playerCamera.fieldOfView = baseFov * (IsAiming ? aimFovMultiplier : 1f);
            }
        }

        public Vector3 AimDirection => (IsAiming && playerCamera != null) ? playerCamera.transform.forward : (cameraPivot != null ? cameraPivot.forward : transform.forward);
    }
}
