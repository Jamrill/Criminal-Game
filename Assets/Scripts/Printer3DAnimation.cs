using System;
using UnityEngine;
using UnityEngine.UI;
using JuegoCriminal.Interaction;

public class Printer3DAnimation : MonoBehaviour
{
    [Serializable]
    public sealed class PrintProject
    {
        [Header("Project Info")]
        public string projectName = "New Print Project";

        [Tooltip("Imagen que se mostrará en la pantalla/canvas de la impresora.")]
        public Sprite previewSprite;

        [Tooltip("Prefab que aparecerá al terminar correctamente la impresión.")]
        public GameObject completedPrintPrefab;

        [Tooltip("Si es mayor que 0, este proyecto usará esta duración en lugar de la duración general.")]
        public float customPrintDuration = -1f;
    }

    [Header("State")]
    [SerializeField] private bool isPrinting;

    [Header("References")]
    [SerializeField] private InteractableObject interactableObject;

    [Header("Camera Selection Mode")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Camera printerCamera;

    [Tooltip("Objeto raíz de la UI de selección de la impresora. Puede ser Screen_Info_canvas o un panel dentro de él.")]
    [SerializeField] private GameObject printerSelectionRoot;

    [Tooltip("Scripts del jugador/cámara que quieres desactivar mientras se elige un proyecto.")]
    [SerializeField] private MonoBehaviour[] disableWhileSelecting;

    [Header("Printer Selection UI")]
    [SerializeField] private Image projectPreviewImage;
    [SerializeField] private Button backProjectButton;
    [SerializeField] private Button nextProjectButton;
    [SerializeField] private Button printProjectButton;

    [Header("Available Print Projects")]
    [SerializeField] private PrintProject[] printProjects;

    [Header("Parts")]
    [SerializeField] private Transform basePart;
    [SerializeField] private Transform headPart;
    [SerializeField] private Transform railsPart;

    [Header("Print Duration")]
    [SerializeField] private float printDuration = 8f;

    [Header("Initial Drop - Head And Rails")]
    [SerializeField] private float initialDropDuration = 1f;
    [SerializeField] private float initialDropY = -0.25f;

    [Header("Slow Rise While Printing")]
    [SerializeField] private float slowRiseY = 0.18f;

    [Header("Head Horizontal Movement")]
    [SerializeField] private float headHorizontalSpeed = 6f;
    [SerializeField] private float headMoveX = 0.35f;

    [Header("Base Movement")]
    [SerializeField] private float baseSpeed = 4f;
    [SerializeField] private float baseMoveZ = 0.12f;

    [Header("Return To Initial Position")]
    [SerializeField] private float returnSpeed = 4f;

    [Header("Prompt Text")]
    [SerializeField] private string idleText = "Print";
    [SerializeField] private string printingText = "Cancel?";

    [Header("Progress Bar")]
    [SerializeField] private GameObject progressRoot;
    [SerializeField] private Slider progressSlider;

    [Header("Printed Result")]
    [SerializeField] private Transform resultSpawnPoint;
    [SerializeField] private GameObject failedPrintPrefab;

    [Header("Print Line")]
    [SerializeField] private LineRenderer printLine;
    [SerializeField] private Transform lineStartPoint;
    [SerializeField] private Transform lineEndPoint;

    private Vector3 baseStartLocalPosition;
    private Vector3 headStartLocalPosition;
    private Vector3 railsStartLocalPosition;

    private float printTimer;
    private bool wasPrinting;
    private bool isReturningToStart;

    private int selectedProjectIndex;
    private PrintProject currentPrintProject;

    private bool isSelectingProject;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

    private void Reset()
    {
        interactableObject = GetComponent<InteractableObject>();
    }

    private void Awake()
    {
        if (interactableObject == null)
            interactableObject = GetComponent<InteractableObject>();

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main;

        if (basePart != null)
            baseStartLocalPosition = basePart.localPosition;

        if (headPart != null)
            headStartLocalPosition = headPart.localPosition;

        if (railsPart != null)
            railsStartLocalPosition = railsPart.localPosition;

        SetupSelectionButtons();
        SetupSelectionCamera();
        SetupLineRenderer();
        SetupProgressBar();

        wasPrinting = isPrinting;

        if (isPrinting)
            BeginPrint();
        else
            SetIdleState();
    }

    private void Update()
    {
        DetectPrintingStateChange();

        if (isPrinting)
        {
            AnimatePrinting();
        }
        else if (isReturningToStart)
        {
            ReturnSmoothlyToStart();
        }

        UpdatePrintLine();
    }

    private void SetupSelectionButtons()
    {
        if (backProjectButton != null)
        {
            backProjectButton.onClick.RemoveAllListeners();
            backProjectButton.onClick.AddListener(SelectPreviousProject);
        }

        if (nextProjectButton != null)
        {
            nextProjectButton.onClick.RemoveAllListeners();
            nextProjectButton.onClick.AddListener(SelectNextProject);
        }

        if (printProjectButton != null)
        {
            printProjectButton.onClick.RemoveAllListeners();
            printProjectButton.onClick.AddListener(ConfirmSelectedProjectAndPrint);
        }
    }

    private void SetupSelectionCamera()
    {
        if (printerSelectionRoot != null)
            printerSelectionRoot.SetActive(false);

        if (printerCamera != null)
            printerCamera.gameObject.SetActive(false);

        if (playerCamera != null)
            playerCamera.gameObject.SetActive(true);
    }

    private void DetectPrintingStateChange()
    {
        if (isPrinting == wasPrinting)
            return;

        if (isPrinting)
            BeginPrint();
        else
            BeginReturnToStart();

        wasPrinting = isPrinting;
    }

    /// <summary>
    /// Método recomendado para llamar desde InteractableObject -> On Interact.
    /// Si la impresora está parada, abre la pantalla de selección.
    /// Si está imprimiendo, cancela la impresión.
    /// </summary>
    public void InteractWithPrinter()
    {
        if (isPrinting)
        {
            CancelPrint();
            return;
        }

        OpenProjectSelection();
    }

    /// <summary>
    /// Compatibilidad con tu configuración anterior.
    /// Si todavía llamas a StartPrint desde el evento, ahora abre el menú de selección.
    /// </summary>
    public void StartPrint()
    {
        InteractWithPrinter();
    }

    public void StartPrinting()
    {
        InteractWithPrinter();
    }

    public void StopPrinting()
    {
        CancelPrint();
    }

    public void OpenProjectSelection()
    {
        if (isPrinting)
            return;

        if (printProjects == null || printProjects.Length == 0)
        {
            Debug.LogWarning("[Printer3DAnimation] No hay proyectos de impresión configurados.", this);
            return;
        }

        isSelectingProject = true;

        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        SetPlayerControlEnabled(false);
        SetCameraMode(usePrinterCamera: true);

        if (printerSelectionRoot != null)
            printerSelectionRoot.SetActive(true);

        selectedProjectIndex = Mathf.Clamp(selectedProjectIndex, 0, printProjects.Length - 1);
        RefreshSelectedProjectUI();

        if (interactableObject != null)
        {
            interactableObject.SetCanInteract(false);
            interactableObject.SetCanShowPrompt(false);
        }
    }

    public void CloseProjectSelection()
    {
        isSelectingProject = false;

        if (printerSelectionRoot != null)
            printerSelectionRoot.SetActive(false);

        SetCameraMode(usePrinterCamera: false);
        SetPlayerControlEnabled(true);

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;

        if (!isPrinting)
            SetIdleState();
    }

    private void SelectPreviousProject()
    {
        if (!isSelectingProject)
            return;

        if (printProjects == null || printProjects.Length == 0)
            return;

        selectedProjectIndex--;

        if (selectedProjectIndex < 0)
            selectedProjectIndex = printProjects.Length - 1;

        RefreshSelectedProjectUI();
    }

    private void SelectNextProject()
    {
        if (!isSelectingProject)
            return;

        if (printProjects == null || printProjects.Length == 0)
            return;

        selectedProjectIndex++;

        if (selectedProjectIndex >= printProjects.Length)
            selectedProjectIndex = 0;

        RefreshSelectedProjectUI();
    }

    private void ConfirmSelectedProjectAndPrint()
    {
        if (!isSelectingProject)
            return;

        if (printProjects == null || printProjects.Length == 0)
            return;

        selectedProjectIndex = Mathf.Clamp(selectedProjectIndex, 0, printProjects.Length - 1);
        currentPrintProject = printProjects[selectedProjectIndex];

        CloseProjectSelection();
        BeginPrint();
    }

    private void RefreshSelectedProjectUI()
    {
        if (printProjects == null || printProjects.Length == 0)
        {
            if (projectPreviewImage != null)
            {
                projectPreviewImage.sprite = null;
                projectPreviewImage.enabled = false;
            }

            return;
        }

        selectedProjectIndex = Mathf.Clamp(selectedProjectIndex, 0, printProjects.Length - 1);

        PrintProject project = printProjects[selectedProjectIndex];

        if (projectPreviewImage != null)
        {
            projectPreviewImage.sprite = project != null ? project.previewSprite : null;
            projectPreviewImage.enabled = project != null && project.previewSprite != null;
        }
    }

    private void SetCameraMode(bool usePrinterCamera)
    {
        if (playerCamera != null)
            playerCamera.gameObject.SetActive(!usePrinterCamera);

        if (printerCamera != null)
            printerCamera.gameObject.SetActive(usePrinterCamera);
    }

    private void SetPlayerControlEnabled(bool enabled)
    {
        if (disableWhileSelecting == null)
            return;

        for (int i = 0; i < disableWhileSelecting.Length; i++)
        {
            if (disableWhileSelecting[i] != null)
                disableWhileSelecting[i].enabled = enabled;
        }
    }

    public void CancelPrint()
    {
        if (!isPrinting)
            return;

        isPrinting = false;
        wasPrinting = false;

        SpawnFailedPrint();
        BeginReturnToStart();
        SetIdleState();
    }

    private void BeginPrint()
    {
        printTimer = 0f;
        isPrinting = true;
        wasPrinting = true;
        isReturningToStart = false;

        if (currentPrintProject == null)
            currentPrintProject = GetSelectedProjectOrNull();

        if (interactableObject != null)
        {
            interactableObject.SetInteractionText(printingText);
            interactableObject.SetCanInteract(true);
            interactableObject.SetCanShowPrompt(true);
        }

        ShowProgressBar(true);
        SetProgress(0f);
    }

    private void CompletePrint()
    {
        isPrinting = false;
        wasPrinting = false;

        SpawnCompletedPrint();

        currentPrintProject = null;

        BeginReturnToStart();
        SetIdleState();

        SetProgress(1f);
        ShowProgressBar(false);
    }

    private void BeginReturnToStart()
    {
        isReturningToStart = true;
    }

    private void SetIdleState()
    {
        if (interactableObject != null)
        {
            interactableObject.SetInteractionText(idleText);
            interactableObject.SetCanInteract(true);
            interactableObject.SetCanShowPrompt(true);
        }

        ShowProgressBar(false);
    }

    private void AnimatePrinting()
    {
        printTimer += Time.deltaTime;

        float duration = GetCurrentPrintDuration();
        float totalProgress = Mathf.Clamp01(printTimer / duration);

        SetProgress(totalProgress);

        if (totalProgress >= 1f)
        {
            CompletePrint();
            return;
        }

        float dropProgress = Mathf.Clamp01(printTimer / initialDropDuration);
        dropProgress = Mathf.SmoothStep(0f, 1f, dropProgress);

        float dropOffset = initialDropY * dropProgress;

        bool initialDropFinished = printTimer >= initialDropDuration;

        float printPhaseTimer = Mathf.Max(0f, printTimer - initialDropDuration);

        float printPhaseDuration = Mathf.Max(0.01f, duration - initialDropDuration);
        float printPhaseProgress = Mathf.Clamp01(printPhaseTimer / printPhaseDuration);

        float riseOffset = initialDropFinished
            ? slowRiseY * printPhaseProgress
            : 0f;

        float verticalOffsetY = dropOffset + riseOffset;

        float headHorizontal = initialDropFinished
            ? Mathf.Sin(printPhaseTimer * headHorizontalSpeed)
            : 0f;

        float baseMovement = initialDropFinished
            ? Mathf.Sin(printPhaseTimer * baseSpeed)
            : 0f;

        if (headPart != null)
        {
            Vector3 horizontalOffset = new Vector3(headMoveX * headHorizontal, 0f, 0f);
            Vector3 verticalOffset = new Vector3(0f, verticalOffsetY, 0f);

            headPart.localPosition =
                headStartLocalPosition +
                horizontalOffset +
                verticalOffset;
        }

        if (railsPart != null)
        {
            Vector3 verticalOffset = new Vector3(0f, verticalOffsetY, 0f);

            railsPart.localPosition =
                railsStartLocalPosition +
                verticalOffset;
        }

        if (basePart != null)
        {
            Vector3 baseOffset = new Vector3(0f, 0f, baseMoveZ * baseMovement);

            basePart.localPosition =
                baseStartLocalPosition +
                baseOffset;
        }
    }

    private float GetCurrentPrintDuration()
    {
        if (currentPrintProject != null && currentPrintProject.customPrintDuration > 0f)
            return currentPrintProject.customPrintDuration;

        return Mathf.Max(0.01f, printDuration);
    }

    private PrintProject GetSelectedProjectOrNull()
    {
        if (printProjects == null || printProjects.Length == 0)
            return null;

        selectedProjectIndex = Mathf.Clamp(selectedProjectIndex, 0, printProjects.Length - 1);
        return printProjects[selectedProjectIndex];
    }

    private void ReturnSmoothlyToStart()
    {
        bool baseReturned = MovePartToStart(basePart, baseStartLocalPosition);
        bool headReturned = MovePartToStart(headPart, headStartLocalPosition);
        bool railsReturned = MovePartToStart(railsPart, railsStartLocalPosition);

        if (baseReturned && headReturned && railsReturned)
        {
            isReturningToStart = false;
            printTimer = 0f;
        }
    }

    private bool MovePartToStart(Transform part, Vector3 startLocalPosition)
    {
        if (part == null)
            return true;

        part.localPosition = Vector3.Lerp(
            part.localPosition,
            startLocalPosition,
            Time.deltaTime * returnSpeed
        );

        float distance = Vector3.Distance(part.localPosition, startLocalPosition);

        if (distance <= 0.001f)
        {
            part.localPosition = startLocalPosition;
            return true;
        }

        return false;
    }

    private void SpawnFailedPrint()
    {
        if (failedPrintPrefab == null)
            return;

        Transform spawn = resultSpawnPoint != null ? resultSpawnPoint : transform;

        Instantiate(
            failedPrintPrefab,
            spawn.position,
            spawn.rotation
        );
    }

    private void SpawnCompletedPrint()
    {
        GameObject prefabToSpawn = null;

        if (currentPrintProject != null)
            prefabToSpawn = currentPrintProject.completedPrintPrefab;

        if (prefabToSpawn == null)
            return;

        Transform spawn = resultSpawnPoint != null ? resultSpawnPoint : transform;

        Instantiate(
            prefabToSpawn,
            spawn.position,
            spawn.rotation
        );
    }

    private void SetupProgressBar()
    {
        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = 0f;
        }

        ShowProgressBar(false);
    }

    private void SetProgress(float value)
    {
        if (progressSlider != null)
            progressSlider.value = value;
    }

    private void ShowProgressBar(bool show)
    {
        if (progressRoot != null)
            progressRoot.SetActive(show);
    }

    private void SetupLineRenderer()
    {
        if (printLine == null)
            return;

        printLine.positionCount = 2;
        printLine.useWorldSpace = true;
    }

    private void UpdatePrintLine()
    {
        if (printLine == null || lineStartPoint == null || lineEndPoint == null)
            return;

        printLine.SetPosition(0, lineStartPoint.position);
        printLine.SetPosition(1, lineEndPoint.position);
    }
}