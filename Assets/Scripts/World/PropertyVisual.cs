using System.Collections;
using TMPro;
using UnityEngine;
using JuegoCriminal.Services;

namespace JuegoCriminal.World
{
    public sealed class PropertyVisual : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private PropertyMarker marker;
        [SerializeField] private TMP_Text forSaleText;          // el TMP del cartel (Se vende)
        [SerializeField] private GameObject signRoot;           // el GO del cartel entero (para ocultarlo)

        [Header("Behavior")]
        [SerializeField] private string forSaleLabel = "FOR SALE";
        [SerializeField] private string soldLabel = "SOLD";
        [SerializeField] private float hideAfterSeconds = 2.0f;

        private PropertyService _properties;
        private Coroutine _hideRoutine;

        private void Awake()
        {
            if (marker == null) marker = GetComponentInParent<PropertyMarker>();
            _properties = FindAnyObjectByType<PropertyService>();

            // Si no lo asignas, intenta encontrarlo en hijos
            if (forSaleText == null) forSaleText = GetComponentInChildren<TMP_Text>(true);
            if (signRoot == null) signRoot = gameObject;
        }

        private void Start()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (marker == null || _properties == null) return;

            bool owned = _properties.IsOwned(marker.propertyId);

            // Si ya estaba corriendo una coroutine, la paramos
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            if (!owned)
            {
                if (signRoot != null) signRoot.SetActive(true);
                if (forSaleText != null) forSaleText.text = forSaleLabel;
                return;
            }

            // Owned
            if (forSaleText != null) forSaleText.text = soldLabel;

            if (hideAfterSeconds > 0f && signRoot != null)
                _hideRoutine = StartCoroutine(HideAfter());
        }

        private IEnumerator HideAfter()
        {
            yield return new WaitForSeconds(hideAfterSeconds);
            if (signRoot != null) signRoot.SetActive(false);
        }
    }
}