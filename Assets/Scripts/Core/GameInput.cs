using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JuegoCriminal.Core
{
    public enum GameInputAction
    {
        Move,
        Look,
        Jump,
        Sprint,
        Interact,
        Pause,
        SwitchTarget,
        SwitchShoulder,
        CameraZoom,
        Inventory
    }

    public static class GameInput
    {
        private const string BindingOverridesKey = "input.bindingOverrides";
        private static bool _overridesLoaded;
        private static bool _missingAssetLogged;
        private static int _pauseConsumedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            _overridesLoaded = false;
            _missingAssetLogged = false;
            _pauseConsumedFrame = -1;
        }

        public static Vector2 Move => ReadVector2(GameInputAction.Move);
        public static Vector2 Look => ReadVector2(GameInputAction.Look);
        public static float CameraZoom => ReadVector2(GameInputAction.CameraZoom).y;

        public static bool JumpPressed => WasPressedThisFrame(GameInputAction.Jump);
        public static bool SprintHeld => IsPressed(GameInputAction.Sprint);
        public static bool InteractPressed => WasPressedThisFrame(GameInputAction.Interact);
        public static bool PausePressed =>
            _pauseConsumedFrame != Time.frameCount && WasPressedThisFrame(GameInputAction.Pause);
        public static bool SwitchTargetPressed => WasPressedThisFrame(GameInputAction.SwitchTarget);
        public static bool SwitchShoulderPressed => WasPressedThisFrame(GameInputAction.SwitchShoulder);
        public static bool InventoryPressed => WasPressedThisFrame(GameInputAction.Inventory);

        public static void ConsumePausePress()
        {
            _pauseConsumedFrame = Time.frameCount;
        }

        public static bool IsLookFromPointer
        {
            get
            {
                InputAction action = GetAction(GameInputAction.Look);
                return action?.activeControl?.device is Pointer;
            }
        }

        public static InputAction GetAction(GameInputAction action)
        {
            EnsureOverridesLoaded();

            InputActionAsset asset = InputSystem.actions;
            if (asset == null)
            {
                if (!_missingAssetLogged)
                {
                    Debug.LogError("[GameInput] No project-wide Input Actions asset is configured.");
                    _missingAssetLogged = true;
                }

                return null;
            }

            (string mapName, string actionName) = GetActionPath(action);
            InputAction result = asset.FindAction($"{mapName}/{actionName}", false);

            if (result != null && !result.enabled)
                result.Enable();

            return result;
        }

        public static void SaveBindingOverrides()
        {
            InputActionAsset asset = InputSystem.actions;
            if (asset == null)
                return;

            PlayerPrefs.SetString(BindingOverridesKey, asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public static void ResetBindingOverrides()
        {
            InputActionAsset asset = InputSystem.actions;
            if (asset == null)
                return;

            asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(BindingOverridesKey);
            PlayerPrefs.Save();
            _overridesLoaded = true;
        }

        public static string GetBindingDisplayString(GameInputAction action, int bindingIndex)
        {
            InputAction inputAction = GetAction(action);

            if (inputAction == null || bindingIndex < 0 || bindingIndex >= inputAction.bindings.Count)
                return string.Empty;

            return inputAction.GetBindingDisplayString(bindingIndex);
        }

        public static InputActionRebindingExtensions.RebindingOperation StartInteractiveRebind(
            GameInputAction action,
            int bindingIndex,
            Action<string> onCompleted = null,
            Action onCanceled = null)
        {
            InputAction inputAction = GetAction(action);

            if (inputAction == null)
                throw new InvalidOperationException($"Input action '{action}' was not found.");

            if (bindingIndex < 0 || bindingIndex >= inputAction.bindings.Count)
                throw new ArgumentOutOfRangeException(nameof(bindingIndex));

            if (inputAction.bindings[bindingIndex].isComposite)
                throw new InvalidOperationException("Rebind a composite part instead of its root binding.");

            inputAction.Disable();

            InputActionRebindingExtensions.RebindingOperation operation = null;
            operation = inputAction
                .PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(_ =>
                {
                    operation.Dispose();
                    inputAction.Enable();
                    onCanceled?.Invoke();
                })
                .OnComplete(_ =>
                {
                    operation.Dispose();
                    inputAction.Enable();
                    SaveBindingOverrides();
                    onCompleted?.Invoke(inputAction.GetBindingDisplayString(bindingIndex));
                })
                .Start();

            return operation;
        }

        private static Vector2 ReadVector2(GameInputAction action)
        {
            InputAction inputAction = GetAction(action);
            return inputAction != null ? inputAction.ReadValue<Vector2>() : Vector2.zero;
        }

        private static bool WasPressedThisFrame(GameInputAction action)
        {
            InputAction inputAction = GetAction(action);
            return inputAction != null && inputAction.WasPressedThisFrame();
        }

        private static bool IsPressed(GameInputAction action)
        {
            InputAction inputAction = GetAction(action);
            return inputAction != null && inputAction.IsPressed();
        }

        private static void EnsureOverridesLoaded()
        {
            if (_overridesLoaded)
                return;

            InputActionAsset asset = InputSystem.actions;
            if (asset == null)
                return;

            string json = PlayerPrefs.GetString(BindingOverridesKey, string.Empty);

            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    asset.LoadBindingOverridesFromJson(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[GameInput] Could not load binding overrides: " + e.Message);
                    PlayerPrefs.DeleteKey(BindingOverridesKey);
                }
            }

            _overridesLoaded = true;
        }

        private static (string mapName, string actionName) GetActionPath(GameInputAction action)
        {
            return action switch
            {
                GameInputAction.Move => ("Player", "Move"),
                GameInputAction.Look => ("Player", "Look"),
                GameInputAction.Jump => ("Player", "Jump"),
                GameInputAction.Sprint => ("Player", "Sprint"),
                GameInputAction.Interact => ("Player", "Interact"),
                GameInputAction.Pause => ("UI", "Cancel"),
                GameInputAction.SwitchTarget => ("Player", "Next"),
                GameInputAction.SwitchShoulder => ("Player", "Crouch"),
                GameInputAction.CameraZoom => ("UI", "ScrollWheel"),
                GameInputAction.Inventory => ("Player", "Inventory"),
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }
    }
}
