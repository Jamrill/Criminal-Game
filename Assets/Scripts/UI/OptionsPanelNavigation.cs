using System;
using UnityEngine;
using UnityEngine.UI;

namespace JuegoCriminal.UI
{
    [DisallowMultipleComponent]
    public sealed class OptionsPanelNavigation : MonoBehaviour
    {
        [SerializeField] private Button backButton;
        Action close;
        public Button BackButton=>backButton;
        void Awake()=>Connect();
        void OnEnable()=>RaiseNavigation();
        public void RaiseNavigation()
        {
            // Category scroll masks/backgrounds can overlap the navigation area.
            // Keep its actual buttons above them for both rendering and raycasts.
            var controls=new System.Collections.Generic.List<Transform>();
            foreach(Transform child in transform)
                if(child.GetComponent<Button>() || child.GetComponent<LanguageSettingsUI>())controls.Add(child);
            foreach(var child in controls)child.SetAsLastSibling();
        }
        void Connect()
        {
            if(!backButton)return;
            backButton.onClick.RemoveListener(RequestClose);
            backButton.onClick.AddListener(RequestClose);
        }
        public void Bind(Action callback){close=callback;Connect();}
        public void Unbind(Action callback){if(close==callback)close=null;}
        void RequestClose()
        {
            var controls=GetComponentInChildren<ControlsMenuUI>(true);if(controls)controls.Close();
            var video=GetComponentInChildren<VideoMenuUI>(true);if(video)video.Close();
            close?.Invoke();
        }
        void OnDestroy(){if(backButton)backButton.onClick.RemoveListener(RequestClose);close=null;}
    }
}
