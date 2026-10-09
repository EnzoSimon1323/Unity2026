using System.Collections.Generic;
using UnityEngine;

namespace JuegoSinNombre
{
    [DisallowMultipleComponent]
    public class RagdollController : MonoBehaviour
    {
        [Header("Main References")]
        [SerializeField] private Animator _animator;
        [SerializeField] private Collider _mainCollider;
        [SerializeField] private HealthModule _healthModule;

        [Header("State")]
        [SerializeField] private bool _isRagdollActive;

        private Rigidbody[] _ragdollRigidbodies;
        private Collider[] _ragdollColliders;

        public bool IsRagdollActive => _isRagdollActive;

        private void Awake()
        {
            CacheReferences();
            CollectRagdollParts();
            SetRagdollState(false);
        }

        private void Reset()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            if (_healthModule == null)
            {
                CacheReferences();
            }

            if (_healthModule != null)
            {
                _healthModule.OnDeath -= HandleDeath;
                _healthModule.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (_healthModule != null)
            {
                _healthModule.OnDeath -= HandleDeath;
            }
        }

        private void CacheReferences()
        {
            if (_healthModule == null)
            {
                if (!TryGetComponent(out _healthModule))
                {
                    _healthModule = GetComponentInParent<HealthModule>();
                }
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_mainCollider == null)
            {
                TryGetComponent(out _mainCollider);
            }
        }

        private void CollectRagdollParts()
        {
            Rigidbody[] allRigidbodies = GetComponentsInChildren<Rigidbody>(true);
            Collider[] allColliders = GetComponentsInChildren<Collider>(true);

            List<Rigidbody> ragdollRbList = new List<Rigidbody>();
            Rigidbody rootRb = GetComponent<Rigidbody>();

            for (int i = 0; i < allRigidbodies.Length; i++)
            {
                Rigidbody rb = allRigidbodies[i];
                if (rb != rootRb)
                {
                    ragdollRbList.Add(rb);
                }
            }

            List<Collider> ragdollColList = new List<Collider>();
            for (int i = 0; i < allColliders.Length; i++)
            {
                Collider col = allColliders[i];
                if (col != _mainCollider)
                {
                    ragdollColList.Add(col);
                }
            }

            _ragdollRigidbodies = ragdollRbList.ToArray();
            _ragdollColliders = ragdollColList.ToArray();
        }

        public void SetRagdollState(bool active)
        {
            _isRagdollActive = active;

            if (_animator != null)
            {
                _animator.enabled = !active;
            }

            if (_mainCollider != null)
            {
                _mainCollider.enabled = !active;
            }

            if (_ragdollRigidbodies != null)
            {
                for (int i = 0; i < _ragdollRigidbodies.Length; i++)
                {
                    Rigidbody rb = _ragdollRigidbodies[i];
                    if (rb != null)
                    {
                        rb.isKinematic = !active;
                    }
                }
            }

            // Los colliders de los huesos permanecen siempre activos para funcionar como Hitboxes mientras está vivo
            if (_ragdollColliders != null)
            {
                for (int i = 0; i < _ragdollColliders.Length; i++)
                {
                    Collider col = _ragdollColliders[i];
                    if (col != null)
                    {
                        col.enabled = true;
                    }
                }
            }
        }

        private void HandleDeath()
        {
            if (_isRagdollActive) return;
            SetRagdollState(true);
        }

        public void ApplyImpactForce(Vector3 force, Vector3 hitPoint, float explosionRadius = 0.5f)
        {
            if (!_isRagdollActive)
            {
                SetRagdollState(true);
            }

            if (_ragdollRigidbodies == null || _ragdollRigidbodies.Length == 0) return;

            Rigidbody closestRb = _ragdollRigidbodies[0];
            float closestDistanceSqr = float.MaxValue;

            for (int i = 0; i < _ragdollRigidbodies.Length; i++)
            {
                Rigidbody rb = _ragdollRigidbodies[i];
                if (rb == null) continue;

                float sqrDist = (rb.position - hitPoint).sqrMagnitude;
                if (sqrDist < closestDistanceSqr)
                {
                    closestDistanceSqr = sqrDist;
                    closestRb = rb;
                }
            }

            if (closestRb != null)
            {
                closestRb.AddExplosionForce(force.magnitude, hitPoint, explosionRadius, 0.2f, ForceMode.Impulse);
            }
        }
    }
}
