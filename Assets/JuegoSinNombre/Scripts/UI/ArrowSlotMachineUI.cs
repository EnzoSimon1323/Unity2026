using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;
using JuegoSinNombre.Core;
using JuegoSinNombre.Gameplay;

namespace JuegoSinNombre.UI
{
    /// <summary>
    /// Elementos visuales de cada una de las 3 ranuras de flecha.
    /// Si asignás solo el slotRect, el script autocompleta el resto buscando en los hijos.
    /// </summary>
    [System.Serializable]
    public class SlotUIElements
    {
        [Tooltip("RectTransform del cuadrado de este slot.")]
        public RectTransform slotRect;

        [Tooltip("Imagen de fondo del cuadro.")]
        public Image background;

        [Tooltip("Borde o halo que se ilumina cuando esta ranura está seleccionada.")]
        public Image outlineBorder;

        [Tooltip("Contenedor interno del contenido que vibra durante la ruleta.")]
        public RectTransform reelContent;

        [Tooltip("Imagen del icono de la flecha.")]
        public Image arrowIcon;

        [Tooltip("Texto del nombre de la flecha (ej: COLOSAL, SKADI).")]
        public TextMeshProUGUI nameText;

        [Tooltip("Texto secundario con el rasgo o efecto (ej: PESADA, CONGELANTE).")]
        public TextMeshProUGUI traitText;

        [Tooltip("Texto de atajo de teclado: [1], [2] o [3].")]
        public TextMeshProUGUI keyBadge;

        [Tooltip("Overlay que se enciende cuando la flecha ya fue disparada.")]
        public GameObject spentOverlay;

        [Tooltip("Botón opcional para seleccionar el slot con click del mouse.")]
        public Button button;
    }

    /// <summary>
    /// Widget HUD de Mano Táctica estilo Tragamoneda (Slot Machine):
    /// 1. Los 3 slots giran en el centro de la pantalla como una ruleta y se detienen de 1 en 1.
    /// 2. Al terminar de repartir, se desplaza suavemente hacia la esquina inferior derecha.
    /// 3. Resalta la flecha seleccionada y marca las gastadas.
    /// </summary>
    public class ArrowSlotMachineUI : MonoBehaviour
    {
        [Header("Contenedor a Desplazar")]
        [Tooltip("El RectTransform del objeto con el Horizontal Layout Group (SlotContainer). Si se deja vacío, busca automáticamente el hijo con HorizontalLayoutGroup.")]
        [FormerlySerializedAs("_rootContainer")]
        [FormerlySerializedAs("_manualRootContainer")]
        [SerializeField] private RectTransform _containerToMove;

        [Header("Las 3 Ranuras")]
        [Tooltip("Los 3 slots (Slot 0 = Tecla 1, Slot 1 = Tecla 2, Slot 2 = Tecla 3).")]
        [FormerlySerializedAs("_slots")]
        [FormerlySerializedAs("_manualSlots")]
        [SerializeField] private SlotUIElements[] _slots = new SlotUIElements[3];

        [Header("Posición y Escala")]
        [Tooltip("Offset adicional para el centro de la pantalla.")]
        [SerializeField] private Vector2 _centerOffset = Vector2.zero;

        [Tooltip("Escala mientras está en el centro de la pantalla.")]
        [SerializeField] private float _centerScale = 1.2f;

        [Tooltip("Posición final en la esquina inferior derecha (distancia desde el borde derecho e inferior).")]
        [SerializeField] private Vector2 _bottomRightOffset = new Vector2(-25f, 25f);

        [Tooltip("Escala final en la esquina inferior derecha.")]
        [SerializeField] private float _hudScale = 0.95f;

        [Tooltip("Tamaño del sprite/icono de la flecha dentro de la ranura.")]
        [SerializeField] private Vector2 _iconSize = new Vector2(70f, 70f);

        [Header("Tiempos de Ruleta (segundos)")]
        [SerializeField] private float _spinDurationSlot0 = 1.0f;
        [SerializeField] private float _spinDurationSlot1 = 1.65f;
        [SerializeField] private float _spinDurationSlot2 = 2.3f;

        [Tooltip("Tiempo que se queda en el centro después de frenar el 3er slot antes de viajar.")]
        [SerializeField] private float _centerHoldDuration = 0.5f;

        [Tooltip("Duración del viaje desde el centro hasta la esquina inferior derecha.")]
        [SerializeField] private float _transitionDuration = 0.75f;

        private Canvas _targetCanvas;
        private CanvasGroup _canvasGroup;
        private PlayerShooter _shooter;
        private Coroutine _slotMachineRoutine;
        private bool _isSpinning = false;
        private ArrowDefinition[] _allPossibleArrows;
        private ArrowDefinition[] _currentHand;
        private int _selectedSlot = 0;

        private void Awake()
        {
            FindContainerToMove();

            _targetCanvas = GetComponentInParent<Canvas>();
            if (_targetCanvas == null)
            {
                _targetCanvas = FindAnyObjectByType<Canvas>();
            }

            // Usar CanvasGroup para ocultar visualmente sin desactivar el GameObject
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;

            AutoWireMissingSlotReferences();
            LoadPossibleArrows();
        }

        private void FindContainerToMove()
        {
            if (_containerToMove == null || _containerToMove == (RectTransform)transform || _containerToMove.GetComponent<Canvas>() != null)
            {
                var layout = GetComponentInChildren<HorizontalLayoutGroup>(true);
                if (layout != null)
                {
                    _containerToMove = layout.GetComponent<RectTransform>();
                }
                else if (transform.childCount > 0 && transform.GetChild(0) is RectTransform childRect)
                {
                    _containerToMove = childRect;
                }
                else
                {
                    _containerToMove = GetComponent<RectTransform>();
                }
            }
        }

        private void OnEnable()
        {
            PlayerIdentity.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
            PlayerIdentity.OnLocalPlayerDespawned += HandleLocalPlayerDespawned;

            TryBindLocalPlayer();
        }

        private void OnDisable()
        {
            PlayerIdentity.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
            PlayerIdentity.OnLocalPlayerDespawned -= HandleLocalPlayerDespawned;
            UnbindShooter();
        }

        private void Update()
        {
            if (_shooter == null)
            {
                TryBindLocalPlayer();
            }
        }

        private void TryBindLocalPlayer()
        {
            if (PlayerIdentity.allPlayers != null)
            {
                foreach (var kvp in PlayerIdentity.allPlayers)
                {
                    if (kvp.Value != null && kvp.Value.isOwner)
                    {
                        HandleLocalPlayerSpawned(kvp.Value);
                        return;
                    }
                }
            }

            if (ArcherPlayer.LocalPlayer != null)
            {
                var shooter = ArcherPlayer.LocalPlayer.GetComponent<PlayerShooter>();
                if (shooter != null)
                {
                    BindShooter(shooter);
                }
            }
        }

        private void HandleLocalPlayerSpawned(PlayerIdentity player)
        {
            if (player == null || !player.isOwner) return;

            var shooter = player.GetComponent<PlayerShooter>();
            if (shooter != null)
            {
                BindShooter(shooter);
            }
        }

        private void BindShooter(PlayerShooter shooter)
        {
            if (_shooter == shooter) return;

            UnbindShooter();

            _shooter = shooter;
            _shooter.OnHandDealt += HandleHandDealt;
            _shooter.OnHandChanged += HandleHandChanged;

            if (_shooter.CurrentHand != null && (_shooter.CurrentHand[0] != null || _shooter.CurrentHand[1] != null || _shooter.CurrentHand[2] != null))
            {
                HandleHandDealt(_shooter.CurrentHand);
            }
        }

        private void HandleLocalPlayerDespawned(PlayerIdentity player = null)
        {
            UnbindShooter();
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        private void UnbindShooter()
        {
            if (_shooter != null)
            {
                _shooter.OnHandDealt -= HandleHandDealt;
                _shooter.OnHandChanged -= HandleHandChanged;
                _shooter = null;
            }
        }

        private void HandleHandDealt(ArrowDefinition[] hand)
        {
            if (hand == null) return;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu") return;

            FindContainerToMove();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
            }

            _currentHand = (ArrowDefinition[])hand.Clone();

            if (_slotMachineRoutine != null)
                StopCoroutine(_slotMachineRoutine);

            _slotMachineRoutine = StartCoroutine(AnimateSlotMachineRoutine(_currentHand));
        }

        private void HandleHandChanged(ArrowDefinition[] hand, int selectedSlot)
        {
            _currentHand = hand;
            _selectedSlot = selectedSlot;

            if (!_isSpinning)
            {
                UpdateHudDisplay();
            }
        }

        private void AutoWireMissingSlotReferences()
        {
            FindContainerToMove();
            if (_containerToMove == null) return;

            if (_slots == null || _slots.Length < 3)
            {
                _slots = new SlotUIElements[3];
            }

            for (int i = 0; i < 3; i++)
            {
                if (_slots[i] == null) _slots[i] = new SlotUIElements();

                if (_slots[i].slotRect == null && i < _containerToMove.childCount)
                {
                    _slots[i].slotRect = _containerToMove.GetChild(i) as RectTransform;
                }

                var sRect = _slots[i].slotRect;
                if (sRect == null) continue;

                if (_slots[i].background == null)
                    _slots[i].background = sRect.GetComponent<Image>();

                if (_slots[i].outlineBorder == null)
                {
                    var b = sRect.Find("OutlineBorder") ?? sRect.Find("Border");
                    if (b != null) _slots[i].outlineBorder = b.GetComponent<Image>();
                }

                if (_slots[i].reelContent == null)
                {
                    var r = sRect.Find("ReelContent") ?? sRect.Find("Content");
                    _slots[i].reelContent = r != null ? r as RectTransform : sRect;
                }

                if (_slots[i].arrowIcon == null)
                {
                    var ic = sRect.Find("ReelContent/ArrowIcon") ?? sRect.Find("ArrowIcon") ?? sRect.Find("Icon");
                    if (ic != null) _slots[i].arrowIcon = ic.GetComponent<Image>();
                }

                if (_slots[i].arrowIcon != null)
                {
                    _slots[i].arrowIcon.color = Color.white;
                    _slots[i].arrowIcon.preserveAspect = true;
                    if (_iconSize.x > 0f && _iconSize.y > 0f)
                    {
                        _slots[i].arrowIcon.rectTransform.sizeDelta = _iconSize;
                    }
                }

                if (_slots[i].nameText == null)
                {
                    var n = sRect.Find("ReelContent/NameText") ?? sRect.Find("NameText") ?? sRect.Find("Name");
                    if (n != null) _slots[i].nameText = n.GetComponent<TextMeshProUGUI>();
                }

                if (_slots[i].traitText == null)
                {
                    var t = sRect.Find("ReelContent/TraitText") ?? sRect.Find("TraitText") ?? sRect.Find("Trait");
                    if (t != null) _slots[i].traitText = t.GetComponent<TextMeshProUGUI>();
                }

                if (_slots[i].keyBadge == null)
                {
                    var k = sRect.Find("KeyBadge") ?? sRect.Find("Badge");
                    if (k != null) _slots[i].keyBadge = k.GetComponent<TextMeshProUGUI>();
                }

                if (_slots[i].spentOverlay == null)
                {
                    var sp = sRect.Find("SpentOverlay") ?? sRect.Find("Spent") ?? sRect.Find("ReelContent/SpentOverlay");
                    if (sp != null) _slots[i].spentOverlay = sp.gameObject;
                }

                if (_slots[i].button == null)
                {
                    _slots[i].button = sRect.GetComponent<Button>();
                }

                if (_slots[i].button != null)
                {
                    int slotIndex = i;
                    _slots[i].button.onClick.RemoveAllListeners();
                    _slots[i].button.onClick.AddListener(() =>
                    {
                        if (_shooter != null)
                        {
                            _shooter.TrySelectSlot(slotIndex);
                        }
                    });
                }
            }
        }

        private void LoadPossibleArrows()
        {
            _allPossibleArrows = Resources.FindObjectsOfTypeAll<ArrowDefinition>();
            if (_allPossibleArrows == null || _allPossibleArrows.Length == 0)
            {
                var def = ScriptableObject.CreateInstance<ArrowDefinition>();
                def.displayName = "Colosal";
                _allPossibleArrows = new[] { def };
            }
        }

        // ================= ANIMACIÓN DE TRAGAMONEDA & TRASLADO =================

        private Vector2 CalculateBottomRightPosition()
        {
            float canvasWidth = 1920f;
            float canvasHeight = 1080f;

            if (_targetCanvas != null)
            {
                var cr = _targetCanvas.GetComponent<RectTransform>();
                if (cr != null && cr.rect.width > 50f && cr.rect.height > 50f)
                {
                    canvasWidth = cr.rect.width;
                    canvasHeight = cr.rect.height;
                }
            }

            float w = 345f;
            float h = 115f;
            if (_containerToMove != null)
            {
                if (_containerToMove.rect.width > 10f) w = _containerToMove.rect.width;
                if (_containerToMove.rect.height > 10f) h = _containerToMove.rect.height;
            }

            float halfCanvasW = canvasWidth * 0.5f;
            float halfCanvasH = canvasHeight * 0.5f;

            float scaledHalfW = (w * 0.5f) * _hudScale;
            float scaledHalfH = (h * 0.5f) * _hudScale;

            float marginX = Mathf.Abs(_bottomRightOffset.x);
            float marginY = Mathf.Abs(_bottomRightOffset.y);
            if (marginX < 1f) marginX = 25f;
            if (marginY < 1f) marginY = 25f;

            float targetX = halfCanvasW - scaledHalfW - marginX;
            float targetY = -halfCanvasH + scaledHalfH + marginY;

            return new Vector2(targetX, targetY);
        }

        private void SetContainerToCenter()
        {
            FindContainerToMove();
            if (_containerToMove == null) return;

            _containerToMove.anchorMin = new Vector2(0.5f, 0.5f);
            _containerToMove.anchorMax = new Vector2(0.5f, 0.5f);
            _containerToMove.pivot = new Vector2(0.5f, 0.5f);

            _containerToMove.anchoredPosition = _centerOffset;
            _containerToMove.localScale = Vector3.one * _centerScale;
        }

        private IEnumerator AnimateSlotMachineRoutine(ArrowDefinition[] targets)
        {
            _isSpinning = true;
            SetContainerToCenter();

            for (int i = 0; i < 3; i++)
            {
                if (_slots[i] != null)
                {
                    if (_slots[i].spentOverlay != null) _slots[i].spentOverlay.SetActive(false);
                    if (_slots[i].outlineBorder != null) _slots[i].outlineBorder.color = new Color(1f, 1f, 1f, 0.15f);
                }
            }

            float timer = 0f;
            bool slot0Stopped = false;
            bool slot1Stopped = false;
            bool slot2Stopped = false;

            float nextCycleTime = 0f;
            const float cycleInterval = 0.065f;

            while (!slot2Stopped)
            {
                timer += Time.deltaTime;

                if (timer >= nextCycleTime)
                {
                    nextCycleTime = timer + cycleInterval;

                    if (!slot0Stopped) ApplyRandomArrowToSlot(_slots[0]);
                    if (!slot1Stopped) ApplyRandomArrowToSlot(_slots[1]);
                    if (!slot2Stopped) ApplyRandomArrowToSlot(_slots[2]);
                }

                // Jitter vertical estilo ruleta
                float jitter = Mathf.Sin(timer * 45f) * 4f;
                if (!slot0Stopped && _slots[0]?.reelContent != null) _slots[0].reelContent.anchoredPosition = new Vector2(0f, jitter);
                if (!slot1Stopped && _slots[1]?.reelContent != null) _slots[1].reelContent.anchoredPosition = new Vector2(0f, -jitter);
                if (!slot2Stopped && _slots[2]?.reelContent != null) _slots[2].reelContent.anchoredPosition = new Vector2(0f, jitter);

                // Frenado progresivo ranura 0
                if (!slot0Stopped && timer >= _spinDurationSlot0)
                {
                    slot0Stopped = true;
                    if (_slots[0]?.reelContent != null) _slots[0].reelContent.anchoredPosition = Vector2.zero;
                    ApplyArrowToSlot(_slots[0], targets != null && targets.Length > 0 ? targets[0] : null);
                    if (_slots[0]?.slotRect != null) StartCoroutine(PunchScaleRoutine(_slots[0].slotRect));
                }

                // Frenado progresivo ranura 1
                if (!slot1Stopped && timer >= _spinDurationSlot1)
                {
                    slot1Stopped = true;
                    if (_slots[1]?.reelContent != null) _slots[1].reelContent.anchoredPosition = Vector2.zero;
                    ApplyArrowToSlot(_slots[1], targets != null && targets.Length > 1 ? targets[1] : null);
                    if (_slots[1]?.slotRect != null) StartCoroutine(PunchScaleRoutine(_slots[1].slotRect));
                }

                // Frenado progresivo ranura 2
                if (!slot2Stopped && timer >= _spinDurationSlot2)
                {
                    slot2Stopped = true;
                    if (_slots[2]?.reelContent != null) _slots[2].reelContent.anchoredPosition = Vector2.zero;
                    ApplyArrowToSlot(_slots[2], targets != null && targets.Length > 2 ? targets[2] : null);
                    if (_slots[2]?.slotRect != null) StartCoroutine(PunchScaleRoutine(_slots[2].slotRect));
                }

                yield return null;
            }

            // Pausa en el centro para contemplar la mano
            yield return new WaitForSeconds(_centerHoldDuration);

            // Transición suave: Centro -> Esquina Inferior Derecha
            yield return StartCoroutine(TransitionToBottomRightRoutine());

            _isSpinning = false;
            UpdateHudDisplay();
        }

        private IEnumerator PunchScaleRoutine(RectTransform rect)
        {
            if (rect == null) yield break;
            Vector3 startScale = Vector3.one * 1.35f;
            Vector3 endScale = Vector3.one;
            float duration = 0.25f;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float progress = t / duration;
                rect.localScale = Vector3.Lerp(startScale, endScale, Mathf.Sin(progress * Mathf.PI * 0.5f));
                yield return null;
            }
            rect.localScale = endScale;
        }

        private IEnumerator TransitionToBottomRightRoutine()
        {
            FindContainerToMove();
            if (_containerToMove == null) yield break;

            Vector2 startPos = _containerToMove.anchoredPosition;
            Vector3 startScale = _containerToMove.localScale;

            Vector2 targetPos = CalculateBottomRightPosition();
            Vector3 targetScale = Vector3.one * _hudScale;

            float elapsed = 0f;
            while (elapsed < _transitionDuration)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / _transitionDuration);
                // Smoothstep (aceleración y desaceleración suave)
                float ease = p * p * (3f - 2f * p);

                _containerToMove.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
                _containerToMove.localScale = Vector3.Lerp(startScale, targetScale, ease);
                yield return null;
            }

            _containerToMove.anchoredPosition = targetPos;
            _containerToMove.localScale = targetScale;
        }

        [ContextMenu("Probar Traslado a Abajo Derecha")]
        public void TestTransitionInPlayMode()
        {
            if (Application.isPlaying)
            {
                StartCoroutine(TransitionToBottomRightRoutine());
            }
        }

        // ================= ACTUALIZACIÓN VISUAL DE SLOTS =================

        private void ApplyRandomArrowToSlot(SlotUIElements view)
        {
            if (view == null || _allPossibleArrows == null || _allPossibleArrows.Length == 0) return;
            var rand = _allPossibleArrows[UnityEngine.Random.Range(0, _allPossibleArrows.Length)];
            ApplyArrowToSlot(view, rand);
        }

        private void ApplyArrowToSlot(SlotUIElements view, ArrowDefinition def)
        {
            if (view == null || def == null) return;

            if (view.nameText != null) view.nameText.text = def.displayName.ToUpper();
            if (view.traitText != null) view.traitText.text = GetArrowTraitLabel(def);

            Color themeColor = GetArrowThemeColor(def);
            if (view.arrowIcon != null)
            {
                view.arrowIcon.color = Color.white;
                view.arrowIcon.preserveAspect = true;
                if (_iconSize.x > 0f && _iconSize.y > 0f)
                {
                    view.arrowIcon.rectTransform.sizeDelta = _iconSize;
                }
                if (def.icon != null) view.arrowIcon.sprite = def.icon;
            }

            if (view.nameText != null)
                view.nameText.color = Color.Lerp(Color.white, themeColor, 0.45f);
        }

        private string GetArrowTraitLabel(ArrowDefinition def)
        {
            if (def == null) return "";
            if (def.homing) return "TELEDIRIGIDA";
            if (def.lifestealRatio > 0f) return $"+{(int)(def.lifestealRatio * 100)}% ROBO";
            if (def.windMultiplier == 0f) return "PESADA / S/VTO";
            if (def.effectsOnTarget != null && def.effectsOnTarget.Length > 0)
            {
                var eff = def.effectsOnTarget[0];
                if (eff != null)
                {
                    if (eff.incomingDamageMultiplier > 1f) return $"DEBILITA +{(int)((eff.incomingDamageMultiplier - 1f) * 100)}%";
                    if (eff.damagePerSecond > 0f) return "VENENO DoT";
                    if (eff.sensitivityMultiplier < 1f) return "CONGELANTE";
                }
            }
            if (def.windMultiplier > 1.5f) return "CRÍTICA / LIVIANA";
            return "ESTÁNDAR";
        }

        private Color GetArrowThemeColor(ArrowDefinition def)
        {
            if (def == null) return Color.white;
            if (def.homing) return new Color(0.75f, 0.4f, 1f); // Violeta / Oro
            if (def.lifestealRatio > 0f) return new Color(1f, 0.2f, 0.3f); // Carmesí
            if (def.windMultiplier == 0f) return new Color(1f, 0.55f, 0.1f); // Naranja Pesado
            if (def.effectsOnTarget != null && def.effectsOnTarget.Length > 0)
            {
                var eff = def.effectsOnTarget[0];
                if (eff != null)
                {
                    if (eff.damagePerSecond > 0f) return new Color(0.1f, 0.95f, 0.4f); // Verde
                    if (eff.sensitivityMultiplier < 1f) return new Color(0.1f, 0.85f, 1f); // Cyan Hielo
                }
            }
            if (def.windMultiplier > 1.5f) return new Color(1f, 0.85f, 0.2f); // Ámbar
            return Color.white;
        }

        private void UpdateHudDisplay()
        {
            if (_currentHand == null) return;

            for (int i = 0; i < 3; i++)
            {
                var def = i < _currentHand.Length ? _currentHand[i] : null;
                var view = _slots[i];
                if (view == null) continue;

                if (def != null)
                {
                    ApplyArrowToSlot(view, def);
                    if (view.spentOverlay != null) view.spentOverlay.SetActive(false);

                    bool isSelected = (_selectedSlot == i);
                    if (view.outlineBorder != null)
                    {
                        view.outlineBorder.color = isSelected
                            ? new Color(1f, 0.88f, 0.25f, 0.95f)
                            : new Color(1f, 1f, 1f, 0.15f);
                    }

                    if (view.slotRect != null)
                    {
                        view.slotRect.localScale = isSelected ? Vector3.one * 1.08f : Vector3.one;
                    }

                    if (view.background != null)
                    {
                        view.background.color = isSelected
                            ? new Color(0.14f, 0.16f, 0.24f, 0.96f)
                            : new Color(0.08f, 0.09f, 0.13f, 0.92f);
                    }
                }
                else
                {
                    // Ranura gastada
                    if (view.spentOverlay != null) view.spentOverlay.SetActive(true);
                    if (view.outlineBorder != null) view.outlineBorder.color = new Color(0.3f, 0.3f, 0.3f, 0.1f);
                    if (view.slotRect != null) view.slotRect.localScale = Vector3.one * 0.95f;
                }
            }
        }
    }
}
