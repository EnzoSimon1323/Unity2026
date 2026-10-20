using PurrNet;
using UnityEngine;

/// <summary>
/// El hub del jugador. Implementa IEffectTarget: es el unico punto por donde
/// el servidor modifica como se siente jugar.
///
/// No aplica nada por su cuenta, solo guarda los valores vigentes.
/// El arco, la mira y la camara los leen de aca cada frame.
/// Asi sumar un efecto nuevo nunca toca este archivo.
/// </summary>
public class ArcherPlayer : NetworkIdentity, IEffectTarget
{
    [Header("Valores base")]
    [Tooltip("Sensibilidad del mouse sin ningun efecto activo.")]
    [SerializeField] private float baseSensitivity = 2f;

    [Tooltip("FOV sin ningun efecto activo.")]
    [SerializeField] private float baseFov = 60f;

    [Header("Referencias")]
    [SerializeField] private Camera playerCamera;

    // ---- Estado vigente. Lo leen BowController, AimController y CameraEffects ----
    public float Sensitivity { get; private set; }
    public bool AxesInverted { get; private set; }
    public float DrawTimeMultiplier { get; private set; } = 1f;
    public float BlurAmount { get; private set; }
    public float Fov { get; private set; }
    public bool CrosshairVisible { get; private set; } = true;
    public bool CanShoot { get; private set; }

    /// <summary>El ArcherPlayer del jugador local, para que otros scripts (UI, flechas) lo encuentren sin pasar referencias a mano.</summary>
    public static ArcherPlayer LocalPlayer { get; private set; }

    /// <summary>Lo escucha el HUD para redibujar la mira o el blur cuando algo cambia.</summary>
    public event System.Action OnModifiersChanged;

    protected override void OnSpawned(bool asServer)
    {
        ResetModifiers();

        if (isOwner)
            LocalPlayer = this;
    }

    protected override void OnDespawned(bool asServer)
    {
        if (LocalPlayer == this)
            LocalPlayer = null;
    }

    // ================= IEffectTarget =================
    // El EffectRunner del servidor llama ResetModifiers() y despues re-aplica
    // la lista de efectos vivos. Nunca acumula ni resta: recalcula de cero.

    public void ResetModifiers()
    {
        Sensitivity = baseSensitivity;
        AxesInverted = false;
        DrawTimeMultiplier = 1f;
        BlurAmount = 0f;
        Fov = baseFov;
        CrosshairVisible = true;
        Notify();
    }

    public void SetAxisInversion(bool inverted)
    {
        AxesInverted = inverted;
        Notify();
    }

    public void SetSensitivityMultiplier(float multiplier)
    {
        Sensitivity = baseSensitivity * multiplier;
        Notify();
    }

    public void SetDrawTimeMultiplier(float multiplier)
    {
        DrawTimeMultiplier = Mathf.Max(0.01f, multiplier);
        Notify();
    }

    public void SetBlur(float amount)
    {
        BlurAmount = Mathf.Clamp01(amount);
        Notify();
    }

    public void SetFovMultiplier(float multiplier)
    {
        Fov = baseFov * Mathf.Max(0.1f, multiplier);
        if (playerCamera != null) playerCamera.fieldOfView = Fov;
        Notify();
    }

    public void SetCrosshairVisible(bool visible)
    {
        CrosshairVisible = visible;
        Notify();
    }

    [ObserversRpc(runLocally: true)]
    public void SetCanShoot(bool canShoot)
    {
        CanShoot = canShoot;
        Notify();
    }

    private void Notify() => OnModifiersChanged?.Invoke();
}