using JuegoCriminal.Interaction;
using JuegoCriminal.CameraSystem;
using JuegoCriminal.Player;
using JuegoCriminal.Core;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using JuegoCriminal.Services;
using UnityEngine.SceneManagement;

namespace JuegoCriminal.Printing
{
    /// <summary>Coordina selección, ciclo de impresión y entrega del resultado.</summary>
    public sealed class Printer3DController : MonoBehaviour
    {
        public static Printer3DController ActiveSelection { get; private set; }

        [Header("Configuration")]
        [SerializeField, Tooltip("Debe ser único para cada impresora colocada en una escena.")]
        private string persistenceId = "printer_01";
        [SerializeField] private Printer3DProfile profile;
        [SerializeField, Min(0.1f)] private float fallbackPrintDuration = 8f;
        [SerializeField] private PrintablePartDefinition[] printableParts;
        [Header("State")]
        [SerializeField] private bool isPrinting;
        [Header("References")]
        [SerializeField] private InteractableObject interactableObject;
        [SerializeField] private Printer3DAnimation animationView;
        [SerializeField] private Transform resultSpawnPoint;
        [SerializeField] private GameObject failedPrintPrefab;
        [Header("Camera Selection Mode")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Camera printerCamera;
        [SerializeField] private GameObject printerSelectionRoot;
        [SerializeField] private MonoBehaviour[] disableWhileSelecting;
        [SerializeField, Min(0.05f)] private float cameraTransitionDuration = 0.45f;
        [Header("Printer Selection UI")]
        [SerializeField] private Image projectPreviewImage;
        [SerializeField] private TMP_Text projectNameText;
        [SerializeField] private Button backProjectButton;
        [SerializeField] private Button nextProjectButton;
        [SerializeField] private Button printProjectButton;
        [SerializeField] private Button exitProjectButton;
        [Header("Prompt Text")]
        [SerializeField] private string idleText = "Print";
        [SerializeField] private string printingText = "Cancel?";
        [Header("Completed Output Prompt")]
        [SerializeField] private string pickupText = "Pick Up";
        [SerializeField, Tooltip("Altura adicional respecto a la parte superior del objeto impreso. Admite valores negativos.")]
        private float pickupPromptExtraHeight = -0.03f;

        private float _printTimer;
        private int _selectedPartIndex;
        private PrintablePartDefinition _currentPart;
        private bool _isSelecting;
        private bool _previousCursorVisible;
        private CursorLockMode _previousCursorLockMode;
        private readonly Dictionary<MonoBehaviour, bool> _disabledComponentStates = new();
        private Vector3 _playerCameraStartPosition;
        private Quaternion _playerCameraStartRotation;
        private float _playerCameraStartFov;
        private Coroutine _cameraTransition;
        private Transform _printRevealRoot;
        private GameObject _printingObject;
        private GameObject _completedOutput;

        public bool IsPrinting => isPrinting;
        public float PrintSpeedMultiplier => profile != null ? profile.PrintSpeedMultiplier : 1f;
        public int PrinterLevel => profile != null ? profile.PrinterLevel : 1;
        public string PersistenceId => persistenceId;
        public bool IsSelecting => _isSelecting;

        private enum SavedStatus { Idle, Printing, Completed }

        /// <summary>Llamar al crear una impresora colocada por el jugador.</summary>
        public void InitializeAsPlayerPlaced()
        {
            persistenceId = System.Guid.NewGuid().ToString("N");
        }

        private void Reset()
        {
            interactableObject = GetComponent<InteractableObject>();
            animationView = GetComponent<Printer3DAnimation>();
        }

        private void Awake()
        {
            if (interactableObject == null) interactableObject = GetComponent<InteractableObject>();
            if (animationView == null) animationView = GetComponent<Printer3DAnimation>();
            if (playerCamera == null) playerCamera = Camera.main;
            AddButtonListeners();
            if (printerSelectionRoot != null) printerSelectionRoot.SetActive(false);
            if (printerCamera != null) printerCamera.gameObject.SetActive(false);
            isPrinting = false;
            SetIdleState();
        }

        private void Start() => RestoreFromSave();

        private void OnDestroy()
        {
            if (ActiveSelection == this)
                ActiveSelection = null;
            RemoveButtonListeners();
        }

        private void Update()
        {
            if (_isSelecting && GameInput.PausePressed)
            {
                GameInput.ConsumePausePress();
                CloseProjectSelection();
                return;
            }

            if (!isPrinting) return;
            _printTimer += Time.deltaTime;
            float duration = GetCurrentPrintDuration();
            animationView?.UpdatePrintVisuals(_printTimer, duration);
            UpdatePrintedObjectReveal(_printTimer / duration);
            if (_printTimer >= duration) CompletePrint();
        }

        public void InteractWithPrinter()
        {
            if (isPrinting) CancelPrint(); else OpenProjectSelection();
        }

        public void StartPrint() => InteractWithPrinter();
        public void StartPrinting() => InteractWithPrinter();
        public void StopPrinting() => CancelPrint();

        public void OpenProjectSelection()
        {
            if (isPrinting || _isSelecting || _completedOutput != null) return;
            if (printableParts == null || printableParts.Length == 0)
            {
                Debug.LogWarning("[Printer3DController] No hay piezas imprimibles configuradas.", this);
                return;
            }

            _isSelecting = true;
            ActiveSelection = this;
            _previousCursorVisible = Cursor.visible;
            _previousCursorLockMode = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SetPlayerControlsLocked(true);
            _selectedPartIndex = Mathf.Clamp(_selectedPartIndex, 0, printableParts.Length - 1);
            RefreshSelectedPartUI();
            SetInteractionAvailable(false, false);

            if (_cameraTransition != null) StopCoroutine(_cameraTransition);
            _cameraTransition = StartCoroutine(EnterPrinterCameraRoutine());
        }

        public void CloseProjectSelection()
        {
            if (!_isSelecting) return;
            _isSelecting = false;
            if (ActiveSelection == this)
                ActiveSelection = null;

            if (_cameraTransition != null) StopCoroutine(_cameraTransition);
            _cameraTransition = StartCoroutine(ExitPrinterCameraRoutine());
        }

        public void CancelPrint()
        {
            if (!isPrinting) return;
            isPrinting = false;
            DestroyPrintingPreview();
            Spawn(failedPrintPrefab);
            animationView?.FinishPrintVisuals(false);
            _currentPart = null;
            SetIdleState();
        }

        private void SelectPreviousPart()
        {
            if (!_isSelecting || printableParts == null || printableParts.Length == 0) return;
            _selectedPartIndex = (_selectedPartIndex - 1 + printableParts.Length) % printableParts.Length;
            RefreshSelectedPartUI();
        }

        private void SelectNextPart()
        {
            if (!_isSelecting || printableParts == null || printableParts.Length == 0) return;
            _selectedPartIndex = (_selectedPartIndex + 1) % printableParts.Length;
            RefreshSelectedPartUI();
        }

        private void ConfirmSelectedPartAndPrint()
        {
            if (!_isSelecting) return;
            PrintablePartDefinition selected = GetSelectedPart();
            if (selected == null || selected.RequiredPrinterLevel > PrinterLevel)
            {
                Debug.LogWarning("[Printer3DController] La impresora no tiene nivel suficiente para esta pieza.", this);
                return;
            }
            _currentPart = selected;
            CloseProjectSelection();
            BeginPrint();
        }

        private void BeginPrint()
        {
            if (_currentPart == null) _currentPart = GetSelectedPart();
            _printTimer = 0f;
            isPrinting = true;
            if (interactableObject != null) interactableObject.SetInteractionText(printingText);
            SetInteractionAvailable(true, true);
            animationView?.BeginPrintVisuals();
            CreatePrintingPreview();
        }

        private void CompletePrint()
        {
            isPrinting = false;
            CompletePrintingPreview();
            animationView?.FinishPrintVisuals(true);
            _currentPart = null;
            SetInteractionAvailable(false, false);
        }

        private float GetCurrentPrintDuration()
        {
            float duration = _currentPart != null ? _currentPart.PrintDuration : fallbackPrintDuration;
            return Mathf.Max(0.01f, duration / PrintSpeedMultiplier);
        }

        private PrintablePartDefinition GetSelectedPart()
        {
            if (printableParts == null || printableParts.Length == 0) return null;
            _selectedPartIndex = Mathf.Clamp(_selectedPartIndex, 0, printableParts.Length - 1);
            return printableParts[_selectedPartIndex];
        }

        private void RefreshSelectedPartUI()
        {
            PrintablePartDefinition part = GetSelectedPart();

            if (projectNameText != null)
                projectNameText.text = part != null ? part.DisplayName : string.Empty;

            if (projectPreviewImage != null)
            {
                projectPreviewImage.sprite = part != null ? part.PrinterPreviewSprite : null;
                projectPreviewImage.enabled = part != null && part.PrinterPreviewSprite != null;
                projectPreviewImage.preserveAspect = true;
            }
        }

        private void SetPlayerControlsLocked(bool locked)
        {
            if (locked)
            {
                _disabledComponentStates.Clear();
                RememberAndDisable(FindAnyObjectByType<ThirdPersonController>());
                RememberAndDisable(FindAnyObjectByType<InteractorRaycast>());
                RememberAndDisable(FindAnyObjectByType<CameraBoomCollision>());

                if (disableWhileSelecting == null) return;
                for (int i = 0; i < disableWhileSelecting.Length; i++)
                    RememberAndDisable(disableWhileSelecting[i]);
            }
            else
            {
                foreach (KeyValuePair<MonoBehaviour, bool> entry in _disabledComponentStates)
                    if (entry.Key != null) entry.Key.enabled = entry.Value;

                _disabledComponentStates.Clear();
            }
        }

        private IEnumerator EnterPrinterCameraRoutine()
        {
            if (playerCamera == null) playerCamera = Camera.main;

            if (playerCamera == null || printerCamera == null)
            {
                if (printerCamera != null) printerCamera.gameObject.SetActive(true);
                if (printerSelectionRoot != null) printerSelectionRoot.SetActive(true);
                _cameraTransition = null;
                yield break;
            }

            _playerCameraStartPosition = playerCamera.transform.position;
            _playerCameraStartRotation = playerCamera.transform.rotation;
            _playerCameraStartFov = playerCamera.fieldOfView;

            printerCamera.gameObject.SetActive(false);
            if (printerSelectionRoot != null) printerSelectionRoot.SetActive(false);

            yield return BlendCamera(
                playerCamera.transform.position,
                playerCamera.transform.rotation,
                playerCamera.fieldOfView,
                printerCamera.transform.position,
                printerCamera.transform.rotation,
                printerCamera.fieldOfView);

            playerCamera.gameObject.SetActive(false);
            printerCamera.gameObject.SetActive(true);
            if (printerSelectionRoot != null) printerSelectionRoot.SetActive(true);
            _cameraTransition = null;
        }

        private IEnumerator ExitPrinterCameraRoutine()
        {
            if (printerSelectionRoot != null) printerSelectionRoot.SetActive(false);

            if (playerCamera != null && printerCamera != null)
            {
                if (printerCamera.gameObject.activeSelf)
                {
                    playerCamera.transform.SetPositionAndRotation(
                        printerCamera.transform.position,
                        printerCamera.transform.rotation);
                    playerCamera.fieldOfView = printerCamera.fieldOfView;
                    playerCamera.gameObject.SetActive(true);
                }

                printerCamera.gameObject.SetActive(false);

                yield return BlendCamera(
                    playerCamera.transform.position,
                    playerCamera.transform.rotation,
                    playerCamera.fieldOfView,
                    _playerCameraStartPosition,
                    _playerCameraStartRotation,
                    _playerCameraStartFov);
            }
            else if (printerCamera != null)
            {
                printerCamera.gameObject.SetActive(false);
            }

            SetPlayerControlsLocked(false);
            Cursor.visible = _previousCursorVisible;
            Cursor.lockState = _previousCursorLockMode;
            if (!isPrinting && _completedOutput == null) SetIdleState();
            _cameraTransition = null;
        }

        private IEnumerator BlendCamera(
            Vector3 startPosition,
            Quaternion startRotation,
            float startFov,
            Vector3 endPosition,
            Quaternion endRotation,
            float endFov)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.05f, cameraTransitionDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = t * t * (3f - 2f * t);
                playerCamera.transform.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, endPosition, smooth),
                    Quaternion.Slerp(startRotation, endRotation, smooth));
                playerCamera.fieldOfView = Mathf.Lerp(startFov, endFov, smooth);
                yield return null;
            }

            playerCamera.transform.SetPositionAndRotation(endPosition, endRotation);
            playerCamera.fieldOfView = endFov;
        }

        private void RememberAndDisable(MonoBehaviour component)
        {
            if (component == null || _disabledComponentStates.ContainsKey(component)) return;
            _disabledComponentStates.Add(component, component.enabled);
            component.enabled = false;
        }

        private void SetIdleState()
        {
            if (interactableObject != null) interactableObject.SetInteractionText(idleText);
            SetInteractionAvailable(true, true);
        }

        private void SetInteractionAvailable(bool canInteract, bool showPrompt)
        {
            if (interactableObject == null) return;
            interactableObject.SetCanInteract(canInteract);
            interactableObject.SetCanShowPrompt(showPrompt);
        }

        private void Spawn(GameObject prefab)
        {
            if (prefab == null) return;
            Transform spawn = resultSpawnPoint != null ? resultSpawnPoint : transform;
            Instantiate(prefab, spawn.position, spawn.rotation);
        }

        private void CreatePrintingPreview()
        {
            DestroyPrintingPreview();
            if (_currentPart == null || _currentPart.PrintedPartPrefab == null) return;

            Transform spawn = resultSpawnPoint != null ? resultSpawnPoint : transform;
            var revealObject = new GameObject("Printing_Reveal");
            _printRevealRoot = revealObject.transform;
            _printRevealRoot.SetParent(spawn, false);
            _printRevealRoot.localPosition = Vector3.zero;
            _printRevealRoot.localRotation = Quaternion.identity;
            _printRevealRoot.localScale = new Vector3(1f, 0.001f, 1f);
            _printingObject = Instantiate(
                _currentPart.PrintedPartPrefab,
                _printRevealRoot,
                false);
            _printingObject.transform.localPosition = Vector3.zero;
            _printingObject.transform.localRotation = Quaternion.identity;

            SetOutputComponentsEnabled(_printingObject, false);
        }

        private void UpdatePrintedObjectReveal(float progress)
        {
            if (_printRevealRoot == null) return;
            _printRevealRoot.localScale = new Vector3(1f, Mathf.Max(0.001f, Mathf.Clamp01(progress)), 1f);
        }

        private void CompletePrintingPreview()
        {
            if (_printingObject == null) return;

            UpdatePrintedObjectReveal(1f);
            _printingObject.transform.SetParent(null, true);
            if (_printRevealRoot != null) Destroy(_printRevealRoot.gameObject);
            _printRevealRoot = null;

            SetOutputComponentsEnabled(_printingObject, true);
            _completedOutput = _printingObject;
            _printingObject = null;

            PrintedPartPickup pickup = _completedOutput.GetComponent<PrintedPartPickup>();
            if (pickup == null) pickup = _completedOutput.AddComponent<PrintedPartPickup>();
            pickup.Initialize(
                _currentPart,
                interactableObject != null ? interactableObject.GetPromptPrefab() : null,
                pickupText,
                pickupPromptExtraHeight,
                OnOutputPickedUp);
        }

        private void DestroyPrintingPreview()
        {
            if (_printingObject != null) Destroy(_printingObject);
            if (_printRevealRoot != null) Destroy(_printRevealRoot.gameObject);
            _printingObject = null;
            _printRevealRoot = null;
        }

        private void OnOutputPickedUp()
        {
            _completedOutput = null;
            SetIdleState();
        }

        public PrinterSaveState CaptureSaveState()
        {
            PrintablePartDefinition part = _currentPart;
            SavedStatus status = SavedStatus.Idle;
            if (isPrinting) status = SavedStatus.Printing;
            else if (_completedOutput != null) status = SavedStatus.Completed;

            if (part == null && status == SavedStatus.Completed)
                part = GetSelectedPart();

            return new PrinterSaveState
            {
                persistenceId = persistenceId,
                sceneName = gameObject.scene.name,
                status = (int)status,
                itemId = part != null ? part.Id : string.Empty,
                elapsedSeconds = Mathf.Max(0f, _printTimer),
                selectedPartIndex = _selectedPartIndex
            };
        }

        private void RestoreFromSave()
        {
            SaveService save = Bootstrapper.Instance != null
                ? Bootstrapper.Instance.SaveService
                : FindAnyObjectByType<SaveService>();
            PrinterSaveState state = save?.GetPrinterState(persistenceId, SceneManager.GetActiveScene().name);
            if (state == null) return;

            int partCount = printableParts != null ? printableParts.Length : 0;
            _selectedPartIndex = Mathf.Clamp(state.selectedPartIndex, 0, Mathf.Max(0, partCount - 1));
            PrintablePartDefinition savedPart = FindPartById(state.itemId);
            if (savedPart != null)
            {
                _currentPart = savedPart;
                for (int i = 0; i < partCount; i++)
                    if (printableParts[i] == savedPart) _selectedPartIndex = i;
            }

            SavedStatus status = (SavedStatus)Mathf.Clamp(state.status, 0, 2);
            if (status == SavedStatus.Printing && _currentPart != null)
            {
                _printTimer = Mathf.Clamp(state.elapsedSeconds, 0f, GetCurrentPrintDuration());
                isPrinting = true;
                if (interactableObject != null) interactableObject.SetInteractionText(printingText);
                SetInteractionAvailable(true, true);
                animationView?.BeginPrintVisuals();
                CreatePrintingPreview();
                animationView?.UpdatePrintVisuals(_printTimer, GetCurrentPrintDuration());
                UpdatePrintedObjectReveal(_printTimer / GetCurrentPrintDuration());
                if (_printTimer >= GetCurrentPrintDuration()) CompletePrint();
            }
            else if (status == SavedStatus.Completed && _currentPart != null)
            {
                isPrinting = false;
                CreatePrintingPreview();
                CompletePrintingPreview();
                animationView?.FinishPrintVisuals(true);
                _currentPart = null;
                SetInteractionAvailable(false, false);
            }
            else
            {
                _currentPart = null;
                _printTimer = 0f;
                SetIdleState();
            }
        }

        private PrintablePartDefinition FindPartById(string itemId)
        {
            if (printableParts == null || string.IsNullOrWhiteSpace(itemId)) return null;
            for (int i = 0; i < printableParts.Length; i++)
                if (printableParts[i] != null && printableParts[i].Id == itemId)
                    return printableParts[i];
            return null;
        }

        private static void SetOutputComponentsEnabled(GameObject output, bool enabled)
        {
            if (output == null) return;

            Collider[] colliders = output.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = enabled;

            MonoBehaviour[] behaviours = output.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++) behaviours[i].enabled = enabled;
        }

        private void AddButtonListeners()
        {
            if (backProjectButton != null) backProjectButton.onClick.AddListener(SelectPreviousPart);
            if (nextProjectButton != null) nextProjectButton.onClick.AddListener(SelectNextPart);
            if (printProjectButton != null) printProjectButton.onClick.AddListener(ConfirmSelectedPartAndPrint);
            if (exitProjectButton != null) exitProjectButton.onClick.AddListener(CloseProjectSelection);
        }

        private void RemoveButtonListeners()
        {
            if (backProjectButton != null) backProjectButton.onClick.RemoveListener(SelectPreviousPart);
            if (nextProjectButton != null) nextProjectButton.onClick.RemoveListener(SelectNextPart);
            if (printProjectButton != null) printProjectButton.onClick.RemoveListener(ConfirmSelectedPartAndPrint);
            if (exitProjectButton != null) exitProjectButton.onClick.RemoveListener(CloseProjectSelection);
        }
    }
}
