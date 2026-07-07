using System.Collections;
using TMPro;
using UnityEngine;

namespace JuegoCriminal.World
{
    public sealed class PropertyVisual : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TMP_Text signText;
        [SerializeField] private GameObject signRoot;

        [Header("Labels")]
        [SerializeField] private string forSaleLabel = "FOR SALE";
        [SerializeField] private string soldLabel = "SOLD";

        [Header("Behavior")]
        [SerializeField] private float hideAfterSoldSeconds = 2f;

        private Coroutine _hideRoutine;

        private void Awake()
        {
            if (signText == null)
                signText = GetComponentInChildren<TMP_Text>(true);

            if (signRoot == null)
                signRoot = gameObject;
        }

        public void ShowForSale()
        {
            StopHideRoutine();

            if (signRoot != null)
                signRoot.SetActive(true);

            SetText(forSaleLabel);
        }

        public void ShowSold()
        {
            StopHideRoutine();

            if (signRoot != null)
                signRoot.SetActive(true);

            SetText(soldLabel);

            if (hideAfterSoldSeconds > 0f)
                _hideRoutine = StartCoroutine(HideAfterDelay(hideAfterSoldSeconds));
        }

        public void Hide()
        {
            StopHideRoutine();

            if (signRoot != null)
                signRoot.SetActive(false);
        }

        public void SetText(string text)
        {
            if (signText != null)
                signText.text = text;
        }

        private IEnumerator HideAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (signRoot != null)
                signRoot.SetActive(false);

            _hideRoutine = null;
        }

        private void StopHideRoutine()
        {
            if (_hideRoutine == null)
                return;

            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }
    }
}