using UnityEngine;

namespace JuegoSinNombre.Core
{
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

        [Tooltip("Velocidad de giro de corrección hacia el torso del rival (Buscadora de Apolo).")]
        public float homingTurnSpeed = 3f;

        [Tooltip("Porcentaje del daño causado que se transfiere como curación al tirador (Lamia: 0.35 = 35%).")]
        [Range(0f, 1f)] public float lifestealRatio = 0f;

        [Tooltip("Radio de daño en area. 0 = impacto puntual.")]
        public float splashRadius;

        [Header("Efectos")]
        [Tooltip("Que se le aplica al jugador impactado. Skadi congela, Jormungandr envenena.")]
        public EffectDefinition[] effectsOnTarget;

        [Tooltip("Que se le aplica al que dispara. Para flechas con contra, si llegan a balancearlo asi.")]
        public EffectDefinition[] effectsOnShooter;

        [Header("Estela Visual (Trail)")]
        [Tooltip("Habilita la estela visual detrás de la flecha en vuelo.")]
        public bool enableTrail = true;

        [Tooltip("Color inicial en el nacimiento de la estela.")]
        public Color trailColor = new Color(1f, 1f, 1f, 0.85f);

        [Tooltip("Color de desvanecimiento al final de la estela.")]
        public Color trailEndColor = new Color(1f, 1f, 1f, 0f);

        [Tooltip("Ancho de la estela al salir de la flecha.")]
        [Range(0.01f, 0.4f)] public float trailWidth = 0.08f;

        [Tooltip("Duración de la estela en el aire en segundos.")]
        [Range(0.05f, 1.5f)] public float trailTime = 0.35f;

        [Tooltip("Material opcional para la estela (si no se asigna, usa el shader unlit de partículas URP).")]
        public Material trailMaterial;
    }
}
