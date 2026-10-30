using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using JuegoCriminal.Core;
using JuegoCriminal.Localization;

namespace JuegoCriminal.UI
{
    public sealed class ControlRebindButtonUI : MonoBehaviour
    {
        [SerializeField] private GameInputAction action;
        [SerializeField, Min(0)] private int bindingIndex;
        [SerializeField] private TMP_Text actionLabel;
        [SerializeField] private TMP_Text bindingLabel;
        [SerializeField] private Button rebindButton;
        [SerializeField] private string waitingText = "Press a key...";

        private InputActionRebindingExtensions.RebindingOperation _operation;
        private string englishDisplayName;
        public void RefreshLayout()=>SettingsRowLayout.Configure(transform,actionLabel,rebindButton);

        private void Awake()
        {
            if (rebindButton == null)
                rebindButton = GetComponentInChildren<Button>(true);

            if (rebindButton != null)
                rebindButton.onClick.AddListener(BeginRebind);
        }

        private void OnEnable()
        {
            RefreshLayout();
            GameLanguage.Changed+=Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GameLanguage.Changed-=Refresh;
            if (_operation != null)
                _operation.Cancel();
        }

        private void OnDestroy()
        {
            if (rebindButton != null)
                rebindButton.onClick.RemoveListener(BeginRebind);

            if (_operation != null)
            {
                _operation.Cancel();
                _operation = null;
            }
        }

        public void Refresh()
        {
            if(actionLabel)
            {
                if(string.IsNullOrEmpty(englishDisplayName))englishDisplayName=actionLabel.text;
                string spanish=englishDisplayName switch
                {
                    "Move Forward"=>"Avanzar","Move Backward"=>"Retroceder","Move Left"=>"Mover a la izquierda","Move Right"=>"Mover a la derecha",
                    "Jump"=>"Saltar","Sprint"=>"Correr","Interact"=>"Interactuar","Pause"=>"Pausa","Switch Target"=>"Cambiar objetivo",
                    "Switch Shoulder"=>"Cambiar hombro","Camera Zoom"=>"Zoom de cámara","Inventory"=>"Inventario","Crouch"=>"Agacharse",
                    "Rotate Inventory Item"=>"Rotar objeto del inventario","Tablet"=>"Tablet","Rotate Building"=>"Rotar construcción",
                    "Demolish Building"=>"Vender construcción","Build Cursor (hold)"=>"Cursor de construcción (mantener)","Build Preview Distance"=>"Distancia de previsualización",
                    _=>englishDisplayName
                };
                actionLabel.text=GameLanguage.T(englishDisplayName,spanish);
            }
            if (bindingLabel != null)
                bindingLabel.text = GameInput.GetBindingDisplayString(action, bindingIndex);
        }
        public void SetDisplayName(string value)
        {
            englishDisplayName=value;Refresh();
        }

        public void Configure(
            GameInputAction inputAction,
            int inputBindingIndex,
            string displayName,
            TMP_Text inputActionLabel,
            TMP_Text inputBindingLabel,
            Button inputButton)
        {
            if (rebindButton != null)
                rebindButton.onClick.RemoveListener(BeginRebind);

            action = inputAction;
            bindingIndex = inputBindingIndex;
            actionLabel = inputActionLabel;
            bindingLabel = inputBindingLabel;
            rebindButton = inputButton;

            if (Application.isPlaying && rebindButton != null)
                rebindButton.onClick.AddListener(BeginRebind);

            englishDisplayName=displayName;
            RefreshLayout();
            if (actionLabel != null)
                actionLabel.text = displayName;

            Refresh();
        }

        private void BeginRebind()
        {
            if (_operation != null)
                return;

            if (bindingLabel != null)
                bindingLabel.text = GameLanguage.T(waitingText,"Pulsa una tecla...");

            if (rebindButton != null)
                rebindButton.interactable = false;

            try
            {
                _operation = GameInput.StartInteractiveRebind(
                    action,
                    bindingIndex,
                    _ => FinishRebind(),
                    FinishRebind
                );
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Controls] Rebind could not start: " + e.Message, this);
                FinishRebind();
            }
        }

        private void FinishRebind()
        {
            _operation = null;

            if (rebindButton != null)
                rebindButton.interactable = true;

            Refresh();
        }
    }
}
