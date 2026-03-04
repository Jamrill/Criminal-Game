using TMPro;
using UnityEngine;
using JuegoCriminal.Services;

namespace JuegoCriminal.UI
{
    public sealed class MoneyHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text moneyText;

        private EconomyService _economy;

        private void Awake()
        {
            _economy = FindAnyObjectByType<EconomyService>();
            if (_economy != null)
                _economy.OnMoneyChanged += UpdateText;
        }

        private void Start()
        {
            if (_economy != null)
                UpdateText(_economy.Money);
        }

        private void OnDestroy()
        {
            if (_economy != null)
                _economy.OnMoneyChanged -= UpdateText;
        }

        private void UpdateText(int money)
        {
            if (moneyText != null)
                moneyText.text = $"Cash: ${money}";
        }
    }
}