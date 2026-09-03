using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JuegoCriminal.Core;

namespace JuegoCriminal.UI
{
    public sealed class ControlsMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private Button backButton;
        [SerializeField] private Button resetButton;

        private void Awake()
        {
            if (panelRoot == null)
                panelRoot = gameObject;

            if (backButton != null)
                backButton.onClick.AddListener(Close);

            if (resetButton != null)
                resetButton.onClick.AddListener(ResetBindings);

            panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (backButton != null)
                backButton.onClick.RemoveListener(Close);

            if (resetButton != null)
                resetButton.onClick.RemoveListener(ResetBindings);
        }

        public void Open()
        {
            panelRoot.SetActive(true);
            RefreshRows();
        }

        public void Close()
        {
            panelRoot.SetActive(false);
        }

        public void ResetBindings()
        {
            GameInput.ResetBindingOverrides();
            RefreshRows();
        }

        private void RefreshRows()
        {
            ControlRebindButtonUI[] rows = panelRoot.GetComponentsInChildren<ControlRebindButtonUI>(true);

            for (int i = 0; i < rows.Length; i++)
                rows[i].Refresh();
        }

#if UNITY_EDITOR
        [ContextMenu("Create Default Keyboard Binding Rows")]
        private void CreateDefaultKeyboardBindingRows()
        {
            EnsureScrollableContent();
            if (contentRoot == null)
                return;

            EnsureLayout(contentRoot.gameObject);

            CreateRow("Move Forward", GameInputAction.Move, 2);
            CreateRow("Move Backward", GameInputAction.Move, 4);
            CreateRow("Move Left", GameInputAction.Move, 6);
            CreateRow("Move Right", GameInputAction.Move, 8);
            CreateRow("Jump", GameInputAction.Jump, 0);
            CreateRow("Sprint", GameInputAction.Sprint, 0);
            CreateRow("Interact", GameInputAction.Interact, 0);
            CreateRow("Pause", GameInputAction.Pause, 0);
            CreateRow("Switch Target", GameInputAction.SwitchTarget, 1);
            CreateRow("Switch Shoulder", GameInputAction.SwitchShoulder, 1);
            CreateRow("Inventory", GameInputAction.Inventory, 0);

            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void EnsureScrollableContent()
        {
            RectTransform panel = transform as RectTransform;
            if (panel == null)
                return;

            Transform existingScrollView = panel.Find("ControlsScrollView");
            if (existingScrollView != null)
            {
                ScrollRect existingScrollRect = existingScrollView.GetComponent<ScrollRect>();
                if (existingScrollRect != null && existingScrollRect.content != null)
                {
                    contentRoot = existingScrollRect.content;
                    return;
                }
            }

            var scrollObject = new GameObject(
                "ControlsScrollView",
                typeof(RectTransform),
                typeof(Image),
                typeof(ScrollRect));
            UnityEditor.Undo.RegisterCreatedObjectUndo(scrollObject, "Create controls scroll view");
            scrollObject.transform.SetParent(panel, false);

            RectTransform scrollRectTransform = (RectTransform)scrollObject.transform;
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(30f, 90f);
            scrollRectTransform.offsetMax = new Vector2(-30f, -90f);

            Image background = scrollObject.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.18f);

            var viewportObject = new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(Image),
                typeof(RectMask2D));
            viewportObject.transform.SetParent(scrollObject.transform, false);

            RectTransform viewport = (RectTransform)viewportObject.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(10f, 10f);
            viewport.offsetMax = new Vector2(-10f, -10f);

            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);

            RectTransform previousContent = contentRoot;
            bool previousContentIsPanel = previousContent == null || previousContent == panel;

            if (previousContentIsPanel)
            {
                var contentObject = new GameObject("Content", typeof(RectTransform));
                contentObject.transform.SetParent(viewport, false);
                contentRoot = (RectTransform)contentObject.transform;

                for (int i = panel.childCount - 1; i >= 0; i--)
                {
                    Transform child = panel.GetChild(i);
                    if (child != scrollObject.transform && child.name.StartsWith("Binding_"))
                        UnityEditor.Undo.SetTransformParent(child, contentRoot, "Move binding row to scroll view");
                }
            }
            else
            {
                UnityEditor.Undo.SetTransformParent(previousContent, viewport, "Move controls content to scroll view");
                contentRoot = previousContent;
            }

            contentRoot.anchorMin = new Vector2(0f, 1f);
            contentRoot.anchorMax = new Vector2(1f, 1f);
            contentRoot.pivot = new Vector2(0.5f, 1f);
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.sizeDelta = Vector2.zero;

            ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = contentRoot;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            UnityEditor.Undo.RecordObject(this, "Assign controls scroll content");
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void CreateRow(string displayName, GameInputAction action, int bindingIndex)
        {
            string objectName = "Binding_" + displayName.Replace(" ", string.Empty);
            if (contentRoot.Find(objectName) != null)
                return;

            var rowObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            UnityEditor.Undo.RegisterCreatedObjectUndo(rowObject, "Create control binding row");
            rowObject.transform.SetParent(contentRoot, false);

            var rowRect = (RectTransform)rowObject.transform;
            rowRect.sizeDelta = new Vector2(600f, 48f);

            var layoutElement = rowObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 48f;
            layoutElement.minHeight = 48f;

            var layout = rowObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            TMP_Text actionText = CreateText("Action", rowObject.transform, displayName);
            Button button = CreateButton(rowObject.transform, out TMP_Text bindingText);

            var row = rowObject.AddComponent<ControlRebindButtonUI>();
            row.Configure(action, bindingIndex, displayName, actionText, bindingText, button);
        }

        private TMP_Text CreateText(string objectName, Transform parent, string value)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = 24f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = Color.white;
            return text;
        }

        private Button CreateButton(Transform parent, out TMP_Text bindingText)
        {
            var buttonObject = new GameObject("RebindButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.22f, 0.22f, 0.22f, 1f);

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            bindingText = CreateText("Binding", buttonObject.transform, "Unassigned");
            bindingText.alignment = TextAlignmentOptions.Center;

            RectTransform textRect = bindingText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }

        private void EnsureLayout(GameObject target)
        {
            var layout = target.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = UnityEditor.Undo.AddComponent<VerticalLayoutGroup>(target);

            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            var fitter = target.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = UnityEditor.Undo.AddComponent<ContentSizeFitter>(target);

            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ControlRebindButtonUI[] existingRows = target.GetComponentsInChildren<ControlRebindButtonUI>(true);
            for (int i = 0; i < existingRows.Length; i++)
            {
                LayoutElement rowLayout = existingRows[i].GetComponent<LayoutElement>();
                if (rowLayout == null)
                    rowLayout = UnityEditor.Undo.AddComponent<LayoutElement>(existingRows[i].gameObject);

                rowLayout.preferredHeight = 48f;
                rowLayout.minHeight = 48f;
            }
        }
#endif
    }
}
