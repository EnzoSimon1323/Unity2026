using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JuegoSinNombre.Core;
using JuegoSinNombre.Gameplay;

namespace JuegoSinNombre.UI
{
    /// <summary>
    /// Elementos visuales de cada uno de los 3 cuadrados de la mano táctica.
    /// Se pueden asignar directamente arrastrando los objetos desde la jerarquía en el Inspector.
    /// </summary>
    [System.Serializable]
    public class SlotUIElements
    {
        [Tooltip("RectTransform del cuadrado contenedor de este slot.")]
        public RectTransform slotRect;

        [Tooltip("Imagen de fondo del cuadro.")]
        public Image background;

        [Tooltip("Borde o halo que se ilumina cuando esta ranura está seleccionada.")]
        public Image outlineBorder;

        [Tooltip("Contenedor interno del contenido que tiembla/vibra durante la ruleta.")]
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
    }

    /// <summary>
    /// Widget HUD de Mano Táctica estilo Tragamoneda (Slot Machine):
    /// - Soporta asignación manual directa desde el Inspector de Unity (para mover, diseñar y escalar a gusto).
    /// - Si no se asignan referencias, genera un fallback procedural automáticamente.
    /// </summary>
    public class ArrowSlotMachineUI : MonoBehaviour
    {
        [Header("Referencias de UI (Asigná tus objetos acá)")]
        [Tooltip("El contenedor padre de los 3 slots (el que se mueve del centro a la esquina).")]
        [SerializeField] private RectTransform _manualRootContainer;

        [Tooltip("Opcional: Cartel 'ARSENAL TÁCTICO' que se muestra en el centro.")]
        [SerializeField] private CanvasGroup _manualBannerGroup;

        [Tooltip("Las 3 ranuras de la fila (Slot 0 = Tecla 1, Slot 1 = Tecla 2, Slot 2 = Tecla 3).")]
        [SerializeField] private SlotUIElements[] _manualSlots = new SlotUIElements[3];

        [Header("Posiciones y Anchors")]
        [Tooltip("Posición del contenedor en el centro de la pantalla.")]
        [SerializeField] private Vector2 _centerAnchoredPosition = Vector2.zero;

        [Tooltip("Escala del contenedor mientras está en el centro de la pantalla.")]
        [SerializeField] private float _centerScale = 1.25f;

        [Tooltip("Posición final del contenedor en la esquina inferior derecha.")]
        [SerializeField] private Vector2 _bottomRightOffset = new Vector2(-25f, 25f);

        [Tooltip("Escala final del contenedor en la esquina inferior derecha.")]
        [SerializeField] private float _hudScale = 0.95f;

        [Header("Tiempos de Animación (segundos)")]
        [SerializeField] private float _spinDurationSlot0 = 1.0f;
        [SerializeField] private float _spinDurationSlot1 = 1.65f;
        [SerializeField] private float _spinDurationSlot2 = 2.3f;
        [SerializeField] private float _centerHoldDuration = 0.5f;
        [SerializeField] private float _transitionDuration = 0.75f;

        [Header("Fallback Procedural (solo si no asignás referencias manuales)")]
        [SerializeField] private Vector2 _fallbackSlotSize = new Vector2(105f, 115f);
        [SerializeField] private float _fallbackSlotSpacing = 16f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (FindAnyObjectByType<ArrowSlotMachineUI>() == null)
            {
                var go = new GameObject("[ArrowSlotMachineUI]");
                go.AddComponent<ArrowSlotMachineUI>();
                DontDestroyOnLoad(go);
            }
        }

        private Canvas _targetCanvas;
        private RectTransform _rootContainer;
        private CanvasGroup _bannerGroup;
        private readonly SlotUIElements[] _slots = new SlotUIElements[3];

        private PlayerShooter _shooter;
        private Coroutine _slotMachineRoutine;
        private bool _isSpinning = false;
        private ArrowDefinition[] _allPossibleArrows;
        private ArrowDefinition[] _currentHand;
        private int _selectedSlot = 0;

        private static Sprite _whiteSprite;
        private static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite == null)
                {
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                    tex.Apply();
                    _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
                }
                return _whiteSprite;
            }
        }

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
            UnbindShooter();
        }

        private void Start()
        {
            EnsureUIHierarchy();
        }

        private void HandleLocalPlayerSpawned(PlayerIdentity player)
        {
            if (player == null || !player.isOwner) return;

            UnbindShooter();

            _shooter = player.GetComponent<PlayerShooter>();
            if (_shooter != null)
            {
                _shooter.OnHandDealt += HandleHandDealt;
                _shooter.OnHandChanged += HandleHandChanged;

                if (_shooter.CurrentHand != null && (_shooter.CurrentHand[0] != null || _shooter.CurrentHand[1] != null || _shooter.CurrentHand[2] != null))
                {
                    HandleHandDealt(_shooter.CurrentHand);
                }
            }
        }

        private void HandleLocalPlayerDespawned(PlayerIdentity player = null)
        {
            UnbindShooter();
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
            EnsureUIHierarchy();

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

        // ================= VINCULACIÓN MANUAL O GENERACIÓN PROCEDURAL =================

        private void EnsureUIHierarchy()
        {
            if (_rootContainer != null) return;

            // Verificar si el usuario ya asignó las referencias manualmente en el Inspector
            if (_manualRootContainer != null && _manualSlots != null && _manualSlots.Length == 3 && _manualSlots[0] != null && _manualSlots[0].slotRect != null)
            {
                _rootContainer = _manualRootContainer;
                _bannerGroup = _manualBannerGroup;
                for (int i = 0; i < 3; i++)
                {
                    _slots[i] = _manualSlots[i];
                }
                _targetCanvas = _rootContainer.GetComponentInParent<Canvas>();
                LoadPossibleArrows();
                SetContainerToCenter();
                return;
            }

            // Fallback: Generación procedural
            _targetCanvas = FindAnyObjectByType<Canvas>();
            if (_targetCanvas == null)
            {
                var canvasGo = new GameObject("TacticalQuiverCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                _targetCanvas = canvasGo.GetComponent<Canvas>();
                _targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _targetCanvas.sortingOrder = 50;

                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            var rootGo = new GameObject("TacticalQuiverRoot", typeof(RectTransform));
            rootGo.transform.SetParent(_targetCanvas.transform, false);
            _rootContainer = rootGo.GetComponent<RectTransform>();

            float totalWidth = (_fallbackSlotSize.x * 3f) + (_fallbackSlotSpacing * 2f);
            _rootContainer.sizeDelta = new Vector2(totalWidth, _fallbackSlotSize.y + 40f);
            SetContainerToCenter();

            var bannerGo = new GameObject("Banner", typeof(RectTransform), typeof(CanvasGroup));
            bannerGo.transform.SetParent(_rootContainer, false);
            _bannerGroup = bannerGo.GetComponent<CanvasGroup>();
            var bannerRect = bannerGo.GetComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.5f, 1f);
            bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 0f);
            bannerRect.anchoredPosition = new Vector2(0f, 6f);
            bannerRect.sizeDelta = new Vector2(totalWidth, 32f);

            var bannerText = CreateTextMesh(bannerGo.transform, "ARSENAL TÁCTICO", 18, Color.yellow, FontStyles.Bold);
            bannerText.alignment = TextAlignmentOptions.Center;
            bannerText.rectTransform.anchorMin = Vector2.zero;
            bannerText.rectTransform.anchorMax = Vector2.one;
            bannerText.rectTransform.sizeDelta = Vector2.zero;

            for (int i = 0; i < 3; i++)
            {
                _slots[i] = CreateProceduralSlotView(i, totalWidth);
            }

            LoadPossibleArrows();
        }

        private SlotUIElements CreateProceduralSlotView(int index, float totalWidth)
        {
            var view = new SlotUIElements();

            var slotGo = new GameObject($"Slot_{index}", typeof(RectTransform));
            slotGo.transform.SetParent(_rootContainer, false);
            view.slotRect = slotGo.GetComponent<RectTransform>();
            view.slotRect.sizeDelta = _fallbackSlotSize;

            float startX = -totalWidth * 0.5f + (_fallbackSlotSize.x * 0.5f);
            float posX = startX + index * (_fallbackSlotSize.x + _fallbackSlotSpacing);
            view.slotRect.anchoredPosition = new Vector2(posX, 0f);

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(slotGo.transform, false);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            view.background = bgGo.GetComponent<Image>();
            view.background.sprite = WhiteSprite;
            view.background.color = new Color(0.08f, 0.09f, 0.13f, 0.92f);

            var borderGo = new GameObject("OutlineBorder", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(slotGo.transform, false);
            var borderRect = borderGo.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = new Vector2(6f, 6f);
            view.outlineBorder = borderGo.GetComponent<Image>();
            view.outlineBorder.sprite = WhiteSprite;
            view.outlineBorder.color = new Color(1f, 0.85f, 0.2f, 0f);

            var reelGo = new GameObject("ReelContent", typeof(RectTransform));
            reelGo.transform.SetParent(slotGo.transform, false);
            view.reelContent = reelGo.GetComponent<RectTransform>();
            view.reelContent.anchorMin = Vector2.zero;
            view.reelContent.anchorMax = Vector2.one;
            view.reelContent.sizeDelta = Vector2.zero;

            var iconGo = new GameObject("ArrowIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(reelGo.transform, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.62f);
            iconRect.anchorMax = new Vector2(0.5f, 0.62f);
            iconRect.sizeDelta = new Vector2(50f, 50f);
            view.arrowIcon = iconGo.GetComponent<Image>();
            view.arrowIcon.sprite = WhiteSprite;
            view.arrowIcon.color = Color.white;

            view.nameText = CreateTextMesh(reelGo.transform, "FLECHA", 14, Color.white, FontStyles.Bold);
            view.nameText.alignment = TextAlignmentOptions.Center;
            view.nameText.rectTransform.anchorMin = new Vector2(0.05f, 0.22f);
            view.nameText.rectTransform.anchorMax = new Vector2(0.95f, 0.38f);
            view.nameText.rectTransform.sizeDelta = Vector2.zero;

            view.traitText = CreateTextMesh(reelGo.transform, "Especial", 11, new Color(0.75f, 0.85f, 1f, 0.9f), FontStyles.Normal);
            view.traitText.alignment = TextAlignmentOptions.Center;
            view.traitText.rectTransform.anchorMin = new Vector2(0.05f, 0.05f);
            view.traitText.rectTransform.anchorMax = new Vector2(0.95f, 0.20f);
            view.traitText.rectTransform.sizeDelta = Vector2.zero;

            view.keyBadge = CreateTextMesh(slotGo.transform, $"[{index + 1}]", 13, new Color(1f, 0.95f, 0.4f), FontStyles.Bold);
            view.keyBadge.alignment = TextAlignmentOptions.TopLeft;
            view.keyBadge.rectTransform.anchorMin = new Vector2(0f, 1f);
            view.keyBadge.rectTransform.anchorMax = new Vector2(0f, 1f);
            view.keyBadge.rectTransform.pivot = new Vector2(0f, 1f);
            view.keyBadge.rectTransform.anchoredPosition = new Vector2(6f, -4f);
            view.keyBadge.rectTransform.sizeDelta = new Vector2(30f, 20f);

            var spentGo = new GameObject("SpentOverlay", typeof(RectTransform), typeof(Image));
            spentGo.transform.SetParent(slotGo.transform, false);
            var spentRect = spentGo.GetComponent<RectTransform>();
            spentRect.anchorMin = Vector2.zero;
            spentRect.anchorMax = Vector2.one;
            spentRect.sizeDelta = Vector2.zero;
            var spentImg = spentGo.GetComponent<Image>();
            spentImg.sprite = WhiteSprite;
            spentImg.color = new Color(0.05f, 0.05f, 0.05f, 0.78f);

            var spentTxt = CreateTextMesh(spentGo.transform, "DISPARADA", 13, new Color(1f, 0.35f, 0.35f), FontStyles.Bold);
            spentTxt.alignment = TextAlignmentOptions.Center;
            spentTxt.rectTransform.anchorMin = Vector2.zero;
            spentTxt.rectTransform.anchorMax = Vector2.one;
            spentTxt.rectTransform.sizeDelta = Vector2.zero;

            view.spentOverlay = spentGo;
            view.spentOverlay.SetActive(false);

            return view;
        }

        private TextMeshProUGUI CreateTextMesh(Transform parent, string text, float size, Color color, FontStyles style)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            return tmp;
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

        private void SetContainerToCenter()
        {
            if (_rootContainer == null) return;
            _rootContainer.anchorMin = new Vector2(0.5f, 0.5f);
            _rootContainer.anchorMax = new Vector2(0.5f, 0.5f);
            _rootContainer.pivot = new Vector2(0.5f, 0.5f);
            _rootContainer.anchoredPosition = _centerAnchoredPosition;
            _rootContainer.localScale = Vector3.one * _centerScale;
            if (_bannerGroup != null) _bannerGroup.alpha = 1f;
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

                    if (!slot0Stopped)
                        ApplyRandomArrowToSlot(_slots[0]);

                    if (!slot1Stopped)
                        ApplyRandomArrowToSlot(_slots[1]);

                    if (!slot2Stopped)
                        ApplyRandomArrowToSlot(_slots[2]);
                }

                // Jitter vertical estilo ruleta mecánica
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

            // Pausa dramática en el centro
            yield return new WaitForSeconds(_centerHoldDuration);

            // Desvanecer banner si existe
            if (_bannerGroup != null)
            {
                float fadeTime = 0.25f;
                float fadeElapsed = 0f;
                while (fadeElapsed < fadeTime)
                {
                    fadeElapsed += Time.deltaTime;
                    _bannerGroup.alpha = Mathf.Lerp(1f, 0f, fadeElapsed / fadeTime);
                    yield return null;
                }
            }

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
            if (_rootContainer == null) yield break;

            Vector3 startScale = _rootContainer.localScale;

            _rootContainer.anchorMin = new Vector2(1f, 0f);
            _rootContainer.anchorMax = new Vector2(1f, 0f);
            _rootContainer.pivot = new Vector2(1f, 0f);

            float canvasWidth = _targetCanvas != null ? _targetCanvas.GetComponent<RectTransform>().rect.width : 1920f;
            float canvasHeight = _targetCanvas != null ? _targetCanvas.GetComponent<RectTransform>().rect.height : 1080f;
            Vector2 centerPosInBrAnchors = new Vector2(-canvasWidth * 0.5f + (_rootContainer.sizeDelta.x * 0.5f), canvasHeight * 0.5f - (_rootContainer.sizeDelta.y * 0.5f));

            _rootContainer.anchoredPosition = centerPosInBrAnchors;

            Vector2 targetPos = _bottomRightOffset;
            Vector3 targetScale = Vector3.one * _hudScale;

            float elapsed = 0f;
            while (elapsed < _transitionDuration)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / _transitionDuration);
                float ease = p < 0.5f ? 4f * p * p * p : 1f - Mathf.Pow(-2f * p + 2f, 3f) / 2f;

                _rootContainer.anchoredPosition = Vector2.Lerp(centerPosInBrAnchors, targetPos, ease);
                _rootContainer.localScale = Vector3.Lerp(startScale, targetScale, ease);
                yield return null;
            }

            _rootContainer.anchoredPosition = targetPos;
            _rootContainer.localScale = targetScale;
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
                view.arrowIcon.color = themeColor;
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
