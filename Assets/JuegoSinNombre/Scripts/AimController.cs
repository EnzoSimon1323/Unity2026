using PurrNet;
using UnityEngine;

/// <summary>
/// Mira con el mouse. Lee la sensibilidad y la inversion de ejes del ArcherPlayer,
/// asi el Sello de Loki y el congelamiento de Skadi funcionan sin que este script
/// sepa que existen.
/// </summary>
public class AimController : NetworkIdentity
{
    [Header("Referencias")]
    [SerializeField] private ArcherPlayer player;
    [SerializeField] private Transform cameraPivot;

    [Header("Limites")]
    [Tooltip("Cuanto puede subir y bajar la mira, en grados.")]
    [SerializeField] private float minPitch = -40f;
    [SerializeField] private float maxPitch = 70f;

    [Tooltip("Cuanto puede girar a los costados desde su posicion inicial. Los jugadores estan fijos.")]
    [SerializeField] private float maxYawOffset = 60f;

    private float _yaw;
    private float _pitch;
    private float _initialYaw;

    protected override void OnSpawned(bool asServer)
    {
        if (!isOwner) return;

        _initialYaw = cameraPivot.eulerAngles.y;
        _yaw = _initialYaw;
        _pitch = cameraPivot.eulerAngles.x;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!isOwner) return;

        float sign = player.AxesInverted ? -1f : 1f;
        float sens = player.Sensitivity;

        _yaw += Input.GetAxisRaw("Mouse X") * sens * sign;
        _pitch -= Input.GetAxisRaw("Mouse Y") * sens * sign;

        _yaw = Mathf.Clamp(_yaw, _initialYaw - maxYawOffset, _initialYaw + maxYawOffset);
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

        cameraPivot.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    /// <summary>Direccion en la que apunta. La usa el PlayerShooter al disparar.</summary>
    public Vector3 AimDirection => cameraPivot.forward;
}
