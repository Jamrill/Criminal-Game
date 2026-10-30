using TMPro;
using UnityEngine;
using JuegoCriminal.Environment;
using JuegoCriminal.Services;
using JuegoCriminal.Inventory;

namespace JuegoCriminal.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class GameplayHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text moneyText;
        [SerializeField] private bool visibleOnlyWithMenus;
        private CanvasGroup group;
        private DayNightCycle clock;
        private EconomyService economy;
        private InventoryMenuUI inventory;
        private PauseMenuUI pause;
        private float nextSearch;
        public void Configure(TMP_Text time, TMP_Text money) { timeText = time; moneyText = money; }
        private void Awake() { group = GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false; }
        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextSearch)
            {
                if (!clock) clock = FindAnyObjectByType<DayNightCycle>();
                if (!economy) economy = FindAnyObjectByType<EconomyService>();
                if (!inventory) inventory = FindAnyObjectByType<InventoryMenuUI>();
                if (!pause) pause = FindAnyObjectByType<PauseMenuUI>();
                nextSearch = Time.unscaledTime + .5f;
            }
            bool menuOpen = (pause && pause.IsOpen) || (inventory && inventory.IsOpen);
            group.alpha = (visibleOnlyWithMenus ? menuOpen : !menuOpen) ? 1 : 0;
            if (timeText && clock)
            {
                int minutes = Mathf.FloorToInt(Mathf.Repeat(clock.CurrentHour, 24) * 60);
                timeText.SetText("{0:00}:{1:00}", minutes / 60, minutes % 60);
            }
            if (moneyText) moneyText.text = economy ? "$ " + economy.Money.ToString("N0") : "$ —";
        }
    }
}
