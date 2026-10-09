using UnityEngine;

namespace JuegoSinNombre
{
    public enum BodyPartType
    {
        Torso,
        Head,
        Arm,
        Leg
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class DamageHitbox : MonoBehaviour
    {
        [Header("Hitbox Settings")]
        [SerializeField] private BodyPartType _bodyPart = BodyPartType.Torso;
        [SerializeField] private float _damageMultiplier = 1.0f;

        [Header("References")]
        [SerializeField] private HealthModule _healthModule;
        [SerializeField] private RagdollController _ragdollController;

        public BodyPartType BodyPart => _bodyPart;
        public float DamageMultiplier => _damageMultiplier;
        public HealthModule HealthModule => _healthModule;

        private void Awake()
        {
            CacheReferences();
        }

        private void Reset()
        {
            CacheReferences();
            SetDefaultMultiplier();
        }

        private void CacheReferences()
        {
            if (_healthModule == null)
            {
                _healthModule = GetComponentInParent<HealthModule>();
            }

            if (_ragdollController == null)
            {
                _ragdollController = GetComponentInParent<RagdollController>();
            }
        }

        private void SetDefaultMultiplier()
        {
            switch (_bodyPart)
            {
                case BodyPartType.Head:
                    _damageMultiplier = 2.0f;
                    break;
                case BodyPartType.Torso:
                    _damageMultiplier = 1.0f;
                    break;
                case BodyPartType.Arm:
                case BodyPartType.Leg:
                    _damageMultiplier = 0.75f;
                    break;
            }
        }

        public void TakeDamage(float baseDamage, Vector3 hitPoint = default, Vector3 hitDirection = default, float impulseForce = 0f)
        {
            if (_healthModule == null || _healthModule.IsDead) 
                return;

            float calculatedDamage = baseDamage * _damageMultiplier;
            _healthModule.ApplyDamageServerRpc(calculatedDamage);

            if (_healthModule.IsDead && _ragdollController != null && impulseForce > 0f)
            {
                _ragdollController.ApplyImpactForce(hitDirection.normalized * impulseForce, hitPoint);
            }
        }
    }
}
