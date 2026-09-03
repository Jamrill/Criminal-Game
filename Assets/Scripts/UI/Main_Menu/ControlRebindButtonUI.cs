using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using JuegoCriminal.Core;

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

        private void Awake()
        {
            if (rebindButton == null)
                rebindButton = GetComponentInChildren<Button>(true);

            if (rebindButton != null)
                rebindButton.onClick.AddListener(BeginRebind);
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDisable()
        {
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
            if (bindingLabel != null)
                bindingLabel.text = GameInput.GetBindingDisplayString(action, bindingIndex);
        }

        public void Configure(
            GameInputAction inputAction,
            int inputBindingIndex,
            string displayName,
            TMP_Text inputActionLabel,
            TMP_Text inputBindingLabel,
            Button inputButton)
        {
            action = inputAction;
            bindingIndex = inputBindingIndex;
            actionLabel = inputActionLabel;
            bindingLabel = inputBindingLabel;
            rebindButton = inputButton;

            if (actionLabel != null)
                actionLabel.text = displayName;

            Refresh();
        }

        private void BeginRebind()
        {
            if (_operation != null)
                return;

            if (bindingLabel != null)
                bindingLabel.text = waitingText;

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
