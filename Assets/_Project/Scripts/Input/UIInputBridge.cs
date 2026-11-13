using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace PlushieOdyssey.Inputs
{
    /// <summary>Connects a scene EventSystem to the shared asset and gives Cancel one owner.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EventSystem), typeof(InputSystemUIInputModule))]
    public class UIInputBridge : MonoBehaviour
    {
        [Tooltip("Optional scene reference; otherwise resolves through the existing GameManager bootstrap.")]
        [SerializeField] private InputContextRouter _router = null;

        private EventSystem _eventSystem;
        private InputSystemUIInputModule _module;
        private ModuleState _previous;
        private readonly List<InputActionReference> _runtimeReferences = new();
        private bool _warnedMissingRouter;

        public bool IsBound { get; private set; }

        private sealed class ModuleState
        {
            internal InputActionAsset Asset;
            internal InputActionReference Point, Move, Submit, Cancel, Click, RightClick, MiddleClick,
                Scroll, TrackedPosition, TrackedOrientation;
            internal bool Enabled, SendNavigation;
        }

        private void Awake()
        {
            _eventSystem = GetComponent<EventSystem>();
            _module = GetComponent<InputSystemUIInputModule>();
        }

        private void OnEnable() => Bind();
        private void Start()
        {
            if (!IsBound) Bind();
        }

        private void OnDisable()
        {
            if (_router != null) _router.DetachUI(this);
            if (IsBound && _module != null)
            {
                _module.enabled = false;
                ApplyReferences(null);
                _module.actionsAsset = _previous.Asset;
                ApplyReferences(_previous);
                _module.enabled = _previous.Enabled;
                if (_eventSystem != null) _eventSystem.sendNavigationEvents = _previous.SendNavigation;
            }
            IsBound = false;
            _previous = null;
            foreach (InputActionReference reference in _runtimeReferences)
            {
                if (reference != null) Destroy(reference);
            }
            _runtimeReferences.Clear();
        }

        private void Bind()
        {
            if (IsBound) return;
            if (_router == null) _router = InputContextRouter.FromBootstrap;
            if (_router == null || !_router.isActiveAndEnabled || !_router.TryInitialize())
            {
                if (!_warnedMissingRouter)
                    Debug.LogWarning("[UIInputBridge] Add GameInputReader and InputContextRouter to the GameManager prefab, or assign an active router. Existing UI bindings are unchanged until setup is complete.", this);
                _warnedMissingRouter = true;
                return;
            }

            InputActionMap ui = _router.Reader.UIActions;
            foreach (string actionName in new[] { "Point", "Click", "Navigate", "Submit" })
            {
                if (ui.FindAction(actionName, false) != null) continue;
                Debug.LogError($"[UIInputBridge] The shared UI map is missing {actionName}. Existing module bindings were not changed.", this);
                return;
            }

            _previous = new ModuleState
            {
                Asset = _module.actionsAsset,
                Point = _module.point,
                Move = _module.move,
                Submit = _module.submit,
                Cancel = _module.cancel,
                Click = _module.leftClick,
                RightClick = _module.rightClick,
                MiddleClick = _module.middleClick,
                Scroll = _module.scrollWheel,
                TrackedPosition = _module.trackedDevicePosition,
                TrackedOrientation = _module.trackedDeviceOrientation,
                Enabled = _module.enabled,
                SendNavigation = _eventSystem.sendNavigationEvents
            };

            // Clear old references before changing assets so the module does not create extra
            // temporary references while remapping them. Change assets before disabling: a
            // module using its built-in runtime defaults would otherwise dispose that old asset.
            ApplyReferences(null);
            _module.actionsAsset = _router.Reader.Actions;
            _module.enabled = false;
            ApplyReferences(new ModuleState
            {
                Point = Reference(ui, "Point"),
                Move = Reference(ui, "Navigate"),
                Submit = Reference(ui, "Submit"),
                // Cancel deliberately remains null: the router is its only owner.
                Click = Reference(ui, "Click"),
                RightClick = Reference(ui, "RightClick"),
                MiddleClick = Reference(ui, "MiddleClick"),
                Scroll = Reference(ui, "ScrollWheel"),
                TrackedPosition = Reference(ui, "TrackedDevicePosition"),
                TrackedOrientation = Reference(ui, "TrackedDeviceOrientation")
            });
            IsBound = true;
            _module.enabled = _previous.Enabled;
            _router.AttachUI(this);
        }

        private void ApplyReferences(ModuleState state)
        {
            _module.point = state?.Point;
            _module.move = state?.Move;
            _module.submit = state?.Submit;
            _module.cancel = state?.Cancel;
            _module.leftClick = state?.Click;
            _module.rightClick = state?.RightClick;
            _module.middleClick = state?.MiddleClick;
            _module.scrollWheel = state?.Scroll;
            _module.trackedDevicePosition = state?.TrackedPosition;
            _module.trackedDeviceOrientation = state?.TrackedOrientation;
        }

        private InputActionReference Reference(InputActionMap map, string name)
        {
            InputAction action = map.FindAction(name, false);
            if (action == null) return null;
            InputActionReference reference = InputActionReference.Create(action);
            reference.hideFlags = HideFlags.HideAndDontSave;
            _runtimeReferences.Add(reference);
            return reference;
        }

        internal void ApplyContext(InputContext context)
        {
            if (IsBound && _eventSystem != null)
                _eventSystem.sendNavigationEvents = context != InputContext.Combat;
            // Pointer/drag/scroll continue to work even when combat owns directional input.
        }

        internal BackCapture CaptureBack()
        {
            GameObject selected = _eventSystem.currentSelectedGameObject;
            if (selected == null) return default;

            TMP_Dropdown tmpDropdown = selected.GetComponentInParent<TMP_Dropdown>();
            if (tmpDropdown != null && tmpDropdown.IsExpanded)
                return new BackCapture(tmpDropdown, BackKind.TmpDropdown, _eventSystem);
            UnityEngine.UI.Dropdown dropdown = selected.GetComponentInParent<UnityEngine.UI.Dropdown>();
            // Legacy uGUI Dropdown exposes no IsExpanded property. Its generated option items
            // implement ICancelHandler and select themselves while the popup is open.
            if (dropdown != null && selected != dropdown.gameObject && ExecuteEvents.CanHandleEvent<ICancelHandler>(selected))
                return new BackCapture(dropdown, BackKind.Dropdown, _eventSystem);
            TMP_InputField tmpInput = selected.GetComponentInParent<TMP_InputField>();
            if (tmpInput != null && tmpInput.isFocused)
                return new BackCapture(tmpInput, BackKind.TmpInput, _eventSystem);
            UnityEngine.UI.InputField input = selected.GetComponentInParent<UnityEngine.UI.InputField>();
            if (input != null && input.isFocused)
                return new BackCapture(input, BackKind.Input, _eventSystem);

            // A closed dropdown's OnCancel is a no-op; it must not swallow panel Back forever.
            if (tmpDropdown == null && dropdown == null && ExecuteEvents.CanHandleEvent<ICancelHandler>(selected))
                return new BackCapture(selected, BackKind.Custom, _eventSystem);
            return default;
        }

        internal enum BackKind { None, TmpDropdown, Dropdown, TmpInput, Input, Custom }

        internal readonly struct BackCapture
        {
            private readonly UnityEngine.Object _widget;
            private readonly BackKind _kind;
            private readonly EventSystem _eventSystem;

            internal BackCapture(UnityEngine.Object widget, BackKind kind, EventSystem eventSystem)
            {
                _widget = widget;
                _kind = kind;
                _eventSystem = eventSystem;
            }

            internal bool TryHandle()
            {
                if (_kind == BackKind.None) return false;
                switch (_kind)
                {
                    case BackKind.TmpDropdown:
                        if (_widget is TMP_Dropdown td && td != null && td.IsExpanded) td.Hide();
                        break;
                    case BackKind.Dropdown:
                        if (_widget is UnityEngine.UI.Dropdown d && d != null) d.Hide();
                        break;
                    case BackKind.TmpInput:
                        if (_widget is TMP_InputField ti && ti != null && ti.isFocused) ti.DeactivateInputField();
                        break;
                    case BackKind.Input:
                        if (_widget is UnityEngine.UI.InputField i && i != null && i.isFocused) i.DeactivateInputField();
                        break;
                    case BackKind.Custom:
                        if (_widget is GameObject target && target != null && _eventSystem != null)
                            ExecuteEvents.Execute(target, new BaseEventData(_eventSystem), ExecuteEvents.cancelHandler);
                        break;
                }
                // Consume even if uGUI already closed the captured widget earlier in this frame.
                return true;
            }
        }
    }
}
