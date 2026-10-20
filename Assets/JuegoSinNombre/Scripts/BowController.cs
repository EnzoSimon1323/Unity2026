using System;
using PurrNet;
using UnityEngine;

/// <summary>
/// Tension del arco. Requiere mantener apuntado (clic derecho, ver AimController)
/// para poder tensar con clic izquierdo. Soltar el apuntado cancela el tiro.
/// </summary>
public class BowController : NetworkIdentity
{
    [Header("Tensado")]
    [SerializeField] private float drawTime = 1.2f;
    [SerializeField] private float minShootTension = 0.15f;
    [SerializeField] private AnimationCurve drawCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Referencias")]
    [SerializeField] private ArcherPlayer player;
    [SerializeField] private AimController aim;
    [SerializeField] private PlayerShooter shooter;
    [SerializeField] private Animator animator;
    [SerializeField] private string tensionParam = "Tension";
    [SerializeField] private string shootTriggerParam = "Shoot";

    public float Tension { get; private set; }
    public bool IsDrawing { get; private set; }

    public event Action OnDrawStarted;
    public event Action<float> OnTensionChanged;
    public event Action<float> OnReleased;
    public event Action OnDrawCancelled;

    private float _drawProgress;

    private void Awake()
    {
        if (player == null) TryGetComponent(out player);
        if (aim == null) TryGetComponent(out aim);
        if (shooter == null) TryGetComponent(out shooter);
        if (animator == null) TryGetComponent(out animator);
    }

    private void Update()
    {
        if (!isOwner) return;

        // Si el jugador ya disparó en esta micro-ronda, queda bloqueado esperando al rival
        if (shooter != null && shooter.HasShotThisRound)
        {
            if (IsDrawing) CancelDraw();
            return;
        }

        // Sin apuntar no hay nada que tensar: primero se levanta el arco (clic derecho).
        if (player == null || !player.CanShoot || aim == null || !aim.IsAiming)
        {
            if (IsDrawing) CancelDraw();
            return;
        }

        if (!IsDrawing)
        {
            if (Input.GetMouseButtonDown(0)) StartDraw();
            return;
        }

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
        float duration = Mathf.Max(0.01f, drawTime * (player != null ? player.DrawTimeMultiplier : 1f));
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
        if (animator != null) animator.SetTrigger(shootTriggerParam);
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
        if (animator != null) animator.SetFloat(tensionParam, value);
        OnTensionChanged?.Invoke(Tension);
    }
}