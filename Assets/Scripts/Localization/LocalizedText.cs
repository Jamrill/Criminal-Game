using TMPro;
using UnityEngine;

namespace JuegoCriminal.Localization
{
    [DisallowMultipleComponent,RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [TextArea] public string english;
        [TextArea] public string spanish;
        void OnEnable(){GameLanguage.Changed+=Refresh;Refresh();}
        void OnDisable()=>GameLanguage.Changed-=Refresh;
        public void Refresh()=>GetComponent<TMP_Text>().text=GameLanguage.T(english,spanish);
    }
}
