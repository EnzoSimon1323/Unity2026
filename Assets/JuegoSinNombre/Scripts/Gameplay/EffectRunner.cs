using System.Collections.Generic;
using UnityEngine;
using JuegoSinNombre.Core;

namespace JuegoSinNombre.Gameplay
{
    /// <summary>
    /// Gestor de ciclo de vida de efectos temporales (veneno de Jörmungandr, congelamiento de Skadi, etc.).
    /// Corre en el servidor, recalcula multiplicadores combinados y aplica el daño en el tiempo (DoT).
    /// </summary>
    public class EffectRunner
    {
        private class ActiveEffect
        {
            public EffectDefinition Definition;
            public int RemainingPhases;
        }

        private readonly List<ActiveEffect> _activeEffects = new();
        private readonly IEffectTarget _target;

        public event System.Action OnEffectsChanged;

        public EffectRunner(IEffectTarget target)
        {
            _target = target;
        }

        public void ApplyEffect(EffectDefinition definition)
        {
            if (definition == null) return;

            // Regla de stack: Efectos con el mismo StackId no se acumulan; el nuevo refresca al viejo.
            string stackId = definition.StackId;
            _activeEffects.RemoveAll(e => e.Definition != null && e.Definition.StackId == stackId);

            _activeEffects.Add(new ActiveEffect
            {
                Definition = definition,
                RemainingPhases = Mathf.Max(1, definition.phaseCount)
            });

            OnEffectsChanged?.Invoke();
        }

        public void ClearAll()
        {
            if (_activeEffects.Count > 0)
            {
                _activeEffects.Clear();
                OnEffectsChanged?.Invoke();
            }
        }

        public void OnPhaseChanged(RoundPhase newPhase)
        {
            bool changed = false;
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var active = _activeEffects[i];
                if (active.Definition != null && active.Definition.expiresOnPhase == newPhase)
                {
                    active.RemainingPhases--;
                    if (active.RemainingPhases <= 0)
                    {
                        _activeEffects.RemoveAt(i);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                OnEffectsChanged?.Invoke();
            }
        }

        public void TickPoison(float deltaTime, HealthModule healthModule)
        {
            if (healthModule == null || healthModule.IsDead || deltaTime <= 0f) return;

            float totalDps = 0f;
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                var def = _activeEffects[i].Definition;
                if (def != null && def.damagePerSecond > 0f)
                {
                    totalDps += def.damagePerSecond;
                }
            }

            if (totalDps > 0f)
            {
                healthModule.ApplyDamage(totalDps * deltaTime);
            }
        }

        public (float sensMult, float drawMult, float blur, float fovMult, bool axesInv, bool hideCross, float dmgTakenMult) CalculateModifiers()
        {
            float sensMult = 1f;
            float drawMult = 1f;
            float blur = 0f;
            float fovMult = 1f;
            bool axesInv = false;
            bool hideCross = false;
            float dmgTakenMult = 1f;

            for (int i = 0; i < _activeEffects.Count; i++)
            {
                var def = _activeEffects[i].Definition;
                if (def == null) continue;

                sensMult *= def.sensitivityMultiplier;
                drawMult *= def.drawTimeMultiplier;
                if (def.blurAmount > blur) blur = def.blurAmount;
                fovMult *= def.fovMultiplier;
                if (def.invertAxes) axesInv = true;
                if (def.hideCrosshair) hideCross = true;
                if (def.incomingDamageMultiplier > 0f) dmgTakenMult *= def.incomingDamageMultiplier;
            }

            return (sensMult, drawMult, blur, fovMult, axesInv, hideCross, dmgTakenMult);
        }

        public float CalculateIncomingDamageMultiplier()
        {
            float mult = 1f;
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                var def = _activeEffects[i].Definition;
                if (def != null && def.incomingDamageMultiplier > 0f)
                {
                    mult *= def.incomingDamageMultiplier;
                }
            }
            return mult;
        }
    }
}

