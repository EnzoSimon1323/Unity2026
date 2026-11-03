using PurrNet;
using UnityEngine;
using JuegoSinNombre.Core;

namespace JuegoSinNombre.Gameplay
{
    /// <summary>
    /// El hub del jugador. Implementa IEffectTarget: es el unico punto por donde
    /// el servidor modifica como se siente jugar.
    ///
    /// Guarda los valores vigentes (Sensitivity, DrawTimeMultiplier, etc.).
    /// El arco, la mira y la camara los leen de aca cada frame.
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

        /// <summary>Lista estática de todos los arqueros vivos en la partida.</summary>
        public static readonly System.Collections.Generic.List<ArcherPlayer> AllPlayers = new();

        /// <summary>El ArcherPlayer del jugador local, para que otros scripts (UI, flechas) lo encuentren sin pasar referencias a mano.</summary>
        public static ArcherPlayer LocalPlayer { get; private set; }

        private Transform _torsoTransform;
        public Transform TorsoTransform
        {
            get
            {
                if (_torsoTransform == null)
                {
                    var hitboxes = GetComponentsInChildren<DamageHitbox>(true);
                    foreach (var h in hitboxes)
                    {
                        if (h.BodyPart == BodyPartType.Torso)
                        {
                            _torsoTransform = h.transform;
                            break;
                        }
                    }
                    if (_torsoTransform == null)
                        _torsoTransform = transform;
                }
                return _torsoTransform;
            }
        }

        /// <summary>Lo escucha el HUD para redibujar la mira o el blur cuando algo cambia.</summary>
        public event System.Action OnModifiersChanged;

        private EffectRunner _effectRunner;
        private HealthModule _healthModule;

        private void Awake()
        {
            _effectRunner = new EffectRunner(this);
            _healthModule = GetComponent<HealthModule>();
        }

        protected override void OnSpawned(bool asServer)
        {
            ResetModifiers();

            if (!AllPlayers.Contains(this))
                AllPlayers.Add(this);

            if (isOwner)
                LocalPlayer = this;

            if (isServer)
            {
                _effectRunner.OnEffectsChanged += HandleEffectsChangedOnServer;
                GameManager.OnPhaseChangedGlobal += HandlePhaseChangedOnServer;
                if (_healthModule != null)
                    _healthModule.OnDeath += HandleDeathOnServer;
            }
        }

        protected override void OnDespawned(bool asServer)
        {
            AllPlayers.Remove(this);

            if (LocalPlayer == this)
                LocalPlayer = null;

            if (isServer)
            {
                _effectRunner.OnEffectsChanged -= HandleEffectsChangedOnServer;
                GameManager.OnPhaseChangedGlobal -= HandlePhaseChangedOnServer;
                if (_healthModule != null)
                    _healthModule.OnDeath -= HandleDeathOnServer;
            }
        }

        private void Update()
        {
            if (isServer && GameManager.Instance != null && GameManager.Instance.CurrentPhase == RoundPhase.Aiming)
            {
                _effectRunner?.TickPoison(Time.deltaTime, _healthModule);
            }
        }

        public void ApplyEffect(EffectDefinition effect)
        {
            if (!isServer) return;
            _effectRunner?.ApplyEffect(effect);
        }

        private void HandleEffectsChangedOnServer()
        {
            if (!isServer) return;
            var mods = _effectRunner.CalculateModifiers();
            SyncModifiersObserversRpc(mods.sensMult, mods.drawMult, mods.blur, mods.fovMult, mods.axesInv, mods.hideCross);
        }

        private void HandlePhaseChangedOnServer(RoundPhase newPhase)
        {
            if (!isServer) return;
            _effectRunner?.OnPhaseChanged(newPhase);
        }

        private void HandleDeathOnServer()
        {
            if (!isServer) return;
            _effectRunner?.ClearAll();
        }

        [ObserversRpc(runLocally: true)]
        private void SyncModifiersObserversRpc(float sensMult, float drawMult, float blur, float fovMult, bool axesInv, bool hideCross)
        {
            Sensitivity = baseSensitivity * sensMult;
            DrawTimeMultiplier = Mathf.Max(0.01f, drawMult);
            BlurAmount = Mathf.Clamp01(blur);
            Fov = baseFov * Mathf.Max(0.1f, fovMult);
            if (playerCamera != null) playerCamera.fieldOfView = Fov;
            AxesInverted = axesInv;
            CrosshairVisible = !hideCross;
            Notify();
        }

        // ================= IEffectTarget =================
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
}
