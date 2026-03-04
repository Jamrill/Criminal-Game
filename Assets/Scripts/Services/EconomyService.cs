using System;
using UnityEngine;
using JuegoCriminal.Services;

namespace JuegoCriminal.Services
{
    public sealed class EconomyService : MonoBehaviour
    {
        public event Action<int> OnMoneyChanged;

        private SaveService _save;

        public int Money => _save != null && _save.Current != null ? _save.Current.money : 0;

        private void Awake()
        {
            _save = FindAnyObjectByType<SaveService>();
        }

        public void SyncFromSave()
        {
            // Fuerza refresh del HUD al cargar escena / cargar slot
            OnMoneyChanged?.Invoke(Money);
        }

        public bool CanAfford(int amount) => Money >= amount;

        public bool TrySpend(int amount)
        {
            if (amount < 0) amount = -amount;
            if (!CanAfford(amount)) return false;

            _save.Current.money -= amount;
            OnMoneyChanged?.Invoke(_save.Current.money);

            // Guardado inmediato opcional: de momento NO lo hacemos aquí.
            // Lo guardas cuando el jugador pulse Save en el PauseMenu.
            return true;
        }

        public void AddMoney(int amount)
        {
            if (amount < 0) amount = -amount;

            _save.Current.money += amount;
            OnMoneyChanged?.Invoke(_save.Current.money);
        }
    }
}