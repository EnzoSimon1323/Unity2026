using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace JuegoSinNombre.UI
{
    /// <summary>
    /// Garantiza que siempre exista exactamente un EventSystem activo en la escena,
    /// eliminando automáticamente duplicados residuales que puedan coexistir tras transiciones de red.
    /// </summary>
    public static class EventSystemCleaner
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Deduplicate();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Deduplicate();
        }

        public static void Deduplicate()
        {
            var systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (systems != null && systems.Length > 1)
            {
                // Conservar el primero y destruir los duplicados adicionales
                for (int i = 1; i < systems.Length; i++)
                {
                    if (systems[i] != null && systems[i].gameObject != null)
                    {
                        Debug.Log($"[EventSystemCleaner] Removiendo EventSystem duplicado en '{systems[i].gameObject.name}'.");
                        Object.Destroy(systems[i].gameObject);
                    }
                }
            }
        }
    }
}

