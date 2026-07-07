using UnityEngine;
using UnityEngine.UI;
using JuegoCriminal.Interaction;

public class Printer3DAnimation : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private bool isPrinting;

    [Header("References")]
    [SerializeField] private InteractableObject interactableObject;

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
    [SerializeField] private GameObject completedPrintPrefab;

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

    private void Reset()
    {
        interactableObject = GetComponent<InteractableObject>();
    }

    private void Awake()
    {
        if (interactableObject == null)
            interactableObject = GetComponent<InteractableObject>();

        if (basePart != null)
            baseStartLocalPosition = basePart.localPosition;

        if (headPart != null)
            headStartLocalPosition = headPart.localPosition;

        if (railsPart != null)
            railsStartLocalPosition = railsPart.localPosition;

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
    /// Este es el método que debes llamar desde InteractableObject -> On Interact.
    /// Si está parada, empieza.
    /// Si está imprimiendo, cancela.
    /// </summary>
    public void StartPrint()
    {
        if (isPrinting)
        {
            CancelPrint();
            return;
        }

        BeginPrint();
    }

    public void StartPrinting()
    {
        StartPrint();
    }

    public void StopPrinting()
    {
        CancelPrint();
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

        float totalProgress = Mathf.Clamp01(printTimer / printDuration);
        SetProgress(totalProgress);

        if (totalProgress >= 1f)
        {
            CompletePrint();
            return;
        }

        // -------------------------
        // FASE 1: bajada inicial
        // -------------------------

        float dropProgress = Mathf.Clamp01(printTimer / initialDropDuration);
        dropProgress = Mathf.SmoothStep(0f, 1f, dropProgress);

        float dropOffset = initialDropY * dropProgress;

        bool initialDropFinished = printTimer >= initialDropDuration;

        // -------------------------
        // FASE 2: impresión real
        // -------------------------

        float printPhaseTimer = Mathf.Max(0f, printTimer - initialDropDuration);

        float printPhaseDuration = Mathf.Max(0.01f, printDuration - initialDropDuration);
        float printPhaseProgress = Mathf.Clamp01(printPhaseTimer / printPhaseDuration);

        // Solo sube lentamente después de haber llegado abajo.
        float riseOffset = initialDropFinished
            ? slowRiseY * printPhaseProgress
            : 0f;

        float verticalOffsetY = dropOffset + riseOffset;

        // El cabezal y la base solo se mueven cuando la bajada inicial ha terminado.
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
        if (completedPrintPrefab == null)
            return;

        Transform spawn = resultSpawnPoint != null ? resultSpawnPoint : transform;

        Instantiate(
            completedPrintPrefab,
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