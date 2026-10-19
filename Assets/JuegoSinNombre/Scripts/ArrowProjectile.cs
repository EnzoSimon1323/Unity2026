using PurrNet;
using UnityEngine;
using JuegoSinNombre;

/// <summary>
/// Vuelo de la flecha. Toda la data viene del ArrowDefinition, asi que
/// este script es el mismo para las seis flechas del GDD.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ArrowProjectile : NetworkIdentity
{
    [Tooltip("Segundos antes de desaparecer si no impacta nada.")]
    [SerializeField] private float lifeTime = 12f;

    private Rigidbody _rb;
    private ArrowDefinition _definition;
    private PlayerID? _shooter;
    private bool _flying;

    public ArrowDefinition Definition => _definition;
    public PlayerID? Shooter => _shooter;

    private void Awake() => _rb = GetComponent<Rigidbody>();

    /// <summary>Lo llama el PlayerShooter apenas crea la flecha.</summary>
    /// <summary>Lo llama el PlayerShooter apenas crea la flecha.</summary>
    public void Launch(ArrowDefinition definition, Vector3 direction, float speed, PlayerID? shooter)
    {
        _definition = definition;
        _shooter = shooter;
        _flying = true;

        _rb.mass = definition.mass;
        _rb.AddForce(direction * speed, ForceMode.VelocityChange);

        Destroy(gameObject, lifeTime);
    }
    
    private void FixedUpdate()
    {
        if (!_flying || _definition == null) return;

        // El viento. Si windMultiplier es 0 (Flecha Colosal) no lo afecta.
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

        // Dos flechas que se cruzan en el aire se destruyen.
        if (collision.gameObject.TryGetComponent(out ArrowProjectile other))
        {
            Destroy(other.gameObject);
            Destroy(gameObject);
            return;
        }

        _flying = false;

        // El daño lo resuelve el servidor (CombatResolver). Esta flecha
        // solo detecta el impacto y arma el struct que viaja a HealthModule.
        if (isServer
            && collision.gameObject.TryGetComponent(out ArcherPlayer victim)
            && collision.gameObject.TryGetComponent(out HealthModule health))
        {
            var hit = new ArrowHitInfo
            {
                shooter = _shooter,
                victim = victim.owner,
                damage = _definition.baseDamage,
                hitPoint = collision.GetContact(0).point,
                effects = _definition.effectsOnTarget
            };

            CombatResolver.ResolveHit(hit, health);
        }

        // Se clava donde pego.
        _rb.isKinematic = true;
        transform.SetParent(collision.transform, true);
    }
}
