using PurrNet;
using UnityEngine;
using JuegoSinNombre.Core;

namespace JuegoSinNombre.Gameplay
{
    /// <summary>
    /// Vuelo de la flecha. Toda la data viene del ArrowDefinition, asi que
    /// este script es el mismo para las seis flechas del GDD.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ArrowProjectile : NetworkIdentity
    {
        [SerializeField] private float lifeTime = 12f;

        private Rigidbody _rb;
        private ArrowDefinition _definition;
        private PlayerID? _shooter;
        private bool _flying;
        private Transform _homingTargetTorso;
        [SerializeField] private TrailRenderer _trailRenderer;
        private static Material _sharedTrailMaterial;

        private readonly SyncVar<Vector3> _velocity = new(Vector3.zero);
        private readonly SyncVar<float> _mass = new(0.1f);
        private readonly SyncVar<float> _windMultiplier = new(1.0f);
        private readonly SyncVar<bool> _alignToVelocity = new(true);

        public ArrowDefinition Definition => _definition;
        public PlayerID? Shooter => _shooter;
        public Rigidbody Rigidbody => _rb;
        public bool IsFlying => _flying;

        /// <summary>Evento global al spawnearse cualquier flecha en la red (para que la cámara local la siga si es propia).</summary>
        public static event System.Action<ArrowProjectile> OnAnyArrowSpawned;

        /// <summary>Disparado cuando la flecha impacta contra cualquier superficie o jugador.</summary>
        public event System.Action<Vector3> OnHit;

        /// <summary>Disparado cuando la flecha frena o se destruye.</summary>
        public event System.Action OnStopped;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                // Iniciar cinemático hasta recibir la velocidad inicial, evitando que la gravedad
                // empiece a desviar la flecha antes de que el vector de disparo sea aplicado.
                _rb.isKinematic = true;
            }
        }

        protected override void OnSpawned(bool asServer)
        {
            _velocity.onChanged += ApplyVelocity;
            _mass.onChanged += HandleMassChanged;

            if (_velocity.value.sqrMagnitude > 0.0001f)
                ApplyVelocity(_velocity.value);

            if (_rb != null && _mass.value > 0f)
                _rb.mass = _mass.value;

            if (isOwner)
                IgnoreOwnColliders();

            OnAnyArrowSpawned?.Invoke(this);
        }

        protected override void OnDespawned(bool asServer)
        {
            _velocity.onChanged -= ApplyVelocity;
            _mass.onChanged -= HandleMassChanged;
            if (_trailRenderer != null) _trailRenderer.emitting = false;
            OnStopped?.Invoke();
        }

        private void HandleMassChanged(float newMass)
        {
            if (_rb != null && newMass > 0f)
                _rb.mass = newMass;
        }

        private void IgnoreOwnColliders()
        {
            if (ArcherPlayer.LocalPlayer == null) return;

            var myColliders = GetComponentsInChildren<Collider>(true);
            var ownColliders = ArcherPlayer.LocalPlayer.GetComponentsInChildren<Collider>(true);

            foreach (var myCol in myColliders)
            {
                if (myCol == null) continue;
                foreach (var col in ownColliders)
                {
                    if (col != null)
                        Physics.IgnoreCollision(myCol, col, true);
                }
            }
        }

        private void ApplyVelocity(Vector3 v)
        {
            if (v.sqrMagnitude <= 0.0001f) return;

            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.linearVelocity = v;
            }
            _flying = true;
        }

        private TrailRenderer EnsureTrailRenderer()
        {
            if (_trailRenderer != null) return _trailRenderer;

            _trailRenderer = GetComponentInChildren<TrailRenderer>();
            if (_trailRenderer == null)
            {
                var trailGo = new GameObject("ArrowTrail");
                trailGo.transform.SetParent(transform, false);
                trailGo.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                _trailRenderer = trailGo.AddComponent<TrailRenderer>();
            }

            if (_trailRenderer.sharedMaterial == null)
            {
                if (_sharedTrailMaterial == null)
                {
                    Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                    if (s == null) s = Shader.Find("Sprites/Default");
                    if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
                    if (s != null)
                    {
                        _sharedTrailMaterial = new Material(s);
                    }
                }
                _trailRenderer.sharedMaterial = _sharedTrailMaterial;
            }

            return _trailRenderer;
        }

        [ObserversRpc(runLocally: true, bufferLast: true)]
        private void ConfigureTrailObserversRpc(bool enabled, Color startColor, Color endColor, float width, float time)
        {
            var trail = EnsureTrailRenderer();
            if (trail == null) return;

            if (!enabled)
            {
                trail.enabled = false;
                trail.emitting = false;
                return;
            }

            trail.enabled = true;
            trail.time = time;
            trail.alignment = LineAlignment.View;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;

            var widthCurve = new AnimationCurve();
            widthCurve.AddKey(0f, width);
            widthCurve.AddKey(1f, 0f);
            trail.widthCurve = widthCurve;

            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new(startColor, 0f), new(endColor, 1f) },
                new GradientAlphaKey[] { new(startColor.a, 0f), new(endColor.a, 1f) }
            );
            trail.colorGradient = gradient;

            trail.Clear();
            trail.emitting = true;
        }

        /// <summary>Lo llama PlayerShooter.ShootServerRpc, siempre en el servidor.</summary>
        public void Launch(ArrowDefinition definition, Vector3 direction, float speed, PlayerID? shooter)
        {
            if (!isServer) return;

            _definition = definition;
            _shooter = shooter;

            _mass.value = definition.mass;
            _windMultiplier.value = definition.windMultiplier;
            _alignToVelocity.value = definition.alignToVelocity;

            if (_rb != null)
            {
                _rb.mass = definition.mass;
            }

            if (definition != null && definition.enableTrail)
            {
                ConfigureTrailObserversRpc(
                    true, 
                    definition.trailColor, 
                    definition.trailEndColor, 
                    definition.trailWidth, 
                    definition.trailTime);
            }
            else
            {
                ConfigureTrailObserversRpc(false, Color.clear, Color.clear, 0f, 0f);
            }

            Vector3 initialVel = direction * speed;
            _velocity.value = initialVel;
            ApplyVelocity(initialVel);

            Destroy(gameObject, lifeTime);
        }

        private void FindHomingTarget()
        {
            foreach (var p in ArcherPlayer.AllPlayers)
            {
                if (p != null && (!p.owner.HasValue || !_shooter.HasValue || p.owner.Value != _shooter.Value))
                {
                    _homingTargetTorso = p.TorsoTransform;
                    break;
                }
            }
        }

        private void FixedUpdate()
        {
            if (!_flying) return;

            // Buscadora de Apolo: guiado autoritativo hacia el torso del oponente
            if (isServer && _definition != null && _definition.homing)
            {
                if (_homingTargetTorso == null)
                    FindHomingTarget();

                if (_homingTargetTorso != null && _rb != null)
                {
                    Vector3 toTarget = (_homingTargetTorso.position - transform.position).normalized;
                    float turnSpeed = _definition.homingTurnSpeed > 0f ? _definition.homingTurnSpeed : 3.5f;
                    Vector3 currentVel = _rb.linearVelocity;
                    float speed = currentVel.magnitude;
                    if (speed > 0.1f)
                    {
                        Vector3 newVel = Vector3.RotateTowards(currentVel, toTarget * speed, turnSpeed * Time.fixedDeltaTime, 0f);
                        _rb.linearVelocity = newVel;
                        _velocity.value = newVel;
                    }
                }
            }

            float windMult = _definition != null ? _definition.windMultiplier : _windMultiplier.value;
            if (windMult != 0f && WindManager.Instance != null)
            {
                _rb.AddForce(
                    WindManager.Instance.Wind * windMult,
                    ForceMode.Acceleration);
            }

            bool align = _definition != null ? _definition.alignToVelocity : _alignToVelocity.value;
            if (align && _rb.linearVelocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(_rb.linearVelocity);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_flying) return;

            string endpoint = isServer ? "SERVER" : "CLIENT";
            Debug.Log($"[ArrowProjectile] Colisión en {endpoint}: impactó con '{collision.gameObject.name}' (layer: {LayerMask.LayerToName(collision.gameObject.layer)})");

            if (collision.gameObject.TryGetComponent(out ArrowProjectile other))
            {
                if (isServer)
                {
                    Destroy(other.gameObject);
                    Destroy(gameObject);
                }
                _flying = false;
                if (_trailRenderer != null) _trailRenderer.emitting = false;
                OnHit?.Invoke(collision.GetContact(0).point);
                OnStopped?.Invoke();
                return;
            }

            _flying = false;
            if (_trailRenderer != null) _trailRenderer.emitting = false;
            Vector3 contactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Vector3 flightDirection = _rb != null && _rb.linearVelocity.sqrMagnitude > 0.01f ? _rb.linearVelocity.normalized : transform.forward;
            float arrowSpeed = _rb != null ? _rb.linearVelocity.magnitude : 0f;

            OnHit?.Invoke(contactPoint);
            OnStopped?.Invoke();

            if (isServer)
            {
                ArcherPlayer victim = collision.gameObject.GetComponentInParent<ArcherPlayer>();
                HealthModule health = collision.gameObject.GetComponentInParent<HealthModule>();

                if (victim != null && health != null)
                {
                    DamageHitbox hitbox = collision.gameObject.GetComponent<DamageHitbox>();
                    if (hitbox == null && collision.collider != null)
                    {
                        hitbox = collision.collider.GetComponent<DamageHitbox>();
                    }

                    BodyPartType bodyPart = hitbox != null ? hitbox.BodyPart : BodyPartType.Torso;
                    float multiplier = hitbox != null ? hitbox.DamageMultiplier : 1.0f;

                    // Regla GDD: La flecha buscadora de Apolo NUNCA puede impactar en la cabeza, solo en el cuerpo/torso
                    if (_definition != null && _definition.homing && bodyPart == BodyPartType.Head)
                    {
                        bodyPart = BodyPartType.Torso;
                        multiplier = 1.0f;
                    }

                    float baseDamage = _definition != null ? _definition.baseDamage : 0f;
                    float finalDamage = baseDamage * multiplier;

                    RagdollController ragdoll = hitbox != null && hitbox.RagdollController != null
                        ? hitbox.RagdollController
                        : collision.gameObject.GetComponentInParent<RagdollController>();

                    var hit = new ArrowHitInfo
                    {
                        shooter = _shooter,
                        victim = victim.owner,
                        damage = finalDamage,
                        hitPoint = contactPoint,
                        bodyPart = bodyPart,
                        damageMultiplier = multiplier,
                        definition = _definition,
                        effects = _definition != null ? _definition.effectsOnTarget : null
                    };

                    float impulseForce = arrowSpeed * (_rb != null ? _rb.mass : 0.1f) * 80f;
                    CombatResolver.ResolveHit(hit, health, ragdoll, flightDirection, impulseForce);
                }
            }

            _rb.isKinematic = true;
            transform.SetParent(collision.transform, true);
        }

        protected override void OnDestroy()
        {
            OnStopped?.Invoke();
            base.OnDestroy();
        }
    }
}
