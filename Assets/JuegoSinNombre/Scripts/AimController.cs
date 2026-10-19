using System;
using PurrNet;
using UnityEngine;

public class AimController : NetworkIdentity
{
    [Header("Referencias")]
    [SerializeField] private ArcherPlayer player;
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
    
    [Header("Camara al apuntar (estilo primera persona)")]
    [SerializeField] private Vector3 normalCameraOffset = new Vector3(0f, 0f, -4.42f);
    [SerializeField] private Vector3 aimCameraOffset = new Vector3(0f, 0f, 0.05f);
    [SerializeField] private float cameraMoveSpeed = 8f;
    

    private float _yaw;
    private float _pitch;
    private float _initialYaw;

    protected override void OnSpawned(bool asServer)
    {
        _isAiming.onChanged += HandleAimingChanged;

        if (!isOwner) return;

        _initialYaw = cameraPivot.eulerAngles.y;
        _yaw = _initialYaw;
        _pitch = cameraPivot.eulerAngles.x;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    protected override void OnDespawned(bool asServer)
    {
        _isAiming.onChanged -= HandleAimingChanged;
    }

    private void HandleAimingChanged(bool aiming)
    {
        if (animator != null) animator.SetBool(isAimingParam, aiming);
        OnAimingChanged?.Invoke(aiming);
    }

    private void Update()
    {
        if (!isOwner) return;

        bool aimingNow = Input.GetMouseButton(1);
        if (aimingNow != IsAiming)
        {
            IsAiming = aimingNow;
            if (animator != null) animator.SetBool(isAimingParam, IsAiming);
        }

        float sign = player.AxesInverted ? -1f : 1f;
        float sens = player.Sensitivity * (IsAiming ? aimSensitivityMultiplier : 1f);

        _yaw += Input.GetAxisRaw("Mouse X") * sens * sign;
        _pitch -= Input.GetAxisRaw("Mouse Y") * sens * sign;

        _yaw = Mathf.Clamp(_yaw, _initialYaw - maxYawOffset, _initialYaw + maxYawOffset);
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

        cameraPivot.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

        if (bodyPivot != null)
            bodyPivot.rotation = Quaternion.Euler(0f, _yaw, 0f);

        if (playerCamera != null)
            playerCamera.fieldOfView = player.Fov * (IsAiming ? aimFovMultiplier : 1f);
        
        if (playerCamera != null)
        {
            Vector3 targetOffset = IsAiming ? aimCameraOffset : normalCameraOffset;
            playerCamera.transform.localPosition = Vector3.Lerp(
                playerCamera.transform.localPosition, targetOffset, Time.deltaTime * cameraMoveSpeed);

            playerCamera.fieldOfView = player.Fov * (IsAiming ? aimFovMultiplier : 1f);
        }
    }

    public Vector3 AimDirection => cameraPivot.forward;
}