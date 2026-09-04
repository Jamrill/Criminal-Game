using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using JuegoCriminal.Services;
using JuegoCriminal.Core;
using JuegoCriminal.Printing;
using JuegoCriminal.Inventory;

namespace JuegoCriminal.UI
{
    public sealed class PauseMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private int pauseCanvasSortingOrder = 100;

        [Header("Shared menu prefabs")]
        [SerializeField] private GameObject optionsPanelPrefab;
        [SerializeField] private GameObject slotsPanelPrefab;

        [Header("Overlay transition")]
        [SerializeField, Min(0.01f)] private float transitionDuration = 0.28f;
        [SerializeField, Min(0f)] private float buttonStagger = 0.055f;
        [SerializeField] private float buttonStartOffsetY = 90f;

        private SaveService _save;
        private SceneLoader _loader;
        private WorldSaveService _worldSave;
        private GameObject _optionsPanelInstance;
        private GameObject _slotsPanelInstance;
        private ControlsMenuUI _controlsMenu;
        private SlotsPanelUI _slotsPanel;
        private Coroutine _transition;
        private bool _overlayOpen;

        private RectTransform _slotsScrollView;
        private RectTransform _slotsBackButton;
        private RectTransform _slotsLoadButton;
        private RectTransform _slotsDeleteButton;
        private CanvasGroup _slotsScrollGroup;
        private CanvasGroup _slotsBackGroup;
        private CanvasGroup _slotsLoadGroup;
        private CanvasGroup _slotsDeleteGroup;
        private Vector2 _slotsScrollFinalPosition;
        private Vector2 _slotsBackFinalPosition;
        private Vector2 _slotsLoadFinalPosition;
        private Vector2 _slotsDeleteFinalPosition;

        private void Awake()
        {
            Bootstrapper app = Bootstrapper.Instance;
            _save = app != null ? app.SaveService : null;
            _loader = app != null ? app.SceneLoader : null;
            _worldSave = app != null ? app.WorldSaveService : null;

            if (_save == null) _save = FindAnyObjectByType<SaveService>();
            if (_loader == null) _loader = FindAnyObjectByType<SceneLoader>();
            if (_worldSave == null) _worldSave = FindAnyObjectByType<WorldSaveService>();

            if (panel != null) panel.SetActive(false);

            Canvas pauseCanvas = panel != null ? panel.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
            if (pauseCanvas != null)
            {
                pauseCanvas.overrideSorting = true;
                pauseCanvas.sortingOrder = pauseCanvasSortingOrder;
            }

            if (saveButton != null) saveButton.onClick.AddListener(SaveGame);
            if (loadButton != null) loadButton.onClick.AddListener(LoadGame);
            if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
            if (quitButton != null) quitButton.onClick.AddListener(QuitToMenu);
        }

        private void OnDestroy()
        {
            if (saveButton != null) saveButton.onClick.RemoveListener(SaveGame);
            if (loadButton != null) loadButton.onClick.RemoveListener(LoadGame);
            if (optionsButton != null) optionsButton.onClick.RemoveListener(OpenOptions);
            if (quitButton != null) quitButton.onClick.RemoveListener(QuitToMenu);

            if (_slotsPanel != null) _slotsPanel.CloseRequested -= CloseOverlay;
        }

        private void Update()
        {
            if (GameInput.PausePressed)
            {
                Printer3DController activePrinter = Printer3DController.ActiveSelection;
                if (activePrinter != null && activePrinter.IsSelecting)
                {
                    GameInput.ConsumePausePress();
                    activePrinter.CloseProjectSelection();
                    return;
                }

                if (_overlayOpen)
                    CloseOverlay();
                else
                    Toggle();
            }
        }

        private void Toggle()
        {
            if (panel == null) return;

            bool show = !panel.activeSelf;
            panel.SetActive(show);

            Time.timeScale = show ? 0f : 1f;

            InventoryMenuUI inventoryMenu = FindAnyObjectByType<InventoryMenuUI>();
            if (inventoryMenu != null && inventoryMenu.IsOpen)
                inventoryMenu.SetInteractionBlocked(show);
            bool inventoryRemainsOpen = !show && inventoryMenu != null && inventoryMenu.IsOpen;
            Cursor.lockState = show || inventoryRemainsOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = show || inventoryRemainsOpen;
        }

        private void SaveGame()
        {
            if (_save == null || !_save.HasCurrentGame)
            {
                Debug.LogWarning("[PauseMenu] SaveService or SaveData missing");
                return;
            }

            if (_worldSave == null || !_worldSave.CaptureCurrentWorld(_save))
            {
                Debug.LogWarning("[PauseMenu] World state could not be captured.");
                return;
            }
            if (!_save.SaveCurrentSlot())
            {
                Debug.LogWarning("[PauseMenu] Game could not be saved.");
                return;
            }

            Debug.Log("[PauseMenu] Game saved.");
        }

        private void LoadGame()
        {
            if (!EnsureSlotsPanel()) return;

            _slotsPanel.Open(SlotPanelMode.LoadOnly);
            OpenSlotsOverlay();
        }

        private void OpenOptions()
        {
            if (!EnsureOptionsPanel()) return;

            if (_controlsMenu != null)
                _controlsMenu.Close();

            OpenOverlay(_optionsPanelInstance);
        }

        private bool EnsureOptionsPanel()
        {
            if (_optionsPanelInstance != null) return true;
            if (optionsPanelPrefab == null)
            {
                Debug.LogError("[PauseMenu] OptionsPanel prefab is not assigned.");
                return false;
            }

            _optionsPanelInstance = Instantiate(optionsPanelPrefab, transform, false);
            _optionsPanelInstance.name = optionsPanelPrefab.name;
            _controlsMenu = _optionsPanelInstance.GetComponentInChildren<ControlsMenuUI>(true);

            Button back = FindButton(_optionsPanelInstance, "Back");
            if (back != null)
                back.onClick.AddListener(CloseOverlay);

            _optionsPanelInstance.SetActive(false);
            return true;
        }

        private bool EnsureSlotsPanel()
        {
            if (_slotsPanelInstance != null) return _slotsPanel != null;
            if (slotsPanelPrefab == null)
            {
                Debug.LogError("[PauseMenu] SlotsPanel prefab is not assigned.");
                return false;
            }

            _slotsPanelInstance = Instantiate(slotsPanelPrefab, transform, false);
            _slotsPanelInstance.name = slotsPanelPrefab.name;
            _slotsPanel = _slotsPanelInstance.GetComponentInChildren<SlotsPanelUI>(true);
            if (_slotsPanel == null)
            {
                Debug.LogError("[PauseMenu] The SlotsPanel prefab has no SlotsPanelUI component.");
                Destroy(_slotsPanelInstance);
                _slotsPanelInstance = null;
                return false;
            }

            _slotsPanel.CloseRequested += CloseOverlay;
            CacheSlotsTransitionParts();
            _slotsPanelInstance.SetActive(false);
            return true;
        }

        private void CacheSlotsTransitionParts()
        {
            _slotsScrollView = FindRectTransform(_slotsPanelInstance, "Scroll View");
            _slotsBackButton = FindRectTransform(_slotsPanelInstance, "Button_Back");
            _slotsLoadButton = FindRectTransform(_slotsPanelInstance, "Button_Load");
            _slotsDeleteButton = FindRectTransform(_slotsPanelInstance, "Button_Delete");

            _slotsScrollGroup = GetOrAddCanvasGroup(_slotsScrollView);
            _slotsBackGroup = GetOrAddCanvasGroup(_slotsBackButton);
            _slotsLoadGroup = GetOrAddCanvasGroup(_slotsLoadButton);
            _slotsDeleteGroup = GetOrAddCanvasGroup(_slotsDeleteButton);

            if (_slotsScrollView != null) _slotsScrollFinalPosition = _slotsScrollView.anchoredPosition;
            if (_slotsBackButton != null) _slotsBackFinalPosition = _slotsBackButton.anchoredPosition;
            if (_slotsLoadButton != null) _slotsLoadFinalPosition = _slotsLoadButton.anchoredPosition;
            if (_slotsDeleteButton != null) _slotsDeleteFinalPosition = _slotsDeleteButton.anchoredPosition;
        }

        private void OpenSlotsOverlay()
        {
            if (_transition != null) StopCoroutine(_transition);
            if (panel != null) panel.SetActive(false);
            _slotsPanelInstance.SetActive(true);
            _overlayOpen = true;

            PrepareSlotsTransition(opening: true);
            _transition = StartCoroutine(AnimateSlotsPanel(opening: true));
        }

        private void OpenOverlay(GameObject overlay)
        {
            if (overlay == null) return;

            if (_transition != null) StopCoroutine(_transition);
            if (panel != null) panel.SetActive(false);
            overlay.SetActive(true);
            _overlayOpen = true;
            _transition = StartCoroutine(AnimateOverlay(overlay, true));
        }

        private void CloseOverlay()
        {
            if (!_overlayOpen) return;

            GameObject activeOverlay = null;
            if (_optionsPanelInstance != null && _optionsPanelInstance.activeSelf)
                activeOverlay = _optionsPanelInstance;
            else if (_slotsPanelInstance != null && _slotsPanelInstance.activeSelf)
                activeOverlay = _slotsPanelInstance;

            if (_transition != null) StopCoroutine(_transition);
            bool closingSlots = activeOverlay == _slotsPanelInstance;
            _transition = StartCoroutine(CloseOverlayRoutine(activeOverlay, closingSlots));
        }

        private IEnumerator CloseOverlayRoutine(GameObject overlay, bool closingSlots)
        {
            if (overlay != null)
            {
                if (closingSlots)
                {
                    PrepareSlotsTransition(opening: false);
                    yield return AnimateSlotsPanel(opening: false);
                }
                else
                {
                    yield return AnimateOverlay(overlay, false);
                }
            }

            if (_controlsMenu != null) _controlsMenu.Close();
            if (_optionsPanelInstance != null) _optionsPanelInstance.SetActive(false);
            if (_slotsPanelInstance != null) _slotsPanelInstance.SetActive(false);
            if (panel != null) panel.SetActive(true);

            _overlayOpen = false;
            _transition = null;
        }

        private void PrepareSlotsTransition(bool opening)
        {
            const float offscreenX = -1200f;
            const float offscreenY = -600f;

            if (_slotsScrollView != null)
                _slotsScrollView.anchoredPosition = opening
                    ? new Vector2(offscreenX, _slotsScrollFinalPosition.y)
                    : _slotsScrollFinalPosition;
            if (_slotsBackButton != null)
                _slotsBackButton.anchoredPosition = opening
                    ? new Vector2(_slotsBackFinalPosition.x, offscreenY)
                    : _slotsBackFinalPosition;
            if (_slotsLoadButton != null)
                _slotsLoadButton.anchoredPosition = opening
                    ? new Vector2(_slotsLoadFinalPosition.x, offscreenY)
                    : _slotsLoadFinalPosition;
            if (_slotsDeleteButton != null)
                _slotsDeleteButton.anchoredPosition = opening
                    ? new Vector2(_slotsDeleteFinalPosition.x, offscreenY)
                    : _slotsDeleteFinalPosition;

            SetCanvasGroup(_slotsScrollGroup, opening ? 0f : 1f, false);
            SetCanvasGroup(_slotsBackGroup, opening ? 0f : 1f, false);
            SetCanvasGroup(_slotsLoadGroup, opening ? 0f : 1f, false);
            SetCanvasGroup(_slotsDeleteGroup, opening ? 0f : 1f, false);
        }

        private IEnumerator AnimateSlotsPanel(bool opening)
        {
            const float offscreenX = -1200f;
            const float offscreenY = -600f;
            const float fadeInStart = 0.55f;
            const float fadeOutStart = 0.15f;
            const float duration = 0.22f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Smooth01(elapsed / duration);
                float fade = opening
                    ? DelayedFade01(t, fadeInStart)
                    : 1f - DelayedFade01(t, fadeOutStart);

                if (_slotsScrollView != null)
                    _slotsScrollView.anchoredPosition = Vector2.Lerp(
                        opening ? new Vector2(offscreenX, _slotsScrollFinalPosition.y) : _slotsScrollFinalPosition,
                        opening ? _slotsScrollFinalPosition : new Vector2(offscreenX, _slotsScrollFinalPosition.y), t);
                if (_slotsBackButton != null)
                    _slotsBackButton.anchoredPosition = Vector2.Lerp(
                        opening ? new Vector2(_slotsBackFinalPosition.x, offscreenY) : _slotsBackFinalPosition,
                        opening ? _slotsBackFinalPosition : new Vector2(_slotsBackFinalPosition.x, offscreenY), t);
                if (_slotsLoadButton != null)
                    _slotsLoadButton.anchoredPosition = Vector2.Lerp(
                        opening ? new Vector2(_slotsLoadFinalPosition.x, offscreenY) : _slotsLoadFinalPosition,
                        opening ? _slotsLoadFinalPosition : new Vector2(_slotsLoadFinalPosition.x, offscreenY), t);
                if (_slotsDeleteButton != null)
                    _slotsDeleteButton.anchoredPosition = Vector2.Lerp(
                        opening ? new Vector2(_slotsDeleteFinalPosition.x, offscreenY) : _slotsDeleteFinalPosition,
                        opening ? _slotsDeleteFinalPosition : new Vector2(_slotsDeleteFinalPosition.x, offscreenY), t);

                SetAlpha(_slotsScrollGroup, fade);
                SetAlpha(_slotsBackGroup, fade);
                SetAlpha(_slotsLoadGroup, fade);
                SetAlpha(_slotsDeleteGroup, fade);
                yield return null;
            }

            if (_slotsScrollView != null) _slotsScrollView.anchoredPosition = _slotsScrollFinalPosition;
            if (_slotsBackButton != null) _slotsBackButton.anchoredPosition = _slotsBackFinalPosition;
            if (_slotsLoadButton != null) _slotsLoadButton.anchoredPosition = _slotsLoadFinalPosition;
            if (_slotsDeleteButton != null) _slotsDeleteButton.anchoredPosition = _slotsDeleteFinalPosition;

            SetCanvasGroup(_slotsScrollGroup, opening ? 1f : 0f, opening);
            SetCanvasGroup(_slotsBackGroup, opening ? 1f : 0f, opening);
            SetCanvasGroup(_slotsLoadGroup, opening ? 1f : 0f, opening);
            SetCanvasGroup(_slotsDeleteGroup, opening ? 1f : 0f, opening);
            if (opening) _transition = null;
        }

        private IEnumerator AnimateOverlay(GameObject overlay, bool opening)
        {
            CanvasGroup rootGroup = overlay.GetComponent<CanvasGroup>();
            if (rootGroup == null) rootGroup = overlay.AddComponent<CanvasGroup>();

            Button[] allButtons = overlay.GetComponentsInChildren<Button>(true);
            var groups = new List<CanvasGroup>();
            var basePositions = new List<Vector3>();

            for (int i = 0; i < allButtons.Length; i++)
            {
                if (!allButtons[i].gameObject.activeInHierarchy) continue;
                if (allButtons[i].GetComponentInParent<ControlsMenuUI>() != null) continue;

                CanvasGroup group = allButtons[i].GetComponent<CanvasGroup>();
                if (group == null) group = allButtons[i].gameObject.AddComponent<CanvasGroup>();
                groups.Add(group);
                basePositions.Add(group.transform.localPosition);
            }

            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
            rootGroup.alpha = opening ? 0f : 1f;

            for (int i = 0; i < groups.Count; i++)
            {
                groups[i].alpha = opening ? 0f : 1f;
                groups[i].transform.localPosition = basePositions[i] +
                    Vector3.up * (opening ? buttonStartOffsetY : 0f);
            }

            float total = transitionDuration + buttonStagger * Mathf.Max(0, groups.Count - 1);
            float elapsed = 0f;
            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;
                float rootT = Smooth01(elapsed / transitionDuration);
                rootGroup.alpha = opening ? rootT : 1f - rootT;

                for (int i = 0; i < groups.Count; i++)
                {
                    float delay = opening ? i * buttonStagger : (groups.Count - 1 - i) * buttonStagger;
                    float t = Smooth01((elapsed - delay) / transitionDuration);
                    groups[i].alpha = opening ? t : 1f - t;
                    groups[i].transform.localPosition = basePositions[i] +
                        Vector3.up * (opening ? Mathf.Lerp(buttonStartOffsetY, 0f, t) : Mathf.Lerp(0f, buttonStartOffsetY, t));
                }

                yield return null;
            }

            for (int i = 0; i < groups.Count; i++)
            {
                groups[i].transform.localPosition = basePositions[i];
                groups[i].alpha = opening ? 1f : 0f;
            }

            rootGroup.alpha = opening ? 1f : 0f;
            rootGroup.interactable = opening;
            rootGroup.blocksRaycasts = opening;
            if (opening) _transition = null;
        }

        private static Button FindButton(GameObject root, string objectName)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i].name == objectName) return buttons[i];

            return null;
        }

        private static RectTransform FindRectTransform(GameObject root, string objectName)
        {
            RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
                if (rects[i].name == objectName) return rects[i];

            return null;
        }

        private static CanvasGroup GetOrAddCanvasGroup(RectTransform rect)
        {
            if (rect == null) return null;
            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            return group != null ? group : rect.gameObject.AddComponent<CanvasGroup>();
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null) group.alpha = alpha;
        }

        private static void SetCanvasGroup(CanvasGroup group, float alpha, bool interactable)
        {
            if (group == null) return;
            group.alpha = alpha;
            group.interactable = interactable;
            group.blocksRaycasts = interactable;
        }

        private static float DelayedFade01(float value, float fadeStart)
        {
            value = Mathf.Clamp01(value);
            if (value <= fadeStart) return 0f;
            return Smooth01((value - fadeStart) / (1f - fadeStart));
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private void QuitToMenu()
        {
            Time.timeScale = 1f;
            if (_loader != null)
                _loader.LoadScene("01_MainMenu");
        }
    }
}
