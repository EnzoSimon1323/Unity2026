/// <summary>
/// Fases de una ronda. Es la pieza de la que cuelga todo lo demas:
/// el arco pregunta si puede tensar, los consumibles si pueden activarse,
/// y los efectos temporales caducan al cambiar de fase.
///
/// Lo maneja el servidor (RoundStateMachine, tarea del Programador B)
/// y se replica a los clientes con un SyncVar.
/// </summary>
public enum RoundPhase
{
    /// <summary>Esperando que se conecten los dos jugadores.</summary>
    WaitingForPlayers,

    /// <summary>El server tira el viento de la ronda. Nadie puede hacer nada.</summary>
    RoundStart,

    /// <summary>Unica fase donde se aceptan consumibles. El arco esta bloqueado.</summary>
    ConsumableWindow,

    /// <summary>Arcos desbloqueados. Los consumibles ya no se aceptan.</summary>
    Aiming,

    /// <summary>Flechas en vuelo. Input bloqueado hasta que todas impacten o caduquen.</summary>
    Resolving,

    /// <summary>Se aplica el daño, se evalua si hubo acierto. Caducan los efectos de 1 turno.</summary>
    Outcome,

    /// <summary>Fallaron los dos: los jugadores se acercan un paso (Acercamiento Progresivo).</summary>
    StepForward,

    /// <summary>Un jugador quedo en el ultimo punto de vida.</summary>
    MatchPoint,

    /// <summary>Partida terminada.</summary>
    GameOver
}
