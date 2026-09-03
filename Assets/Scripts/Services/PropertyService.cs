using UnityEngine;

namespace JuegoCriminal.Services
{
    public sealed class PropertyService : MonoBehaviour
    {
        private SaveService _save;

        private void Awake()
        {
            _save = GetComponent<SaveService>();
        }

        public bool IsOwned(int propertyId)
        {
            return _save != null && _save.IsPropertyOwned(propertyId);
        }

        public bool AddOwned(int propertyId)
        {
            return _save != null && _save.TryAddOwnedProperty(propertyId);
        }
    }
}
