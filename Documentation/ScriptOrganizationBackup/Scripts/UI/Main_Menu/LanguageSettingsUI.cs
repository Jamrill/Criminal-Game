using JuegoCriminal.Localization;
using TMPro;
using UnityEngine;

namespace JuegoCriminal.UI
{
    public sealed class LanguageSettingsUI : MonoBehaviour
    {
        public TMP_Dropdown dropdown;
        public TMP_Text label;
        void Awake()=>dropdown.onValueChanged.AddListener(Select);
        void OnEnable(){GameLanguage.Changed+=Refresh;Refresh();}
        void OnDisable(){GameLanguage.Changed-=Refresh;if(dropdown)dropdown.Hide();}
        void OnDestroy(){if(dropdown)dropdown.onValueChanged.RemoveListener(Select);}
        void Select(int value)=>GameLanguage.Set((GameLocale)value);
        void Refresh()
        {
            if(label)label.text=GameLanguage.T("Language","Idioma");
            if(dropdown)dropdown.SetValueWithoutNotify((int)GameLanguage.Current);
        }
    }
}
