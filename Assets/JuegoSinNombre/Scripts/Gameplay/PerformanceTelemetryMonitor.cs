using System;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using PurrNet;
using JuegoSinNombre.Core;

namespace JuegoSinNombre.Gameplay
{
    /// <summary>
    /// Monitor de telemetría automática y rendimiento en tiempo real:
    /// - Mide FPS, tiempos de frame (ms) y picos de memoria del Garbage Collector (GC).
    /// - Supervisa el estado de la conexión multijugador (Host/Cliente, fase de FSM, jugadores conectados y flechas en vuelo).
    /// - Imprime reportes periódicos en consola y dibuja un HUD liviano en pantalla (activable/desactivable con F3).
    /// - Diseñado con Zero-Alloc para no contaminar las lecturas del Profiler.
    /// </summary>
    [DisallowMultipleComponent]
    public class PerformanceTelemetryMonitor : MonoBehaviour
    {
        public static PerformanceTelemetryMonitor Instance { get; private set; }

        [Header("Configuración de Reporte")]
        [Tooltip("Intervalo en segundos para registrar un resumen consolidado en la consola.")]
        [SerializeField] private float _consoleLogInterval = 5.0f;

        [Tooltip("Mostrar u ocultar el HUD de telemetría en pantalla.")]
        [SerializeField] private bool _showOnScreenHud = true;

        [Tooltip("Tecla rápida para alternar la visualización del HUD en pantalla.")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.F3;

        // Métricas de frames
        private float _fpsAccumulator;
        private int _fpsFrameCount;
        private float _currentFps;
        private float _currentMs;
        private float _minFps = 999f;
        private float _maxFps = 0f;

        // Métricas de memoria
        private long _lastTotalMemoryBytes;
        private long _allocatedSinceLastReport;
        private int _lastGcCount;
        private int _gcCollectionsInPeriod;

        // Temporizadores
        private float _fpsTimer;
        private float _consoleLogTimer;

        // Buffer reutilizable para GUI (evita GC Allocations en cada frame)
        private readonly StringBuilder _hudBuilder = new(512);
        private string _cachedHudText = "Iniciando telemetría...";
        private GUIStyle _hudBoxStyle;
        private GUIStyle _hudTextStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null && FindAnyObjectByType<PerformanceTelemetryMonitor>() == null)
            {
                var go = new GameObject("[PerformanceTelemetryMonitor]");
                go.AddComponent<PerformanceTelemetryMonitor>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _lastTotalMemoryBytes = GC.GetTotalMemory(false);
            _lastGcCount = GC.CollectionCount(0);
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                _showOnScreenHud = !_showOnScreenHud;
            }

            float dt = Time.unscaledDeltaTime;
            _fpsAccumulator += dt;
            _fpsFrameCount++;
            _fpsTimer += dt;
            _consoleLogTimer += dt;

            // Muestreo de FPS cada 0.5s
            if (_fpsTimer >= 0.5f)
            {
                _currentFps = _fpsFrameCount / _fpsAccumulator;
                _currentMs = (_fpsAccumulator / _fpsFrameCount) * 1000f;

                if (_currentFps < _minFps && _currentFps > 0f) _minFps = _currentFps;
                if (_currentFps > _maxFps) _maxFps = _currentFps;

                _fpsAccumulator = 0f;
                _fpsFrameCount = 0;
                _fpsTimer = 0f;

                UpdateCachedHudText();
            }

            // Monitoreo de GC y emisión de log periódico
            if (_consoleLogTimer >= _consoleLogInterval)
            {
                EmitConsoleTelemetryReport();
                _consoleLogTimer = 0f;
            }
        }

        private void UpdateCachedHudText()
        {
            long currentMem = GC.GetTotalMemory(false);
            long monoUsed = Profiler.GetMonoUsedSizeLong();
            int currentGcCount = GC.CollectionCount(0);

            string netRole = "OFFLINE";
            if (NetworkManager.main != null)
            {
                if (NetworkManager.main.isServer && NetworkManager.main.isClient) netRole = "HOST (Servidor + Cliente)";
                else if (NetworkManager.main.isServer) netRole = "DEDICATED SERVER";
                else if (NetworkManager.main.isClient) netRole = "CLIENT";
            }

            string phaseName = GameManager.Instance != null ? GameManager.Instance.CurrentPhase.ToString() : "N/A";
            int playersCount = PlayerIdentity.allPlayers != null ? PlayerIdentity.allPlayers.Count : 0;
            int arrowsInFlight = 0;

            var arrows = FindObjectsByType<ArrowProjectile>(FindObjectsInactive.Exclude);
            if (arrows != null)
            {
                for (int i = 0; i < arrows.Length; i++)
                {
                    if (arrows[i] != null && arrows[i].IsFlying) arrowsInFlight++;
                }
            }

            _hudBuilder.Clear();
            _hudBuilder.Append("<color=#FFD700><b>[TELEMETRÍA DE RENDIMIENTO (F3)]</b></color>\n");
            _hudBuilder.Append($"FPS: <b>{_currentFps:F1}</b> ({_currentMs:F1} ms) | Min: {_minFps:F0} | Max: {_maxFps:F0}\n");
            _hudBuilder.Append($"Memoria Heap: <b>{monoUsed / (1024f * 1024f):F1} MB</b> | GC Total: {currentMem / (1024f * 1024f):F1} MB | GC Hits: {currentGcCount}\n");
            _hudBuilder.Append($"Red: <color=#66FF66><b>{netRole}</b></color> | Jugadores: {playersCount}/2\n");
            _hudBuilder.Append($"Fase FSM: <b>{phaseName}</b> | Flechas en vuelo: {arrowsInFlight}");

            _cachedHudText = _hudBuilder.ToString();
        }

        private void EmitConsoleTelemetryReport()
        {
            long currentMem = GC.GetTotalMemory(false);
            long deltaMem = currentMem - _lastTotalMemoryBytes;
            _lastTotalMemoryBytes = currentMem;

            int currentGcCount = GC.CollectionCount(0);
            int gcHits = currentGcCount - _lastGcCount;
            _lastGcCount = currentGcCount;

            string netRole = "OFFLINE";
            if (NetworkManager.main != null)
            {
                if (NetworkManager.main.isServer && NetworkManager.main.isClient) netRole = "HOST";
                else if (NetworkManager.main.isServer) netRole = "SERVER";
                else if (NetworkManager.main.isClient) netRole = "CLIENT";
            }

            string phase = GameManager.Instance != null ? GameManager.Instance.CurrentPhase.ToString() : "N/A";
            int players = PlayerIdentity.allPlayers != null ? PlayerIdentity.allPlayers.Count : 0;

            string colorTag = _currentFps >= 50f ? "green" : (_currentFps >= 30f ? "yellow" : "red");
            float deltaKb = deltaMem > 0 ? (deltaMem / 1024f) : 0f;

            Debug.Log($"<color={colorTag}><b>[TELEMETRÍA]</b></color> FPS: <b>{_currentFps:F1}</b> ({_currentMs:F1}ms) | Heap: {Profiler.GetMonoUsedSizeLong() / (1024 * 1024):F1}MB | ΔMem: +{deltaKb:F0}KB | GC:{gcHits} | Red: <b>{netRole}</b> ({players} jug) | Fase: <b>{phase}</b>");
        }

        private void OnGUI()
        {
            if (!_showOnScreenHud) return;

            if (_hudBoxStyle == null)
            {
                _hudBoxStyle = new GUIStyle(GUI.skin.box);
                _hudTextStyle = new GUIStyle(GUI.skin.label)
                {
                    richText = true,
                    fontSize = 13,
                    wordWrap = false
                };
            }

            float boxWidth = 370f;
            float boxHeight = 98f;
            float xPos = 12f;
            float yPos = 12f;

            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.Box(new Rect(xPos, yPos, boxWidth, boxHeight), GUIContent.none, _hudBoxStyle);
            GUI.color = Color.white;

            GUI.Label(new Rect(xPos + 10f, yPos + 6f, boxWidth - 20f, boxHeight - 12f), _cachedHudText, _hudTextStyle);
        }
    }
}

