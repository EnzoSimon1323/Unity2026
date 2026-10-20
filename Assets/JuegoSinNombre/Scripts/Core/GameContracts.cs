using PurrNet;
using UnityEngine;

/// <summary>
/// Lo que tu capa de cliente expone para que el servidor le aplique efectos.
/// El cliente NO sabe que existe Skadi: recibe "sensibilidad por 0.4" y obedece.
///
/// Lo implementa el jugador local (tu parte, Programador A).
/// Lo llama el EffectRunner del servidor (Programador B).
/// </summary>
public interface IEffectTarget
{
    void SetAxisInversion(bool inverted);
    void SetSensitivityMultiplier(float multiplier);
    void SetDrawTimeMultiplier(float multiplier);
    void SetBlur(float amount);
    void SetFovMultiplier(float multiplier);
    void SetCrosshairVisible(bool visible);

    /// <summary>Habilita o bloquea el arco. Lo usa la FSM al entrar en Aiming o Resolving.</summary>
    void SetCanShoot(bool canShoot);

    /// <summary>Vuelve todo a los valores base. El EffectRunner lo llama antes de re-aplicar los efectos vivos.</summary>
    void ResetModifiers();
}

/// <summary>
/// Lo que vos expones (WindManager) y consume el HUD del Programador B
/// para la Rosa de los Vientos.
/// </summary>
public interface IWindProvider
{
    Vector3 Wind { get; }
    event System.Action<Vector3> OnWindChanged;
}

/// <summary>
/// Lo que viaja de tu CombatResolver al HealthModule de tu compañero.
/// Es un struct: no genera basura y se serializa facil si despues necesitan mandarlo por RPC.
/// </summary>
public struct ArrowHitInfo
{
    public PlayerID? shooter;
    public PlayerID? victim;
    public float damage;
    public Vector3 hitPoint;

    /// <summary>Los efectos de la flecha. El EffectRunner los aplica, el HealthModule solo mira el daño.</summary>
    public EffectDefinition[] effects;

    /// <summary>True si la victima es el que disparo (splash propio).</summary>
    public bool IsSelfHit => shooter.HasValue && victim.HasValue && shooter.Value == victim.Value;
}

/// <summary>
/// Representa la intención de disparo de un jugador en la micro-ronda actual.
/// Guardado por el servidor en la fase Aiming y ejecutado simultáneamente cuando ambos jugadores dispararon.
/// </summary>
public struct PendingShot
{
    public PlayerID shooter;
    public Vector3 spawnPosition;
    public Vector3 direction;
    public float speed;
    public ArrowDefinition arrowDef;
    public NetworkIdentity shooterIdentity;
}