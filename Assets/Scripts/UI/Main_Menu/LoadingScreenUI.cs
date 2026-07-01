using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JuegoCriminal.UI
{
    public sealed class LoadingScreenUI : MonoBehaviour
    {
        public static LoadingScreenUI Instance { get; private set; }

        [Header("Root")]
        [SerializeField] private GameObject loadingScreenRoot;

        [Header("UI")]
        [SerializeField] private Image loadingIcon;
        [SerializeField] private TMP_Text loadingText;

        [Header("Icon Animation")]
        [SerializeField] private Sprite[] loadingSprites;
        [SerializeField] private float spriteDelay = 1f;

        [Header("Text")]
        [SerializeField] private string defaultLoadingText = "Loading...";

        [Header("Persistence")]
        [SerializeField] private bool persistAcrossScenes = true;

        private Coroutine _iconRoutine;
        private int _spriteIndex;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (persistAcrossScenes)
                DontDestroyOnLoad(gameObject);

            ResolveReferences();

            HideImmediate();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        public void Show(string text = null)
        {
            ResolveReferences();

            if (loadingScreenRoot != null)
                loadingScreenRoot.SetActive(true);
            else
                gameObject.SetActive(true);

            if (loadingText != null)
                loadingText.text = string.IsNullOrWhiteSpace(text) ? defaultLoadingText : text;

            StartIconAnimation();
        }

        public void Hide()
        {
            StopIconAnimation();

            ResolveReferences();

            if (loadingScreenRoot != null)
                loadingScreenRoot.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        private void HideImmediate()
        {
            StopIconAnimation();

            ResolveReferences();

            if (loadingScreenRoot != null)
                loadingScreenRoot.SetActive(false);
        }

        private void ResolveReferences()
        {
            if (loadingScreenRoot == null)
            {
                Transform root = FindChildRecursive(transform, "Loading_screen");

                if (root != null)
                    loadingScreenRoot = root.gameObject;
                else
                    loadingScreenRoot = gameObject;
            }

            if (loadingIcon == null)
            {
                Transform icon = FindChildRecursive(transform, "Loading_icon");

                if (icon != null)
                    loadingIcon = icon.GetComponent<Image>();
            }

            if (loadingText == null)
            {
                Transform text = FindChildRecursive(transform, "Loading_text");

                if (text != null)
                    loadingText = text.GetComponent<TMP_Text>();
            }
        }

        private Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                if (child.name == childName)
                    return child;

                Transform result = FindChildRecursive(child, childName);

                if (result != null)
                    return result;
            }

            return null;
        }

        private void StartIconAnimation()
        {
            StopIconAnimation();

            _spriteIndex = 0;

            if (loadingIcon != null && loadingSprites != null && loadingSprites.Length > 0)
                loadingIcon.sprite = loadingSprites[0];

            _iconRoutine = StartCoroutine(IconRoutine());
        }

        private void StopIconAnimation()
        {
            if (_iconRoutine != null)
            {
                StopCoroutine(_iconRoutine);
                _iconRoutine = null;
            }
        }

        private IEnumerator IconRoutine()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(spriteDelay);

                if (loadingIcon == null)
                {
                    ResolveReferences();
                    continue;
                }

                if (loadingSprites == null || loadingSprites.Length == 0)
                    continue;

                _spriteIndex++;

                if (_spriteIndex >= loadingSprites.Length)
                    _spriteIndex = 0;

                loadingIcon.sprite = loadingSprites[_spriteIndex];
            }
        }
    }
}