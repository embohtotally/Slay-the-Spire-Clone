using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlushieOdyssey.Inputs
{
    /// <summary>Reads one shared action asset. It never navigates or performs gameplay.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public class GameInputReader : MonoBehaviour
    {
        [Tooltip("Assign the project-wide InputSystem_Actions asset. Falls back to InputSystem.actions.")]
        [SerializeField] private InputActionAsset _actions = null;
        [Tooltip("Optional explicit reference. Otherwise UI/Cancel in the shared asset is used.")]
        [SerializeField] private InputActionReference _backAction = null;

        private InputAction _back;
        private bool _subscribed;

        public event Action BackRequested;
        public InputActionAsset Actions => _actions;
        public InputActionMap UIActions { get; private set; }
        public bool IsInitialized { get; private set; }

        private void Awake() => TryInitialize();

        private void OnEnable()
        {
            if (!TryInitialize()) return;
            if (!_subscribed)
            {
                _back.performed += OnBackPerformed;
                _subscribed = true;
            }
            EnsureBackEnabled();
        }

        private void OnDisable()
        {
            if (_subscribed) _back.performed -= OnBackPerformed;
            _subscribed = false;
            // Do not disable the shared UI map: the EventSystem also uses its actions.
        }

        public bool TryInitialize()
        {
            if (IsInitialized) return true;
            if (_actions == null && _backAction != null && _backAction.action != null)
                _actions = _backAction.action.actionMap?.asset;
            if (_actions == null) _actions = InputSystem.actions;
            if (_actions == null)
            {
                Debug.LogError("[GameInputReader] Assign the shared project action asset.", this);
                return false;
            }

            UIActions = _actions.FindActionMap("UI", false);
            _back = _backAction != null ? _backAction.action : UIActions?.FindAction("Cancel", false);
            if (UIActions == null || _back == null || _back.actionMap != UIActions || _back.type != InputActionType.Button)
            {
                Debug.LogError("[GameInputReader] Back must reference a Button action in the shared UI map, normally UI/Cancel, in the same asset used by the UI module.", this);
                return false;
            }

            IsInitialized = true;
            return true;
        }

        internal void EnsureBackEnabled()
        {
            if (TryInitialize() && isActiveAndEnabled) _back.Enable();
        }

        internal void SetContext(InputContext context)
        {
            if (!TryInitialize()) return;

            string activeMap = context switch
            {
                InputContext.Combat => "Combat",
                InputContext.Dialogue => "Dialogue",
                _ => null
            };

            // Project-wide assets enable maps automatically. Own non-UI maps explicitly.
            // Combat/Dialogue maps are optional until those input consumers are migrated.
            foreach (InputActionMap map in _actions.actionMaps)
            {
                if (map == UIActions) continue;
                if (map.name == activeMap) map.Enable();
                else map.Disable();
            }
            EnsureBackEnabled();
        }

        private void OnBackPerformed(InputAction.CallbackContext context)
        {
            BackRequested?.Invoke();
        }
    }
}
