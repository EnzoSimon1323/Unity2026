using PurrNet;
using UnityEngine;

/// <summary>
/// Crea la flecha. La decision real de spawnear vive en el servidor
/// (ShootServerRpc): el cliente solo pide el disparo, nunca instancia nada.
/// </summary>
public class PlayerShooter : NetworkIdentity
{
    [Header("Flechas")]
    [Tooltip("Las flechas que este jugador tiene disponibles.")]
    [SerializeField] private ArrowDefinition[] loadout;

    [Tooltip("Velocidad de salida con tension al 100%, antes del multiplicador de la flecha.")]
    [SerializeField] private float baseLaunchSpeed = 40f;

    [Header("Referencias")]
    [SerializeField] private Transform arrowSpawnPoint;
    [SerializeField] private AimController aim;

    private int _selectedIndex;

    /// <summary>La flecha equipada. El HUD la lee para mostrar el icono.</summary>
    public ArrowDefinition Selected =>
        (loadout != null && loadout.Length > 0) ? loadout[_selectedIndex] : null;

    private void Update()
    {
        if (!isOwner || loadout == null || loadout.Length == 0) return;

        // Rueda del mouse o teclas 1..6 para cambiar de flecha.
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.1f)
        {
            int dir = scroll > 0f ? 1 : -1;
            _selectedIndex = (_selectedIndex + dir + loadout.Length) % loadout.Length;
        }

        for (int i = 0; i < loadout.Length && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                _selectedIndex = i;
        }
    }

    /// <summary>Lo llama BowController al soltar. Solo valida lo imprescindible
    /// para no mandar basura, y delega la decision real al servidor.</summary>
    public void Shoot(float tension)
    {
        if (!isOwner) return;

        Vector3 direction = aim.AimDirection;
        if (direction.sqrMagnitude < 0.0001f) return;

        ShootServerRpc(Mathf.Clamp01(tension), direction.normalized, _selectedIndex);
    }

    [ServerRpc(requireOwnership: true)]
    private void ShootServerRpc(float tension, Vector3 direction, int arrowIndex)
    {
        // Validar todo de nuevo: nunca confiar en lo que mando el cliente.
        if (loadout == null || arrowIndex < 0 || arrowIndex >= loadout.Length) return;

        ArrowDefinition def = loadout[arrowIndex];
        if (def == null || def.prefab == null) return;

        tension = Mathf.Clamp01(tension);
        if (direction.sqrMagnitude < 0.0001f) return;
        direction.Normalize();

        GameObject go = Instantiate(
            def.prefab,
            arrowSpawnPoint.position,
            Quaternion.LookRotation(direction));

        if (!go.TryGetComponent(out ArrowProjectile arrow))
        {
            Debug.LogError($"El prefab de {def.displayName} no tiene ArrowProjectile.", go);
            Destroy(go);
            return;
        }

        if (go.TryGetComponent(out NetworkIdentity identity))
            identity.GiveOwnership(owner);

        float speed = baseLaunchSpeed * def.launchSpeedMultiplier * tension;
        arrow.Launch(def, direction, speed, owner);
    }
}