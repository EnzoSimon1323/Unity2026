using UnityEngine;

/// <summary>
/// Una flecha del juego. Las seis flechas del GDD son seis assets de esto,
/// no seis clases. Balancear = mover sliders en el Inspector.
/// </summary>
[CreateAssetMenu(menuName = "Arqueria/Arrow", fileName = "NewArrow")]
public class ArrowDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string displayName = "Flecha";
    public Sprite icon;
    [TextArea] public string description;

    [Header("Prefab")]
    [Tooltip("Prefab con Rigidbody + NetworkIdentity. Puede ser el mismo para varias flechas si solo cambia la data.")]
    public GameObject prefab;

    [Header("Fisica")]
    [Tooltip("Multiplica la velocidad de salida. Mjolnir pesada = menos de 1.")]
    public float launchSpeedMultiplier = 1f;

    [Tooltip("Masa del Rigidbody. Mas masa = mas caida.")]
    [Min(0.01f)] public float mass = 0.1f;

    [Tooltip("Cuanto la arrastra el viento. Flecha Colosal = 0 (lo ignora), Gae Bulg liviana = 2.")]
    public float windMultiplier = 1f;

    [Tooltip("Apaga el alineado con la velocidad. La Buscadora de Apolo lo necesita en false.")]
    public bool alignToVelocity = true;

    [Header("Daño")]
    [Tooltip("Daño al impactar. El HealthModule del Programador B lo consume.")]
    public float baseDamage = 1f;

    [Tooltip("Impacta al rival aunque no le apunten (Buscadora de Apolo). Lo resuelve el servidor.")]
    public bool homing;

    [Tooltip("Radio de daño en area. 0 = impacto puntual.")]
    public float splashRadius;

    [Header("Efectos")]
    [Tooltip("Que se le aplica al jugador impactado. Skadi congela, Jormungandr envenena.")]
    public EffectDefinition[] effectsOnTarget;

    [Tooltip("Que se le aplica al que dispara. Para flechas con contra, si llegan a balancearlo asi.")]
    public EffectDefinition[] effectsOnShooter;
}
