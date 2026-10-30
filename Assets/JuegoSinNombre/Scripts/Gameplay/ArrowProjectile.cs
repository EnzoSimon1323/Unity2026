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

        private readonly SyncVar<Vector3> _velocity = new(Vector3.zero);

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

        private void Awake() => _rb = GetComponent<Rigidbody>();

        protected override void OnSpawned(bool asServer)
        {
            _velocity.onChanged += ApplyVelocity;

            if (_velocity.value.sqrMagnitude > 0.0001f)
                ApplyVelocity(_velocity.value);

            if (isOwner)
                IgnoreOwnColliders();

            OnAnyArrowSpawned?.Invoke(this);
        }

        protected override void OnDespawned(bool asServer)
        {
            _velocity.onChanged -= ApplyVelocity;
            OnStopped?.Invoke();
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
            _rb.AddForce(v, ForceMode.VelocityChange);
            _flying = v.sqrMagnitude > 0.0001f;
        }

        /// <summary>Lo llama PlayerShooter.ShootServerRpc, siempre en el servidor.</summary>
        public void Launch(ArrowDefinition definition, Vector3 direction, float speed, PlayerID? shooter)
        {
            if (!isServer) return;

            _definition = definition;
            _shooter = shooter;

            _rb.mass = definition.mass;
            _velocity.value = direction * speed;

            Destroy(gameObject, lifeTime);
        }

        private void FixedUpdate()
        {
            if (!_flying || _definition == null) return;

            if (_definition.windMultiplier != 0f && WindManager.Instance != null)
            {
                _rb.AddForce(
                    WindManager.Instance.Wind * _definition.windMultiplier,
                    ForceMode.Acceleration);
            }

            if (_definition.alignToVelocity && _rb.linearVelocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(_rb.linearVelocity);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_flying) return;

            if (collision.gameObject.TryGetComponent(out ArrowProjectile other))
            {
                if (isServer)
                {
                    Destroy(other.gameObject);
                    Destroy(gameObject);
                }
                _flying = false;
                OnHit?.Invoke(collision.GetContact(0).point);
                OnStopped?.Invoke();
                return;
            }

            _flying = false;
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
