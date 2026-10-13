using PurrNet;
using UnityEngine;

/// <summary>
/// MOCK TEMPORAL. Borrar cuando exista la RoundStateMachine del Programador B.
///
/// Sirve para que puedas probar tu lado sin esperar la FSM:
/// habilita el arco de una y te deja simular efectos con el teclado.
/// </summary>
public class RoundPhaseStub : NetworkIdentity
{
    [SerializeField] private ArcherPlayer player;

    protected override void OnSpawned(bool asServer)
    {
        if (!asServer) return;

        // Sin FSM, el arco arranca habilitado.
        player.SetCanShoot(true);
    }

    private void Update()
    {
        if (!isServer) return;

        // F1: simula el Sello de Loki
        if (Input.GetKeyDown(KeyCode.F1))
            player.SetAxisInversion(!player.AxesInverted);

        // F2: simula el congelamiento de Skadi
        if (Input.GetKeyDown(KeyCode.F2))
        {
            player.SetSensitivityMultiplier(0.4f);
            player.SetDrawTimeMultiplier(2f);
        }

        // F3: limpia todo, como hace el EffectRunner al caducar los efectos
        if (Input.GetKeyDown(KeyCode.F3))
            player.ResetModifiers();
    }
}
