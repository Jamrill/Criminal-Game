using System.Linq;
using UnityEngine;

namespace JuegoCriminal.Services
{
    public sealed class PropertyService : MonoBehaviour
    {
        private SaveService _save;

        private void Awake()
        {
            _save = FindAnyObjectByType<SaveService>();
        }

        public bool IsOwned(int propertyId)
        {
            if (_save?.Current?.ownedProperties == null) return false;
            return _save.Current.ownedProperties.Contains(propertyId);
        }

        public void AddOwned(int propertyId)
        {
            if (_save?.Current == null) return;

            var arr = _save.Current.ownedProperties ?? new int[0];
            if (arr.Contains(propertyId)) return;

            var newArr = new int[arr.Length + 1];
            for (int i = 0; i < arr.Length; i++) newArr[i] = arr[i];
            newArr[newArr.Length - 1] = propertyId;

            _save.Current.ownedProperties = newArr;
        }
    }
}