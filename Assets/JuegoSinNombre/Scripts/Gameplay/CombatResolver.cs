using PurrNet;
using UnityEngine;
using JuegoSinNombre.Core;

namespace JuegoSinNombre.Gameplay
{
    /// <summary>
    /// Resuelve autoritativamente el impacto de una flecha en el servidor:
    /// - Aplica el daño calculado según el multiplicador de la hitbox impactada.
    /// - Si el impacto es letal, acciona el ragdoll con la fuerza de choque de la flecha.
    /// - Notifica al GameManager y demás oyentes mediante OnHitResolved.
    /// - (A futuro) Pasará los efectos a EffectRunner.
    /// </summary>
    public static class CombatResolver
    {
        public static event System.Action<ArrowHitInfo, HealthModule> OnHitResolved;

        public static void ResolveHit(
            ArrowHitInfo hit, 
            HealthModule victim, 
            RagdollController ragdoll = null, 
            Vector3 hitDirection = default, 
            float impulseForce = 0f)
        {
            if (victim == null || hit.damage <= 0f) return;

            victim.ApplyDamage(hit.damage);

            if (victim.IsDead && ragdoll != null && impulseForce > 0f)
            {
                ragdoll.ApplyImpactForce(hitDirection.normalized * impulseForce, hit.hitPoint);
            }

            Debug.Log($"<color=red><b>[CombatResolver] Impacto resuelto:</b> Parte={hit.bodyPart} | Mult=x{hit.damageMultiplier} | Daño={hit.damage:F1} | Víctima={hit.victim}</color>");

            OnHitResolved?.Invoke(hit, victim);
        }
    }
}
