using TMPro;
using UnityEngine;
using JuegoCriminal.Services;
using JuegoCriminal.Core;

namespace JuegoCriminal.UI
{
    public sealed class MoneyHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text moneyText;

        private EconomyService _economy;

        private void Awake()
        {
            Bootstrapper app = Bootstrapper.Instance;
            _economy = app != null ? app.EconomyService : null;

            if (_economy == null)
                _economy = FindAnyObjectByType<EconomyService>();
            if (_economy != null)
                _economy.OnMoneyChanged += UpdateText;
        }

        private void Start()
        {
            if (_economy != null)
                UpdateText(_economy.Money);
        }
        private void OnEnable(){JuegoCriminal.Localization.GameLanguage.Changed+=RefreshLanguage;RefreshLanguage();}
        private void OnDisable()=>JuegoCriminal.Localization.GameLanguage.Changed-=RefreshLanguage;
        private void RefreshLanguage(){if(_economy)UpdateText(_economy.Money);}

        private void OnDestroy()
        {
            if (_economy != null)
                _economy.OnMoneyChanged -= UpdateText;
        }

        private void UpdateText(int money)
        {
            if (moneyText != null)
                moneyText.text = JuegoCriminal.Localization.GameLanguage.T("Cash: ${0}","Dinero: ${0}",money);
        }
    }
}
