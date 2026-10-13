using PurrNet;
using UnityEngine;

/// <summary>
/// Viento global sincronizado. Implementa IWindProvider para que el HUD
/// de tu compañero se enganche sin conocer esta clase.
///
/// Va en un objeto de escena con NetworkIdentity, no en el prefab del jugador.
/// </summary>
public class WindManager : NetworkIdentity, IWindProvider
{
    public static WindManager Instance { get; private set; }

    [Header("Rango de generacion")]
    [SerializeField] private float minSpeed = 2f;
    [SerializeField] private float maxSpeed = 12f;

    // SyncVar como tipo generico instanciado. El server escribe, todos leen.
    private readonly SyncVar<Vector3> _wind = new(Vector3.zero);

    public Vector3 Wind => _wind.value;
    public event System.Action<Vector3> OnWindChanged;

    protected override void OnSpawned(bool asServer)
    {
        Instance = this;
        _wind.onChanged += HandleWindChanged;

        if (asServer) RollWind();
    }

    protected override void OnDespawned(bool asServer)
    {
        _wind.onChanged -= HandleWindChanged;
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// El server lo llama al entrar en RoundStart.
    /// El Fuelle de Eolo tambien lo usa para forzar un viento nuevo.
    /// </summary>
    public void RollWind()
    {
        if (!isServer) return;

        Vector2 dir = Random.insideUnitCircle.normalized;
        float speed = Random.Range(minSpeed, maxSpeed);

        _wind.value = new Vector3(dir.x, 0f, dir.y) * speed;
    }

    private void HandleWindChanged(Vector3 newWind) => OnWindChanged?.Invoke(newWind);
}
