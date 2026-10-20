using System;
using PurrNet;
using UnityEngine;

/// <summary>
/// Crea la flecha. La decision real de spawnear vive en el servidor
/// (SubmitShotServerRpc): el cliente solo pide el disparo, nunca instancia nada.
/// En el flujo de micro-rondas sincronizadas, cada jugador registra su intención de tiro
/// y ambas flechas se lanzan en simultáneo cuando los dos hayan disparado.
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
    [SerializeField] private ArcherPlayer player;

    private int _selectedIndex;
    private bool _hasShotThisRound;

    /// <summary>Indica si el jugador local ya envió su flecha para esta micro-ronda.</summary>
    public bool HasShotThisRound => _hasShotThisRound;

    /// <summary>Evento para que la UI o animaciones sepan cuando el jugador está listo / esperando al rival.</summary>
    public event Action<bool> OnShotStatusChanged;

    /// <summary>La flecha equipada. El HUD la lee para mostrar el icono.</summary>
    public ArrowDefinition Selected =>
        (loadout != null && loadout.Length > 0) ? loadout[_selectedIndex] : null;

    private void Awake()
    {
        if (aim == null) TryGetComponent(out aim);
        if (player == null) TryGetComponent(out player);
    }

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

    /// <summary>Lo llama BowController al soltar. Valida y envía la intención de disparo.</summary>
    public void Shoot(float tension)
    {
        if (!isOwner || _hasShotThisRound) return;

        Vector3 direction = aim != null ? aim.AimDirection : transform.forward;
        if (direction.sqrMagnitude < 0.0001f) return;

        _hasShotThisRound = true;
        OnShotStatusChanged?.Invoke(true);

        // Bloquear temporalmente el arco local para no re-tensar mientras espera al rival
        if (player != null)
        {
            player.SetCanShoot(false);
        }

        SubmitShotServerRpc(Mathf.Clamp01(tension), direction.normalized, _selectedIndex);
    }

    [ServerRpc(requireOwnership: true)]
    private void SubmitShotServerRpc(float tension, Vector3 direction, int arrowIndex)
    {
        if (loadout == null || arrowIndex < 0 || arrowIndex >= loadout.Length) return;

        ArrowDefinition def = loadout[arrowIndex];
        if (def == null || def.prefab == null) return;

        tension = Mathf.Clamp01(tension);
        if (direction.sqrMagnitude < 0.0001f) return;
        direction.Normalize();

        Vector3 spawnPosition = transform.position + Vector3.up * spawnHeight + direction * spawnForwardOffset;
        float speed = baseLaunchSpeed * def.launchSpeedMultiplier * tension;

        if (JuegoSinNombre.GameManager.Instance != null && owner.HasValue)
        {
            JuegoSinNombre.GameManager.Instance.RegisterPendingShotServer(new PendingShot
            {
                shooter = owner.Value,
                spawnPosition = spawnPosition,
                direction = direction,
                speed = speed,
                arrowDef = def,
                shooterIdentity = this
            });
        }
        else
        {
            // Fallback de disparo directo si se testea en escenas sin GameManager
            SpawnAndLaunchDirectServer(def, spawnPosition, direction, speed);
        }
    }

    /// <summary>Llamado por el servidor al arrancar una nueva micro-ronda.</summary>
    [ObserversRpc(runLocally: true)]
    public void ResetRoundShotRpc()
    {
        _hasShotThisRound = false;
        OnShotStatusChanged?.Invoke(false);
    }

    private void SpawnAndLaunchDirectServer(ArrowDefinition def, Vector3 spawnPosition, Vector3 direction, float speed)
    {
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

        arrow.Launch(def, direction, speed, owner);
    }
}