using UnityEngine;

/// <summary>
/// Un consumible. Casi lo mismo que una flecha pero sin fisica:
/// se activa en la fase ConsumableWindow y aplica efectos de una.
/// </summary>
[CreateAssetMenu(menuName = "Arqueria/Consumable", fileName = "NewConsumable")]
public class ConsumableDefinition : ScriptableObject
{
    public enum TargetKind
    {
        /// <summary>Catalejo de Argos, Ambrosia.</summary>
        Self,

        /// <summary>Sello de Loki, Polvo de Nix.</summary>
        Opponent,

        /// <summary>Fuelle de Eolo: no afecta a nadie, cambia el mundo.</summary>
        World
    }

    [Header("Identidad")]
    public string displayName = "Consumible";
    public Sprite icon;
    [TextArea] public string description;

    [Header("Uso")]
    public TargetKind target = TargetKind.Opponent;

    [Tooltip("Usos por partida. El GDD dice 1 para casi todos.")]
    [Min(1)] public int usesPerMatch = 1;

    [Header("Efectos")]
    [Tooltip("Que aplica al objetivo. Vacio si solo toca el mundo.")]
    public EffectDefinition[] effects;

    [Header("Mundo")]
    [Tooltip("Fuelle de Eolo: fuerza al servidor a tirar un viento nuevo.")]
    public bool rerollWind;

    [Tooltip("Ambrosia: vida que restaura. 0 si no cura.")]
    public float healAmount;
}
