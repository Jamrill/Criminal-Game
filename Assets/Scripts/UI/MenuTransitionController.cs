using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace JuegoCriminal.UI
{
    public sealed class MenuTransitionController : MonoBehaviour
    {
        [Header("Main Menu Buttons (in order)")]
        [SerializeField] private CanvasGroup[] mainButtons; // Continue..Quit (cada botón con CanvasGroup)
        [SerializeField] private float buttonHideStagger = 0.05f;
        [SerializeField] private float buttonHideDuration = 0.12f;
        [SerializeField] private float buttonSlideX = 40f;

        [Header("Slots Panel")]
        [SerializeField] private GameObject slotsPanelRoot;
        [SerializeField] private JuegoCriminal.UI.SlotsPanelUI slotsPanelUI;

        [Header("Slots Panel - ScrollView enters from left")]
        [SerializeField] private RectTransform slotsScrollView;
        [SerializeField] private float scrollEnterDuration = 0.22f;
        [SerializeField] private float scrollOffscreenX = -1200f;

        [Header("Slots Panel - Buttons enter from bottom")]
        [SerializeField] private RectTransform backButton;
        [SerializeField] private RectTransform loadButton;
        [SerializeField] private RectTransform deleteButton;
        [SerializeField] private float buttonsEnterDuration = 0.22f;
        [SerializeField] private float buttonsOffscreenY = -600f;

        private Vector2 _scrollFinalPos;
        private Vector2 _backFinalPos;
        private Vector2 _loadFinalPos;
        private Vector2 _deleteFinalPos;
        private Vector3[] _mainButtonsBaseLocalPos;

        private bool _isTransitioning;

        private void Awake()
        {
            // Guardar posiciones finales (las que tienes colocadas en UI)
            if (slotsScrollView != null) _scrollFinalPos = slotsScrollView.anchoredPosition;
            if (backButton != null) _backFinalPos = backButton.anchoredPosition;
            if (loadButton != null) _loadFinalPos = loadButton.anchoredPosition;
            if (deleteButton != null) _deleteFinalPos = deleteButton.anchoredPosition;

            // Slots panel oculto al inicio
            if (slotsPanelRoot != null) slotsPanelRoot.SetActive(false);

            _mainButtonsBaseLocalPos = new Vector3[mainButtons.Length];
            for (int i = 0; i < mainButtons.Length; i++)
            {
                if (mainButtons[i] != null)
                    _mainButtonsBaseLocalPos[i] = mainButtons[i].transform.localPosition;
            }
        }

        // Llamar desde MainMenuUI cuando pulses Load Game
        public void TransitionToLoadGame()
        {
            Debug.Log("[MenuTransition] TransitionToLoadGame called");
            if (_isTransitioning) return;
            StartCoroutine(LoadGameRoutine());
        }
        public void TransitionBackToMainMenu()
        {
            Debug.Log("[MenuTransition] BackToMainMenu called");
            if (_isTransitioning) return;
            StartCoroutine(BackToMainMenuRoutine());
        }

        private IEnumerator LoadGameRoutine()
        {
            _isTransitioning = true;

            // 1) Ocultar botones del main menu uno a uno
            for (int i = 0; i < mainButtons.Length; i++)
            {
                if (mainButtons[i] != null)
                    StartCoroutine(FadeAndSlideOut(mainButtons[i], buttonHideDuration, buttonSlideX));

                yield return WaitUnscaled(buttonHideStagger);
            }

            // 2) Activar slots panel (pero colocar fuera de pantalla antes)
            if (slotsPanelRoot != null) slotsPanelRoot.SetActive(true);
            
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

            // 3) Animar entrada del scroll y botones (en paralelo)
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, scrollEnterDuration);
                float s = Smooth01(t);

                if (slotsScrollView != null)
                    slotsScrollView.anchoredPosition = Vector2.Lerp(
                        new Vector2(scrollOffscreenX, _scrollFinalPos.y),
                        _scrollFinalPos,
                        s
                    );

                if (backButton != null)
                    backButton.anchoredPosition = Vector2.Lerp(
                        new Vector2(_backFinalPos.x, buttonsOffscreenY),
                        _backFinalPos,
                        s
                    );

                if (loadButton != null)
                    loadButton.anchoredPosition = Vector2.Lerp(
                        new Vector2(_loadFinalPos.x, buttonsOffscreenY),
                        _loadFinalPos,
                        s
                    );

                if (deleteButton != null)
                    deleteButton.anchoredPosition = Vector2.Lerp(
                        new Vector2(_deleteFinalPos.x, buttonsOffscreenY),
                        _deleteFinalPos,
                        s
                    );

                yield return null;
            }

            // Asegurar finales exactos
            if (slotsScrollView != null) slotsScrollView.anchoredPosition = _scrollFinalPos;
            if (backButton != null) backButton.anchoredPosition = _backFinalPos;
            if (loadButton != null) loadButton.anchoredPosition = _loadFinalPos;
            if (deleteButton != null) deleteButton.anchoredPosition = _deleteFinalPos;

            _isTransitioning = false;
        }

        private IEnumerator BackToMainMenuRoutine()
        {
            _isTransitioning = true;

            // 1) Sacar SlotsPanel (scroll a la izquierda, botones hacia abajo)
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, scrollEnterDuration);
                float s = Smooth01(t);

                if (slotsScrollView != null)
                    slotsScrollView.anchoredPosition = Vector2.Lerp(
                        _scrollFinalPos,
                        new Vector2(scrollOffscreenX, _scrollFinalPos.y),
                        s
                    );

                if (backButton != null)
                    backButton.anchoredPosition = Vector2.Lerp(
                        _backFinalPos,
                        new Vector2(_backFinalPos.x, buttonsOffscreenY),
                        s
                    );

                if (loadButton != null)
                    loadButton.anchoredPosition = Vector2.Lerp(
                        _loadFinalPos,
                        new Vector2(_loadFinalPos.x, buttonsOffscreenY),
                        s
                    );

                if (deleteButton != null)
                    deleteButton.anchoredPosition = Vector2.Lerp(
                        _deleteFinalPos,
                        new Vector2(_deleteFinalPos.x, buttonsOffscreenY),
                        s
                    );

                yield return null;
            }

            // 2) Apagar SlotsPanel root
            if (slotsPanelRoot != null) slotsPanelRoot.SetActive(false);

            // 3) Mostrar botones del menú uno a uno (fade in + slide back)
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
            if (cg == null) yield break;

            int idx = System.Array.IndexOf(mainButtons, cg);
            Vector3 basePos = (idx >= 0 && idx < _mainButtonsBaseLocalPos.Length) ? _mainButtonsBaseLocalPos[idx] : cg.transform.localPosition;

            Vector3 endPos = basePos;
            Vector3 startPos = basePos + new Vector3(slideX, 0f, 0f);

            // Preparar estado inicial visible desde fuera
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
            cg.transform.localPosition = endPos;
        }

        private IEnumerator FadeAndSlideOut(CanvasGroup cg, float duration, float slideX)
        {
            if (cg == null) yield break;

            // Busca el índice para usar la base pos guardada
            int idx = System.Array.IndexOf(mainButtons, cg);
            Vector3 basePos = (idx >= 0 && idx < _mainButtonsBaseLocalPos.Length) ? _mainButtonsBaseLocalPos[idx] : cg.transform.localPosition;

            Vector3 startPos = basePos;
            Vector3 endPos = basePos + new Vector3(slideX, 0f, 0f);

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

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t); // smoothstep
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