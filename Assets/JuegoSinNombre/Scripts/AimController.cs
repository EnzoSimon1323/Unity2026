using System;
using PurrNet;
using UnityEngine;

public class AimController : NetworkIdentity
{
    [Header("Referencias")]
    [SerializeField] private ArcherPlayer player;
    [SerializeField] private PlayerShooter shooter;
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Transform bodyPivot;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Animator animator;

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

    public void ForceIdle()
    {
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

    private void Awake()
    {
        if (player == null) TryGetComponent(out player);
        if (shooter == null) TryGetComponent(out shooter);
        if (animator == null) TryGetComponent(out animator);
    }

    protected override void OnSpawned(bool asServer)
    {
        _isAiming.onChanged += HandleAimingChanged;

        if (!isOwner) return;

        _initialYaw = cameraPivot != null ? cameraPivot.eulerAngles.y : transform.eulerAngles.y;
        _yaw = _initialYaw;
        _pitch = cameraPivot != null ? cameraPivot.eulerAngles.x : 0f;

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
        if (IsAiming == aiming) return;
        IsAiming = aiming;
        _isAiming.value = aiming;
        if (animator != null) animator.SetBool(isAimingParam, aiming);
        OnAimingChanged?.Invoke(aiming);
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
            // Queda apuntando fijado y bloqueado sin poder mover la mira ni la cámara.
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

        if (cameraPivot != null && !OverrideCameraControl)
            cameraPivot.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

        if (bodyPivot != null)
            bodyPivot.rotation = Quaternion.Euler(0f, _yaw, 0f);

        // Solo actualizar posición y FOV si la cámara no está en modo seguimiento cinemático
        if (playerCamera != null && !OverrideCameraControl)
        {
            Vector3 targetOffset = IsAiming ? aimCameraOffset : normalCameraOffset;
            playerCamera.transform.localPosition = Vector3.Lerp(
                playerCamera.transform.localPosition, targetOffset, Time.deltaTime * cameraMoveSpeed);

            float baseFov = player != null ? player.Fov : 60f;
            playerCamera.fieldOfView = baseFov * (IsAiming ? aimFovMultiplier : 1f);
        }
    }

    public Vector3 AimDirection => cameraPivot != null ? cameraPivot.forward : transform.forward;
}