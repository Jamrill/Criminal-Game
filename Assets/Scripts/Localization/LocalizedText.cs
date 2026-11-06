using TMPro;
using UnityEngine;
using System.Collections.Generic;

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
        public static void Bind(TMP_Text text,string en,string es)
        {
            if(!text)return;
            var binding=text.GetComponent<LocalizedText>()??text.gameObject.AddComponent<LocalizedText>();
            binding.english=en;binding.spanish=es;binding.enabled=true;binding.Refresh();
        }

        // Only authored UI captions, never a general string replacement over game data.
        // New static menu/tablet captions should use Bind or an Inspector component.
        static readonly Dictionary<string,string> Captions=new()
        {
            {"Continue","Continuar"},{"New Game","Nueva partida"},{"Load Game","Cargar partida"},
            {"Save Game","Guardar partida"},{"Coop","Cooperativo"},{"Co-op","Cooperativo"},
            {"Options","Opciones"},{"Quit","Salir"},{"Back","Volver"},{"Accept","Aceptar"},
            {"Load","Cargar"},{"Delete","Eliminar"},{"YES","SÍ"},{"NO","NO"},
            {"Yes","Sí"},{"No","No"},{"Cancel","Cancelar"},{"Reset","Restablecer"},
            {"Controls","Controles"},{"Accessibility","Accesibilidad"},{"Video","Vídeo"},
            {"Audio","Audio"},{"Online","En línea"},{"Build","Construir"},
            {"Properties","Propiedades"},{"Walls","Paredes"},{"Furniture","Muebles"},
            {"Finish / paint","Acabado / pintar"},{"Enter text...","Escribe aquí..."},
            {"Game name","Nombre de la partida"},{"Save name","Nombre de la partida"},
            {"Are you sure you want to permanently delete this save?","¿Seguro que quieres eliminar esta partida permanentemente?"}
        };
        public static void BindMenu(GameObject root)
        {
            if(!root)return;
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var existing=text.GetComponent<LocalizedText>();
                if(existing){if(existing.enabled)existing.Refresh();continue;}
                // Save names and rebind labels are owned by their respective editors.
                if(text.GetComponentInParent<JuegoCriminal.UI.SaveSlotRowUI>(true) ||
                   text.GetComponentInParent<JuegoCriminal.UI.ControlRebindButtonUI>(true))continue;
                var input=text.GetComponentInParent<TMP_InputField>(true);
                if(input && input.textComponent==text)continue;
                if(Captions.TryGetValue(text.text.Trim(),out var es))Bind(text,text.text.Trim(),es);
            }
        }
    }
}
