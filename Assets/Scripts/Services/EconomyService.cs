using System;
using UnityEngine;

namespace JuegoCriminal.Services
{
    public sealed class EconomyService : MonoBehaviour
    {
        public event Action<int> OnMoneyChanged;

        private SaveService _save;

        public int Money => _save != null ? _save.CurrentMoney : 0;

        private void Awake()
        {
            _save = GetComponent<SaveService>();
        }

        public void SyncFromSave()
        {
            // Fuerza refresh del HUD al cargar escena / cargar slot
            OnMoneyChanged?.Invoke(Money);
        }

        public bool CanAfford(int amount) => Money >= amount;

        public bool TrySpend(int amount)
        {
            if (_save == null || !_save.TrySpendMoney(amount, out int remainingMoney))
                return false;

            OnMoneyChanged?.Invoke(remainingMoney);

            // Guardado inmediato opcional: de momento NO lo hacemos aquí.
            // Lo guardas cuando el jugador pulse Save en el PauseMenu.
            return true;
        }

        public void AddMoney(int amount)
        {
            if (_save == null)
                return;

            int currentMoney = _save.AddMoney(amount);
            OnMoneyChanged?.Invoke(currentMoney);
        }
    }
}
