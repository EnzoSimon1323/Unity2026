using System;
using PurrNet;
using UnityEngine;

/// <summary>
/// Tension del arco. Mantener clic para tensar, soltar para disparar.
///
/// Cambio respecto a la primera version: ya no tiene sus propios flags de
/// CanShoot ni DrawTimeMultiplier. Los lee del ArcherPlayer, que es
/// el unico que recibe ordenes del servidor.
/// </summary>
public class BowController : NetworkIdentity
{
    [Header("Tensado")]
    [Tooltip("Segundos para llegar al 100% de tension, sin efectos activos.")]
    [SerializeField] private float drawTime = 1.2f;

    [Tooltip("Por debajo de esta tension el disparo se cancela en vez de salir flojo.")]
    [SerializeField] private float minShootTension = 0.15f;

    [Tooltip("Da feel al tensado. X = progreso, Y = tension resultante.")]
    [SerializeField] private AnimationCurve drawCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Referencias")]
    [SerializeField] private ArcherPlayer player;
    [SerializeField] private PlayerShooter shooter;

    public float Tension { get; private set; }
    public bool IsDrawing { get; private set; }

    public event Action OnDrawStarted;
    public event Action<float> OnTensionChanged;
    public event Action<float> OnReleased;
    public event Action OnDrawCancelled;

    private float _drawProgress;

    private void Update()
    {
        if (!isOwner) return;

        // La FSM del servidor habilita el arco solo en la fase Aiming.
        if (!player.CanShoot)
        {
            if (IsDrawing) CancelDraw();
            return;
        }

        if (!IsDrawing)
        {
            if (Input.GetMouseButtonDown(0)) StartDraw();
            return;
        }

        if (Input.GetMouseButtonDown(1))
        {
            CancelDraw();
            return;
        }

        // Se consulta el estado del boton, no el "se soltó este frame".
        // Asi, si el jugador hace alt-tab, el arco se libera y no queda trabado.
        if (Input.GetMouseButton(0))
            UpdateDraw();
        else
            Release();
    }

    private void StartDraw()
    {
        IsDrawing = true;
        _drawProgress = 0f;
        SetTension(0f);
        OnDrawStarted?.Invoke();
    }

    private void UpdateDraw()
    {
        float duration = Mathf.Max(0.01f, drawTime * player.DrawTimeMultiplier);
        _drawProgress = Mathf.MoveTowards(_drawProgress, 1f, Time.deltaTime / duration);
        SetTension(drawCurve.Evaluate(_drawProgress));
    }

    private void Release()
    {
        float finalTension = Tension;
        IsDrawing = false;

        if (finalTension < minShootTension)
        {
            ResetDraw();
            OnDrawCancelled?.Invoke();
            return;
        }

        shooter.Shoot(finalTension);
        OnReleased?.Invoke(finalTension);
        ResetDraw();
    }

    private void CancelDraw()
    {
        IsDrawing = false;
        ResetDraw();
        OnDrawCancelled?.Invoke();
    }

    private void ResetDraw()
    {
        _drawProgress = 0f;
        SetTension(0f);
    }

    private void SetTension(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(value, Tension)) return;
        Tension = value;
        OnTensionChanged?.Invoke(Tension);
    }
}
