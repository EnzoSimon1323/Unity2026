using UnityEngine;
using UnityEngine.InputSystem;

namespace JuegoSinNombre
{
    public class HealthTester : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private float _damageAmount = 20f;
        [SerializeField] private float _healAmount = 20f;

        private void Update()
        {
            if (HealthModule.LocalPlayer == null) 
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) 
                return;

            // Presiona K para recibir daño
            if (keyboard.kKey.wasPressedThisFrame)
            {
                Debug.Log($"<color=orange>[Test] Aplicando {_damageAmount} de daño al jugador local.</color>");
                HealthModule.LocalPlayer.ApplyDamageServerRpc(_damageAmount);
            }

            // Presiona H para curarse
            if (keyboard.hKey.wasPressedThisFrame)
            {
                Debug.Log($"<color=green>[Test] Curando {_healAmount} de vida al jugador local.</color>");
                HealthModule.LocalPlayer.HealServerRpc(_healAmount);
            }
        }
    }
}
