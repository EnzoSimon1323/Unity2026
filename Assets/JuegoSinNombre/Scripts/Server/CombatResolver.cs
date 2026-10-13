using PurrNet;
using UnityEngine;

namespace JuegoSinNombre
{
    /// <summary>
    /// Resuelve el impacto de una flecha. Por ahora solo aplica daño;
    /// cuando exista el EffectRunner, acá se le pasarán tambien los
    /// effects de hit.effects al jugador golpeado.
    /// </summary>
    public static class CombatResolver
    {
        public static void ResolveHit(ArrowHitInfo hit, HealthModule victim)
        {
            if (victim == null || hit.damage <= 0f) return;

            victim.ApplyDamageServerRpc(hit.damage);
        }
    }
}