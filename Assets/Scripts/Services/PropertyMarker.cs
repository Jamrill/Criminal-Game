using UnityEngine;
using JuegoCriminal.Interaction;
using JuegoCriminal.Services;
using JuegoCriminal.Core;
using System.Collections;
using TMPro;

namespace JuegoCriminal.World
{
    public sealed class PropertyMarker : MonoBehaviour
    {
        [Header("Property Data")]
        public int propertyId = 1;
        public int price = 500;

        [Header("Refs")]
        [SerializeField] private InteractableObject interactableObject;
        [SerializeField] private TMP_Text signText;
        [SerializeField] private GameObject signRoot;

        [Header("Sign Visual")]
        [SerializeField] private string forSaleLabel = "FOR SALE";
        [SerializeField] private string soldLabel = "SOLD";
        [SerializeField, Min(0f)] private float hideAfterSoldSeconds = 2f;

        [Header("Prompt Text")]
        [SerializeField] private string buyText = "Buy";
        [SerializeField] private string ownedText = "Owned";
        [SerializeField] private string needMoneyText = "Need";

        [Header("Debug")]
        [SerializeField] private bool debugLogs;

        private EconomyService _economy;
        private PropertyService _properties;
        private Coroutine _hideRoutine;

        public bool IsOwned
        {
            get
            {
                EnsureServices();
                return _properties != null && _properties.IsOwned(propertyId);
            }
        }

        private void Reset()
        {
            interactableObject = GetComponent<InteractableObject>();
            signText = GetComponentInChildren<TMP_Text>(true);
            signRoot = gameObject;
        }

        private void Awake()
        {
            if (interactableObject == null)
                interactableObject = GetComponent<InteractableObject>();

            if (signText == null)
                signText = GetComponentInChildren<TMP_Text>(true);
            if (signRoot == null)
                signRoot = gameObject;

            EnsureServices();
        }

        private void OnEnable()
        {
            EnsureServices();

            if (_economy != null)
                _economy.OnMoneyChanged += HandleMoneyChanged;

            if (interactableObject != null)
                interactableObject.AddInteractionListener(BuyFromInteraction);
        }

        private void OnDisable()
        {
            if (_economy != null)
                _economy.OnMoneyChanged -= HandleMoneyChanged;

            if (interactableObject != null)
                interactableObject.RemoveInteractionListener(BuyFromInteraction);
        }

        private void Start()
        {
            RefreshState();
        }

        private void EnsureServices()
        {
            if (_economy == null)
            {
                Bootstrapper app = Bootstrapper.Instance;
                _economy = app != null ? app.EconomyService : null;

                if (_economy == null)
                    _economy = FindAnyObjectByType<EconomyService>();
            }

            if (_properties == null)
            {
                Bootstrapper app = Bootstrapper.Instance;
                _properties = app != null ? app.PropertyService : null;

                if (_properties == null)
                    _properties = FindAnyObjectByType<PropertyService>();
            }
        }

        private void HandleMoneyChanged(int money)
        {
            RefreshState();
        }

        public void BuyFromInteraction()
        {
            TryBuy();
        }

        public bool TryBuy()
        {
            EnsureServices();

            if (_economy == null || _properties == null)
            {
                if (debugLogs)
                    Debug.LogWarning($"PropertyMarker: faltan servicios en {name}.", this);

                return false;
            }

            if (_properties.IsOwned(propertyId))
            {
                RefreshState();
                return false;
            }

            if (!_economy.TrySpend(price))
            {
                RefreshState();
                return false;
            }

            _properties.AddOwned(propertyId);

            if (debugLogs)
                Debug.Log($"PropertyMarker: propiedad {propertyId} comprada por ${price}.", this);

            RefreshState();

            return true;
        }

        public void RefreshState()
        {
            EnsureServices();

            bool owned = IsOwned;
            int money = _economy != null ? _economy.Money : 0;
            bool canAfford = money >= price;

            if (owned) ShowSold();
            else ShowForSale();

            if (interactableObject == null)
                return;

            if (owned)
            {
                interactableObject.SetInteractionText(ownedText);
                interactableObject.SetCanInteract(false);
                interactableObject.SetCanShowPrompt(false);
                return;
            }

            interactableObject.SetCanShowPrompt(true);

            if (canAfford)
            {
                interactableObject.SetInteractionText($"{buyText} ${price}");
                interactableObject.SetCanInteract(true);
            }
            else
            {
                interactableObject.SetInteractionText($"{needMoneyText} ${price}");
                interactableObject.SetCanInteract(false);
            }
        }

        private void ShowForSale()
        {
            StopHideRoutine();
            if (signRoot != null) signRoot.SetActive(true);
            if (signText != null) signText.text = forSaleLabel;
        }

        private void ShowSold()
        {
            StopHideRoutine();
            if (signRoot != null) signRoot.SetActive(true);
            if (signText != null) signText.text = soldLabel;
            if (hideAfterSoldSeconds > 0f)
                _hideRoutine = StartCoroutine(HideAfterSoldDelay());
        }

        private IEnumerator HideAfterSoldDelay()
        {
            yield return new WaitForSeconds(hideAfterSoldSeconds);
            if (signRoot != null) signRoot.SetActive(false);
            _hideRoutine = null;
        }

        private void StopHideRoutine()
        {
            if (_hideRoutine == null) return;
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }
    }
}
