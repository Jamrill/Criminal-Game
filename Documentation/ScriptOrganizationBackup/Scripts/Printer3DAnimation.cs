using UnityEngine;
using UnityEngine.UI;

/// <summary>Representa únicamente el movimiento y progreso visual de una impresora.</summary>
public sealed class Printer3DAnimation : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private Transform basePart;
    [SerializeField] private Transform headPart;
    [SerializeField] private Transform railsPart;
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
    [Header("Progress Bar")]
    [SerializeField] private GameObject progressRoot;
    [SerializeField] private Image progressFillImage;
    [Header("Print Line")]
    [SerializeField] private LineRenderer printLine;
    [SerializeField] private Transform lineStartPoint;
    [SerializeField] private Transform lineEndPoint;

    private Vector3 _baseStart;
    private Vector3 _headStart;
    private Vector3 _railsStart;
    private bool _returning;

    private void Awake()
    {
        if (basePart != null) _baseStart = basePart.localPosition;
        if (headPart != null) _headStart = headPart.localPosition;
        if (railsPart != null) _railsStart = railsPart.localPosition;

        if (progressFillImage != null)
        {
            progressFillImage.type = Image.Type.Filled;
            progressFillImage.fillMethod = Image.FillMethod.Horizontal;
            progressFillImage.fillOrigin = (int)Image.OriginHorizontal.Right;
            progressFillImage.fillClockwise = true;
            progressFillImage.fillAmount = 0f;
        }
        if (progressRoot != null) progressRoot.SetActive(false);
        if (printLine != null)
        {
            printLine.positionCount = 2;
            printLine.useWorldSpace = true;
        }
    }

    private void Update()
    {
        if (_returning) ReturnToStart();
        UpdatePrintLine();
    }

    public void BeginPrintVisuals()
    {
        _returning = false;
        SetProgress(0f);
        ShowProgress(true);
    }

    public void UpdatePrintVisuals(float elapsed, float totalDuration)
    {
        float duration = Mathf.Max(0.01f, totalDuration);
        SetProgress(elapsed / duration);

        float dropDuration = Mathf.Max(0.01f, initialDropDuration);
        float dropProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / dropDuration));
        bool dropFinished = elapsed >= dropDuration;
        float phaseTime = Mathf.Max(0f, elapsed - dropDuration);
        float phaseProgress = Mathf.Clamp01(phaseTime / Mathf.Max(0.01f, duration - dropDuration));
        float y = initialDropY * dropProgress + (dropFinished ? slowRiseY * phaseProgress : 0f);
        float headX = dropFinished ? Mathf.Sin(phaseTime * headHorizontalSpeed) * headMoveX : 0f;
        float baseZ = dropFinished ? Mathf.Sin(phaseTime * baseSpeed) * baseMoveZ : 0f;

        if (headPart != null) headPart.localPosition = _headStart + new Vector3(headX, y, 0f);
        if (railsPart != null) railsPart.localPosition = _railsStart + new Vector3(0f, y, 0f);
        if (basePart != null) basePart.localPosition = _baseStart + new Vector3(0f, 0f, baseZ);
    }

    public void FinishPrintVisuals(bool completed)
    {
        if (completed) SetProgress(1f);
        ShowProgress(false);
        _returning = true;
    }

    private void ReturnToStart()
    {
        bool a = MoveToStart(basePart, _baseStart);
        bool b = MoveToStart(headPart, _headStart);
        bool c = MoveToStart(railsPart, _railsStart);
        _returning = !(a && b && c);
    }

    private bool MoveToStart(Transform part, Vector3 target)
    {
        if (part == null) return true;
        part.localPosition = Vector3.Lerp(part.localPosition, target, Time.deltaTime * Mathf.Max(0.01f, returnSpeed));
        if (Vector3.Distance(part.localPosition, target) > 0.001f) return false;
        part.localPosition = target;
        return true;
    }

    private void SetProgress(float value)
    {
        if (progressFillImage != null) progressFillImage.fillAmount = Mathf.Clamp01(value);
    }

    private void ShowProgress(bool show)
    {
        if (progressRoot != null) progressRoot.SetActive(show);
    }

    private void UpdatePrintLine()
    {
        if (printLine == null || lineStartPoint == null || lineEndPoint == null) return;
        printLine.SetPosition(0, lineStartPoint.position);
        printLine.SetPosition(1, lineEndPoint.position);
    }
}
