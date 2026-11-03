using PurrNet;
using UnityEngine;
using JuegoSinNombre.Core;

namespace JuegoSinNombre.Gameplay
{
    /// <summary>
    /// Resuelve autoritativamente el impacto de una flecha en el servidor:
    /// - Aplica el daño calculado según el multiplicador de la hitbox impactada.
    /// - Resuelve el robo de vida (Lamia Lifesteal) curando al tirador.
    /// - Aplica efectos de estado directos (Skadi, Jörmungandr) únicamente ante impacto al rival.
    /// - Si el impacto es letal, acciona el ragdoll con la fuerza de choque de la flecha.
    /// - Notifica al GameManager y demás oyentes mediante OnHitResolved.
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

            // Lamia Lifesteal: Curación autoritativa al tirador
            if (hit.definition != null && hit.definition.lifestealRatio > 0f && hit.shooter.HasValue)
            {
                var shooterPlayer = ArcherPlayer.AllPlayers.Find(p => p != null && p.owner.HasValue && p.owner.Value == hit.shooter.Value);
                if (shooterPlayer != null)
                {
                    HealthModule shooterHealth = shooterPlayer.GetComponent<HealthModule>();
                    if (shooterHealth != null)
                    {
                        float healAmount = hit.damage * hit.definition.lifestealRatio;
                        shooterHealth.ApplyHeal(healAmount);
                        Debug.Log($"<color=green><b>[CombatResolver] Lamia Lifesteal:</b> Curó {healAmount:F1} de vida al tirador {hit.shooter.Value}.</color>");
                    }
                }
            }

            // Efectos de estado (Skadi congelante, Jörmungandr veneno):
            // Regla GDD: Solo se aplican si impactan al rival (no a sí mismo, no al piso).
            if (hit.effects != null && hit.effects.Length > 0 && !hit.IsSelfHit)
            {
                ArcherPlayer victimPlayer = victim.GetComponent<ArcherPlayer>();
                if (victimPlayer != null)
                {
                    foreach (var effect in hit.effects)
                    {
                        if (effect != null)
                        {
                            victimPlayer.ApplyEffect(effect);
                            Debug.Log($"<color=cyan><b>[CombatResolver] Efecto aplicado:</b> {effect.displayName} a la víctima {hit.victim}.</color>");
                        }
                    }
                }
            }

            if (victim.IsDead && ragdoll != null && impulseForce > 0f)
            {
                ragdoll.ApplyImpactForce(hitDirection.normalized * impulseForce, hit.hitPoint);
            }

            Debug.Log($"<color=red><b>[CombatResolver] Impacto resuelto:</b> Parte={hit.bodyPart} | Mult=x{hit.damageMultiplier} | Daño={hit.damage:F1} | Víctima={hit.victim}</color>");

            OnHitResolved?.Invoke(hit, victim);
        }
    }
}
