using System;
using UnityEngine;
using TMPro;

namespace JuegoSinNombre
{
    /// <summary>
    /// UI temporal para indicar el estado del jugador durante la micro-ronda:
    /// (Apuntando, Tensando, Listo / Esperando rival, Flechas en vuelo).
    /// Soporta TextMeshProUGUI si está asignado, y provee fallback automático por OnGUI
    /// para testear inmediatamente sin requerir configuración manual previa en el Editor.
    /// </summary>
    public class PlayerTurnStatusUI : MonoBehaviour
    {
        [Header("UI Reference (Opcional - Si no se asigna, usa OnGUI)")]
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("Estilo OnGUI Fallback")]
        [SerializeField] private bool _enableOnGuiFallback = true;
        [SerializeField] private int _fontSize = 18;

        private PlayerIdentity _localPlayer;
        private ArcherPlayer _archer;
        private BowController _bow;
        private AimController _aim;
        private PlayerShooter _shooter;

        private string _lastFormattedMessage = "";
        private string _rawMessage = "";
        private Color _currentColor = Color.white;

        private void OnEnable()
        {
            PlayerIdentity.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
            PlayerIdentity.OnLocalPlayerDespawned += HandleLocalPlayerDespawned;

            if (PlayerIdentity.allPlayers != null)
            {
                foreach (var kvp in PlayerIdentity.allPlayers)
                {
                    if (kvp.Value != null && kvp.Value.isOwner)
                    {
                        HandleLocalPlayerSpawned(kvp.Value);
                        break;
                    }
                }
            }
        }

        private void OnDisable()
        {
            PlayerIdentity.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
            PlayerIdentity.OnLocalPlayerDespawned -= HandleLocalPlayerDespawned;
            UnbindPlayer();
        }

        private void HandleLocalPlayerSpawned(PlayerIdentity player)
        {
            if (player == null || !player.isOwner) return;

            _localPlayer = player;
            player.TryGetComponent(out _archer);
            player.TryGetComponent(out _bow);
            player.TryGetComponent(out _aim);
            player.TryGetComponent(out _shooter);
        }

        private void HandleLocalPlayerDespawned(PlayerIdentity player)
        {
            if (_localPlayer == player)
            {
                UnbindPlayer();
            }
        }

        private void UnbindPlayer()
        {
            _localPlayer = null;
            _archer = null;
            _bow = null;
            _aim = null;
            _shooter = null;
        }

        private void Update()
        {
            // Reintentar encontrar componentes si aún no se engancharon
            if (_localPlayer == null && ArcherPlayer.LocalPlayer != null)
            {
                var player = ArcherPlayer.LocalPlayer.GetComponent<PlayerIdentity>();
                if (player != null) HandleLocalPlayerSpawned(player);
            }

            ComputeCurrentStatus(out _rawMessage, out _lastFormattedMessage, out _currentColor);

            if (_statusText != null)
            {
                _statusText.text = _lastFormattedMessage;
            }
        }

        private void ComputeCurrentStatus(out string raw, out string formatted, out Color color)
        {
            RoundPhase phase = GameManager.Instance != null ? GameManager.Instance.CurrentPhase : RoundPhase.Aiming;
            bool hasShot = _shooter != null && _shooter.HasShotThisRound;
            bool isDrawing = _bow != null && _bow.IsDrawing;
            bool isAiming = _aim != null && _aim.IsAiming;

            switch (phase)
            {
                case RoundPhase.WaitingForPlayers:
                    raw = "Esperando que se conecte el oponente...";
                    color = new Color(0.7f, 0.7f, 0.7f);
                    formatted = $"<color=#B0B0B0>{raw}</color>";
                    break;

                case RoundPhase.RoundStart:
                    int rNum = GameManager.Instance != null ? GameManager.Instance.RoundNumber : 1;
                    raw = $"Ronda {rNum} - Preparando micro-ronda...";
                    color = new Color(0.4f, 0.8f, 1f);
                    formatted = $"<color=#66CCFF>{raw}</color>";
                    break;

                case RoundPhase.ConsumableWindow:
                    raw = "Ventana de Consumibles";
                    color = new Color(1f, 0.85f, 0.4f);
                    formatted = $"<color=#FFD966>{raw}</color>";
                    break;

                case RoundPhase.Aiming:
                    if (hasShot)
                    {
                        raw = "MIRA FIJADA: Esperando que el rival dispare...";
                        color = new Color(0.2f, 1f, 0.4f);
                        formatted = $"<color=#33FF66><b>MIRA FIJADA:</b> Esperando que el rival dispare...</color>";
                    }
                    else if (isDrawing)
                    {
                        int pct = Mathf.RoundToInt((_bow != null ? _bow.Tension : 0f) * 100f);
                        raw = $"Tensando arco ({pct}%) - Soltá para disparar";
                        color = new Color(1f, 0.6f, 0.1f);
                        formatted = $"<color=#FF991A><b>TENSANDO ({pct}%):</b> Soltá para disparar</color>";
                    }
                    else if (isAiming)
                    {
                        raw = "Apuntando... Mantené Clic Izquierdo para tensar";
                        color = new Color(1f, 1f, 0.3f);
                        formatted = $"<color=#FFFF4D><b>APUNTANDO:</b> Mantené Clic Izquierdo para tensar</color>";
                    }
                    else
                    {
                        raw = "Tu turno: Mantené Clic Derecho para apuntar";
                        color = new Color(0.3f, 0.9f, 1f);
                        formatted = $"<color=#4DE6FF><b>TU TURNO:</b> Mantené Clic Derecho para apuntar</color>";
                    }
                    break;

                case RoundPhase.Resolving:
                    raw = "¡FLECHAS EN VUELO! Resolviendo impactos...";
                    color = new Color(1f, 0.3f, 0.3f);
                    formatted = $"<color=#FF4D4D><b>¡FLECHAS EN VUELO!</b> Resolviendo...</color>";
                    break;

                case RoundPhase.Outcome:
                    raw = "Resolviendo resultado de la micro-ronda...";
                    color = new Color(0.9f, 0.9f, 0.9f);
                    formatted = $"<color=#E6E6E6>{raw}</color>";
                    break;

                case RoundPhase.GameOver:
                    raw = "Partida Maestra Finalizada";
                    color = new Color(1f, 0.85f, 0.2f);
                    formatted = $"<color=#FFD700><b>{raw}</b></color>";
                    break;

                default:
                    raw = "";
                    formatted = "";
                    color = Color.white;
                    break;
            }
        }

        private void OnGUI()
        {
            // Solo dibujar fallback por OnGUI si no hay un TextMeshProUGUI asignado y está habilitado
            if (_statusText != null || !_enableOnGuiFallback) return;
            if (string.IsNullOrEmpty(_rawMessage)) return;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.alignment = TextAnchor.MiddleCenter;
            boxStyle.fontSize = _fontSize;
            boxStyle.fontStyle = FontStyle.Bold;
            boxStyle.normal.textColor = _currentColor;

            // Caja centrada en la parte superior de la pantalla
            float width = Mathf.Max(380f, _rawMessage.Length * 11f);
            float height = 44f;
            float x = (Screen.width - width) * 0.5f;
            float y = 24f;

            // Fondo translúcido oscuro
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.85f);
            GUI.Box(new Rect(x, y, width, height), _rawMessage, boxStyle);
            GUI.backgroundColor = oldBg;
        }
    }
}