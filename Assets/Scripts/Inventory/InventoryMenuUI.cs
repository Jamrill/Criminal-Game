using JuegoCriminal.CameraSystem;
using JuegoCriminal.Core;
using JuegoCriminal.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JuegoCriminal.Inventory
{
    public sealed class InventoryMenuUI : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField, Tooltip("Prefab visual que se instancia al aparecer el jugador.")]
        private GameObject uiPrefab;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private RectTransform gridRoot;
        [SerializeField] private TMP_Text capacityText;
        [SerializeField, Min(24f)] private float cellSize = 56f;
        [SerializeField] private Color cellColor = new Color(0.12f, 0.12f, 0.12f, 0.92f);
        [SerializeField] private Color itemCellColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField, Range(0.5f, 2f)] private float itemIconScale = 1.3f;

        private bool _open;
        private bool _previousCursorVisible;
        private CursorLockMode _previousCursorLock;
        private ThirdPersonController _movement;
        private InteractorRaycast _interactor;
        private CameraBoomCollision _cameraBoom;
        private bool _movementWasEnabled;
        private bool _interactorWasEnabled;
        private bool _cameraBoomWasEnabled;
        private GameObject _uiInstance;

        public RectTransform GridRoot => gridRoot;
        public float CellSize => cellSize;

        private void Awake()
        {
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if ((panelRoot == null || gridRoot == null) && uiPrefab != null)
                CreateUIFromPrefab();
            if (panelRoot == null || gridRoot == null) CreateDevelopmentUI();
            panelRoot.SetActive(false);
            if (inventory != null) inventory.Changed += Refresh;
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.Changed -= Refresh;
        }

        private void Update()
        {
            if (GameInput.InventoryPressed && (_open || Cursor.lockState == CursorLockMode.Locked)) Toggle();
        }

        public void Toggle()
        {
            if (_open) Close(); else Open();
        }

        public void Open()
        {
            if (inventory == null) return;
            _open = true;
            _previousCursorVisible = Cursor.visible;
            _previousCursorLock = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SetPlayerControl(false);
            panelRoot.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            _open = false;
            panelRoot.SetActive(false);
            SetPlayerControl(true);
            Cursor.visible = _previousCursorVisible;
            Cursor.lockState = _previousCursorLock;
        }

        public void TryDrop(InventoryPlacement placement, Vector2 screenPosition, bool rotated)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRoot, screenPosition, null, out Vector2 local))
            {
                Refresh(); return;
            }
            int x = Mathf.RoundToInt(local.x / cellSize);
            int y = Mathf.RoundToInt(-local.y / cellSize);
            if (!inventory.TryMove(placement.instanceId, x, y, rotated)) Refresh();
        }

        public void TryDropAtLocalPosition(InventoryPlacement placement, Vector2 localPosition, bool rotated)
        {
            int x = Mathf.RoundToInt(localPosition.x / cellSize);
            int y = Mathf.RoundToInt(-localPosition.y / cellSize);
            if (!inventory.TryMove(placement.instanceId, x, y, rotated)) Refresh();
        }

        public void TryRotate(InventoryPlacement placement)
        {
            InventoryItemDefinition item = inventory.Resolve(placement.itemId);
            if (item == null || !item.CanRotate
                || !inventory.TryMove(placement.instanceId, placement.x, placement.y, !placement.rotated)) Refresh();
        }

        public void Refresh()
        {
            if (inventory == null || gridRoot == null) return;
            for (int i = gridRoot.childCount - 1; i >= 0; i--)
            {
                gridRoot.GetChild(i).gameObject.SetActive(false);
                Destroy(gridRoot.GetChild(i).gameObject);
            }

            int capacity = inventory.Capacity;
            int rows = Mathf.Max(1, Mathf.CeilToInt(capacity / (float)InventoryGrid.Width));
            gridRoot.sizeDelta = new Vector2(InventoryGrid.Width * cellSize, rows * cellSize);
            if (capacityText != null) capacityText.text = $"Inventory {capacity}/{InventoryGrid.MaxCapacity}";

            for (int i = 0; i < capacity; i++) CreateCell(i % InventoryGrid.Width, i / InventoryGrid.Width);
            for (int i = 0; i < inventory.Grid.Placements.Count; i++) CreateItem(inventory.Grid.Placements[i]);
        }

        private void CreateCell(int x, int y)
        {
            var go = new GameObject($"Cell_{x}_{y}", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(gridRoot, false);
            SetGridRect(rect, x, y, 1, 1);
            rect.sizeDelta -= Vector2.one * 2f;
            go.GetComponent<Image>().color = cellColor;
            go.GetComponent<Image>().raycastTarget = false;
        }

        private void CreateItem(InventoryPlacement placement)
        {
            InventoryItemDefinition item = inventory.Resolve(placement.itemId);
            if (item == null) return;
            var go = new GameObject("Item_" + item.DisplayName, typeof(RectTransform), typeof(Image), typeof(InventoryItemViewUI));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(gridRoot, false);
            SetGridRect(rect, placement.x, placement.y, item.Width(placement.rotated), item.Height(placement.rotated));
            Image hitArea = go.GetComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = false;

            for (int y = 0; y < item.Height(placement.rotated); y++)
            for (int x = 0; x < item.Width(placement.rotated); x++)
            {
                if (!item.Occupies(x, y, placement.rotated)) continue;
                var cell = new GameObject($"Footprint_{x}_{y}", typeof(RectTransform), typeof(Image));
                RectTransform cellRect = (RectTransform)cell.transform;
                cellRect.SetParent(rect, false);
                SetGridRect(cellRect, x, y, 1, 1);
                cellRect.sizeDelta -= Vector2.one * 5f;
                Image background = cell.GetComponent<Image>();
                background.color = itemCellColor;
                background.raycastTarget = true;
            }

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            RectTransform iconRect = (RectTransform)iconObject.transform;
            iconRect.SetParent(rect, false);
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = rect.sizeDelta * itemIconScale;
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = item.InventorySprite;
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            go.GetComponent<InventoryItemViewUI>().Initialize(this, placement);
        }

        private void SetGridRect(RectTransform rect, int x, int y, int width, int height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x * cellSize, -y * cellSize);
            rect.sizeDelta = new Vector2(width * cellSize, height * cellSize);
        }

        private void SetPlayerControl(bool enabled)
        {
            if (_movement == null) _movement = GetComponent<ThirdPersonController>();
            if (_interactor == null) _interactor = GetComponent<InteractorRaycast>();
            if (_cameraBoom == null) _cameraBoom = FindAnyObjectByType<CameraBoomCollision>();
            if (!enabled)
            {
                if (_movement != null) { _movementWasEnabled = _movement.enabled; _movement.enabled = false; }
                if (_interactor != null) { _interactorWasEnabled = _interactor.enabled; _interactor.enabled = false; }
                if (_cameraBoom != null) { _cameraBoomWasEnabled = _cameraBoom.enabled; _cameraBoom.enabled = false; }
            }
            else
            {
                if (_movement != null) _movement.enabled = _movementWasEnabled;
                if (_interactor != null) _interactor.enabled = _interactorWasEnabled;
                if (_cameraBoom != null) _cameraBoom.enabled = _cameraBoomWasEnabled;
            }
        }

        private void CreateDevelopmentUI()
        {
            var canvasObject = new GameObject("InventoryCanvas_Development", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var panel = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(620f, 570f);
            panel.GetComponent<Image>().color = new Color(0.025f, 0.025f, 0.025f, 0.96f);
            panelRoot = panel;

            var title = new GameObject("Capacity", typeof(RectTransform), typeof(TextMeshProUGUI));
            title.transform.SetParent(panel.transform, false);
            capacityText = title.GetComponent<TextMeshProUGUI>();
            capacityText.fontSize = 26f; capacityText.alignment = TextAlignmentOptions.Center;
            RectTransform titleRect = capacityText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f); titleRect.anchorMax = Vector2.one;
            titleRect.pivot = new Vector2(0.5f, 1f); titleRect.sizeDelta = new Vector2(0f, 48f);

            var grid = new GameObject("Grid", typeof(RectTransform));
            grid.transform.SetParent(panel.transform, false);
            gridRoot = (RectTransform)grid.transform;
            gridRoot.anchorMin = gridRoot.anchorMax = gridRoot.pivot = new Vector2(0.5f, 1f);
            gridRoot.anchoredPosition = new Vector2(0f, -60f);
        }

        private void CreateUIFromPrefab()
        {
            _uiInstance = Instantiate(uiPrefab, transform, false);
            _uiInstance.name = uiPrefab.name;
            _uiInstance.SetActive(true);

            RectTransform[] rects = _uiInstance.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                if (panelRoot == null && rects[i].name == "InventoryPanel")
                    panelRoot = rects[i].gameObject;
                else if (gridRoot == null && rects[i].name == "Grid")
                    gridRoot = rects[i];
            }

            TMP_Text[] texts = _uiInstance.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
                if (texts[i].name == "Capacity")
                {
                    capacityText = texts[i];
                    break;
                }

            if (panelRoot == null || gridRoot == null)
            {
                Debug.LogError(
                    "[InventoryUI] El prefab debe contener objetos llamados InventoryPanel y Grid.", this);
                Destroy(_uiInstance);
                _uiInstance = null;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Create Editable Inventory UI")]
        private void CreateEditableInventoryUI()
        {
            if (panelRoot != null || gridRoot != null)
            {
                Debug.LogWarning("[InventoryUI] Ya existe una UI asignada.", this);
                return;
            }

            CreateDevelopmentUI();
            UnityEditor.Undo.RegisterCreatedObjectUndo(
                panelRoot.transform.parent.gameObject,
                "Create editable inventory UI");
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
