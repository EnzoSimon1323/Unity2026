using PurrNet;
using UnityEngine;

/// <summary>
/// Crea la flecha. La decision real de spawnear vive en el servidor
/// (ShootServerRpc): el cliente solo pide el disparo, nunca instancia nada.
/// </summary>
public class PlayerShooter : NetworkIdentity
{
    [Header("Flechas")]
    [SerializeField] private ArrowDefinition[] loadout;
    [SerializeField] private float baseLaunchSpeed = 40f;

    [Header("Spawn (independiente del BodyPivot, para que funcione igual en el servidor)")]
    [SerializeField] private float spawnHeight = 1.4f;
    [SerializeField] private float spawnForwardOffset = 0.5f;

    [Header("Referencias")]
    [SerializeField] private AimController aim;
    
    private Collider[] _ownColliders;

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
        
        if (loadout == null || arrowIndex < 0 || arrowIndex >= loadout.Length) return;

        ArrowDefinition def = loadout[arrowIndex];
        if (def == null || def.prefab == null) return;

        tension = Mathf.Clamp01(tension);
        if (direction.sqrMagnitude < 0.0001f) return;
        direction.Normalize();

        Vector3 spawnPosition = transform.position + Vector3.up * spawnHeight + direction * spawnForwardOffset;

        GameObject go = Instantiate(def.prefab, spawnPosition, Quaternion.LookRotation(direction));

        if (!go.TryGetComponent(out ArrowProjectile arrow))
        {
            Debug.LogError($"El prefab de {def.displayName} no tiene ArrowProjectile.", go);
            Destroy(go);
            return;
        }
        var arrowColliders = go.GetComponentsInChildren<Collider>(true);
        var ownColliders = GetComponentsInChildren<Collider>(true);
        foreach (var arrowCol in arrowColliders)
        {
            if (arrowCol == null) continue;
            foreach (var col in ownColliders)
            {
                if (col != null)
                    Physics.IgnoreCollision(arrowCol, col, true);
            }
        }
        
        

        if (go.TryGetComponent(out NetworkIdentity identity))
            identity.GiveOwnership(owner);

        float speed = baseLaunchSpeed * def.launchSpeedMultiplier * tension;
        arrow.Launch(def, direction, speed, owner);
    }
}