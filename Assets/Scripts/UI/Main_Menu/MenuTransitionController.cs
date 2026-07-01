using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using JuegoCriminal.Scenes;

namespace JuegoCriminal.UI
{
    public sealed class MenuTransitionController : MonoBehaviour
    {
        [Header("Main Menu Buttons (in order)")]
        [SerializeField] private CanvasGroup[] mainButtons;
        [SerializeField] private float buttonHideStagger = 0.05f;
        [SerializeField] private float buttonHideDuration = 0.12f;
        [SerializeField] private float buttonSlideX = 40f;

        [Header("Slots Panel")]
        [SerializeField] private GameObject slotsPanelRoot;
        [SerializeField] private SlotsPanelUI slotsPanelUI;

        [Header("Slots Panel - ScrollView enters from left")]
        [SerializeField] private RectTransform slotsScrollView;
        [SerializeField] private float scrollEnterDuration = 0.22f;
        [SerializeField] private float scrollOffscreenX = -1200f;

        [Header("Slots Panel - Buttons enter from bottom")]
        [SerializeField] private RectTransform backButton;
        [SerializeField] private RectTransform loadButton;
        [SerializeField] private RectTransform deleteButton;
        [SerializeField] private float buttonsOffscreenY = -600f;

        [Header("Slots Panel - Fade")]
        [SerializeField] private CanvasGroup slotsScrollGroup;
        [SerializeField] private CanvasGroup backButtonGroup;
        [SerializeField] private CanvasGroup loadButtonGroup;
        [SerializeField] private CanvasGroup deleteButtonGroup;
        [SerializeField, Range(0f, 0.95f)] private float slotsFadeInStartAt = 0.55f;
        [SerializeField, Range(0f, 0.95f)] private float slotsFadeOutStartAt = 0.15f;

        [Header("New Game Panel")]
        [SerializeField] private NewGameDialogUI newGameDialog;
        [SerializeField] private RectTransform newGamePanelRect;
        [SerializeField] private CanvasGroup newGamePanelGroup;

        [Tooltip("Posición Y exacta desde la que entra y hacia la que sale el NewGamePanel.")]
        [SerializeField] private float newGameOffscreenY = 900f;

        [SerializeField] private float newGameEnterDuration = 0.28f;
        [SerializeField, Range(0f, 0.95f)] private float newGameFadeStartAt = 0.55f;
        [SerializeField, Range(0f, 0.95f)] private float newGameFadeOutStartAt = 0.15f;

        private Vector2 _scrollFinalPos;
        private Vector2 _backFinalPos;
        private Vector2 _loadFinalPos;
        private Vector2 _deleteFinalPos;
        private Vector2 _newGameFinalPos;

        private Vector3[] _mainButtonsBaseLocalPos;

        private bool _isTransitioning;

        private void Awake()
        {
            CachePositions();
            CacheCanvasGroups();
            CacheMainButtonPositions();

            if (slotsPanelRoot != null)
                slotsPanelRoot.SetActive(false);

            if (newGameDialog != null)
            {
                newGameDialog.OnBackRequested -= TransitionBackFromNewGame;
                newGameDialog.OnBackRequested += TransitionBackFromNewGame;
            }

            if (newGamePanelRect != null)
            {
                SetCanvasGroup(newGamePanelGroup, 0f, false);
                newGamePanelRect.anchoredPosition = _newGameFinalPos;
                newGamePanelRect.gameObject.SetActive(false);
            }
            else if (newGameDialog != null)
            {
                newGameDialog.HidePanelOnly();
            }

            SetCanvasGroup(slotsScrollGroup, 0f, false);
            SetCanvasGroup(backButtonGroup, 0f, false);
            SetCanvasGroup(loadButtonGroup, 0f, false);
            SetCanvasGroup(deleteButtonGroup, 0f, false);
        }

        private void OnDestroy()
        {
            if (newGameDialog != null)
                newGameDialog.OnBackRequested -= TransitionBackFromNewGame;
        }

        private void CachePositions()
        {
            if (slotsScrollView != null)
                _scrollFinalPos = slotsScrollView.anchoredPosition;

            if (backButton != null)
                _backFinalPos = backButton.anchoredPosition;

            if (loadButton != null)
                _loadFinalPos = loadButton.anchoredPosition;

            if (deleteButton != null)
                _deleteFinalPos = deleteButton.anchoredPosition;

            if (newGamePanelRect == null && newGameDialog != null)
                newGamePanelRect = newGameDialog.PanelRect;

            if (newGamePanelRect != null)
                _newGameFinalPos = newGamePanelRect.anchoredPosition;
        }

        private void CacheCanvasGroups()
        {
            if (slotsScrollGroup == null && slotsScrollView != null)
                slotsScrollGroup = slotsScrollView.GetComponent<CanvasGroup>();

            if (backButtonGroup == null && backButton != null)
                backButtonGroup = backButton.GetComponent<CanvasGroup>();

            if (loadButtonGroup == null && loadButton != null)
                loadButtonGroup = loadButton.GetComponent<CanvasGroup>();

            if (deleteButtonGroup == null && deleteButton != null)
                deleteButtonGroup = deleteButton.GetComponent<CanvasGroup>();

            if (newGamePanelGroup == null && newGamePanelRect != null)
                newGamePanelGroup = newGamePanelRect.GetComponent<CanvasGroup>();
        }

        private void CacheMainButtonPositions()
        {
            if (mainButtons == null)
                mainButtons = new CanvasGroup[0];

            _mainButtonsBaseLocalPos = new Vector3[mainButtons.Length];

            for (int i = 0; i < mainButtons.Length; i++)
            {
                if (mainButtons[i] != null)
                    _mainButtonsBaseLocalPos[i] = mainButtons[i].transform.localPosition;
            }
        }

        public void TransitionToLoadGame()
        {
            Debug.Log("[MenuTransition] TransitionToLoadGame called");

            if (_isTransitioning)
                return;

            StartCoroutine(LoadGameRoutine());
        }

        public void TransitionBackToMainMenu()
        {
            Debug.Log("[MenuTransition] BackToMainMenu called");

            if (_isTransitioning)
                return;

            StartCoroutine(BackToMainMenuRoutine());
        }

        public void TransitionToNewGame(bool coop)
        {
            Debug.Log("[MenuTransition] TransitionToNewGame called");

            if (_isTransitioning)
                return;

            StartCoroutine(NewGameRoutine(coop));
        }

        public void TransitionBackFromNewGame()
        {
            Debug.Log("[MenuTransition] TransitionBackFromNewGame called");

            if (_isTransitioning)
                return;

            StartCoroutine(BackFromNewGameRoutine());
        }

        private IEnumerator NewGameRoutine(bool coop)
        {
            _isTransitioning = true;

            for (int i = 0; i < mainButtons.Length; i++)
            {
                if (mainButtons[i] != null)
                    StartCoroutine(FadeAndSlideOut(mainButtons[i], buttonHideDuration, buttonSlideX));

                yield return WaitUnscaled(buttonHideStagger);
            }

            if (newGamePanelRect == null && newGameDialog != null)
                newGamePanelRect = newGameDialog.PanelRect;

            if (newGamePanelRect == null)
            {
                Debug.LogError("[MenuTransition] NewGamePanelRect is null. Cannot animate New Game panel.");
                _isTransitioning = false;
                yield break;
            }

            if (newGamePanelGroup == null)
                newGamePanelGroup = newGamePanelRect.GetComponent<CanvasGroup>();

            Vector2 startPos = new Vector2(_newGameFinalPos.x, newGameOffscreenY);
            Vector2 endPos = _newGameFinalPos;

            newGamePanelRect.anchoredPosition = startPos;
            SetCanvasGroup(newGamePanelGroup, 0f, false);

            newGamePanelRect.gameObject.SetActive(true);

            if (newGameDialog != null)
                newGameDialog.Open(coop);

            float t = 0f;

            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, newGameEnterDuration);
                float s = Smooth01(t);

                newGamePanelRect.anchoredPosition = Vector2.Lerp(startPos, endPos, s);

                if (newGamePanelGroup != null)
                    newGamePanelGroup.alpha = DelayedFade01(s, newGameFadeStartAt);

                yield return null;
            }

            newGamePanelRect.anchoredPosition = endPos;
            SetCanvasGroup(newGamePanelGroup, 1f, true);

            _isTransitioning = false;
        }

        private IEnumerator BackFromNewGameRoutine()
        {
            _isTransitioning = true;

            if (newGamePanelRect == null && newGameDialog != null)
                newGamePanelRect = newGameDialog.PanelRect;

            if (newGamePanelGroup == null && newGamePanelRect != null)
                newGamePanelGroup = newGamePanelRect.GetComponent<CanvasGroup>();

            if (newGamePanelRect != null)
            {
                Vector2 startPos = _newGameFinalPos;
                Vector2 endPos = new Vector2(_newGameFinalPos.x, newGameOffscreenY);

                newGamePanelRect.anchoredPosition = startPos;
                SetCanvasGroup(newGamePanelGroup, 1f, false);

                float t = 0f;

                while (t < 1f)
                {
                    t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, newGameEnterDuration);
                    float s = Smooth01(t);

                    newGamePanelRect.anchoredPosition = Vector2.Lerp(startPos, endPos, s);

                    if (newGamePanelGroup != null)
                        newGamePanelGroup.alpha = 1f - DelayedFade01(s, newGameFadeOutStartAt);

                    yield return null;
                }

                newGamePanelRect.anchoredPosition = _newGameFinalPos;
                SetCanvasGroup(newGamePanelGroup, 0f, false);
            }

            if (newGameDialog != null)
                newGameDialog.HidePanelOnly();

            MainMenuUI menu = FindAnyObjectByType<MainMenuUI>();
            if (menu != null)
                menu.ShowMainButtons();

            for (int i = 0; i < mainButtons.Length; i++)
            {
                if (mainButtons[i] != null)
                    StartCoroutine(FadeAndSlideIn(mainButtons[i], buttonHideDuration, buttonSlideX));

                yield return WaitUnscaled(buttonHideStagger);
            }

            _isTransitioning = false;
        }

        private IEnumerator LoadGameRoutine()
        {
            _isTransitioning = true;

            for (int i = 0; i < mainButtons.Length; i++)
            {
                if (mainButtons[i] != null)
                    StartCoroutine(FadeAndSlideOut(mainButtons[i], buttonHideDuration, buttonSlideX));

                yield return WaitUnscaled(buttonHideStagger);
            }

            if (slotsPanelRoot != null)
                slotsPanelRoot.SetActive(true);

            if (slotsPanelUI != null)
                slotsPanelUI.Open(SlotPanelMode.LoadOnly);

            if (slotsScrollView != null)
                slotsScrollView.anchoredPosition = new Vector2(scrollOffscreenX, _scrollFinalPos.y);

            if (backButton != null)
                backButton.anchoredPosition = new Vector2(_backFinalPos.x, buttonsOffscreenY);

            if (loadButton != null)
                loadButton.anchoredPosition = new Vector2(_loadFinalPos.x, buttonsOffscreenY);

            if (deleteButton != null)
                deleteButton.anchoredPosition = new Vector2(_deleteFinalPos.x, buttonsOffscreenY);

            SetCanvasGroup(slotsScrollGroup, 0f, false);
            SetCanvasGroup(backButtonGroup, 0f, false);
            SetCanvasGroup(loadButtonGroup, 0f, false);
            SetCanvasGroup(deleteButtonGroup, 0f, false);

            float t = 0f;

            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, scrollEnterDuration);
                float s = Smooth01(t);

                if (slotsScrollView != null)
                {
                    slotsScrollView.anchoredPosition = Vector2.Lerp(
                        new Vector2(scrollOffscreenX, _scrollFinalPos.y),
                        _scrollFinalPos,
                        s
                    );
                }

                if (backButton != null)
                {
                    backButton.anchoredPosition = Vector2.Lerp(
                        new Vector2(_backFinalPos.x, buttonsOffscreenY),
                        _backFinalPos,
                        s
                    );
                }

                if (loadButton != null)
                {
                    loadButton.anchoredPosition = Vector2.Lerp(
                        new Vector2(_loadFinalPos.x, buttonsOffscreenY),
                        _loadFinalPos,
                        s
                    );
                }

                if (deleteButton != null)
                {
                    deleteButton.anchoredPosition = Vector2.Lerp(
                        new Vector2(_deleteFinalPos.x, buttonsOffscreenY),
                        _deleteFinalPos,
                        s
                    );
                }

                float fade = DelayedFade01(s, slotsFadeInStartAt);

                SetAlpha(slotsScrollGroup, fade);
                SetAlpha(backButtonGroup, fade);
                SetAlpha(loadButtonGroup, fade);
                SetAlpha(deleteButtonGroup, fade);

                yield return null;
            }

            if (slotsScrollView != null)
                slotsScrollView.anchoredPosition = _scrollFinalPos;

            if (backButton != null)
                backButton.anchoredPosition = _backFinalPos;

            if (loadButton != null)
                loadButton.anchoredPosition = _loadFinalPos;

            if (deleteButton != null)
                deleteButton.anchoredPosition = _deleteFinalPos;

            SetCanvasGroup(slotsScrollGroup, 1f, true);
            SetCanvasGroup(backButtonGroup, 1f, true);
            SetCanvasGroup(loadButtonGroup, 1f, true);
            SetCanvasGroup(deleteButtonGroup, 1f, true);

            _isTransitioning = false;
        }

        private IEnumerator BackToMainMenuRoutine()
        {
            _isTransitioning = true;

            float t = 0f;

            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, scrollEnterDuration);
                float s = Smooth01(t);
                float fade = 1f - DelayedFade01(s, slotsFadeOutStartAt);

                if (slotsScrollView != null)
                {
                    slotsScrollView.anchoredPosition = Vector2.Lerp(
                        _scrollFinalPos,
                        new Vector2(scrollOffscreenX, _scrollFinalPos.y),
                        s
                    );
                }

                if (backButton != null)
                {
                    backButton.anchoredPosition = Vector2.Lerp(
                        _backFinalPos,
                        new Vector2(_backFinalPos.x, buttonsOffscreenY),
                        s
                    );
                }

                if (loadButton != null)
                {
                    loadButton.anchoredPosition = Vector2.Lerp(
                        _loadFinalPos,
                        new Vector2(_loadFinalPos.x, buttonsOffscreenY),
                        s
                    );
                }

                if (deleteButton != null)
                {
                    deleteButton.anchoredPosition = Vector2.Lerp(
                        _deleteFinalPos,
                        new Vector2(_deleteFinalPos.x, buttonsOffscreenY),
                        s
                    );
                }

                SetAlpha(slotsScrollGroup, fade);
                SetAlpha(backButtonGroup, fade);
                SetAlpha(loadButtonGroup, fade);
                SetAlpha(deleteButtonGroup, fade);

                yield return null;
            }

            SetCanvasGroup(slotsScrollGroup, 0f, false);
            SetCanvasGroup(backButtonGroup, 0f, false);
            SetCanvasGroup(loadButtonGroup, 0f, false);
            SetCanvasGroup(deleteButtonGroup, 0f, false);

            if (slotsPanelRoot != null)
                slotsPanelRoot.SetActive(false);

            MainMenuUI menu = FindAnyObjectByType<MainMenuUI>();
            if (menu != null)
                menu.ShowMainButtons();

            for (int i = 0; i < mainButtons.Length; i++)
            {
                if (mainButtons[i] != null)
                    StartCoroutine(FadeAndSlideIn(mainButtons[i], buttonHideDuration, buttonSlideX));

                yield return WaitUnscaled(buttonHideStagger);
            }

            _isTransitioning = false;
        }

        private IEnumerator FadeAndSlideIn(CanvasGroup cg, float duration, float slideX)
        {
            if (cg == null)
                yield break;

            int idx = System.Array.IndexOf(mainButtons, cg);

            Vector3 basePos = (idx >= 0 && idx < _mainButtonsBaseLocalPos.Length)
                ? _mainButtonsBaseLocalPos[idx]
                : cg.transform.localPosition;

            Vector3 endPos = basePos;
            Vector3 startPos = basePos + new Vector3(slideX, 0f, 0f);

            cg.transform.localPosition = startPos;
            cg.alpha = 0f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            float t = 0f;

            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, duration);
                float s = Smooth01(t);

                cg.alpha = Mathf.Lerp(0f, 1f, s);
                cg.transform.localPosition = Vector3.Lerp(startPos, endPos, s);

                yield return null;
            }

            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
            cg.transform.localPosition = endPos;
        }

        private IEnumerator FadeAndSlideOut(CanvasGroup cg, float duration, float slideX)
        {
            if (cg == null)
                yield break;

            int idx = System.Array.IndexOf(mainButtons, cg);

            Vector3 basePos = (idx >= 0 && idx < _mainButtonsBaseLocalPos.Length)
                ? _mainButtonsBaseLocalPos[idx]
                : cg.transform.localPosition;

            Vector3 startPos = basePos;
            Vector3 endPos = basePos + new Vector3(slideX, 0f, 0f);

            cg.transform.localPosition = startPos;
            cg.alpha = 1f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            float t = 0f;

            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, duration);
                float s = Smooth01(t);

                cg.alpha = Mathf.Lerp(1f, 0f, s);
                cg.transform.localPosition = Vector3.Lerp(startPos, endPos, s);

                yield return null;
            }

            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            cg.transform.localPosition = endPos;
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group == null)
                return;

            group.alpha = alpha;
        }

        private static void SetCanvasGroup(CanvasGroup group, float alpha, bool interactable)
        {
            if (group == null)
                return;

            group.alpha = alpha;
            group.interactable = interactable;
            group.blocksRaycasts = interactable;
        }

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float DelayedFade01(float t, float fadeStartAt)
        {
            t = Mathf.Clamp01(t);
            fadeStartAt = Mathf.Clamp(fadeStartAt, 0f, 0.95f);

            if (t <= fadeStartAt)
                return 0f;

            float normalized = (t - fadeStartAt) / (1f - fadeStartAt);
            return Smooth01(normalized);
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float t = 0f;

            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}