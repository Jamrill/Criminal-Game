using System;
using UnityEngine;

namespace JuegoCriminal.Localization
{
    public enum GameLocale { English, Spanish }

    public static class GameLanguage
    {
        public const string PreferenceKey="Game.Language";
        public static event Action Changed;
        public static GameLocale Current => (GameLocale)Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey,0),0,1);
        public static string T(string english,string spanish,params object[] args)
        {
            string text=Current==GameLocale.Spanish?spanish:english;
            return args.Length==0?text:string.Format(text,args);
        }
        public static void Set(GameLocale language)
        {
            language=(GameLocale)Mathf.Clamp((int)language,0,1);
            if(Current==language)return;
            PlayerPrefs.SetInt(PreferenceKey,(int)language);PlayerPrefs.Save();Changed?.Invoke();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEvents()=>Changed=null;
    }
}
