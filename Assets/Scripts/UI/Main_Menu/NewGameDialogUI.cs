using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JuegoCriminal.Services;
using JuegoCriminal.Core;
using UnityEngine.Events;

namespace JuegoCriminal.UI
{
    public sealed class NewGameDialogUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button backButton;
        [SerializeField] private Button acceptButton;

        [Header("Defaults")]
        [SerializeField] private string worldSceneName = "10_World_City";
        [SerializeField] private string singleTitle = "New Game";
        [SerializeField] private string coopTitle = "New Co-op Game";

        public event Action OnBackRequested;

        public RectTransform PanelRect
        {
            get
            {
                if (panelRoot != null)
                    return panelRoot.GetComponent<RectTransform>();

                return GetComponent<RectTransform>();
            }
        }

        private SaveService _save;
        private SceneLoader _loader;

        private string _mode = "Single";
        private bool _isStartingGame;
        private UnityAction<string> _submitListener;

        private void Awake()
        {
            Bootstrapper app = Bootstrapper.Instance;
            _save = app != null ? app.SaveService : null;
            _loader = app != null ? app.SceneLoader : null;

            if (_save == null) _save = FindAnyObjectByType<SaveService>();
            if (_loader == null) _loader = FindAnyObjectByType<SceneLoader>();

            if (panelRoot != null)
                panelRoot.SetActive(false);

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(OnBackPressed);
                backButton.onClick.AddListener(OnBackPressed);
            }

            if (acceptButton != null)
            {
                acceptButton.onClick.RemoveListener(OnAcceptPressed);
                acceptButton.onClick.AddListener(OnAcceptPressed);
            }

            if (nameInput != null)
            {
                _submitListener = _ => OnAcceptPressed();
                nameInput.onSubmit.AddListener(_submitListener);
            }
        }

        private void OnDestroy()
        {
            if (backButton != null)
                backButton.onClick.RemoveListener(OnBackPressed);

            if (acceptButton != null)
                acceptButton.onClick.RemoveListener(OnAcceptPressed);

            if (nameInput != null && _submitListener != null)
                nameInput.onSubmit.RemoveListener(_submitListener);
        }

        public void Open(bool coop)
        {
            _isStartingGame = false;
            _mode = coop ? "Coop" : "Single";

            if (titleText != null)
                titleText.text = coop ? coopTitle : singleTitle;

            if (panelRoot != null)
                panelRoot.SetActive(true);

            if (nameInput != null)
            {
                nameInput.text = "";
                nameInput.Select();
                nameInput.ActivateInputField();
            }
        }

        public void Close()
        {
            HidePanelOnly();

            if (_isStartingGame)
                return;

            ShowMainMenuButtons();
        }

        public void HidePanelOnly()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private void OnBackPressed()
        {
            if (OnBackRequested != null)
                OnBackRequested.Invoke();
            else
                Close();
        }

        private void OnAcceptPressed()
        {
            if (_save == null || _loader == null)
                return;

            _isStartingGame = true;

            string displayName = GetEnteredNameOrDefault();
            int slotId = _save.GetFirstFreeSlotId();

            _save.NewGame(displayName: displayName, mode: _mode, slotId: slotId);

            if (panelRoot != null)
                panelRoot.SetActive(false);

            _loader.LoadScene(worldSceneName);
        }

        private string GetEnteredNameOrDefault()
        {
            string displayName = nameInput != null ? nameInput.text.Trim() : "";

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = _mode == "Coop" ? "New Co-op Save" : "New Save";

            return displayName;
        }

        private void ShowMainMenuButtons()
        {
            var menu = FindAnyObjectByType<JuegoCriminal.Scenes.MainMenuUI>();
            if (menu != null)
                menu.ShowMainButtons();
        }
    }
}
