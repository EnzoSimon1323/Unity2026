using System;
using PurrNet;
using UnityEngine;

namespace JuegoSinNombre.Gameplay
{
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

        [Header("Visual de Flecha en Mano")]
        [Tooltip("Objeto de la flecha en la mano (HumanArcher_ArrowInHand). Si no se asigna, se autodetecta en los hijos.")]
        [SerializeField] private GameObject arrowInHand;

        [Tooltip("Si es true, la flecha aparece al levantar el arco para apuntar (clic derecho). Si es false, recién al tensar la cuerda (clic izquierdo).")]
        [SerializeField] private bool showOnAim = true;

        private readonly SyncVar<bool> _isArrowVisible = new(false, ownerAuth: true);

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

            FindArrowInHand();
            SetArrowActive(false);
        }

        protected override void OnSpawned(bool asServer)
        {
            base.OnSpawned(asServer);
            _isArrowVisible.onChanged += HandleArrowVisibilityChanged;

            if (aim != null)
                aim.OnAimingChanged += HandleAimingChanged;
            if (shooter != null)
                shooter.OnShotStatusChanged += HandleShotStatusChanged;

            if (TryGetComponent<HealthModule>(out var health))
                health.OnDeath += HandleDied;
        }

        protected override void OnDespawned(bool asServer)
        {
            base.OnDespawned(asServer);
            _isArrowVisible.onChanged -= HandleArrowVisibilityChanged;

            if (aim != null)
                aim.OnAimingChanged -= HandleAimingChanged;
            if (shooter != null)
                shooter.OnShotStatusChanged -= HandleShotStatusChanged;

            if (TryGetComponent<HealthModule>(out var health))
                health.OnDeath -= HandleDied;
        }

        private void FindArrowInHand()
        {
            if (arrowInHand != null) return;
            var allTransforms = GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t.name == "HumanArcher_ArrowInHand" || t.name.Contains("ArrowInHand"))
                {
                    arrowInHand = t.gameObject;
                    break;
                }
            }
        }

        private void HandleAimingChanged(bool isAiming)
        {
            UpdateArrowVisibility();
        }

        private void HandleShotStatusChanged(bool hasShot)
        {
            UpdateArrowVisibility();
        }

        private void HandleDied()
        {
            if (isOwner)
            {
                _isArrowVisible.value = false;
            }
            SetArrowActive(false);
        }

        private void HandleArrowVisibilityChanged(bool visible)
        {
            SetArrowActive(visible);
        }

        private void UpdateArrowVisibility()
        {
            if (!isOwner) return;

            bool canShow = false;

            if (player != null && player.CanShoot && (shooter == null || !shooter.HasShotThisRound))
            {
                if (showOnAim)
                {
                    canShow = aim != null && aim.IsAiming;
                }
                else
                {
                    canShow = IsDrawing;
                }
            }

            if (_isArrowVisible.value != canShow)
            {
                _isArrowVisible.value = canShow;
            }
            SetArrowActive(canShow);
        }

        private void SetArrowActive(bool active)
        {
            if (arrowInHand == null)
            {
                FindArrowInHand();
                if (arrowInHand == null) return;
            }

            if (arrowInHand.activeSelf != active)
            {
                arrowInHand.SetActive(active);
            }
        }

        private void Update()
        {
            if (!isOwner) return;

            // Si el jugador ya disparó en esta micro-ronda, queda bloqueado esperando al rival
            if (shooter != null && shooter.HasShotThisRound)
            {
                if (IsDrawing) CancelDraw();
                if (_isArrowVisible.value) _isArrowVisible.value = false;
                SetArrowActive(false);
                return;
            }

            // Sin apuntar no hay nada que tensar: primero se levanta el arco (clic derecho).
            if (player == null || !player.CanShoot || aim == null || !aim.IsAiming)
            {
                if (IsDrawing) CancelDraw();
                if (_isArrowVisible.value && (aim == null || !aim.IsAiming || !player.CanShoot))
                {
                    _isArrowVisible.value = false;
                    SetArrowActive(false);
                }
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
            UpdateArrowVisibility();
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
                UpdateArrowVisibility();
                return;
            }

            shooter.Shoot(finalTension);
            if (animator != null) animator.SetTrigger(shootTriggerParam);
            OnReleased?.Invoke(finalTension);
            ResetDraw();

            // Ocultar inmediatamente la flecha de la mano al disparar el proyectil
            if (_isArrowVisible.value)
            {
                _isArrowVisible.value = false;
            }
            SetArrowActive(false);
        }

        private void CancelDraw()
        {
            IsDrawing = false;
            ResetDraw();
            OnDrawCancelled?.Invoke();
            UpdateArrowVisibility();
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
}
