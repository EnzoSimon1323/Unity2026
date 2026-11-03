using PurrNet;
using UnityEditor;

namespace JuegoSinNombre.EditorTools
{
    /// <summary>
    /// Fuerza el cierre del socket de red al salir del Play Mode en el Editor.
    /// Sin esto, detener Play sin pasar por un flujo de "salir de sala" deja el
    /// transporte UDP abierto en el proceso del Editor, bloqueando el puerto en
    /// el siguiente Play (WSAEADDRINUSE).
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeNetworkCleanup
    {
        static PlayModeNetworkCleanup()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode) return;

            NetworkManager manager = NetworkManager.main;
            if (manager == null) return;

            if (manager.isServer) manager.StopServer();
            if (manager.isClient) manager.StopClient();
        }
    }
}
