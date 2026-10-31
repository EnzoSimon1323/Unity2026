using System;
using UnityEngine;
using PurrNet.Utils;

namespace JuegoSinNombre.Core
{
    /// <summary>
    /// Registra tipos definidos por el usuario en el Hasher de PurrNet al arrancar el juego.
    /// Evita excepciones 'Type is not registered' cuando se usan enums u objetos serializados en SyncVars/RPCs.
    /// </summary>
    public static class NetworkTypeRegistry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RegisterNetworkTypes()
        {
            Hasher.PrepareType(typeof(RoundPhase));
            Hasher.PrepareType(typeof(BodyPartType));
            Hasher.PrepareType(typeof(MatchFormat));
        }
    }
}

