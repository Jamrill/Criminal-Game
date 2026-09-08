using System;
using System.Collections.Generic;
using JuegoCriminal.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JuegoCriminal.UI
{
    public sealed class VideoMenuUI : MonoBehaviour
    {
        [SerializeField] private ControlsMenuUI controlsMenu;
        [SerializeField] private Button antialiasingButton;
        [SerializeField] private TMP_Text antialiasingLabel;
        [SerializeField] private Button presetButton;
        [SerializeField] private TMP_Text presetLabel;
        [SerializeField] private Button msaaButton;
        [SerializeField] private TMP_Text msaaLabel;
        [SerializeField] private Button smaaQualityButton;
        [SerializeField] private TMP_Text smaaQualityLabel;
        [SerializeField] private Button resolutionButton;
        [SerializeField] private TMP_Text resolutionLabel;
        [SerializeField] private Button windowModeButton;
        [SerializeField] private TMP_Text windowModeLabel;
        [SerializeField] private Button vsyncButton;
        [SerializeField] private TMP_Text vsyncLabel;
        private GameObject popup;

        private void Awake()
        {
            presetButton.onClick.AddListener(() => ShowChoices(presetButton,
                new[] { "Bajo", "Medio", "Alto", "Ultra", "Personalizado" }, (int)VideoSettings.Preset, VideoSettings.SetPreset));
            antialiasingButton.onClick.AddListener(() => ShowChoices(antialiasingButton,
                new[] { "Off", "FXAA", "SMAA", "TAA" }, VideoSettings.Antialiasing, VideoSettings.SetAntialiasing));
            msaaButton.onClick.AddListener(() => ShowChoices(msaaButton,
                new[] { "Off", "x2", "x4", "x8" }, Array.IndexOf(new[] { 1, 2, 4, 8 }, VideoSettings.MsaaSamples),
                index => VideoSettings.SetMsaa(new[] { 1, 2, 4, 8 }[index])));
            smaaQualityButton.onClick.AddListener(() => ShowChoices(smaaQualityButton,
                new[] { "Baja", "Media", "Alta" }, VideoSettings.SmaaQuality, VideoSettings.SetSmaaQuality));
            resolutionButton.onClick.AddListener(OpenResolutions);
            windowModeButton.onClick.AddListener(() => ShowChoices(windowModeButton,
                DisplaySettings.ModeLabels, DisplaySettings.Mode, DisplaySettings.SetMode));
            vsyncButton.onClick.AddListener(() => ShowChoices(vsyncButton,
                new[] { "Desactivado", "Activado" }, DisplaySettings.VSyncEnabled ? 1 : 0, DisplaySettings.SetVSync));
        }

        private void OnEnable()
        {
            VideoSettings.Changed += RefreshLabel;
            RefreshLabel();
        }

        private void OnDisable()
        {
            VideoSettings.Changed -= RefreshLabel;
            CloseChoices();
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public void Open()
        {
            if (controlsMenu != null) controlsMenu.Close();
            gameObject.SetActive(true);
            RefreshLabel();
        }

        public void Close() => gameObject.SetActive(false);

        private void OpenResolutions()
        {
            var labels = new List<string>();
            int selected = 0;
            for (int i = 0; i < DisplaySettings.Resolutions.Count; i++)
            {
                var size = DisplaySettings.Resolutions[i];
                labels.Add(size.x + " x " + size.y);
                if (size == DisplaySettings.Resolution) selected = i;
            }
            ShowChoices(resolutionButton, labels, selected, DisplaySettings.SetResolution);
        }

        private void RefreshLabel()
        {
            antialiasingLabel.text = VideoSettings.AntialiasingLabel + "  v";
            presetLabel.text = VideoSettings.PresetLabel + "  v";
            msaaButton.interactable = VideoSettings.Antialiasing != 3;
            msaaLabel.text = VideoSettings.Antialiasing == 3 ? "Off (TAA)" :
                (VideoSettings.MsaaSamples == 1 ? "Off" : "x" + VideoSettings.MsaaSamples) + "  v";
            smaaQualityButton.interactable = VideoSettings.Antialiasing == 2;
            smaaQualityLabel.text = VideoSettings.Antialiasing == 2 ? VideoSettings.SmaaQualityLabel + "  v" : "No aplicable";
            resolutionLabel.text = DisplaySettings.Resolution.x + " x " + DisplaySettings.Resolution.y + "  v";
            windowModeLabel.text = DisplaySettings.ModeLabels[DisplaySettings.Mode] + "  v";
            vsyncLabel.text = (DisplaySettings.VSyncEnabled ? "Activado" : "Desactivado") + "  v";
        }

        private void CloseChoices()
        {
            if (popup == null) return;
            popup.SetActive(false);
            Destroy(popup);
            popup = null;
        }

        // A shared dropdown overlay keeps every field styled like the existing menu.
        // It sits outside the scroll mask, with a blocker to prevent clicks through it.
        private void ShowChoices(Button source, IList<string> options, int selected, Action<int> select)
        {
            CloseChoices();
            Canvas owner = GetComponentInParent<Canvas>().rootCanvas;
            popup = new GameObject("VideoDropdown", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var root = (RectTransform)popup.transform;
            root.SetParent(owner.transform, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            Canvas canvas = popup.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = owner.sortingOrder + 200;

            var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image), typeof(Button));
            blocker.transform.SetParent(root, false);
            var blockRect = (RectTransform)blocker.transform;
            blockRect.anchorMin = Vector2.zero;
            blockRect.anchorMax = Vector2.one;
            blockRect.offsetMin = blockRect.offsetMax = Vector2.zero;
            blocker.GetComponent<Image>().color = Color.clear;
            blocker.GetComponent<Button>().onClick.AddListener(CloseChoices);

            var list = new GameObject("Options", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var rect = (RectTransform)list.transform;
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0, 1);
            Vector3[] corners = new Vector3[4];
            ((RectTransform)source.transform).GetWorldCorners(corners);
            Vector2 position = root.InverseTransformPoint(corners[0]);
            float width = Mathf.Min(root.rect.width, Mathf.Max(280f, Vector3.Distance(root.InverseTransformPoint(corners[0]), root.InverseTransformPoint(corners[3]))));
            float height = Mathf.Min(options.Count * 48f, Mathf.Min(288f, root.rect.height));
            rect.sizeDelta = new Vector2(width, height);
            position.x = Mathf.Clamp(position.x, root.rect.xMin, root.rect.xMax - width);
            if (position.y - height < root.rect.yMin) position.y = root.InverseTransformPoint(corners[1]).y + height;
            position.y = Mathf.Clamp(position.y, root.rect.yMin + height, root.rect.yMax);
            rect.anchoredPosition = position;
            list.GetComponent<Image>().color = new Color(0.09f, 0.09f, 0.09f, 1);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var viewRect = (RectTransform)viewport.transform;
            viewRect.SetParent(rect, false);
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.offsetMin = viewRect.offsetMax = Vector2.zero;
            var content = new GameObject("Content", typeof(RectTransform));
            var contentRect = (RectTransform)content.transform;
            contentRect.SetParent(viewRect, false);
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, options.Count * 48f);
            var scroll = list.GetComponent<ScrollRect>();
            scroll.viewport = viewRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;

            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                var item = new GameObject("Option_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                var itemRect = (RectTransform)item.transform;
                itemRect.SetParent(contentRect, false);
                itemRect.anchorMin = new Vector2(0, 1);
                itemRect.anchorMax = Vector2.one;
                itemRect.pivot = new Vector2(0.5f, 1);
                itemRect.sizeDelta = new Vector2(0, 48);
                itemRect.anchoredPosition = new Vector2(0, -i * 48);
                var background = item.GetComponent<Image>();
                background.color = i == selected ? new Color(0.25f, 0.35f, 0.45f) : new Color(0.16f, 0.16f, 0.16f);
                var button = item.GetComponent<Button>();
                button.targetGraphic = background;
                button.onClick.AddListener(() => { CloseChoices(); select(index); RefreshLabel(); });
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(itemRect, false);
                var text = labelObject.GetComponent<TextMeshProUGUI>();
                text.font = antialiasingLabel.font;
                text.fontSize = antialiasingLabel.fontSize;
                text.enableAutoSizing = true;
                text.fontSizeMin = 12;
                text.fontSizeMax = antialiasingLabel.fontSize;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.text = options[i];
                text.raycastTarget = false;
                text.rectTransform.anchorMin = Vector2.zero;
                text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = new Vector2(12, 0);
                text.rectTransform.offsetMax = new Vector2(-12, 0);
            }
        }
    }
}
