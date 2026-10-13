using UnityEngine;

/// <summary>
/// Un efecto temporal sobre un jugador. Es el corazon de la arquitectura:
/// Sello de Loki, Polvo de Nix, Catalejo de Argos, el congelamiento de Skadi
/// y el veneno de Jormungandr son TODOS instancias de esto, con distintos valores.
///
/// Crear un efecto nuevo = crear un asset. No se toca codigo.
/// </summary>
[CreateAssetMenu(menuName = "Arqueria/Effect", fileName = "NewEffect")]
public class EffectDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string displayName = "Efecto";
    public Sprite icon;

    [Tooltip("Texto para el HUD del jugador afectado.")]
    public string hudDescription;

    [Header("Duracion")]
    [Tooltip("En que fase caduca. Outcome = dura un turno. Resolving = dura hasta que el jugador dispare (veneno).")]
    public RoundPhase expiresOnPhase = RoundPhase.Outcome;

    [Tooltip("Cuantas veces tiene que alcanzarse esa fase antes de caducar. 1 para casi todo.")]
    [Min(1)] public int phaseCount = 1;

    [Header("Modificadores de input")]
    [Tooltip("Invierte los ejes del mouse. Sello de Loki.")]
    public bool invertAxes;

    [Tooltip("Multiplica la sensibilidad. Skadi usa ~0.4 para que la mira se sienta pesada.")]
    public float sensitivityMultiplier = 1f;

    [Tooltip("Multiplica el tiempo de tensado. Skadi usa 2.")]
    public float drawTimeMultiplier = 1f;

    [Header("Modificadores de camara")]
    [Tooltip("0 = sin blur. Polvo de Nix usa ~0.6.")]
    [Range(0f, 1f)] public float blurAmount;

    [Tooltip("Multiplica el FOV. Catalejo de Argos usa ~0.5 para hacer zoom.")]
    public float fovMultiplier = 1f;

    [Tooltip("Oculta la mira del HUD.")]
    public bool hideCrosshair;

    [Header("Daño en el tiempo")]
    [Tooltip("Vida por segundo que drena. Jormungandr. Negativo para curar.")]
    public float damagePerSecond;

    /// <summary>
    /// Para el EffectRunner: dos efectos con el mismo stackId no se acumulan,
    /// el nuevo reemplaza al viejo. Evita que dos Lokis seguidos se cancelen entre si.
    /// </summary>
    public string StackId => string.IsNullOrEmpty(displayName) ? name : displayName;
}
